# Privacy

Roster Companion is a local Windows desktop utility. It has no analytics, advertising, telemetry, or developer-operated server.

## Data stored locally

The application stores data under `%APPDATA%\ChatGPTRoster`:

- `profiles.json`: aliases, email addresses, plan labels, usage snapshots, reset times, and managed profile paths;
- `profiles\`: Codex authentication files and bounded desktop-session snapshots for each account;
- `backups\`: timestamped credential backups used for recovery.

These files may contain sensitive authentication material. Do not sync, upload, or attach this directory to an issue.

## Network activity

- Adding an account uses the official Codex CLI login flow when available. Otherwise, it uses the supported ChatGPT sign-in screen after backing up the active account and session.
- Refreshing usage sends the selected account's bearer credential to an allowlisted ChatGPT HTTPS usage endpoint.
- Roster Companion does not send data to the developer or to any unrelated host.

## Removing local data

Use **… → Remove account** to delete a non-active managed profile. To remove all Roster Companion data, exit the application and delete `%APPDATA%\ChatGPTRoster`. This does not delete any OpenAI account.
