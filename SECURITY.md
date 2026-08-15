# Security policy

Roster Companion handles local authentication material, so reports involving credential exposure, unsafe file operations, account confusion, or unintended network destinations are treated as security issues.

## Reporting a vulnerability

When the GitHub repository is public, use its private **Security → Report a vulnerability** form. Do not include tokens, cookies, `auth.json`, session files, or other credentials in a public issue.

Until private reporting is configured, do not publish exploit details. Contact the maintainer privately and include only:

- the affected Roster Companion version;
- the Windows and ChatGPT desktop app versions;
- reproduction steps using redacted or synthetic data;
- the expected and observed result.

Never send a real access token or an archive of `%APPDATA%\ChatGPTRoster`.

## Supported versions

Roster Companion is currently pre-release software. Security fixes are applied to the latest published version only.

## Security boundaries

- Managed credentials and session snapshots stay under `%APPDATA%\ChatGPTRoster` and are ACL-restricted to the current Windows user, `SYSTEM`, and local administrators.
- The application never stores credentials in `profiles.json`, logs, analytics, or crash reports.
- Usage requests are restricted to HTTPS endpoints on `chatgpt.com` or `chat.openai.com`.
- Account removal deletes the selected local managed profile; it does not delete the OpenAI account.
- Releases are currently unsigned. Verify the SHA-256 value published with a release before running it.

Local administrators and software running as the same Windows user can still access the managed data. Roster Companion is not a defense against a compromised Windows account.
