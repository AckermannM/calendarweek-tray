# 01 — Restructure into `src/` + `test/` and delete the prototype harness

**What to build:** The applet behaves exactly as it does today — placeholder glyph in the tray, clean
quit, no ghost icon — but the repo becomes the tree v1 is built in, and the shipping exe stops
carrying nine undocumented argv commands, one of which writes the user's accessibility settings.

Nothing here is tidying. Read [§1.1–§1.4](../../calendarweek-tray-v1/spec.md) for the tree, the two
mandatory csproj additions, the one csproj line that must **not** be added, and the `.editorconfig`
rules that change what you would otherwise type. The mutex belongs to §8.1; the rest of that
section's constructor ordering is ticket 06's.

The eight files to delete are named in §1.3. The five measurement primitives living beside them —
`MeasureInk`, `InkBoundsOf`, `OpticalCentreX`, `Fit`, `ReferenceFor` and the centring loop — are
**production code** and are ticket 02's raw material, which is why the branch below is pushed first.

**Blocked by:** None — can start immediately.

**Status:** resolved

- [x] `prototype/06-13` branch pushed **before** anything is deleted — ticket 02 extracts the renderer from it
- [x] the eight prototype files deleted and the four survivors moved, **in one commit** (§1.3)
- [x] `Main` is mutex + `ApplicationConfiguration.Initialize()` + `Application.Run`, with no argv handling whatsoever
- [x] the mutex is `Local\`-scoped, held for the process lifetime, and a second instance exits silently with no dialog
- [x] solution file at the repo root; bare `dotnet build` works with no arguments
- [x] applet csproj gains `<Version>` and `<InternalsVisibleTo>`, and **does not** gain `<Compile Remove="Tests/**" />` — see §1.1 for why
- [x] every property in §1.2's "not to be touched" table survives the move unchanged
- [x] `.editorconfig` conformance: csproj tab-indented, no leading-underscore fields, `this.`-qualified instance members, file-scoped namespace, no `var`, Allman braces
- [x] `dotnet build` at 0 warnings, 0 errors
- [x] the icon still appears in the tray and still quits cleanly

## Answer

Restructured in commit `fa1509d`. `prototype/06-13` pushed to origin at `6fbcb2a` before any
deletion. The eight prototype files and nine argv commands are gone; `CalendarWeekTray.csproj`,
`Program.cs`, `TrayApplicationContext.cs`, `NativeMethods.cs` moved to `src/CalendarWeekTray/` with
the `.editorconfig` renames applied (`_notifyIcon`/`_iconHandle` → `this.notifyIcon`/`this.iconHandle`,
tab-indented csproj). `Program.cs` is now `Local\CalendarWeekTray` mutex (`GC.KeepAlive`d past
`Application.Run`) + `ApplicationConfiguration.Initialize()` + `Application.Run`, no argv handling.
`CalendarWeekTray.slnx` added at the root via `dotnet new sln -f slnx`. Verified: `dotnet build` at
repo root is 0 warnings/0 errors; running the exe twice leaves exactly one process with no dialog on
the second launch; the placeholder glyph renders without throwing and the process exits with no
residue. Test project intentionally **not** created here — its checklist item belongs to ticket 03,
which is blocked by 02.
