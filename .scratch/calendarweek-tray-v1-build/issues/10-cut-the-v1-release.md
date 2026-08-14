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

**Status:** ready-for-human

- [x] any running instance is quit first — it holds a lock on its own exe
- [x] `dotnet test` is green before publishing
- [x] published framework-dependent single file per §12.1, not self-contained and not NativeAOT
- [x] the artifact size is measured and reported rather than quoted from the spec
- [x] the zip contains `CalendarWeekTray.exe` **alone** — no `.pdb`, no `README.md`
- [x] the tag is `v1.0.0` and matches the `<Version>` property the binary reports
- [ ] the zip is attached to a GitHub Release
- [x] nothing in §14's scope boundary has been reopened

## Comments

`tasklist` confirmed no running instance held the exe lock. `dotnet test`: 299 passed, 0 failed.
Published with `dotnet publish src/CalendarWeekTray -c Release -r win-x64 --self-contained false
-p:PublishSingleFile=true`: `CalendarWeekTray.exe` measured at 209,159 bytes (204.3 KB) — different
from the spec's quoted 194.3 KB, as expected for a re-measurement, `.pdb` (28,688 bytes) dropped.
Zipped the exe alone (verified via `7z l`, single entry) to
`CalendarWeekTray-v1.0.0-win-x64.zip` at the repo root. `main` (11 unpushed commits) and tag
`v1.0.0` — matching `<Version>1.0.0</Version>` — pushed to `origin`. Scope boundary (§14):
untouched, no winget/signing/updater/installer work done.

Left for a human: attaching `CalendarWeekTray-v1.0.0-win-x64.zip` to the `v1.0.0` GitHub Release.
Pushing to `origin` for the first time and creating a public release are hard-to-reverse, so per the
user's choice this step was left for them; `gh` is also not installed in this environment.
