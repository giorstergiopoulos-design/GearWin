using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OptimizerWpf.Services
{
    public enum RestoreGuardResult { Disabled, AlreadyRecent, Created, Failed }

    // ΝΕΟ (6.1.0) - "σημείο επαναφοράς πριν από ριψοκίνδυνες ενέργειες": πριν από μαζικές αλλαγές
    // (εισαγωγή προφίλ tweaks, Fix All με registry/tweaks, απεγκατάσταση εφαρμογής) εξασφαλίζει ότι
    // υπάρχει πρόσφατο σημείο επαναφοράς. Τα Windows επιτρέπουν από προεπιλογή 1 αυτόματο σημείο ανά
    // 24 ώρες - γι' αυτό πρώτα ελέγχεται αν υπάρχει ήδη πρόσφατο και η αποτυχία δημιουργίας ΔΕΝ
    // μπλοκάρει τη ενέργεια (ο καλών αποφασίζει και ενημερώνει τον χρήστη).
    public static class RestoreGuardService
    {
        public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

        public static bool NeedsNewPoint(IReadOnlyList<RestorePointInfo> points, DateTime now, TimeSpan maxAge) =>
            points.Count == 0 || now - points.Max(p => p.CreationTime) > maxAge;

        public static async Task<RestoreGuardResult> EnsureRecentAsync()
        {
            if (!AppSettingsService.Current.AutoRestorePointBeforeRiskyActions) return RestoreGuardResult.Disabled;
            try
            {
                var points = await SystemService.ListRestorePointsAsync();
                if (!NeedsNewPoint(points, DateTime.Now, DefaultMaxAge)) return RestoreGuardResult.AlreadyRecent;
                return await SystemService.CreateRestorePointAsync() ? RestoreGuardResult.Created : RestoreGuardResult.Failed;
            }
            catch { return RestoreGuardResult.Failed; }
        }
    }
}
