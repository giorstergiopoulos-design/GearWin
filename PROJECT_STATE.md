# PROJECT STATE

## Project
Name: GearWin - Complete PC Care (WPF port of the original Optimizer.ps1)

## Technology
- .NET: net8.0-windows
- C#: WPF (OptimizerWpf.csproj), assembly/exe name `GearWin.exe`
- UI: WPF, `wpf/OptimizerWpf/` — Views (XAML + code-behind, no MVVM/ViewModels — direct code-behind is the established pattern here), Services (static classes), Themes/Styles.xaml
- Mobile: none
- Backend: none (fully local desktop app)
- Tests: `wpf/OptimizerWpf.Tests/` (xUnit-style, run via `dotnet test`), 60 tests, all passing as of last verification
- Installer: Inno Setup, `installer/OptimizerWpf.iss` → `installer/Output/GearWin-Setup-<version>.exe`. Build: `dotnet publish -c Release -r win-x64 --self-contained true -o wpf/OptimizerWpf/publish/win-x64` then `ISCC.exe installer/OptimizerWpf.iss`. ISCC lives at `AppData\Local\Programs\Inno Setup 6\ISCC.exe` (not Program Files).
- 14 supported UI languages, all strings in `Services/LanguageService.cs` (huge file, ~18k lines, one dictionary block per language in fixed order: el, en, de, fr, es, ko, zh, it, ru, ja, pt, tr, ar, hi). New keys must be added to all 14 blocks (use a small Python script to insert by anchor-key line number, descending order, rather than hand-editing).
- Repo: `giorstergiopoulos-design/GearWin` on GitHub. `gh release create` works from this environment (has previously needed a retry after an auto-mode permission block).

## Current Objective
No single active objective — user drives work in small/medium batches (Greek, terse). Just shipped v5.7.0 (most of the v5.7.0 backlog). Remaining v5.7.0 items need user decisions (see Next Actions). v5.8.0 backlog not yet started.

## Current Phase
Between releases. Governance docs (CLAUDE.md, PROJECT_STATE.md, ROADMAP.md) adopted 2026-09-29 at user's request.

## Completed
- v5.7.0 shipped (2026-09-29): REQ-570-01 (msstore "false success" fix — bucketed as opened-in-Store, not counted success/fail), REQ-570-02 (immediate update check on `--tray` launch), REQ-570-03 (Ultimate Performance toggle moved into Optimization tab), REQ-570-04 (Registry Cleaner card missing top margin, HealthView.xaml), REQ-570-05 (Opera GX browser-cache support added), REQ-570-07 (Brave/OBS/Telegram/Everything/ShareX/LibreOffice added to Recommended apps), REQ-570-08 (ThemedMessageBox icon Foreground now set per icon type — dark-mode visibility fix), REQ-570-09 (Settings sidebar icon Palette → Gear), REQ-570-11 (Skins tab merged into Theme Settings tab), REQ-570-12 (new first "Settings" tab: Desktop Widget + Update/Low-disk notifications + new "Launch with Windows to Tray" toggle, `SystemService.SetLaunchWithWindowsToTray`). See ROADMAP.md for full per-item detail.
- Committed + pushed + released on GitHub as v5.7.0 with installer attached.
- v5.6.0 shipped (2026-09-29): Windows-toast update notifications (apps/drivers/Windows Update, via `Services/UpdateNotificationService.cs` + `TrayIconService.ShowNotificationBalloon`), low-disk-space alerts (same mechanism), cumulative "bytes freed" + boot-time-trend card on Home (`Services/ImpactTrackingService.cs`), Health Check PDF export now includes historical score trend (`Services/HealthScoreDailyHistoryService.cs` — NOTE: distinct from the pre-existing, in-memory-only `Services/HealthScoreHistoryService.cs` used for the Home tab's live sparkline; do not conflate the two, see Known Bugs/history below), "Appearance Settings" renamed to "Settings", Run_OptimizerWpf.bat now follows the most recent real build (publish > Release > Debug).
- Committed + pushed (`6a8a3ca`) + released on GitHub as v5.6.0 with installer attached.

## In Progress
Nothing mid-flight.

## Blocked
- REQ-570-06 (unified browser password manager) — needs scope decision: Chromium-family only (straightforward, DPAPI+AES-GCM) vs. also Firefox (NSS interop, much bigger lift).
- REQ-570-10 (ViVeTool auto-sync of "top experimental features") — needs a data-source decision; no official feed exists.
- REQ-570-13 (code signing) — needs a purchased certificate; not a code task.

## Next Actions
v5.8.0 backlog (REQ-580-01..06) not started. REQ-580-01 and REQ-580-05 depend on / overlap MotionDeskStudio (separate project) — treat independently, don't assume one fix covers both. Re-confirm with user whether/how to proceed on MotionDeskStudio before touching anything there.

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
Tests: `dotnet test` (OptimizerWpf.Tests) — 60/60 passed.
Installer: `ISCC.exe installer/OptimizerWpf.iss` — succeeded, `GearWin-Setup-5.7.0.exe`.
Date: 2026-09-29 (v5.7.0 release).

## Last Updated
2026-09-29
