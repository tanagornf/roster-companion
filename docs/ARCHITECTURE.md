# Roster Companion architecture

Roster Companion uses a native .NET 8/WPF companion shell with its presentation layer separated from window tracking, authentication, usage, process, and session operations. It remains a separate process and never injects into or modifies ChatGPT.

## Main boundaries

- `Models`: non-secret profile metadata and usage snapshots.
- `ViewModels`: search, commands, progress, confirmations, and row presentation.
- `ChatGptWindowTracker`: verifies the packaged ChatGPT process and tracks its bounds, visibility, occlusion, monitor, DPI, theme, and Settings-route state. Its single adaptive position timer runs at display-frame speed only during movement, falls back to a low-frequency idle check, and keeps theme/occlusion work off the movement path.
- `ChatGptClickMonitor` and `ChatGptLayerDetector`: perform read-only accessibility hit-testing only when the user clicks inside ChatGPT. They distinguish File/Edit, Settings, and the Settings back action from View, Help, and the profile popup. Mouse input is never blocked or modified.
- `OutsideClickMonitor`: observes mouse-down notifications only while the dropdown is open, closes it when the target belongs to another process, never suppresses input, and stores no pointer history.
- `OverlayPositioner`: calculates DPI-independent selector and dropdown placement without covering ChatGPT controls.
- `AuthFileReader`: reads credentials locally but returns only identity claims to presentation code.
- `CodexAccountEnrollmentService`: launches the official Codex login with an isolated `CODEX_HOME`.
- `AdaptiveAccountEnrollmentService`: selects isolated CLI enrollment or a guarded desktop sign-in flow with recovery.
- `CodexUsageService`: retrieves usage through an allowlisted, replaceable compatibility adapter.
- `CodexTaskDetector`: combines held writer locks with latest rollout lifecycle events.
- `DesktopProcessService`: operates only on executables whose paths match the installed OpenAI Codex package.
- `DesktopSessionService`: snapshots and restores a bounded list of account-specific browser state.
- `CredentialSwitcher`: mirrors the current account, creates a recoverable backup, atomically activates the target credential, and updates compatible global state.
- `WindowsAccountSwitcher`: coordinates close, backup, activation, session restore, rollback, and reopen.

For automated smoke tests, `CHATGPT_ROSTER_DATA_ROOT` and `CHATGPT_ROSTER_CODEX_HOME` can redirect the two data roots to isolated temporary directories. Normal launches leave these variables unset and use `%APPDATA%\ChatGPTRoster` and `%USERPROFILE%\.codex`.

## Switching sequence

1. Request normal close/reopen confirmation.
2. Detect running work and request a separate interruption confirmation when needed.
3. Capture and gracefully close verified packaged desktop processes.
4. Save the current account's bounded desktop-session snapshot.
5. Mirror and back up the active ambient credential.
6. Atomically activate the selected profile credential.
7. Update compatible account-specific global state.
8. Restore the selected profile session, or clear stale account state on first use.
9. Roll back credentials/session if a later step fails.
10. Reopen the desktop app if it was running before the switch.

If switching succeeds but ChatGPT cannot be reopened, the switch is retained, the UI is updated to the new active account, and the user is told to open ChatGPT manually. A post-activation failure instead rolls back the ambient credential and restores the previous session.

## Compatibility policy

ChatGPT subscription usage and desktop session storage are not documented public integration surfaces. They stay behind small adapters with tests and explicit host/path validation. A desktop update should require changing an adapter rather than presentation or profile-storage code.
