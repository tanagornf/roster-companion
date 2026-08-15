# Release audit: 0.1.0

Audit date: 2026-08-16

Status: **GitHub-ready source and pre-release package prepared. Publication still depends on the maintainer's Git operations, green GitHub Actions checks, and the external manual checks listed below.**

## Completed automatically

- Locked NuGet restore succeeds.
- Release build succeeds with warnings treated as errors.
- All 41 automated tests pass.
- Direct and transitive NuGet vulnerability audit reports no known vulnerable package.
- Repository safety scan passes across 105 text files without finding a token-like value, private key, absolute user-profile path, real email address, or included credential file.
- Self-contained Windows x64 single-file publish succeeds.
- The packaged executable remains running through an isolated three-second startup check, even when the normal installed companion is already running.
- Executable metadata reports `Roster Companion`, product version `0.1.0`, file version `0.1.0.0`, and file description `Roster Companion`.
- The binary is confirmed unsigned and the resulting SmartScreen warning is documented in the README, release notes, security policy, and troubleshooting guide.
- The public ZIP contains only the executable, license/privacy/security/changelog files, release notes, troubleshooting guide, and required .NET redistribution notices.
- Internal implementation handoff material and contributor-only documentation are excluded from the binary ZIP.
- Generated SHA-256 values verify against the matching `SHA256SUMS.txt`. The public release workflow rebuilds on a clean hosted runner, so the checksum attached to that GitHub Release is authoritative for the published assets.
- CI is configured to repeat restore, build, tests, vulnerability audit, repository scan, self-contained packaging, and isolated startup smoke testing on a clean `windows-latest` hosted runner.
- Tag builds are configured to publish `RosterCompanion-<version>-win-x64.zip` and `SHA256SUMS.txt` as a GitHub pre-release using the matching versioned release notes.
- Dependabot is configured for monthly NuGet and GitHub Actions dependency checks.

## Verified against the current ChatGPT desktop app

During development on the maintainer's current Windows installation, the following were exercised interactively:

- attached selector and dropdown positioning in normal and maximized windows;
- responsive window movement and resize tracking;
- automatic light and dark appearance matching;
- hiding on Settings and on overlapping File/Edit menus while remaining visible for View, Help, and the profile popup;
- hiding when another application covers the selector;
- click-outside dropdown dismissal;
- official Codex CLI discovery and automatic selection when it is installed;
- guarded desktop enrollment fallback when CLI discovery is deliberately disabled.

Automated tests cover positioning, refresh policy, enrollment cancellation, credential/session rollback, process-path validation, usage parsing, and account-switch recovery behavior.

## External checks still required

This machine does not have an available Windows Sandbox or separate Hyper-V VM, so it cannot supply an honest clean-PC result. Before promoting this pre-release to a stable release:

- let GitHub Actions complete successfully after the source is pushed;
- test the downloaded ZIP on a separate, fully updated Windows 10 or 11 x64 PC or VM without a separately installed .NET runtime;
- compare the downloaded archive with the published SHA-256 value;
- observe and document the actual Microsoft Defender SmartScreen prompt for the downloaded unsigned asset;
- complete one live add, refresh, change-account, rollback/failure, and non-active-account removal cycle using non-production test accounts;
- enable GitHub private vulnerability reporting in the repository's security settings.

The release remains intentionally marked as a **pre-release** until these external checks are recorded. See [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

## Naming and publication scope

The neutral product name **Roster Companion** does not place `ChatGPT` or `GPT` in the product name. ChatGPT is referenced only to explain compatibility, and the project includes an explicit unofficial/non-endorsement disclaimer. This is an engineering interpretation of the [OpenAI brand guidelines](https://openai.com/brand/), not legal advice.

No Git repository, remote, commit, tag, or GitHub Release was created during this audit, as requested by the maintainer.
