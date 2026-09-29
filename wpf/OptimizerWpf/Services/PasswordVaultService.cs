using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace OptimizerWpf.Services
{
    // REQ-570-06: ενοποιημένος διαχειριστής κωδικών browser (Chromium-family: Chrome, Edge, Brave,
    // Vivaldi, Opera, Opera GX - ΟΧΙ Firefox, το δικό του NSS-based store είναι εντελώς διαφορετικό
    // format, ξεχωριστό scope). Διαβάζει ΜΟΝΟ το προφίλ του ίδιου Windows χρήστη (ίδιο μηχάνημα, ίδιος
    // λογαριασμός) - καμία δικτυακή μετάδοση πουθενά, τίποτα δεν γράφεται σε log/αρχείο.
    public static class PasswordVaultService
    {
        public record SavedCredential(string Browser, string Origin, string Username, string Password);

        private record BrowserProfile(string DisplayName, string LoginDataPath, string LocalStatePath);

        private static IEnumerable<BrowserProfile> DiscoverProfiles()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var multiProfileBrowsers = new (string Name, string UserDataDir)[]
            {
                ("Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data")),
                ("Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data")),
                ("Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")),
                ("Vivaldi", Path.Combine(localAppData, "Vivaldi", "User Data")),
            };
            foreach (var (name, userDataDir) in multiProfileBrowsers)
            {
                if (!Directory.Exists(userDataDir)) continue;
                var localStatePath = Path.Combine(userDataDir, "Local State");
                foreach (var profileDir in Directory.GetDirectories(userDataDir))
                {
                    var folderName = Path.GetFileName(profileDir);
                    if (folderName != "Default" && !folderName.StartsWith("Profile ", StringComparison.Ordinal)) continue;
                    var loginData = Path.Combine(profileDir, "Login Data");
                    if (File.Exists(loginData))
                        yield return new BrowserProfile($"{name} ({folderName})", loginData, localStatePath);
                }
            }

            // Opera/Opera GX: single-profile layout, "Login Data" and "Local State" sit directly in the root.
            var singleProfileBrowsers = new (string Name, string Root)[]
            {
                ("Opera", Path.Combine(roamingAppData, "Opera Software", "Opera Stable")),
                ("Opera GX", Path.Combine(roamingAppData, "Opera Software", "Opera GX Stable")),
            };
            foreach (var (name, root) in singleProfileBrowsers)
            {
                var loginData = Path.Combine(root, "Login Data");
                if (File.Exists(loginData))
                    yield return new BrowserProfile(name, loginData, Path.Combine(root, "Local State"));
            }
        }

        private static byte[]? GetMasterKey(string localStatePath)
        {
            if (!File.Exists(localStatePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(localStatePath));
            if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt)) return null;
            if (!osCrypt.TryGetProperty("encrypted_key", out var encKeyProp)) return null;
            var encKeyB64 = encKeyProp.GetString();
            if (string.IsNullOrEmpty(encKeyB64)) return null;

            var encKey = Convert.FromBase64String(encKeyB64);
            const string dpapiPrefix = "DPAPI";
            var prefixBytes = Encoding.ASCII.GetBytes(dpapiPrefix);
            if (encKey.Length <= prefixBytes.Length) return null;
            var keyBlob = encKey.Skip(prefixBytes.Length).ToArray();
            return ProtectedData.Unprotect(keyBlob, null, DataProtectionScope.CurrentUser);
        }

        private static string? DecryptPassword(byte[] masterKey, byte[] encryptedValue)
        {
            if (encryptedValue.Length == 0) return null;

            // Pre-"v10" format: the whole blob is DPAPI-protected directly (very old Chrome profiles).
            if (encryptedValue.Length < 3 || encryptedValue[0] != (byte)'v')
            {
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(encryptedValue, null, DataProtectionScope.CurrentUser)); }
                catch { return null; }
            }

            // "v10"/"v11" format: 3-byte version prefix + 12-byte GCM nonce + ciphertext + 16-byte tag.
            const int prefixLen = 3, nonceLen = 12, tagLen = 16;
            if (encryptedValue.Length < prefixLen + nonceLen + tagLen) return null;
            var nonce = encryptedValue.Skip(prefixLen).Take(nonceLen).ToArray();
            var cipherLen = encryptedValue.Length - prefixLen - nonceLen - tagLen;
            var ciphertext = encryptedValue.Skip(prefixLen + nonceLen).Take(cipherLen).ToArray();
            var tag = encryptedValue.Skip(encryptedValue.Length - tagLen).Take(tagLen).ToArray();

            try
            {
                using var aesGcm = new AesGcm(masterKey, tagLen);
                var plain = new byte[ciphertext.Length];
                aesGcm.Decrypt(nonce, ciphertext, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        public static Task<(List<SavedCredential> Credentials, List<string> Browsers)> ScanAllAsync() => Task.Run(() =>
        {
            var results = new List<SavedCredential>();
            var scannedBrowsers = new List<string>();

            foreach (var profile in DiscoverProfiles())
            {
                string tempCopy = Path.Combine(Path.GetTempPath(), $"gearwin_logindata_{Guid.NewGuid():N}.db");
                try
                {
                    var masterKey = GetMasterKey(profile.LocalStatePath);
                    if (masterKey == null) continue;

                    File.Copy(profile.LoginDataPath, tempCopy, overwrite: true);
                    scannedBrowsers.Add(profile.DisplayName);

                    using var conn = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var username = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        if (string.IsNullOrEmpty(username)) continue;
                        var encBytes = (byte[])reader[2];
                        var password = DecryptPassword(masterKey, encBytes);
                        if (password == null) continue;
                        results.Add(new SavedCredential(profile.DisplayName, reader.GetString(0), username, password));
                    }
                }
                catch { /* one browser/profile failing to read must not block the rest */ }
                finally { try { if (File.Exists(tempCopy)) File.Delete(tempCopy); } catch { } }
            }

            return (results, scannedBrowsers);
        });
    }
}
