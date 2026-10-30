# Changelog

All notable changes to GearWin (formerly "PC Performance & Maintenance Suite"), summarized here for
GitHub. The full changelog — with complete detail and text in all 14 supported languages — is always
available inside the app itself, via Help/Instructions → the "Version History" tab.

Format loosely follows [Keep a Changelog](https://keepachangelog.com/).

## v6.9.0 — Git repo updates, SDI core, System tab absorbs Advanced/Devices

- **Git repositories in the app updater**: choose a folder ("Git repos folder…"); repos up to 3 levels deep are fetched and listed as `git` rows ("+N commits"); the update is `git pull --ff-only` only (never merge/rebase/force, no prompts, 40 s timeout per command, paths validated). Real fetch/pull round trip covered by a test.
- **SDI core**: when Snappy Driver Installer Origin is missing the driver card offers "Install SDI core (winget)" (fixed package id `GlennDelahoy.SnappyDriverInstallerOrigin`, confirmation first; SDIO is used scan-only as before).
- **Tab merge**: "Backup & Reports" (Advanced) is gone: Reports, Backups and Tools are on System › Tools; Devices (displays, audio, camera/microphone) and Codecs now sit under System › Information; Games & Media keeps only Games. Old tags (`Advanced#N`, `System#2`, `GamesMedia#1`) were remapped in the search index/Health Check/shortcuts; the first-run tutorial lost the two slides of removed tabs; welcome-guide text updated in 14 languages. 7 tabs.
- **Installer**: already machine-wide (Program Files, admin, 64-bit); GearWin never had a per-user install, so there is nothing to migrate or remove.
- Tests: 323.
- **Remaining for 6.9.1** (not done): Vivetool "update feature list" (needs a verified online source), UWP view redesign, fonts (more sources, move to Tools), health timeline into Health & Repair, system updates inside the nightly task, merging the two guides, per-source updater progress/cancel/filters/ignore list, accessibility names, async-void/blocking-wait audit and silent-failure review of the updater, README/screenshots, repo hygiene (`reference\`, old notes), grammar pass of the 14 languages, Help tab-map texts still describe the pre-6.5 tabs.

## v6.8.0 — Tabs merged, one scan button, uninstall, Windows features list

- **"Cleanup & Performance" merged into "Health & Repair"**: the tab now has Repair | Performance | Cleanup | Startup. The old `Optimization#N` tags (tray, Health Check, PC Manager quick cards, search index, Ctrl+3) redirect to `Health#N+1`. 8 tabs instead of 9; the welcome-guide text (14 languages) was updated to the new tab names.
- **Uniform card spacing**: after tab composition every page now gets the same 16 px between cards (the first card has none), also between views of one page (`TabLayout.NormalizeSpacing`).
- **One scan for updates**: "Scan everything (apps, Windows, drivers)" starts the app scan (which already lists Windows Update items) and the driver scan together; single-flight kept.
- **Uninstall from the update list** (winget, pip, npm/pnpm global, choco, scoop, gem, cargo, dotnet tool — also AI agents installed that way): per-row button with explicit confirmation, id whitelist as for upgrades; Windows Update and Store rows have no uninstall.
- **Windows optional features**: the list with each feature's state is now always shown (loaded automatically the first time, silent on failure).
- Tests: +9 (uninstall commands/unsafe ids, Health sub-pages, no Optimization nav item).
- **Remaining for 6.8.1** (not done): system updates inside the nightly task (kept out on purpose: restarts), SDI core download for the driver updater, git-repo update detection, Vivetool "update feature list" (needs a verified online source of feature IDs), UWP view redesign, fonts/devices/health-timeline/codecs/reports re-grouping, merging the two guides, accessibility names, async-void/blocking-wait audit, README screenshots, repo-hygiene pass, grammar pass of the 14 languages.

## v6.7.1 — Update popup like the Crawler's

- New-version notice is now a small popup (bottom right, never steals focus) with the same buttons and messages as Opsis News Crawler: **Install** (Settings > GearWin update, SHA-256 mandatory, explicit confirmation; release page when the release has no digest), **Later**, **Skip this version** (never announced again, nor any older one) and **What's new** (release page), plus the tray balloon. Check/ETag/once-per-version/full-screen deferral unchanged from 6.6.0. New strings in 14 languages, new test for the skip rule (306 tests).
- NOT done in 6.7.1 (carry over): system updates together with the updater, updater progress/cancel/filters/ignore list, "Cleanup & Performance" into "Health & Repair", card spacing, SDI core download, winget uninstall, git-repo updates, Vivetool list button, UWP view, Windows-features list, fonts/devices/timeline/codecs/reports regrouping, merging the two guides, keep-alive tab pages, accessibility names, README screenshots, async-void/blocking-wait audit, repo hygiene, grammar pass. Not verified live: the popup in the real app (only built and unit-tested).

## v6.7.0 — Media converter removed

- **Removed the video/audio converter** (user decision: conversion now lives in Opsis Media Player Pro for video/image and Waveframe for audio): the card, `MediaConverterService`/`MediaPreset`/`MediaQuality`, the ffmpeg detection, 28 unused translation keys in all 14 languages and their tests. In its place a "File conversion has moved" card on Games & Multimedia > Media opens "The Opsis family". The Media page keeps the Codecs card; `Help_MediaDesc` no longer mentions ffmpeg (14 languages). The font cards stay for now.
- New test: every app of the suite catalogue resolves to an embedded real icon (>= 64 px).
- Audit: all `new HttpClient` in the code already have a timeout (checked).
- Tests: 305. Installs to Program Files (admin installer) as before.
- **Remaining for 6.7.1** (not done in this release): system updates together with the updater and updater bug fixes (progress/cancel/filters/ignore list), merging "Cleanup & Performance" into "Health & Repair" and card spacing, SDI core download, winget uninstall, git-repo update detection, Vivetool list button, UWP view, Windows-features list, fonts/devices/health-timeline/codecs/reports re-grouping, merging usage and welcome guides, keep-alive tab pages, accessibility names, README screenshots, async-void/blocking-wait audit, repo-hygiene pass, grammar pass of 14 languages.

## v6.6.0 — Welcome guide, Opsis family, new-version notice

- **Welcome guide** (`WelcomeGuideWindow`): 5 short pages, shown once per new major.minor version (per-version "Don't show again", on by default) and from Help / tray / classic menu. Closing it shrinks the card into the window's gear icon (~400 ms; plain fade when Windows animations are off or the render tier is low). On a first install the older detailed tutorial still runs and the guide is skipped for that run.
- **The Opsis family** (`SuiteShowcaseWindow`): one card per app of the suite (animated icon, tagline, capabilities, "Installed vX / Not installed / Update available / You are here", Open / Install / Details). Offered once after the guide, reachable from Help, tray and classic menu. The catalogue (`suite/apps.json` in the Opsis repo) is embedded as a fallback and fetched at most once a day (5 s timeout, https and GitHub hosts only, 256 KB cap, owner pinned, unknown fields ignored). Install downloads only after a confirmation (file, size, source), saves to Downloads, verifies SHA-256 (GitHub `digest` or the `.sha256` asset) and deletes the file on mismatch; the installer is never run by GearWin. Installed state is read-only (known paths + Uninstall registry keys).
- **New-version balloon** (`ReleaseCheckService`): own check ~30 s after start, then every 24 h (before: queued behind the winget/driver/Windows Update scans and repeated on every start). One GET to the GitHub API with ETag, once per version, deferred while a full-screen app/game or presentation is active; click opens Settings (installer with SHA-256) or the release page.
- Reusable classes: `ReleaseVersion`, `OncePerVersionStore`, `SuiteCatalogService`, `InstalledAppDetector`, `GitHubReleaseClient`, `SuiteInstallerService`, `OverlayCardHost`.
- Install-path corrections found in the installer scripts: Opsis `%ProgramFiles%\Opsis - Desktop Customization`, GearWin `%ProgramFiles%\GearWin - Complete PC Care` (both admin installs), Kombos uninstall name is "Κόμβος", Waveframe (NSIS per-user) `%LocalAppData%\Programs\Waveframe`.
- Privacy: the update check and the catalogue fetch are plain GETs to api.github.com / raw.githubusercontent.com, no telemetry.
- Tests: new `SuiteAndReleaseTests` (version comparison, once-per-version store, catalogue validation incl. bad host/oversize/malformed, SHA-256 match/mismatch, installed detection with fakes, ETag/304, balloon rule) and `WelcomeSuiteWindowTests` (text fit of every guide page and the showcase in all 14 languages, closing effect). NOT verified live: a real download of another app's installer, a real newer GearWin release, the balloon as rendered by Windows.

## v6.5.5 — Gaming switches and startup in one place

- **HAGS** (hardware-accelerated GPU scheduling) had two switches for the same registry value with different "off" behaviour: Windows Tweaks > Performance (restores the previous value) and Games > Gaming settings (writes 1). It now lives only in **Games > Gaming settings**. The old tweak definition stays internally, so anything changed from the Tweaks tab earlier is still undone by "Restore all tweaks".
- **Startup:** the startup list (Cleanup & Performance > Startup) has a "Startup Delay" button that opens the Maintenance Center straight on its delayed-start tab, instead of a feature you could only find with Ctrl+J.
- Unchanged on purpose: the *Gaming Mode* bundle (Performance modes) still sets HAGS/Game DVR/network throttling as one action; the individual Game Bar and network-throttling switches stay in Windows Tweaks.
- Tests: 257 (new `GamingStartupGroupingTests`). Not verified live: toggling HAGS (needs administrator and a restart).

## v6.5.4 — Cards regrouped: Games | Media, and a Devices page in System

- **Games & Multimedia** is now two sub-pages: *Games* (library, per-game settings, gaming settings, shader cache, game-save backup) and *Media* (codecs, video/audio converter).
- **Displays, Audio devices and Camera & microphone usage** - cards that have nothing to do with games - moved to **System & Tools > Devices** (new sub-page between Information and Tools). Nothing was removed: every card, button and setting works as before.
- Global search (Ctrl+K) now points to the new pages, the in-app tab map and user guide describe them, and `TabLayoutTests.DeviceCardsMovedFromGamesToSystemDevices` guards the layout.
- Still open (needs a product decision, not changed): the gaming switches that exist in three places, the video/audio converter and the font cards, and the duplicated startup management.

## v6.5.3 — Updaters keep their state, wheel scrolls the list under the mouse, tab strip and text boxes fixed

- **Updaters (apps and drivers):** the scan/install state now lives in a shared `ScanState`, not in the view that is recreated on every tab change. Coming back to the tab shows the running scan (button locked, progress, status text) instead of an idle button, a second scan cannot start while one runs, and the result list is replaced atomically and de-duplicated (source + id) - no more doubled results after checking twice. The driver scan's cancel button works from any view instance.
- **Mouse wheel:** over an inner list (updates, drivers, processes…) the wheel scrolls that list; the page scrolls only when the list is at its end. Root cause: since 6.5.0 every hosted view has its own vertical-scroll-disabled `ScrollViewer` with the forwarding handler, which ran first (events tunnel from outside in) and always handed the wheel to the page. `NestedScrollHelper` now lets a deeper scroller that can still move take it, and forwards only to an ancestor that really scrolls.
- **Tab strip:** when the 9 tabs do not fit (narrow window, 150% scaling), only the selected tab keeps its name (others show the icon + tooltip), so no tab is cut at the right edge.
- **Wide side rail (PC Manager, Windows 11 Settings, Gaming Hub skins):** long labels wrap instead of being cut (Russian "Очистка и производительность", "Безопасность и конфиденциальность").
- **Text boxes / password boxes** are themed (they were unstyled light-grey Windows boxes in dark themes).
- **Status bar:** identical messages from parallel tasks appear once; the spinner no longer stays on if the hardware scan throws; concurrent hardware scans share one run.
- Tests: 252 (new: `ScanStateTests`, `NestedScrollTests` - the wheel test fails on the old helper - and `UiClippingProbeTests`, which opens the real window on every tab and sub-page and reports button text that is cut by its button or tabs cut by the strip; `GEARWIN_UIPROBE_*` environment variables select theme/skin, width, languages, screenshots).

## v6.5.2 — User guide rewritten, missing app logos, thread-safe language/theme switching

- **Help (user guide)** rebuilt around the new tab/sub-page structure (`HelpWindow.xaml`): cards are described on the tab where they live; new entries for RAM release, nightly updates, GearWin self-update, the Reports card, registry backup history and the Home security summary; the maintenance entry lists the real 10 buttons; obsolete per-old-tab section texts (`Help_OptTitle` etc.) removed from all 14 languages.
- **Recommended apps:** the 6 apps without a logo domain (Brave, OBS Studio, Telegram, Everything, ShareX, LibreOffice) now get their logo; all 29 verified with a contact sheet. `EveryRecommendedAppHasALogoSource` keeps it that way.
- **Robustness:** `ThemeManager`/`MainWindow` static event handlers (theme, language, status) now run on the window's own thread instead of throwing when raised from another thread. `LocalizationCoverageTests` checks that every `{tr:Tr ...}` key in the XAML exists in all 14 languages (found no gaps).
- Tests: 245, stable over repeated runs (test parallelisation stays disabled).

## v6.5.1 — Theme button label no longer cut off

- The header "Light Theme"/"Dark Theme" button had a fixed 120 px width; since the 6.4.0 padding fix its label was clipped in Greek ("☀ Φωτεινό Θέμα" needs 126 px) and Russian (122 px). It now sizes to its text (`MinWidth`), and `HeaderLayoutTests` checks all 14 languages in both modes (verified to fail on the old layout).

## v6.5.0 — New tab layout, GitHub self-update, RAM release

- **New 9-tab structure with sub-pages** (`TabLayout.cs`, `CompositeTabView`): Updates & Apps (Updates | Drivers | Apps), Cleanup & Performance (Performance | Cleanup | Startup), Health & Repair, Security & Privacy (Security | Network), Games & Multimedia, Windows Tweaks, Backups & Reports (Reports | Backups & Restore), System & Tools (Information | Tools). Tags are unchanged (shortcuts/tray/settings keep working); the tabs are composed from cards of the existing views, filtered by a per-view section table that `TabLayoutTests` keeps in sync with the XAML. Similar features now live together: all startup items/tasks/services/boot timeline on one page, all backups (restore points, registry, drivers) and reports in one tab. Search, Ctrl+1..9, tray balloons, PC Manager cards, Health Check actions and Onboarding follow the new structure; Help has a tab map.
- **GitHub self-update (Settings › GearWin update):** checks the latest GitHub release, downloads the installer only from the official repository, verifies its SHA-256 against the GitHub digest, installs silently and restarts the app. Optional startup/periodic check with a balloon (once per version). Never runs an unverified file (no digest or no admin rights -> opens the release page instead). `SelfUpdateService`, `SelfUpdateTests`.
- **RAM release (RAMMap equivalent):** `MemoryCleanerService` = empty background working sets + flush modified list + purge standby list through `NtSetSystemInformation` (needs admin; trim-only otherwise), skips the foreground app. Gaming Mode option "Free RAM when enabled" and a "Free RAM now" button, with a real before/after free-memory reading. The elevated purge path could not be exercised from the non-elevated test process (NOT VERIFIED live).
- **System Score buttons now do different things:** Refresh recalculates (guarded against double clicks, shows the update time); Boost = RAM only (no file deletion); Fix All = safe fixes through `QuickCleanService`/restore point with an honest summary (Defender/startup/restart only reported); Full Health Check = detailed staged scan. The "7 days since the last check" reminder is a line in the message list instead of a second "Full Health Check" button.
- **Header fixes:** horizontal modern menu adapts its spacing and keeps language/theme/window buttons on the right (they were pushed out of the window); icon-only buttons use `Padding="0"` (the 6.4.0 template fix had squeezed their icons); language flags redrawn (Korea, Japan) with borders; theme list shows swatches.
- Tests: 242 (default battery runs non-parallel to avoid the `Application` creation race); live UI Automation click-through passed (25 s).

## v6.4.0 — Button padding root cause, grouped modes/reports, new tools

- **Buttons (root cause):** the `FlatButtonStyle`/`AccentButtonStyle` template never applied `Padding`, so every padding value was dead and only explicit Width/Height/MinWidth gave spacing. The template now binds `Padding`; verified by rendering the tabs to images. (The universal `ButtonAutoFit` from 6.3.6 remains as protection against Grid compression.)
- **Update manager button:** while installing, the circular button shows a down arrow with a horizontal line beneath it.
- **Grouping:** Office/Gaming/Ultimate/auto-Gaming/scheduled-maintenance are one "Performance Modes" card; reports (summary TXT/PDF, diagnostics ZIP, battery, systeminfo, problem bundle) are one "Reports & Diagnostics" card; the Optimization tab has section headers (Updates / Drivers / Performance Modes). Removed the duplicate Advanced "System Report" and the 6-line "Deep Diagnostics Report".
- **Gaming Mode:** state persisted (toggles were never initialised, so a tab switch showed "off" while Windows stayed in the mode); restore point + change-journal entry (undoable from the Maintenance Center); switching with Office Mode really disables the other; auto Gaming Mode respects a manual one and no longer restarts Explorer; Game Mode registry value kept in sync with the Games tab; Optimization <-> Games cross-links.
- **New:** security summary card on Home (same `SecurityStatusService` as the Maintenance Center); global search indexes every card of every tab; optional automatic nightly winget app updates (03:30 scheduled task, runs `--auto-updates` headless; Windows Update/drivers/Store apps deliberately excluded); problem-report bundle (`ProblemReportService`, whitelist: crash.log, update-check.log, AppSettings.json + version info; nothing is sent automatically).
- Scheduled maintenance and nightly-update tasks (run with highest privileges) are only created when the executable's folder is not writable by normal users, like the start-with-Windows task.
- 221 tests.

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
