# PROJECT STATE

## Project
Name: GearWin - Complete PC Care (WPF port of the original Optimizer.ps1)

## Technology
- .NET: net10.0-windows
- C#: WPF (OptimizerWpf.csproj), assembly/exe name `GearWin.exe`
- UI: WPF, `wpf/OptimizerWpf/` — Views (XAML + code-behind, no MVVM/ViewModels — direct code-behind is the established pattern here), Services (static classes), Themes/Styles.xaml. WPF-UI 4.3.0 nuget also in use for the standalone `DashboardPreviewWindow` pilot only (normal app/MainWindow untouched).
- Mobile: none
- Backend: none (fully local desktop app)
- Tests: `wpf/OptimizerWpf.Tests/` (xUnit-style, run via `dotnet test`), 182 tests in the default battery (all passing as of last verification), plus one opt-in UI Automation click-through test (`GEARWIN_UI_CLICKTHROUGH=1`, shows real windows - not run by default)
- Installer: Inno Setup, `installer/OptimizerWpf.iss` → `installer/Output/GearWin-Setup-<version>.exe`. Build: `dotnet publish -c Release -r win-x64 --self-contained true -o wpf/OptimizerWpf/publish/win-x64` then `ISCC.exe installer/OptimizerWpf.iss`. ISCC lives at `AppData\Local\Programs\Inno Setup 6\ISCC.exe` (not Program Files).
- 14 supported UI languages, all strings in `Services/LanguageService.cs` (huge file, ~18k lines, one dictionary block per language in fixed order: el, en, de, fr, es, ko, zh, it, ru, ja, pt, tr, ar, hi). New keys must be added to all 14 blocks (use a small Python script to insert by anchor-key line number, descending order, rather than hand-editing).
- Repo: `giorstergiopoulos-design/GearWin` on GitHub. `gh release create` works from this environment (has previously needed a retry after an auto-mode permission block).

