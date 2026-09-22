# Changelog

All notable changes to GearWin (formerly "PC Performance & Maintenance Suite"), summarized here for
GitHub. The full changelog — with complete detail and text in all 14 supported languages — is always
available inside the app itself, via Help/Instructions → the "Version History" tab.

Format loosely follows [Keep a Changelog](https://keepachangelog.com/).

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
