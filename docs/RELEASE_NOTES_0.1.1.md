# Roster Companion 0.1.1

Fixes desktop-wide mouse lag and stutters while ChatGPT/Codex is visible.

## Download and run

- **RosterCompanion.exe**: download to a permanent folder and run. No installer or separate .NET installation is required.
- **RosterCompanion-0.1.1-win-x64.zip**: the same executable with documentation and license notices. Extract it, then run `RosterCompanion.exe`.
- **SHA256SUMS.txt**: checksums for the executable and ZIP.

Requires Windows 10 or 11, x64, and the official ChatGPT/Codex desktop app.

## Upgrade from 0.1.0

Exit Roster Companion using its gear menu, replace the old executable with this version, and run it again. Existing accounts and settings remain in the same local data folder. You do not need to reinstall ChatGPT/Codex or disable its GPU acceleration for this fix.

## What changed

- Mouse hooks now run on dedicated message loops, independently of the companion's UI work.
- Accessibility checks run on a background worker, with one active query and only the latest click waiting.
- Late results are ignored after a newer interaction, menu dismissal, or tracker restart.

All 47 tests pass. Packaged startup was checked, and testing on the affected Windows machine confirmed the lag was gone.

This remains an unofficial, unsigned prerelease. Windows SmartScreen may show a warning for the new download. Download only from this repository and verify against `SHA256SUMS.txt`.
