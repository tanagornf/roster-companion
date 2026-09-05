# Roster Companion 0.1.1-lagfix

Local diagnostic build for desktop-wide mouse stutters while ChatGPT/Codex is visible.

- Move mouse hooks off the WPF UI thread onto dedicated message loops.
- Move accessibility hit-testing out of the mouse hook and onto a background worker.
- Allow only one click hit-test at a time, retaining only the latest waiting click, so an unresponsive accessibility provider cannot accumulate workers.
- Ignore late click results after a newer click, menu dismissal, tracking stops, or the target window changes.

The account-switching and credential-storage code is unchanged.

Validation: all 47 tests pass, including regressions for a stalled accessibility provider, overlapping clicks, provider errors, hook thread ownership, restart, and shutdown. The packaged application passed its startup check, and testing on the affected Windows machine confirmed that the lag was gone.
