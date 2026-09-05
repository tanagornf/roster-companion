# Contributing

Thanks for helping improve Roster Companion.

## Before opening a change

- Use a current Windows x64 environment with the .NET 8 SDK.
- Keep credentials, account data, screenshots with real emails, and generated build output out of source control.
- Open a security report rather than a public issue for credential exposure, unsafe switching, or path-validation problems.
- Keep compatibility logic behind the existing service interfaces; do not couple undocumented desktop/session formats to the UI.

## Build and test

```powershell
dotnet restore .\ChatGPTRoster.sln
dotnet build .\ChatGPTRoster.sln --configuration Release --no-restore
dotnet test .\ChatGPTRoster.sln --configuration Release --no-build
```

Create the same self-contained package used for releases:

```powershell
.\scripts\Build-Release.ps1 -Version 0.1.1
```

## Pull requests

- Explain the user-visible behavior and failure cases.
- Add or update tests for switching, rollback, file boundaries, parsing, or usage-host validation.
- Confirm that no personal path, email, token, session file, or credential backup is present.
- Include synthetic or fully redacted screenshots for UI changes.

By contributing, you agree that your contribution is licensed under the MIT License.
