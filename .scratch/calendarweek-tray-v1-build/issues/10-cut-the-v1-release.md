# 10 — Cut the v1.0.0 release

**What to build:** A single zipped `CalendarWeekTray.exe` attached to a GitHub Release tagged
`v1.0.0`. That is the whole distribution story — **no winget manifest, no code signing, no updater, no
installer**, each ruled out on the map with reasoning that should be read before any of them is
reopened.

Read [§12](../../calendarweek-tray-v1/spec.md) for the procedure and §14 for the scope boundary this
release must not quietly widen.

Two details are deliberate rather than lazy. The `.pdb` is dropped because there is no log file, no
crash reporting and no support channel to send a stack trace to, so symbols serve nobody. `README.md`
stays out of the zip because it is one click away on the repo the user just downloaded from, and a
stale copy inside a zip is worse than none.

Since there is no CI (§11.5), `dotnet test` passing is a **documented gate in this procedure** and the
only one there is.

**Blocked by:** 03, 07, 08, 09

**Status:** ready-for-agent

- [ ] any running instance is quit first — it holds a lock on its own exe
- [ ] `dotnet test` is green before publishing
- [ ] published framework-dependent single file per §12.1, not self-contained and not NativeAOT
- [ ] the artifact size is measured and reported rather than quoted from the spec
- [ ] the zip contains `CalendarWeekTray.exe` **alone** — no `.pdb`, no `README.md`
- [ ] the tag is `v1.0.0` and matches the `<Version>` property the binary reports
- [ ] the zip is attached to a GitHub Release
- [ ] nothing in §14's scope boundary has been reopened
