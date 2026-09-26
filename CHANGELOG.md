# Changelog

All notable changes to this project will be documented here. The format follows Keep a Changelog, and versions use Semantic Versioning.

## [Unreleased]

## [0.1.2] - 2026-09-27

### Changed

- Moved the account selector into the title bar, left of the window controls, to avoid overlapping the updated Codex sidebar. The dropdown opens below it and narrow windows hide the selector before it overlaps the menus.

### Fixed

- Account-switch reopening now activates the registered Windows desktop app and waits for its window. Bundled CLI executables are excluded from launcher selection, and a missing launcher or failed startup is reported instead of silently succeeding.
- Closing ChatGPT during an account switch no longer terminates Roster Companion along with the desktop process tree or its launching job.
- Starting a new copy while an older one is running now explains how to finish the update.

## [0.1.1] - 2026-09-06

### Fixed

- Desktop-wide mouse stutters while ChatGPT/Codex is visible: mouse hooks now use dedicated message loops, and accessibility queries run on a background worker with only the latest click queued.
- Late accessibility results no longer restore stale menu suppression after a newer click, menu dismissal, or tracker restart.

## [0.1.0] - 2026-08-16

### Added

- Attached selector and responsive account dropdown for the current ChatGPT Windows app.
- Event-driven window tracking, multi-monitor positioning, DPI handling, occlusion detection, and automatic light/dark theme matching.
- Adaptive single-loop position tracking that accelerates during movement and idles at low frequency.
- Immediate dropdown dismissal on any click outside Roster Companion.
- Selector suppression for the overlapping File/Edit menus while View, Help, and the profile popup remain unobstructed.
- Click-scoped accessibility detection for Settings navigation without continuous tree scans or profile-popup suppression.
- Direct control-under-click classification instead of relying on inconsistent ChatGPT menu events.
- Explicit restoration when ChatGPT's Settings back action returns to the main Codex page.
- Filtering of ChatGPT child-content accessibility events so streamed responses cannot trigger repeated full tracking work.
- Search, gear menu, startup toggle, About dialog, local countdown updates, stale usage indicators, and scheduled refresh with retry delays.
- One-time confirmation before importing the current ChatGPT account.
- Automatic selection of the official Codex CLI enrollment flow when available, with a guarded ChatGPT desktop sign-in fallback.
- Guarded desktop enrollment with backup and rollback when the Codex CLI is unavailable.
- Positioning, refresh scheduling, enrollment cancellation, and recovery tests.

### Changed

- Renamed the product to Roster Companion.
- Replaced the standalone management window with the attached companion interface.

### Security

- Restricted process control to verified executables installed inside the official OpenAI Windows package.
- Kept profiles, credentials, usage data, session snapshots, and backups local with current-user access controls.
- Added bounded backups, validation, and rollback around account activation.
- Added locked dependency restore, vulnerability auditing, repository safety scanning, and isolated packaged-app startup checks.

[Unreleased]: https://github.com/tanagornf/roster-companion/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/tanagornf/roster-companion/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/tanagornf/roster-companion/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/tanagornf/roster-companion/releases/tag/v0.1.0
