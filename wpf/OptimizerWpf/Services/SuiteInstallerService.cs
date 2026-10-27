using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    // Ο installer + το αναμενόμενο SHA-256 που θα ελεγχθεί πριν δοθεί το αρχείο στον χρήστη.
    public sealed record VerifiedInstaller(ReleaseAsset Asset, string ExpectedSha256);

    // Λήψη installer άλλης εφαρμογής της σουίτας. ΚΑΝΟΝΕΣ (ίδιοι με το SelfUpdateService): μόνο github.com/<owner>/<repo>/releases/download/,
    // το SHA-256 είναι ΥΠΟΧΡΕΩΤΙΚΟ (από το πεδίο `digest` του asset ή από το αρχείο .sha256 του release) και σε αναντιστοιχία το αρχείο διαγράφεται και
    // η λήψη αποτυγχάνει. Το αρχείο πάει στον φάκελο Λήψεις του χρήστη και ΠΟΤΕ δεν εκτελείται από την εφαρμογή.
    public static class SuiteInstallerService
    {
        public static ReleaseAsset? PickInstaller(SuiteApp app, ReleaseData release)
        {
            Regex rx;
            try { rx = new Regex(app.InstallerAssetPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
            catch (ArgumentException) { return null; }
            foreach (var a in release.Assets)
            {
                try { if (rx.IsMatch(a.Name) && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsPlainFileName(a.Name)) return a; }
                catch (RegexMatchTimeoutException) { return null; }
            }
            return null;
        }

        public static bool IsPlainFileName(string name) =>
            name.Length is > 0 and <= 120 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains("..") && name == Path.GetFileName(name);

        // "abc...  file.exe" (sha256sum) ή απλά "abc..." -> 64 hex.
        public static string? ParseChecksumFile(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4096) return null;
            var m = Regex.Match(text, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])");
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        // Επιλέγει installer + αναμενόμενο hash. null αν δεν υπάρχει τρόπος επαλήθευσης (τότε η UI ανοίγει μόνο τη σελίδα του release).
        public static async Task<VerifiedInstaller?> ResolveAsync(SuiteApp app, ReleaseData release, HttpMessageHandler? handler = null, CancellationToken ct = default)
        {
            var asset = PickInstaller(app, release);
            if (asset == null) return null;
            if (asset.Sha256 != null) return new VerifiedInstaller(asset, asset.Sha256);
            var sumName = app.ChecksumAsset ?? asset.Name + ".sha256";
            var sum = release.Assets.FirstOrDefault(a => string.Equals(a.Name, sumName, StringComparison.OrdinalIgnoreCase) && a.Size <= 4096);
            if (sum == null) return null;
            var text = await SuiteCatalogService.FetchSmallTextAsync(sum.Url, handler, ct);
            var hash = ParseChecksumFile(text);
            return hash == null ? null : new VerifiedInstaller(asset, hash);
        }

        public static bool VerifySha256(string path, string expectedHex)
        {
            if (string.IsNullOrEmpty(expectedHex) || expectedHex.Length != 64) return false;
            using var fs = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(fs));
            return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
        }

        public static string DownloadsFolder()
        {
            try
            {
                var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var p) == 0 && !string.IsNullOrEmpty(p)) return p;
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, [MarshalAs(UnmanagedType.LPWStr)] out string ppszPath);

        // Κατεβάζει σε .part, ελέγχει SHA-256, και μόνο τότε μετονομάζει στο τελικό όνομα. InvalidDataException("hash-mismatch") σε αναντιστοιχία.
        public static async Task<string> DownloadAsync(VerifiedInstaller item, string destinationDir, HttpMessageHandler? handler = null,
            IProgress<double>? progress = null, CancellationToken ct = default)
        {
            if (!SuiteCatalogService.IsAllowedUrl(item.Asset.Url) || !item.Asset.Url.StartsWith("https://github.com/", StringComparison.Ordinal))
                throw new InvalidOperationException("untrusted-url");
            if (!IsPlainFileName(item.Asset.Name)) throw new InvalidOperationException("bad-name");
            Directory.CreateDirectory(destinationDir);
            var finalPath = UniquePath(destinationDir, item.Asset.Name);
            var part = finalPath + ".part";
            try
            {
                using var http = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(15);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("GearWin-Suite/1.0");
                using var resp = await http.GetAsync(item.Asset.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                // μετά από ανακατεύθυνση το τελικό URI πρέπει να είναι πάλι επιτρεπόμενο host
                var finalUri = resp.RequestMessage?.RequestUri?.ToString();
                if (finalUri != null && !SuiteCatalogService.IsAllowedUrl(finalUri, allowRedirectHosts: true)) throw new InvalidOperationException("untrusted-redirect");
                var total = resp.Content.Headers.ContentLength ?? item.Asset.Size;
                if (total > 600L * 1024 * 1024) throw new InvalidOperationException("too-large");
                using var sha = SHA256.Create();
                await using (var src = await resp.Content.ReadAsStreamAsync(ct))
                await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long read = 0;
                    int n;
                    while ((n = await src.ReadAsync(buffer, ct)) > 0)
                    {
                        await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                        sha.TransformBlock(buffer, 0, n, null, 0);
                        read += n;
                        if (read > 600L * 1024 * 1024) throw new InvalidOperationException("too-large");
                        if (total > 0) progress?.Report(Math.Min(1.0, read / (double)total));
                    }
                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }
                var actual = Convert.ToHexString(sha.Hash!);
                if (!string.Equals(actual, item.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("hash-mismatch");
                File.Move(part, finalPath);
                return finalPath;
            }
            catch
            {
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                throw;
            }
        }

        private static string UniquePath(string dir, string name)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) return path;
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var i = 1; i < 100; i++)
            {
                var p = Path.Combine(dir, $"{stem} ({i}){ext}");
                if (!File.Exists(p)) return p;
            }
            return Path.Combine(dir, $"{stem}-{Guid.NewGuid():N}{ext}");
        }
    }
}
