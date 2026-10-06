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
Shipped. `claude/full-audit` fast-forward-merged into `master` (no conflicts, master had no unique
commits) and both pushed, 2026-10-06. Followed by: upgrade to .NET 10 (`net8.0-windows` ->
`net10.0-windows`, app + tests) and enlarging the title bar's animated gear icon (44->56 badge, 32->38
glyph) and title text (20->26 "GearWin", 11->13 version) per explicit user request - also committed
and pushed to both branches. Governance docs (CLAUDE.md, PROJECT_STATE.md, ROADMAP.md) adopted
2026-09-29 at user's request.

## Completed
- v6.3.5 built (2026-10-06, branch `claude/full-audit`, NOT committed yet) - see Current Objective for the full 13-item list. First time this branch's WPF Views were actually built+run-verified on Windows (prior `claude/full-audit` commits, per the "Audit pass" section below, were compile-checked Services-only on Linux).
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
Build: `dotnet build -c Release` — succeeded, 0 errors (2 pre-existing WFAC010 warnings, unrelated).
Tests: `dotnet test` (OptimizerWpf.Tests, 156 tests) — 156/156 passed, including the 14-language key-parity test (all new keys added to all 14 blocks).
Installer: `ISCC.exe installer/OptimizerWpf.iss` — succeeded, `GearWin-Setup-6.3.5.exe`, sent to user.
Date: 2026-10-06 (v6.3.5, branch `claude/full-audit`, not yet committed/pushed/released).
**Still NOT visually/manually verified**: all v6.3.5 UI changes (Boost button, searchbar, tab wheel-scroll, shield icon, codec grid, converter quality combo, window sizing) - no GUI-automation capability in this session. Also still carried over, never confirmed: the v5.10.1 borderless-window rebuild (drag, resize, maximize, edge-snap, real transparency).

## Last Updated
2026-10-06

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
