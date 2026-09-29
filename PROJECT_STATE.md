# PROJECT STATE

## Project
Name: GearWin - Complete PC Care (WPF port of the original Optimizer.ps1)

## Technology
- .NET: net8.0-windows
- C#: WPF (OptimizerWpf.csproj), assembly/exe name `GearWin.exe`
- UI: WPF, `wpf/OptimizerWpf/` — Views (XAML + code-behind, no MVVM/ViewModels — direct code-behind is the established pattern here), Services (static classes), Themes/Styles.xaml
- Mobile: none
- Backend: none (fully local desktop app)
- Tests: `wpf/OptimizerWpf.Tests/` (xUnit-style, run via `dotnet test`), 62 tests in the default battery (all passing as of last verification), plus one opt-in UI Automation click-through test (`GEARWIN_UI_CLICKTHROUGH=1`, shows real windows - not run by default)
- Installer: Inno Setup, `installer/OptimizerWpf.iss` → `installer/Output/GearWin-Setup-<version>.exe`. Build: `dotnet publish -c Release -r win-x64 --self-contained true -o wpf/OptimizerWpf/publish/win-x64` then `ISCC.exe installer/OptimizerWpf.iss`. ISCC lives at `AppData\Local\Programs\Inno Setup 6\ISCC.exe` (not Program Files).
- 14 supported UI languages, all strings in `Services/LanguageService.cs` (huge file, ~18k lines, one dictionary block per language in fixed order: el, en, de, fr, es, ko, zh, it, ru, ja, pt, tr, ar, hi). New keys must be added to all 14 blocks (use a small Python script to insert by anchor-key line number, descending order, rather than hand-editing).
- Repo: `giorstergiopoulos-design/GearWin` on GitHub. `gh release create` works from this environment (has previously needed a retry after an auto-mode permission block).

## Current Objective
No single active objective — user drives work in small/medium batches (Greek, terse). Just shipped v5.9.0 (font management + hardened UI Automation click-through). REQ-570-13 skipped per user; REQ-580-03 dropped per user (policy concern - see Blocked history, now resolved as dropped). REQ-580-05 remains blocked on MotionDeskStudio access.

## Current Phase
Between releases. Governance docs (CLAUDE.md, PROJECT_STATE.md, ROADMAP.md) adopted 2026-09-29 at user's request.

## Completed
- v5.9.0 shipped (2026-09-30): REQ-580-02 (Font management — new "Fonts" tab in Settings: live preview of every installed system font with user-typed text, plus 12 suggested free/open-license fonts linking to their official Google Fonts page — no font files hosted by us, licensing handled by Google Fonts). REQ-580-01 extended significantly: `UIAutomationClickThroughTests.cs` now does genuine UI-Automation-driven click-through (real `System.Windows.Automation` APIs, in-process, opt-in via `GEARWIN_UI_CLICKTHROUGH=1` — NOT part of default `dotnet test`, since it shows real windows and clicks on the live desktop). Getting this safe took several live-debugging rounds, each a real finding: a cross-thread Invoke()-with-timeout design corrupted WPF's native message loop and crashed the test host (~13 min); the automation tree included native title-bar chrome (clicking "System Menu" opened a real blocking Win32 menu); `ThemedMessageBox.Show()` genuinely calls `ShowDialog()` (~108 call sites app-wide) requiring a generic auto-dismiss watcher instead of guessing which sites to skip; `UwpAppManagerWindow`'s "Scan" (real WinRT `PackageManager` enumeration) reproducibly crashed the test host and had to be excluded. Final state: clean ~19s pass, 0 real findings. REQ-580-03 (Spotify/YouTube downloader) dropped entirely per user after declining to build it (DRM circumvention / distributed-piracy-tool concerns) — see ROADMAP.md.
- Committed + pushed + released on GitHub as v5.9.0 with installer attached.
- v5.8.0 shipped (2026-09-29): REQ-570-06 (unified Chromium-family browser password manager — Chrome/Edge/Brave/Vivaldi/Opera/Opera GX, DPAPI+AES-GCM decrypt of local "Login Data" SQLite, new `Services/PasswordVaultService.cs` + `Views/PasswordManagerWindow.xaml`, opened from Network & Security), REQ-570-10 (ViVeTool curated list expanded 10→15 features), a full pop-up text-truncation audit across all 14 secondary windows, REQ-580-06 (Chinese/Korean/Indian flag icons fixed — were missing canonical elements), REQ-580-04 (Help window refreshed), REQ-580-01 first pass (`WindowSmokeTests.cs` — passive construction-only smoke test, still kept alongside the newer click-through test).
- v5.7.0 shipped (2026-09-29): REQ-570-01 (msstore "false success" fix — bucketed as opened-in-Store, not counted success/fail), REQ-570-02 (immediate update check on `--tray` launch), REQ-570-03 (Ultimate Performance toggle moved into Optimization tab), REQ-570-04 (Registry Cleaner card missing top margin, HealthView.xaml), REQ-570-05 (Opera GX browser-cache support added), REQ-570-07 (Brave/OBS/Telegram/Everything/ShareX/LibreOffice added to Recommended apps), REQ-570-08 (ThemedMessageBox icon Foreground now set per icon type — dark-mode visibility fix), REQ-570-09 (Settings sidebar icon Palette → Gear), REQ-570-11 (Skins tab merged into Theme Settings tab), REQ-570-12 (new first "Settings" tab: Desktop Widget + Update/Low-disk notifications + new "Launch with Windows to Tray" toggle, `SystemService.SetLaunchWithWindowsToTray`).
- v5.6.0 shipped (2026-09-29): Windows-toast update notifications, low-disk-space alerts, cumulative "bytes freed" + boot-time-trend card on Home, Health Check PDF export historical trend, "Appearance Settings" renamed to "Settings" (first pass), Run_OptimizerWpf.bat build-path fix.