## Current Objective
v6.3.5 (2026-10-06, branch `claude/full-audit` - this is the ACTIVE branch, NOT master; a separate
cloud session created it, this session verified it builds/runs on Windows for the first time and
continued work on it) implements the full 13-item GEARWIN.MD backlog: Games+Multimedia merged into
one tab (`Views/GamesMediaView`, replaces GamesView/MultimediaView), GPU-pref/FSO/exe-picker on one
row, shield icon dark-mode fill fix, third-party codec detection (K-Lite + DirectShow filter registry
scan, `Services/MultimediaServices.cs`'s `ThirdPartyCodecService`) + 3-column codec grid, converter
gained WebM/MKV/OGG/AAC/WAV/FLAC + Low/Medium/High quality (`MediaQuality` enum), tab strip scrolls
with mouse wheel when it overflows, searchbar modernized (rounded, icon, clear button), "Cumulative
Benefit" folded into the health-score message list instead of its own Home card, new Boost button on
Home (next to Full Health Check, mirrors PcManagerHomeView's), update-manager button now shows
"Installing" instead of "Scan" during installs, fixed a real bug where msstore-sourced winget rows
were mislabeled "winget" (causing failures + an unexplained Store popup), ESET/Defender status wording
fixed, Maintenance Center tabs now use the app's pill style (was default white Windows tabs), and pill
button padding increased app-wide (TabPillStyle/RailPillStyle/PillTabItemStyle). Built: `dotnet build`
0 errors, `dotnet test` 156/156, installer `GearWin-Setup-6.3.5.exe` built and sent to the user.
**NOT YET committed to git** (working tree only) - awaiting the user's decision on whether to commit/
push to `claude/full-audit` and whether/when to merge to `master` or create a GitHub release.

## Current Phase
Shipped, both branches kept in sync by repeated fast-forward merges (`claude/full-audit` -> `master`)
after every commit, latest 2026-10-07 (`e5d5f65`). Since the v6.3.5 ship (2026-10-06): .NET 10 upgrade,
title-bar icon/text enlargement, a `DashboardPreviewWindow` WPF-UI pilot (opens only via
`--dashboard-preview`, normal app untouched), two rounds of app-wide button-padding/sizing fixes (plain
tight-padding sweep, then a separate Grid Auto-column-compression fix), winget App-Execution-Alias +
onboarding-wizard autostart fixes with new diagnostic logging + status-bar visibility, and the
Maintenance Center one-line-tabs sizing fix - see Completed for details. Published binary
(`publish/win-x64`) re-built after every change (lesson: the `.bat` launchers prefer that folder over
`bin/Release`, going stale silently otherwise). Governance docs (CLAUDE.md, PROJECT_STATE.md,
ROADMAP.md) adopted 2026-09-29 at user's request.

## Completed
- Games & Multimedia additions (2026-10-07, user asked for suggestions then "κανε τα ολα" - do them
  all): checked each idea against existing code first (Game Mode toggle turned out to already exist
  in `GamingTweaksService.GlobalTweaks()` - skipped, not duplicated). Games: per-game tweak profiles
  (REQ-590-06, now DONE - `GameProfileService.cs`, stores which of the 4 machine-global gaming
  tweaks should be on for a given exe; GPU pref/FSO were already per-exe via registry so didn't need
  a new store; Save/Apply are explicit user actions, no process-watching/auto-apply since that's not
  verifiable here), launcher cache cleanup (new QuickClean "LauncherCache" category - Steam
  httpcache + Epic webcache only, never depotcache/downloading), GPU driver freshness hint in the
  Games library card (reads `OptimizationView.CachedVendorScan`, never triggers its own scan), game
  save backup (`GameSaveBackupService.cs` - zips ONLY the two documented generic Windows save
  folders, "Saved Games" + "Documents\My Games" - UI text says exactly that, not "all saves", since
  per-game paths can't be reliably guessed). Multimedia: batch conversion (Multiselect + loop),
  drag-and-drop onto the converter, live mic/camera test (opens the real Windows Camera app +
  Sound control panel's Recording tab instead of building unverifiable custom capture code),
  one-click codec pack (links to the official download page, not an unconfirmed winget id), HDR
  hint (same heuristic style as the existing high-refresh hint, BitsPerPixel>=30 - not a real
  DXGI HDR-active check). Added `GamesMediaViewConstructsWithoutThrowing` - this UserControl had
  ZERO smoke-test coverage before despite being the tab most changed here; a real gap, not a
  deliberate exclusion. 23 new language keys x 14 languages. 182 tests passing.
  **NOT visually verified** (no GUI automation in this session).
- Universal button text-touches-edge fix (2026-10-07, user explicitly demanded a systemic rule
  instead of more per-button guessing - "δεν μπορεις να βαλεις εναν universal kanona"): new
  `Services/ButtonAutoFit.cs` attached behavior, wired once onto `FlatButtonStyle` in `Styles.xaml`
  (`services:ButtonAutoFit.Enabled="True"`, inherited automatically by `AccentButtonStyle`). On
  `Loaded` and on every `Content` change (covers live language switching via `{tr:Tr}`'s binding),
  it measures the button's OWN text with `FormattedText` using its actual `FontFamily`/`FontSize`/
  `Padding`/`BorderThickness` and raises `MinWidth` to fit - automatically, per button, per language,
  for every button that exists today AND every button written from now on, with zero manual
  measurement. Never shrinks an existing larger `MinWidth` (the ~85 hand-picked values from the two
  earlier rounds below still work as floors; nothing needs removing, and nothing like them needs
  adding again). This is the actual root-cause fix for the Grid Auto-column-compression bug - the
  two rounds below were the stopgap, button-by-button version of what this now does for the whole
  app in one place. One real bug caught during implementation: `DependencyProperty.Register`
  requires the owner type to derive from `DependencyObject`; `RegisterAttached` is the correct API
  for an attached property on a static helper class - threw `TypeInitializationException` at
  startup until fixed (caught immediately by the existing `WindowSmokeTests`, not shipped).
  New `ButtonAutoFitTests.cs` (6 tests: grows to fit text+padding, longer text needs more width,
  never shrinks an existing larger MinWidth, raises an existing too-small one, ignores non-string
  Content, wider padding needs more width) locks the behavior in. 175 -> 181 tests, all passing.
  Build + tests verified; **NOT visually verified** (no GUI automation in this session).
- Maintenance Center tabs forced onto one line (2026-10-07, explicit user request: "οι καρτελες να
  ειναι σε μια ευθεια αρα προσαρμοσε το μεγεθος του παραθυρου αναλογα"): fixed `Width="760"` didn't
  always fit all 5 `PillTabItemStyle` tabs in a single row for longer-language labels. Added
  `MaintenanceCenterWindow.FitWidthToTabs()` (called from the constructor) - measures the widest
  5-tab-label sum across all 14 languages via `FormattedText` (using the style's known fixed
  Padding/Margin/FontSize/FontWeight constants - `TabControl` doesn't expose its internal tab strip
  as a named element the way `MainWindow`'s hand-built `StackPanel` strip does) and sets `Width`
  accordingly (clamped between `MinWidth` and 90% of the work-area width). Build + full test suite
  (175/175) verified; **NOT visually verified** (no GUI automation in this session).
- Button text-touches-edge fix, second root cause (2026-10-07, user screenshot of
  "Αναβάθμιση Επιλεγμένων" vs "Έλεγχος Ενημερώσεων μέσω Microsoft Store" - one had margin, the other
  didn't despite both having the same kind of `Padding`): this is a DIFFERENT bug from the plain-
  padding sweep below. Root cause: a `Button` sitting in a `Grid.Column` next to a sibling with a
  hardcoded `Width` inside a `Grid` that also has a `*` column - when the row's total desired width
  exceeds available width, WPF compresses `Auto` columns below their desired size as a last resort,
  visually "eating" the button's own correct `Padding` with no error anywhere. Fixed the reported
  button (`OptimizationView.xaml`'s `BtnUpgradeSelected`: `Width="180"` -> `MinWidth="180"`;
  `BtnCheckMsStore` gained `MinWidth="300"`) plus 17 more buttons across 8 files sharing the identical
  structural risk pattern (found via grep: `Grid.Column` child + `Padding`-only + sibling `*` column),
  each given an individually-sized `MinWidth` (not `Width`, to preserve room for longer translations).
  Build + full test suite (175/175) verified; **NOT visually verified**.
- Button padding, app-wide (2026-10-07, user flagged this repeatedly across the session): the 6.3.5
  fix only widened the Styles.xaml DEFAULT Padding on `FlatButtonStyle`/`AccentButtonStyle`/pill
  styles - it didn't help most real buttons, which set their OWN explicit (tighter) `Padding` per
  View, overriding that default entirely. Found 65+ instances of `Padding="8,4"`/`"12,6"`/`"14,6"`/
  `"10,5"`/`"10,6"` etc. across 16 files. Scripted sweep: every `Button` using
  `FlatButtonStyle`/`AccentButtonStyle` with horizontal padding < 16 got it bumped to 16 (vertical
  padding left untouched, so button heights/layout rhythm didn't shift) - 68 buttons fixed via regex
  sweep, +1 manual fix for a `<Button.Style>` property-element case the regex couldn't reach
  (`OptimizationView.xaml`'s pin button). Left alone on purpose: `TextBox`/`PasswordBox` `Padding="8,4"`
  (input fields, not the "text touches the button edge" complaint) and icon-only square buttons
  (`Padding="0"`, e.g. window chrome). Build + full test suite (175/175) verified.
- v6.3.5 polish (2026-10-07): fixed the real "no update check/balloon tip on autostart" bug - winget.exe
  is an App Execution Alias (reparse-point stub in `%LOCALAPPDATA%\Microsoft\WindowsApps`) whose PATH
  entry can lag right after logon, especially when launched via Task Scheduler's LogonTrigger (the
  `--tray` autostart) rather than explorer.exe; the bare `Process.Start("winget.exe", ...)` failed
  silently (`Win32Exception`), `WingetService.ScanAsync()` read that as "0 updates" (honest-by-design,
  but indistinguishable from a real empty result), so `UpdateNotificationService.StartupCheckAsync()`
  never had anything to announce - no error anywhere. Fixed with an absolute-path fallback in
  `WingetService.RunToolAsync` (covers every winget call site: scan/pin/export/import/upgrade). Also
  found and fixed a related bug while investigating: the first-run onboarding wizard
  (`App.xaml.cs`) could block the entire `--tray` silent-startup sequence (tray icon, update check)
  since it wasn't gated on `--tray` - now skipped on autostart.
  Expanded automated test coverage: `WindowSmokeTests.cs`/`UIAutomationClickThroughTests.cs` gained
  `MaintenanceCenterWindow`/`HealthTimelineWindow` (both real gaps, not prior exclusions);
  `ParseWingetTable` made `internal` (same `InternalsVisibleTo` pattern as `ThemedMessageBox`) and
  covered by new `WingetServiceTests.cs`; `GamesMultimediaServicesTests.cs` extended with the 6 new
  6.3.5 converter presets, a quality-level-actually-changes-the-args test, and a
  `ThirdPartyCodecService` never-throws smoke test. 156 -> 175 tests, all passing. **Honest limit**:
  this is unit/construction-level coverage, not literally "every tool verified" - genuinely
  destructive/slow actions (Optimize Now, ViVeTool toggles, real driver/winget installs) remain
  deliberately excluded from automated click-through (documented in that file), and the opt-in live
  GUI click-through suite itself could not be run by this session (no elevation/display available,
  same constraint as the Dashboard preview below).
- WPF-UI pilot: `Views/DashboardPreviewWindow.xaml(.cs)` (2026-10-06) - a standalone, additive "Πίνακας
  Ελέγχου" window built with the WPF-UI 4.3.0 nuget (ui:FluentWindow/TitleBar/Card/InfoBar/Button/
  TextBox), reusing the SidebarShortcuts/NavItems/RailPillStyle/PcManagerHomeView.BuildArc/TweakRowVm
  patterns already proven in the real app, wired to real data (HealthScoreService, UpdatesHubService,
  WingetService, pinned tweaks) - no fabricated numbers/names. Opens ONLY via `--dashboard-preview`
  (App.xaml.cs), the normal app/MainWindow is 100% untouched. Visual reference: user's screenshot of
  "PC Performance & Maintenance Suite v5.1.0" (see `[[project_optimizer_shell_v510_reference]]` memory).
  Build + full test suite (156/156) verified; **NOT runtime/visually verified** - this session cannot
  elevate (app.manifest requires Administrator, `Start-Process -Verb RunAs` would hang with no user to
  click UAC, see `[[feedback_avoid_runas_hangs]]` memory) - user needs to run
  `GearWin.exe --dashboard-preview` themselves and report back.
- Installer gained a "Start with Windows" task on the Additional Tasks page (2026-10-06, unchecked by default) - writes `LaunchWithWindowsToTray=true` into `AppSettings.json` via a bundled `installer/SetAutostart.ps1` (PS 5.1-compatible, merges rather than overwrites so upgrades keep existing settings); the app's own already-tested `SystemService.EnsureLaunchTaskAsync()` (runs every launch) then creates the real Task Scheduler task - no duplicated logic in the installer script. 13 installer languages (same set as `ResetPromptText`/`UpdateDetected` - the installer wizard doesn't have a Turkish translation, pre-existing gap, not part of this change).
- v6.3.5 shipped (2026-10-06) - see Current Objective for the full 13-item GEARWIN.MD list, plus the .NET 10 upgrade and title-bar enlargement. Merged `claude/full-audit` into `master`, both pushed. First time this branch's WPF Views were actually built+run-verified on Windows (prior `claude/full-audit` commits, per the "Audit pass" section below, were compile-checked Services-only on Linux).
- v5.11.0 shipped (2026-09-30): REQ-590-01 (Fonts moved from Settings to System tab - it's inventory, not a preference), REQ-590-02 (saved Wi-Fi password viewer in Network & Security, using native WLAN API rather than locale-fragile netsh text parsing - this machine is Greek-locale, English-string parsing would have silently broken), REQ-590-03 (settings backup/restore - export/import AppSettings.json, validates before overwriting).
- v5.10.1 shipped (2026-09-30): fixed v5.10.0's opacity slider, which the user correctly reported produced no real transparency (just darkened/lightened the window's own color). Root cause: WPF's `Window.Opacity` requires `AllowsTransparency="True"` for genuine see-through transparency, which itself requires giving up native OS window chrome (`WindowStyle="None"`). Confirmed via git history that the *original* `Optimizer.ps1` (WinForms) had real transparency natively (WinForms doesn't have this restriction) - lost silently during the pre-session PowerShell→WPF port. Fixed properly, after explicitly asking the user given the real tradeoffs (forced software rendering, losing native chrome): `MainWindow` is now borderless with `shell:WindowChrome` restoring move/resize/maximize/edge-snap and the "maximized covers taskbar" fix automatically, plus new custom-drawn Minimize/Maximize/Close buttons. **Not visually verified by the assistant** (no GUI automation for a native, admin-elevated desktop app in this session) - explicitly flagged for the user to test by hand. Known limitation: Windows 11's snap-layout hover flyout on the maximize button doesn't appear (basic edge/keyboard snapping still works).
- v5.10.0 shipped (2026-09-30): REQ-580-05 — ported MotionDeskStudio's opacity mechanism (separate project at `C:\Users\gstrj\Documents\MotionDeskStudio`, per user's explicit request after providing that path). Replaced the old 3-option radio-button opacity (None/Light/Medium) with a continuous 60-100% `Slider` with live drag preview, matching MotionDeskStudio's own `BuildOpacityRow` UX exactly. New `AppSettingsService.WindowOpacityPercent` (int) replaces the old string `WindowOpacityMode`; dead language keys removed. One real bug found and fixed during implementation (XAML-wired `ValueChanged` firing mid-`InitializeComponent()` before a sibling element existed) — caught by the existing passive smoke test.
- v5.9.0 shipped (2026-09-30): REQ-580-02 (Font management — new "Fonts" tab in Settings: live preview of every installed system font with user-typed text, plus 12 suggested free/open-license fonts linking to their official Google Fonts page — no font files hosted by us, licensing handled by Google Fonts). REQ-580-01 extended significantly: `UIAutomationClickThroughTests.cs` now does genuine UI-Automation-driven click-through (real `System.Windows.Automation` APIs, in-process, opt-in via `GEARWIN_UI_CLICKTHROUGH=1` — NOT part of default `dotnet test`, since it shows real windows and clicks on the live desktop). Getting this safe took several live-debugging rounds, each a real finding: a cross-thread Invoke()-with-timeout design corrupted WPF's native message loop and crashed the test host (~13 min); the automation tree included native title-bar chrome (clicking "System Menu" opened a real blocking Win32 menu); `ThemedMessageBox.Show()` genuinely calls `ShowDialog()` (~108 call sites app-wide) requiring a generic auto-dismiss watcher instead of guessing which sites to skip; `UwpAppManagerWindow`'s "Scan" (real WinRT `PackageManager` enumeration) reproducibly crashed the test host and had to be excluded. Final state: clean ~19s pass, 0 real findings. REQ-580-03 (Spotify/YouTube downloader) dropped entirely per user after declining to build it (DRM circumvention / distributed-piracy-tool concerns) — see ROADMAP.md.
- Committed + pushed + released on GitHub as v5.9.0 with installer attached.
- v5.8.0 shipped (2026-09-29): REQ-570-06 (unified Chromium-family browser password manager — Chrome/Edge/Brave/Vivaldi/Opera/Opera GX, DPAPI+AES-GCM decrypt of local "Login Data" SQLite, new `Services/PasswordVaultService.cs` + `Views/PasswordManagerWindow.xaml`, opened from Network & Security), REQ-570-10 (ViVeTool curated list expanded 10→15 features), a full pop-up text-truncation audit across all 14 secondary windows, REQ-580-06 (Chinese/Korean/Indian flag icons fixed — were missing canonical elements), REQ-580-04 (Help window refreshed), REQ-580-01 first pass (`WindowSmokeTests.cs` — passive construction-only smoke test, still kept alongside the newer click-through test).
- v5.7.0 shipped (2026-09-29): REQ-570-01 (msstore "false success" fix — bucketed as opened-in-Store, not counted success/fail), REQ-570-02 (immediate update check on `--tray` launch), REQ-570-03 (Ultimate Performance toggle moved into Optimization tab), REQ-570-04 (Registry Cleaner card missing top margin, HealthView.xaml), REQ-570-05 (Opera GX browser-cache support added), REQ-570-07 (Brave/OBS/Telegram/Everything/ShareX/LibreOffice added to Recommended apps), REQ-570-08 (ThemedMessageBox icon Foreground now set per icon type — dark-mode visibility fix), REQ-570-09 (Settings sidebar icon Palette → Gear), REQ-570-11 (Skins tab merged into Theme Settings tab), REQ-570-12 (new first "Settings" tab: Desktop Widget + Update/Low-disk notifications + new "Launch with Windows to Tray" toggle, `SystemService.SetLaunchWithWindowsToTray`).
- v5.6.0 shipped (2026-09-29): Windows-toast update notifications, low-disk-space alerts, cumulative "bytes freed" + boot-time-trend card on Home, Health Check PDF export historical trend, "Appearance Settings" renamed to "Settings" (first pass), Run_OptimizerWpf.bat build-path fix.

## In Progress
Nothing mid-flight.

## Blocked
Nothing currently blocked.

## Next Actions
1. Decide with the user: create a GitHub release for v6.3.5? (merge/push already done, see Current Phase)
2. REQ-590-05 (app-usage bloatware suggestions) and REQ-590-06 (per-game Tweaks profiles) remain NOT STARTED (carried over, not part of GEARWIN.MD v6.3.5).
3. Manually verify by hand (no GUI automation available in this session): the new Boost button's visual placement, the modernized searchbar, the tab-strip wheel-scroll, the enlarged title-bar icon/text, and that the window's new size constants (estimated, not runtime-measured) actually look right for the 9-tab strip in every language.

## MotionDeskStudio note
Separate project, separate repo, at `C:\Users\gstrj\Documents\MotionDeskStudio` (own `.sln`, own git repo, `MOTIONDESK_MASTER_PROPOSALS_AND_ROADMAP.md`) — path confirmed by the user 2026-09-30 when asked. REQ-580-05 read its opacity mechanism (`src/UI/MainWindow.cs`) and ported the UX into GearWin (see Completed). No other MotionDeskStudio work has been requested or done from this session — still a distinct project with its own state, don't mix contexts without an explicit ask each time.

## Important Decisions
- 2026-09-29/30: MotionDeskStudio is a **separate project** — never mix its context with GearWin by default. Its path is now known (see above) but that doesn't imply standing permission to act on its own backlog; each cross-project task (like REQ-580-05) should still be a specific, confirmed ask.
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
Build: `dotnet build -c Release` (net10.0-windows) — succeeded, 0 errors.
Tests: `dotnet test` (OptimizerWpf.Tests) — 182/182 passed, including the 14-language key-parity test, `ButtonAutoFitTests`, and the new `GamesMediaViewConstructsWithoutThrowing`.
Publish: `dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64` (run from `wpf/OptimizerWpf` - NOT `../../publish/win-x64`, a path mistake made once this session that left `Run_OptimizerWpf.bat` launching a stale build; fixed, confirmed via `publish/win-x64/GearWin.exe` timestamp matching the latest build).
Installer: `ISCC.exe installer/OptimizerWpf.iss` — succeeded, `GearWin-Setup-6.3.5.exe`, sent to user.
Date: 2026-10-07.
**Still NOT visually/manually verified** (no GUI-automation/elevation capability in this session, all UI verification depends on the user testing and reporting back): v6.3.5's UI changes (Boost button, searchbar, tab wheel-scroll, shield icon, codec grid, converter quality combo), the Dashboard Preview pilot window, both rounds of button-padding/sizing fixes, the Maintenance Center one-line-tabs fix, and the v5.10.1 borderless-window rebuild (drag, resize, maximize, edge-snap, real transparency).

## Last Updated
2026-10-07

## Audit pass (2026-10-01, cloud session, branch `claude/full-audit`, NOT merged, NOT verified on Windows)
Compile-checked only (Services/* via scratch project on Linux; Views/XAML not buildable there). Needs `dotnet build` + `dotnet test` + manual click-through before release.
- Data-loss guards: FolderLock verifies the vault (GCM tag) before shredding the original and refuses drive roots/system/profile folders; duplicate delete always keeps one copy per group and skips junctions; registry cleaner aborts if `reg export` fails and uses unique backup names; hosts file written as UTF-8 (ASCII broke the Greek telemetry markers); TweakBackup entries removed after restore; service restore uses `Automatic` (not `Auto`).
- Hangs/leaks: redirected pipes drained (winget/store/driver/backup/wsl/system/vivetool), `Kill(true)`; HomeView PerformanceCounters disposed; Process objects disposed; ViveTool now installs into %LocalAppData%\OptimizerWpf\tools\vivetool (never into the app dir).
- App: single-instance mutex (+ installer AppMutex), error dialog throttled (30s), atomic AppSettings/TweakBackup saves, passwords/Wi-Fi keys auto-cleared from clipboard (30s), residual-folder search needs >=4 chars.
- Locale: added missing `Tweaks_ConfirmTitle`/`Tweaks_VisualEffectsConfirm` (14 languages). Parity script: all 14 languages have identical key sets (License_* intentionally el-only).
- Round 2 (same branch): powercfg — Ultimate Performance now really activates (/setactive) and is detected/removed via the GUID we created + previous scheme restored (old code searched the template GUID / English plan name; never activated); P1/P2 power tweaks apply with `/S SCHEME_CURRENT`. Tray — popup no longer reopens on the same click that closed it, ping throttled (10s, non-overlapping), quick-clean exceptions caught. Views — Gaming/Office mode + Ultimate toggles off the UI thread; winget upgrade loop / driver scan / driver backup+restore wrapped in try/finally (buttons no longer stuck); deep-uninstall only offers residual-folder deletion after exit code 0/3010; driver-store delete no longer uses `/uninstall /force` (could strip the active driver of a device); tweak toggle reverts on failure; firewall toggle reverts on failure; temp cleanup in Home 'Fix all' off the UI thread; process kill verifies name (PID reuse); Process objects disposed. Privacy — clipboard history now honours `ExcludeClipboardContentFromMonitorProcessing`/`CanIncludeInClipboardHistory=0` and is DPAPI-encrypted at rest (was plaintext JSON incl. copied passwords).
- Round 3 (same branch, compile-checked Services only): tweak profiles/pins now language-independent — `SimpleTweak.Id` (RegTweak.BackupKey or explicit id), `TweakRowVm.PinKey = list:Id`, legacy label-based keys still accepted/migrated (pins, `ApplyProfile`). Old exported profiles keep working. AppearanceSettings: language combo selected by Tag (was resetting es/it/ru/zh/ja/pt/ko/tr/ar/hi to Greek on open), `_loading` guard for XAML-wired handlers, opacity save debounced, tray/widget try/catch. HealthView: `BusyScope` guarantees SetIdle, tool re-entrancy guard, try/catch on refreshes. HealthCheckWindow: close via X cancels scan, optimize try/finally.
- Still not audited: Optimizer.ps1 (legacy). NOT verified on Windows (Views need build + click-through).

- 6.2.5 follow-up (2026-10-02): window transparency with a border. Layered-alpha on the native frame (WindowOpacityService) was never confirmed on Windows, so the DEFAULT is now own frame: AllowsTransparency + WindowChrome + own min/max/close buttons + 1px CardBorderBrush outline + work-area WM_GETMINMAXINFO hook; opacity = Window.Opacity (always live). `NativeWindowFrame` (new setting, Appearance checkbox, restart) restores the native frame with best-effort layered alpha. Old `BorderlessWindow` setting unused. NOT verified on Windows (compile + 90 tests only).

- REQ-590-04 (2026-10-02): System tab card "System health timeline" -> `Views/HealthTimelineWindow` + `Services/HealthTimelineService` (Event Log, last 30 days: Kernel-Power 41 / EventLog 6008 / BugCheck 1001 / Application Error 1000 + MsiInstaller 11707 / UserPnp 20001; installs <=48h before a BSOD/unexpected shutdown are flagged as suspect). 10 `Timeline_*` strings x 14 languages. Read-only. NOT verified on Windows (real Event Log queries/XPath). Still NOT STARTED: REQ-590-05 (usage-based bloatware suggestions), REQ-590-06 (per-game tweak profiles).
