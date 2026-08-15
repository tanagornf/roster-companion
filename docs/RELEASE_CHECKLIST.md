# Release checklist

Use this checklist for every public version. Automated items should pass in CI; manual items require a current Windows test environment and non-production test accounts.

## Automated gate

- [ ] Restore succeeds from a clean NuGet cache.
- [ ] Release build completes without warnings.
- [ ] All automated tests pass.
- [ ] Dependency vulnerability audit reports no known vulnerable direct or transitive package.
- [ ] Self-contained `win-x64` publish succeeds.
- [ ] Packaged executable stays running through the startup smoke test.
- [ ] ZIP and `SHA256SUMS.txt` are generated.
- [ ] Secret/personal-data scan finds no real token, credential file, email, or workstation path.

## Manual account flows

- [ ] Fresh launch with no stored Roster Companion data shows the empty state.
- [ ] Add account completes through the official browser login.
- [ ] Closing the browser and choosing **Cancel login** restores the add button and removes the incomplete profile.
- [ ] Adding a duplicate account is rejected and its temporary managed profile is removed.
- [ ] Alias save, alias clearing, search by alias/email, and account removal work.
- [ ] Plan, five-hour usage, weekly usage, and both reset dates match the account's current state.
- [ ] Refresh handles offline, expired-login, and unexpected-service responses without exposing a token.

## Manual switching and recovery

- [ ] Switching while the ChatGPT desktop app is closed changes the active account.
- [ ] Switching while the ChatGPT desktop app is open closes and reopens only the verified packaged application.
- [ ] A running Codex task produces the interruption warning; choosing **No** leaves state unchanged.
- [ ] An idle completed task does not produce the uncertain-state warning.
- [ ] Forced failure after credential activation restores the previous account and session.
- [ ] First-use switching clears stale session state rather than inheriting another account's session.
- [ ] Removing the active account is blocked.
- [ ] Removing a non-active account deletes only its managed directory.

## Clean-machine verification

- [ ] Test the ZIP on a separate, fully updated Windows 10 or 11 x64 PC or VM.
- [ ] Confirm it starts without a separately installed .NET runtime.
- [ ] Confirm SmartScreen behavior is documented for the unsigned build.
- [ ] Confirm no developer account, alias, email, credential, or absolute development path is present.
- [ ] Complete one add/refresh/switch/remove cycle.

## Publication

- [ ] Confirm the application name and branding comply with the current OpenAI brand guidelines or obtain permission where required.
- [ ] Update `CHANGELOG.md` and project version.
- [ ] Review README compatibility and security warnings.
- [ ] Verify the release tag is `v<version>` and matches the binary version.
- [ ] Attach the ZIP and `SHA256SUMS.txt` to the GitHub Release.
- [ ] Compare the uploaded asset's SHA-256 value with the locally generated checksum.

Do not mark the release verified when any required manual item was skipped. Record the tested Windows and ChatGPT desktop app versions in the release notes.
