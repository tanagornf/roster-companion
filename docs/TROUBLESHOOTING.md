# Troubleshooting

## Windows protected your PC

Pre-release builds are not code-signed, so Microsoft Defender SmartScreen may warn before first launch. Download only from the official GitHub Releases page and compare the file's SHA-256 hash with `SHA256SUMS.txt`. Do not bypass a warning for a file from another source.

## The official sign-in cannot be opened

Install or repair the ChatGPT desktop app, close Roster Companion, and try again. Roster Companion uses the official Codex CLI when it is available and otherwise opens a guarded ChatGPT desktop sign-in. It does not implement its own password form.

## Add account remains in progress

Choose **Cancel login**. Roster Companion stops only the isolated sign-in process it started, restores the previous local state, and removes the incomplete managed profile. Start the sign-in again when ready.

## Usage is unavailable

- Confirm that the account is still signed in.
- Confirm that the PC has network access to `https://chatgpt.com`.
- Choose refresh once; repeated rapid refreshes will not repair an expired credential.
- The usage endpoint is a compatibility surface and may change after a ChatGPT update.

## Account switching is blocked

Finish any active Codex task first. If task state cannot be confirmed, the default action is to cancel the switch. Closing the ChatGPT desktop app completely can clear stale process state.

## ChatGPT did not reopen

Open the ChatGPT desktop app manually. The selected account remains active when only reopening fails. Roster Companion uses the running app's registered Windows application identity to reopen it and waits up to 20 seconds for a desktop window. If that identity is unavailable, it uses the verified desktop executable from the installed OpenAI package; bundled CLI executables are never used as desktop launchers.

If both ChatGPT and Roster Companion disappear during switching, use a build with the process shutdown fix. Older builds terminated the entire desktop process tree, which could also terminate a companion launched from a Codex task before credential activation and reopening. Shutdown now terminates only individually verified package processes.

Roster also starts independently of the Windows process job used by Codex tools. Without this, closing ChatGPT could terminate Roster before credential activation even with the process tree fix. Update and restart Roster; its startup now preserves the switching process when ChatGPT exits.

## Recovering from an interrupted switch

Do not delete `%APPDATA%\ChatGPTRoster\backups`. Close the ChatGPT desktop app and Roster Companion, then report the issue with redacted reproduction steps. Never attach the backup itself because it contains authentication material.

## Complete uninstall

1. Close Roster Companion.
2. Delete the downloaded application or extracted folder.
3. Delete `%APPDATA%\ChatGPTRoster` to remove all aliases, managed credentials, session snapshots, and backups.

Uninstalling Roster Companion does not uninstall the ChatGPT desktop app or delete an OpenAI account.
