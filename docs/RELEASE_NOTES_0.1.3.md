# Roster Companion 0.1.3

## Download and run

Download **RosterCompanion.exe** from the assets below, save it in a permanent folder, and double-click it. No installer or separate .NET runtime is required. Windows 10 or 11 x64 and the official ChatGPT/Codex desktop app are required.

The ZIP contains the same executable with documentation and license notices. `SHA256SUMS.txt` lists checksums for both downloads.

## Updating an older copy

Exit the running Roster Companion from **gear menu → Exit Roster Companion**. Replace its EXE with this download, then launch it. Your saved accounts and settings remain in `%APPDATA%\ChatGPTRoster`; leave that folder in place. Launching from a new location updates the **Start with Windows** path when enabled.

## Changes

- Correct five-hour and weekly percentages when one usage limit is reached.
- Preserve cached usage when the server returns invalid or empty data, and time out stalled response bodies.
- Handle malformed authentication files with a readable error.
- Restore the previous credentials and session even when switching is cancelled after activation.
- Preserve the current account when desktop enrollment preparation fails, and reopen ChatGPT after partial shutdown failures.
- Prevent overlapping account actions and refreshes while keeping **Cancel login** available.
- Update aliases and search results immediately, and avoid unnecessary background profile writes.

## Verification

The Release build and all 80 automated tests pass, including 24 new regression cases covering usage, authentication, recovery, and account commands. Packaging includes a self-contained Windows x64 executable and an isolated startup check.

Real-account sign-in/switching and clean-machine installation were not tested for this release. Automated account-flow tests use synthetic credentials and simulated desktop processes. This release is not marked as fully manually verified.

This is an unofficial, unsigned prerelease. Windows SmartScreen may warn on first launch. Download from this repository and compare the download with `SHA256SUMS.txt` to verify it.
