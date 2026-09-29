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
No single active objective — user drives work in small/medium batches (Greek, terse). Just shipped v5.8.0 (REQ-570-06/10 + partial v5.8.0 backlog). REQ-570-13 explicitly skipped per user. REQ-580-02/03/05 remain (see Next Actions).

## Current Phase
Between releases. Governance docs (CLAUDE.md, PROJECT_STATE.md, ROADMAP.md) adopted 2026-09-29 at user's request.

## Completed
- v5.8.0 shipped (2026-09-29): REQ-570-06 (unified Chromium-family browser password manager — Chrome/Edge/Brave/Vivaldi/Opera/Opera GX, DPAPI+AES-GCM decrypt of local "Login Data" SQLite, new `Services/PasswordVaultService.cs` + `Views/PasswordManagerWindow.xaml`, opened from Network & Security), REQ-570-10 (ViVeTool curated list expanded 10→15 features, sourced from a current public listing, documented as periodic manual refresh since no live feed exists), a full pop-up text-truncation audit across all 14 secondary windows (7 files fixed, `Width`→`MinWidth` on translated buttons — verified across all 14 languages, not assumed), REQ-580-06 (Chinese/Korean/Indian flag icons fixed — were missing canonical elements, not just "low-res"; other 11 flags audited and already correct), REQ-580-04 (Help window was stale — missing the new password manager entirely and still said "Appearance Settings" instead of "Settings"; both fixed in all 14 languages), REQ-580-01 partial (new `OptimizerWpf.Tests/WindowSmokeTests.cs` — constructs all 12 parameterless secondary windows on a real STA thread, asserts no exception; does NOT click individual buttons or detect leaks/freezes, no UI Automation driver available in this environment). See ROADMAP.md for full per-item detail and honest DONE/PARTIAL rationale.
- Committed + pushed + released on GitHub as v5.8.0 with installer attached.
- v5.7.0 shipped (2026-09-29): REQ-570-01 (msstore "false success" fix — bucketed as opened-in-Store, not counted success/fail), REQ-570-02 (immediate update check on `--tray` launch), REQ-570-03 (Ultimate Performance toggle moved into Optimization tab), REQ-570-04 (Registry Cleaner card missing top margin, HealthView.xaml), REQ-570-05 (Opera GX browser-cache support added), REQ-570-07 (Brave/OBS/Telegram/Everything/ShareX/LibreOffice added to Recommended apps), REQ-570-08 (ThemedMessageBox icon Foreground now set per icon type — dark-mode visibility fix), REQ-570-09 (Settings sidebar icon Palette → Gear), REQ-570-11 (Skins tab merged into Theme Settings tab), REQ-570-12 (new first "Settings" tab: Desktop Widget + Update/Low-disk notifications + new "Launch with Windows to Tray" toggle, `SystemService.SetLaunchWithWindowsToTray`).
- v5.6.0 shipped (2026-09-29): Windows-toast update notifications, low-disk-space alerts, cumulative "bytes freed" + boot-time-trend card on Home, Health Check PDF export historical trend, "Appearance Settings" renamed to "Settings" (first pass), Run_OptimizerWpf.bat build-path fix.

## In Progress
Nothing mid-flight.

## Blocked
- REQ-580-05 (port MotionDeskStudio's opacity/transparency mechanism) — that codebase isn't in this repo/session; needs its actual project path before anything can be done.
- REQ-580-03 (Multimedia tab: Spotify/YouTube downloader) — flagged as a policy concern, not a scope/technical blocker: a built-in downloader for copyrighted streaming content (Spotify tracks, YouTube video/audio) would facilitate ToS/copyright infringement. Needs the user to either drop this item or narrow it to something legitimate (e.g. a local media format converter for files the user already has) before implementation.

## Next Actions
REQ-580-02 (font management: style preview + free-font suggestions with web preview) not started. REQ-580-01 could be extended further (real UI Automation click-through, memory-leak detection) if a suitable driver/tool becomes available — currently out of reach in this environment.

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
Tests: `dotnet test` (OptimizerWpf.Tests, now 61 incl. new WindowSmokeTests) — 61/61 passed.
Installer: `ISCC.exe installer/OptimizerWpf.iss` — succeeded, `GearWin-Setup-5.8.0.exe`.
Date: 2026-09-29 (v5.8.0 release).

## Last Updated
2026-09-29
