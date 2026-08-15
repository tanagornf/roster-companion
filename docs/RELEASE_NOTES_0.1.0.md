# Roster Companion 0.1.0

This is the first public pre-release of Roster Companion, a lightweight account and usage manager that attaches to the current Windows ChatGPT desktop app.

## Highlights

- Compact account selector positioned beside ChatGPT's Codex selector.
- Responsive searchable dropdown with account aliases, plans, usage limits, reset times, refresh controls, and stale-data indicators.
- Automatic light and dark appearance matching.
- Fast window movement, resize, monitor, DPI, visibility, settings-page, and overlapping-window tracking.
- Explicit account changes with task detection, backups, validation, rollback, and automatic ChatGPT reopening.
- Official Codex CLI sign-in when available, with a guarded ChatGPT desktop sign-in fallback.
- Local-only storage with no telemetry, ads, analytics, or developer-operated server.

## Requirements

- Windows 10 or Windows 11 on x64 hardware.
- The current official ChatGPT desktop app installed from the `OpenAI.Codex` Windows package.
- Internet access for sign-in and usage refresh.

## Before installing

Roster Companion is unofficial and is not affiliated with or endorsed by OpenAI. This pre-release depends on local ChatGPT compatibility surfaces that may change after an app update.

The executable is not code-signed. Microsoft Defender SmartScreen may show **Windows protected your PC** on first launch. Download the archive only from this project's GitHub Releases page and verify its SHA-256 value against `SHA256SUMS.txt`.

Roster Companion stores profiles, credentials, usage data, session snapshots, and backups locally under `%APPDATA%\ChatGPTRoster`. Treat that directory as sensitive and never attach it to a public issue.

## Known limitations

- Only the current Windows ChatGPT desktop app is supported.
- The attached selector is a separate companion window; it does not inject into or modify ChatGPT.
- A future ChatGPT update may temporarily affect placement, route detection, sign-in, switching, or usage retrieval.
- This build has no publisher signature, so Windows reputation warnings are expected for a new download.

See `README.md`, `PRIVACY.md`, `SECURITY.md`, and `docs/TROUBLESHOOTING.md` for installation, privacy, and recovery details.
