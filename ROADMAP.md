# ROADMAP

Source: user-authored backlog (GEARWIN.MD, pasted 2026-09-29). Requirement format per CLAUDE.md §8.
Status values: DONE / PARTIAL / BLOCKED / NOT STARTED.

## v5.7.0

- REQ-570-01 DONE (2026-09-29) — Root cause: the "msstore" upgrade source only *opens* the Store's Downloads & Updates page (`cmd /c start ms-windows-store://...`), which returns exit code 0 almost instantly — long before any real update could happen — and this was being counted as a genuine success. `OptimizationView.BtnUpgradeSelected_Click` now buckets msstore items separately ("opened in Store", not counted as succeeded/failed, not removed from the list).
- REQ-570-02 DONE (2026-09-29) — Implemented together with REQ-570-12's new "launch to tray" toggle: `App.xaml.cs` now runs `UpdateNotificationService.RunCheckAsync()` immediately when started with `--tray`, instead of waiting for the periodic timer. Home tab already updates reactively via `UpdatesHubService.Changed`.
- REQ-570-03 DONE (2026-09-29) — "Ultimate Performance" toggle moved from the generic Tweaks list into the Optimization tab, directly between Gaming Mode and Auto Gaming Mode; Scheduled Maintenance moved to the end.
- REQ-570-04 DONE (2026-09-29) — Found and fixed: the Registry Cleaner card in HealthView.xaml was the one card in the whole app missing its standard `Margin="0,16,0,0"` (verified by auditing every `CardStyle` Border in Views/*.xaml — no other instances found).
- REQ-570-05 DONE (2026-09-29) — Chrome/Edge/Brave/Opera/Vivaldi/Firefox were already supported; added the one clearly-missing variant, Opera GX (separate profile folder from regular Opera).
- REQ-570-06 NOT STARTED — Unified password manager across all browsers. Needs a scoping decision before starting (see chat) — Chromium-family (Chrome/Edge/Brave/Opera/Vivaldi) decryption is straightforward (DPAPI + AES-GCM), Firefox's NSS-based store is a much bigger lift (needs nss3.dll interop). Flagged for user confirmation on scope before implementation.
- REQ-570-07 DONE (2026-09-29) — Added Brave, OBS Studio, Telegram Desktop, Everything, ShareX, LibreOffice to the Recommended list.
- REQ-570-08 DONE (2026-09-29) — Root cause: `ThemedMessageBox`'s icon TextBlock had no explicit Foreground at all (inherited a dark default) — the "ℹ" (Information) glyph in particular renders as a squared-off "i", nearly invisible in dark mode. Now colored per icon type (red/orange/accent), visible in both themes.
- REQ-570-09 DONE (2026-09-29) — `SidebarShortcuts.cs`'s Settings entry: `GlyphKind.Palette` → `GlyphKind.Gear` (shared by both the sidebar and the horizontal modern menu strip, same data source).
- REQ-570-10 NOT STARTED — Needs a design decision: what's the actual data source for "top experimental ViVeTool features" (no official feed exists) — flagged for user input before implementation.
- REQ-570-11 DONE (2026-09-29) — Skins tab content merged into the Theme Settings tab (radio buttons + description moved in as-is, same `Skin_Changed` handler); the separate Skins TabItem removed.
- REQ-570-12 DONE (2026-09-29) — New first tab (reuses the "Settings" window-title string) with Desktop Widget, Update Notifications, Low Disk Notifications (all moved from Theme tab, unchanged logic), plus a new "Launch with Windows to Tray" toggle (`SystemService.SetLaunchWithWindowsToTray`, registry Run key + `--tray` launch arg).
- REQ-570-13 NOT STARTED — Code signing is not a code change (needs a purchased certificate + signing step in the release pipeline) — flagged for user decision, not something to implement blindly.

## v5.8.0

- REQ-580-01 NOT STARTED — Automated pass clicking through every button/feature in the app to catch bugs, memory leaks, freezes and malfunctions, fixing what's found.
- REQ-580-02 NOT STARTED — Font management: style preview, plus suggestions for free fonts to download/install (with web preview).
- REQ-580-03 NOT STARTED — New "Multimedia" tab: proposal + implementation (e.g. Spotify downloader/transcoder to various audio formats, YouTube audio/video downloader, and similar).
- REQ-580-04 NOT STARTED — Update the in-app user guide (Help) to reflect current features.
- REQ-580-05 NOT STARTED — Replace the opacity/transparency mechanism with the one used in MotionDeskStudio (separate project — needs that implementation ported/adapted).
- REQ-580-06 NOT STARTED — Language flag icons should be higher resolution/fidelity.

## Notes

- REQ-570-13 and REQ-580-01 are worded almost identically to items already given for the MotionDeskStudio project (separate app, separate repo) — track them independently per project, don't assume one fix covers both.
- v5.7.0 shipped 2026-09-29 (REQ-570-01,02,03,04,05,07,08,09,11,12). REQ-570-06/10/13 remain, each blocked on a user decision (see PROJECT_STATE.md "Blocked"). v5.8.0 not started.
