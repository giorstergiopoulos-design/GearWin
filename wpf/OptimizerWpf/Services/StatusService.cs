using System;

namespace OptimizerWpf.Services
{
    // Global, view-agnostic "what's happening right now" signal for the status bar (ρητό αίτημα
    // χρήστη: το κυκλικό εικονίδιο φόρτωσης της WinForms έκδοσης / $global:activitySpinner δεν είχε
    // περάσει στο WPF shell). Any view/service can call SetBusy/SetIdle - MainWindow is the only
    // subscriber and owns the actual spinner animation + status text (see MainWindow.xaml.cs).
    // Not tied to any specific async operation - purely a shared broadcast so unrelated tabs
    // (HomeView, OptimizationView, ...) can all drive the same status bar without knowing about
    // each other or about MainWindow.
    public static class StatusService
    {
        public static event Action<string, bool>? Changed;

        public static void SetBusy(string message) => Changed?.Invoke(message, true);

        public static void SetIdle(string message) => Changed?.Invoke(message, false);
    }
}