## In Progress
Nothing mid-flight.

## Blocked
- REQ-580-05 (port MotionDeskStudio's opacity/transparency mechanism) — that codebase isn't in this repo/session; needs its actual project path before anything can be done.

## Next Actions
v5.8.0/v5.9.0 backlog otherwise complete. REQ-580-01 could still be extended further (memory-leak detection specifically — the click-through part is now real and working) if worth the effort; `HealthCheckWindow`'s real cleanup action and `ViveToolWindow`'s real feature toggles remain intentionally excluded from automated clicking (genuinely destructive/system-modifying, must never be triggered blindly).

## MotionDeskStudio note
Distinct from the REQ-580-05/580-03 items above: earlier in v5.7.0's session the user separately pasted MotionDeskStudio's own roadmap file for awareness (different project, different repo, not resolvable from here) — still unanswered whether/how to act on it. Not part of GearWin's backlog.

## Important Decisions
- 2026-09-29: MotionDeskStudio is a **separate project** (its own roadmap, MOTIONDESK STUDIO.MD, was also pasted the same day) — never mix its context with GearWin. Its source is not part of this repo/session; if asked to act on it, need its actual project directory first.
- New service files must be checked against existing ones before creation (`Grep` for the class name) — see Known Bugs below for why.

## Known Bugs
- (User-reported, v5.7.0 backlog, not yet investigated) Software Update Manager can show an "installed" success message without the winget install actually having succeeded — check exit-code handling in the winget install call sites (OptimizationView.xaml.cs / BloatwareView.xaml.cs).
- (User-reported, v5.7.0 backlog) Dark-mode rendering issue with the square icon in the install-success popup.

## Known Limitations
- Windows Update scan (`Services/WindowsUpdateService.cs`) uses the `Microsoft.Update.Session` COM API — search only, no install; can fail under WSUS/Group-Policy-managed environments (best-effort, silently returns unsuccessful).
- Startup-item "impact" (High/Medium/Low, System tab) is a real but session-observed estimate (time since explorer.exe start), not a guaranteed prediction for the next boot — see `SystemService.GetStartupDelayEstimates`.

## Relevant Modules
- `Services/UpdateNotificationService.cs`, `Services/UpdatesHubService.cs`, `Services/TrayIconService.cs` — background update/low-disk notifications.
- `Services/AppSettingsService.cs` — all persisted user settings (JSON, `%LocalAppData%\OptimizerWpf\AppSettings.json`).
- `Services/LanguageService.cs` — all UI strings, 14 languages.
- `Views/HomeView.xaml(.cs)`, `Views/OptimizationView.xaml(.cs)`, `Views/SystemView.xaml(.cs)`, `Views/HealthCheckWindow.xaml(.cs)` — largest/most frequently touched views.

## Last Verification
Build: `dotnet build -c Release` — succeeded, 0 errors (2 pre-existing WFAC010 warnings, unrelated).
Tests: `dotnet test` (OptimizerWpf.Tests, 62 tests) — 62/62 passed (default battery; opt-in `UIAutomationClickThroughTests` separately verified clean with `GEARWIN_UI_CLICKTHROUGH=1`).
Installer: `ISCC.exe installer/OptimizerWpf.iss` — succeeded, `GearWin-Setup-5.9.0.exe`.
Date: 2026-09-30 (v5.9.0 release).

## Last Updated
2026-09-30
