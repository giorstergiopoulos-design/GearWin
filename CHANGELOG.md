# Changelog

All notable changes to GearWin (formerly "PC Performance & Maintenance Suite"), summarized here for
GitHub. The full changelog — with complete detail and text in all 14 supported languages — is always
available inside the app itself, via Help/Instructions → the "Version History" tab.

Format loosely follows [Keep a Changelog](https://keepachangelog.com/).

## v6.3.9 — Windows updates in the update list, test isolation

- **Windows Update:** available Windows updates (including optional ones) are now listed in the Optimization tab's update list and installed from there through the Windows Update Agent, mirroring WMT 6.7 (`IsInstalled=0 and IsHidden=0`, `Online=true`; updates already installed since boot and awaiting a restart are counted separately, drivers stay in the driver scan). Verified with a real scan on a machine with 5 pending updates.
- **Root cause of "updates not detected":** the live UI-Automation test clicked every toggle and persisted the result into the user's real `AppSettings.json`, which turned `UpdateNotificationsEnabled` off and silently stopped the automatic check. Tests now use an isolated data folder (`GEARWIN_DATA_DIR`), run the test host without the normal app startup (`GEARWIN_TEST_HOST`), and skip buttons that open native file dialogs.
- **Fix:** tray/clipboard popups no longer throw when closed while losing focus ("...while a Window is closing").
- 219 tests.

## v6.3.8 — Faster tabs, updated libraries, crash log

- **Performance:** the Tweaks tab no longer reads every switch state (registry + `powercfg.exe` processes) synchronously on the UI thread - states are detected in the background; Home builds rows only for pinned tweaks and detects just those.
- **Security:** `Microsoft.Data.Sqlite` 8.0.10 -> 10.0.13 (removes the transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 high-severity advisory GHSA-2m69-gcr7-jv3q); `System.Management` updated; redundant `System.Diagnostics.PerformanceCounter` reference removed.
- **Stability:** unhandled/unobserved exceptions on background threads are written to `%LocalAppData%\OptimizerWpf\crash.log` (256 KB cap) instead of silently ending the process.
- **Start with Windows:** when the executable is in a folder normal users can modify, the message now says so (instead of "make sure the app runs as administrator").
- Tests: deferred-detection tests, live UI-Automation click-through updated for the settings export/import dialogs (216 tests).

## v6.3.7 — Security and memory audit

- **Security (the app runs elevated):** every PowerShell call now uses `-EncodedCommand` (no data inside quoted command lines, no scripts left in `%TEMP%` for an elevated process to run); files consumed by elevated tools (scheduled-task XML, driver packages, default-app XML, script fallbacks) live in `%ProgramData%\GearWin` with an administrators-only ACL; ViVeTool is installed there too and extracted from memory; package ids coming from npm/pip/gem/scoop/etc. are whitelist-validated before they reach `cmd.exe`; cleanup and game-save backup never traverse junctions; the elevated "Start with Windows" task is only registered when the executable's folder is not writable by normal users.
- **Gaming Mode** no longer disables VBS / Memory Integrity (HVCI), and no longer force-enables them when switched off.
- **Memory:** fixed a leak where every button stayed referenced by a static handler table after its view was recreated; the action history (500) and the update-check log (256 KB) are now bounded, and the action history is thread-safe.
- New regression tests for injection payloads, encoded-command round-trip, ACL checks and Gaming Mode (212 tests).
- Known remaining risk: tools found through the user's PATH (ffmpeg, python, npm...) run with the app's elevation.

## v6.3.6 — Games & Multimedia merged, per-game profiles, universal button fit, Store updates

(Also contains the 6.3.5 work: the Games + Multimedia tab merge, Boost button, new converter formats.)

- **Games & Multimedia** are now one tab (9 tabs total). Games: per-game GPU/FSO plus per-game profiles for the global gaming tweaks, launcher-cache cleanup (Steam/Epic), game-save backup (Saved Games + Documents\My Games), GPU-driver freshness hint. Multimedia: third-party codec detection (K-Lite etc.), batch conversion with drag-and-drop, WebM/MKV/OGG/AAC/WAV/FLAC presets with Low/Medium/High quality, live camera/microphone test shortcuts, HDR hint.
- **Home:** Boost button next to the full Health Check; "Cumulative Benefit" folded into the health-score messages.
- **Buttons:** a single `ButtonAutoFit` behavior measures every button's text and padding so a Grid can no longer squeeze it (fixes text touching the button edges, in every language).
- **Updates:** msstore apps are now updated through the Microsoft Store GUI (mechanism taken from WMT 6.7) with a confirmed result; winget failures show the real reason; the automatic check at startup is visible in the status bar and no longer blocked by the first-run wizard or a late-resolving winget path.
- **Drivers:** "Clean Old Drivers" keeps the in-use package first, then the highest version, then the newest date.
- **Popups:** tray/clipboard popups are placed correctly on high-DPI monitors.
- **Platform:** upgraded to .NET 10; installer gained an optional "Start with Windows" task; Maintenance Center sizes its window so all tabs fit on one line.
- Reference script `reference/WMT-GUI.ps1` synced to WMT 6.7.

## v6.2.5 — Unified skin sidebar, window sizing, Classic tabs

- Skins with a vertical bar (PC Manager, Windows 11 Settings, Gaming Hub) share the MotionDesk Studio collapse mechanism: 232/80 px animated width, ‹ › toggle, remembered state (`RailCollapsed`), icon-only with tooltips when collapsed, no clipping scrollbar. Gaming Hub starts collapsed.
- Window width is measured from the widest tab strip across all 14 languages and the strip is centred, so no language scrolls or clips; opacity slider now forces the style change (SWP_FRAMECHANGED), re-applies on first render and verifies the alpha; the settings window finds the main window even when opened from the tray.
- (previous note) Window width is measured from the real tab strip: it ends right after the System tab with the same 24 px gap Home has on the left (not overstretched); tab pills slightly tighter.
- Startup curtain sized from the window's content root, so it matches framed and borderless windows (and DPI-converted gear target).
- Windows Classic shows the tab strip again next to the classic menu; classic Settings menu no longer has a language submenu (Menu/Language opens the settings tab).

## v6.2.0 — Games & Multimedia tabs, new skins, Maintenance & Security Center, startup fixes

(Also contains the 6.1.0 work below. Help, quick tour and shortcuts were updated for the new tabs.)

- **Maintenance & Security Center** (Ctrl+J / Tools menu): security status panel (antivirus, firewall, UAC, Secure Boot, TPM, BitLocker, pending restart), session change list with one-click undo, restore point button, temperature / S.M.A.R.T. alert settings, startup program delay (scheduled task, reversible), one-click system report (.txt/.pdf), shared appearance with MotionDesk Studio.
- **Safety nets:** restore point is ensured before importing tweak profiles, "Optimize" with registry/tweaks, and app uninstall; "Optimize" now shows a preview (dry run) of what will run; every tweak toggle is recorded in the change list.
- **Cancel buttons:** SFC/DISM/chkdsk/WinSxS, driver scan, duplicate finder and disk analysis can be cancelled.
- 🛡 badge on tweaks that change the system (HKLM/services); Ctrl+K opens the global search.
- **New tabs:** *Games* (local library for Steam/Epic/GOG/Ubisoft/Xbox with launch/verify/uninstall, Steam pending-update flag, per-game GPU preference and fullscreen-optimizations toggle, gaming tweaks incl. Game Mode/HAGS/VRR, shader-cache cleanup) and *Multimedia* (displays and refresh rate, audio devices, who used the camera/microphone, HEVC/AV1/VP9/WebP/HEIF extensions, ffmpeg-based converter with cancel). Tab order is now Home, Optimization, Health, Network, Tweaks, Games, Multimedia, Bloatware, Advanced, System (Ctrl+1..9, Ctrl+0).
- **Skins:** PC Manager reworked (Mica palette, wide icon+label navigation, hero Home with health ring/Boost/Health check); new skins Windows 11 Settings (uses your Windows accent colour), Gaming Hub, Office Ribbon. Skin radio buttons kept.
- **Borderless option:** Appearance Settings > "Borderless window" (restart required) keeps the previous custom-frame window with its own minimize/maximize/close buttons; the Classic menu is clickable there too (`IsHitTestVisibleInChrome`).
- **Window & startup fixes:** normal Windows frame restored with real transparency via layered-window alpha (same technique as MotionDesk Studio); start-with-Windows now uses a Task Scheduler task (the Run key silently skips apps that require administrator); update check runs on every launch (waits for network); classic menu Settings now offers Settings/Appearance/Menu/Language submenu; Classic skin menu clicks fixed (they were swallowed by the old custom caption area); 7th shortcut (Maintenance Center) in side/horizontal menu.
- **Tab reorganisation:** System Restore Points and System Image Backup moved from System to Health; Startup Apps, System Services, Duplicate Finder, Folder Lock and Disk Benchmark moved from System to Advanced (hosted as `RecoveryCards` / `PowerToolsCards` user controls). System now only shows information, processes, storage and fonts.
- Ten themes got their own animated background; "Theme Settings" is now "Appearance Settings"; Home's per-category space analysis has its own drive selector.

## v5.3.0 — Renamed to GearWin

The app was renamed to "GearWin" with the subtitle "Complete PC Care" (after checking app-store
listings and trademark registries to avoid colliding with existing products - several earlier
candidates turned out to already be taken by real, similarly-purposed apps). The executable, window
titles, tray icon, installer, and license were all updated to match. The code's internal namespace
and the user data folder stay the same, so no existing settings are lost in this update.

## v5.2.0 — Tab Merges, FPS/Widget Fix, MIT+GPL3 License, and More

The software update card in Optimization now has a large circular, animated scan button (like the
driver one) instead of a plain button - the old update shortcuts were removed from Home. The disk
health trend moved to the end of the Health & Maintenance tab. Windows built-in components and
Windows optional features were unified into one card (Apps & Bloat tab), as was duplicate file
finding with the storage tools (System tab). Version History is now a tab inside the Help window -
its sidebar shortcut was replaced with Clipboard History. The Menu and Language tabs in Appearance
Settings were merged into one. Fixed a bug that permanently prevented FPS from showing in the
Desktop/Gaming Widget. The Microsoft PC Manager/Windows Classic skins were removed from the theme
dropdown - they're now selectable only from the Skins tab. The License now also includes the full
text of the GNU GPL v3.0.

## v3.7.0 — Vector Icons & Visual Fixes

Every emoji in the app (card titles, tabs, sidebar, onboarding guide) was replaced with new vector
icons that correctly follow every theme/mode — WPF's `TextBlock` doesn't reliably render color emoji
fonts in this environment, and two earlier font-based fix attempts weren't enough. Fixed the "Orbit"
animated background (now elliptical, angled, and strictly clipped to the tab strip instead of
spilling into content below it). Fixed a "C::" double-colon typo in the storage drives list. New
"System Information" header on the System tab.

## v3.6.0 — Lighter App, Lighter Windows & Visual Additions

New "Lighter Windows" card in Tweaks (disable Background Apps, SysMain/Superfetch, Indexing Options,
one-click Low Resource Profile). "My Device" now caches hardware facts that don't change within a
session. The animated background now also pauses when the window is fully hidden, not just minimized.
Smooth tab-switch fade, a red attention dot on the Health tab when the health score is low, and a
before/after free-space comparison after clearing browser cache.

## v3.5.1 — Visual Fixes: Title Gap, App Theme, Thermometers

More breathing room between the tab strip and the title bar. Fixed a real bug that made the "App
Theme" ComboBox render light-colored in every theme (dark and light alike) instead of its actual
theme color — confirmed via live pixel measurement. Temperature icons (🌡) replaced with vector icons,
correctly colored on every Windows build.

## v3.5.0 — Four Known Gaps Fixed

Tweaks toggles now reflect the real current registry state on load instead of always starting "off".
The pre-driver-install restore point (Microsoft Update Catalog source) is now automatic instead of a
separate prompt. Fixed a real bug in Windows Defender detection on "My Device" (wrong WMI class name).
The "Skins" tab now goes through the translation system.

## v3.4.x — Smart Update, Onboarding Guide, Crash Fixes

First-run onboarding tutorial (one slide per tab), safer uninstall flow, a smart-update mechanism for
the installer (skips reinstalling unchanged files), and fixes for a first-launch crash and an
update-detection bug in the installer.

## v3.3.0 — "My Device" & More Accurate Drive Detection

Full hardware/OS breakdown tab ("My Device": motherboard, CPU, RAM, GPU, drives, network, battery),
plus more accurate drive-type detection.

## v3.0.0 – v3.2.1 — The WPF Port

The application was fully rewritten from PowerShell/WinForms to native WPF/C# (.NET 8) — same feature
set, new foundation. Includes full 4-language translation, telemetry blocking, cloud-storage cleanup
helpers, a memory-freeing tool, and network status on the Home tab.

## Pre-3.0 — Optimizer.ps1 era

Versions 1.0 through 2.8.2 were the original PowerShell + WinForms tool (`Optimizer.ps1`, kept in this
repo for historical reference). That era covered: the initial feature set (1.0–1.3), a modernized menu
and first translations (1.4–1.5), visual themes and animated backgrounds (1.6), stabilization (1.7),
driver tools/health/security features (1.8–1.9), the Home tab and a large tool expansion (2.0), an
integrated menu and multi-source driver updates (2.1–2.6), and real window resizing with final polish
(2.7–2.8.2). Full detail for this era is in the in-app Version History and in `HANDOFF.md`.
