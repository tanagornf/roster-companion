# Roster Companion 0.1.2

## Download and run

Download **RosterCompanion.exe** from the assets below, save it in a permanent folder, and double-click it. There is no installer and no separate .NET runtime to install. Windows 10 or 11 x64 and the official ChatGPT/Codex desktop app are required.

The ZIP contains the same executable with documentation and license notices. `SHA256SUMS.txt` lists checksums for both downloads.

## Updating from 0.1.0 or 0.1.1

Exit the running Roster Companion from **gear menu → Exit Roster Companion**. Replace the old EXE with the new one in the same folder, then launch it. If you saved this version elsewhere, you can delete the old EXE after exiting it. Your accounts and settings remain in `%APPDATA%\ChatGPTRoster`; leave that folder in place. The new launch updates the **Start with Windows** path when enabled. There is no automatic updater. If the old copy is still running, the new copy will tell you to exit it first.

## Changes

- Moved the account selector into the title bar, left of the window controls, to avoid the updated Codex sidebar. It hides on narrow windows when space is insufficient.
- Fixed account switching so closing ChatGPT does not also terminate Roster Companion when it was launched from Codex.
- Reopening now uses the registered Windows app identity, with a verified desktop executable as fallback, and reports failed launches.

This is an unofficial, unsigned prerelease. Windows SmartScreen may warn on first launch. Download from this repository and compare the download with `SHA256SUMS.txt` if you want to verify it.
