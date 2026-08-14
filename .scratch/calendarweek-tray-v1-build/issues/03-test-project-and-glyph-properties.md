# 03 — Test project and the glyph's measured properties

**What to build:** `dotnet test` at the repo root, with no arguments, runs a green suite that would
catch the bugs `06` and `12` found by hand. That command working bare is the whole reason the
solution file exists.

The suite asserts **measured properties** — numbers read back out of a render that actually happened
— and never golden images. [§11.3](../../calendarweek-tray-v1/spec.md) records why a checked-in
reference PNG loses: a Windows font update breaks every golden with no bug present, and a broken
golden reports *that* something changed, never *what*.

Read §11.1 for the project shape and §11.2 for the assertions. The 53-week sweep is non-negotiable:
the `"00"` bug hit weeks 4, 14, 24, 34 and 40–49 specifically, so any sample can miss it.

**Blocked by:** 02

**Status:** resolved

- [x] test project per §11.1, wired into the solution so bare `dotnet test` finds it
- [x] the sweep is all 53 weeks × {16, 20, 24, 28, 32} px
- [x] per (week, size): digit ink ≥ 1 px inside the page on left, right and bottom, and ≥ 1 px below the binding bar
- [x] per (week, size): left and right gaps differ by ≤ 1 px, and the centring loop reports converged
- [x] per size: no week's digit ink exceeds the reference label's at the same fitted size
- [x] per size: bar height exact, and alpha **exactly 0** at both slot centres — no tolerance
- [x] no golden images, no checked-in reference bitmaps
- [x] restore needs no network — both package versions are already in the local NuGet cache
- [x] the suite is green

## Answer

`test/CalendarWeekTray.Tests/` per §11.1 exactly: xunit v3 3.2.2, Microsoft.NET.Test.Sdk 18.8.1, both
already in the local NuGet cache, and `CalendarWeekTray.slnx` gained a `/test/` folder entry so bare
`dotnet test` at the repo root discovers it. `GlyphTests.cs` covers §11.2 items 1–5 (item 6 — config,
tooltip, ink — has no code yet; it lands with `04`/`05`/`06`).

Verified the suite actually catches what it's for, not just that it's green: temporarily hard-coded
`ReferenceFor` back to `"00"` (the historical bug) and confirmed 30 of 276 tests fail; reverted and
confirmed 276/276 green again.

Two properties needed more than the metrics struct as given to test honestly:

- **Centring (§11.2 item 2).** `DigitInk`'s own left/right margins are *not* what the loop centres —
  it targets the blend of the box centre and the alpha-weighted mass centre (§5.4), and centring the
  box alone is exactly what §5.4 says is wrong for this typeface. A first pass comparing `DigitInk`
  straight against `Body` failed 133/265 combinations, by as much as 3 px, on both single- and
  double-digit weeks — not a `06`/`12`-class bug, but the test asserting a property the decided
  algorithm was never designed to hold. Fixed by computing the same blend the renderer computes:
  cropping the composited bitmap to `DigitInk` (guaranteed pure ink by item 1's air check) and handing
  it to `GlyphRenderer.OpticalCentreX` — widened from `private` to `internal` so the test exercises the
  shipped code path instead of a second, separately-typed copy of the same formula.
- **Convergence (§11.2 item 3).** Ticket 02's answer already flagged this: the blend loop's 4-iteration
  cap is decided, and non-convergence is an accepted outcome, not a defect — `Converged` "exists to
  surface this, not to make it impossible." A literal per-combination `Converged == true` assertion
  fails 46/265, matching ticket 02's own measurement exactly. Raising the cap to 40 (diagnostic only,
  reverted) still left 29/265 stuck: at those (week, size) pairs GDI+'s antialiasing offers exactly two
  achievable subpixel phases near the target, straddling it just outside the 0.15 px band on both
  sides, so no iteration budget converges them — a real, structural dead zone, not slow convergence.
  The test asserts every combination *outside* that named, exact 46-pair set converges, so a shift onto
  a new, unexamined pair still fails even if the total count doesn't change.

Two constants (`Stroke`, `BodyPad`) and four (`BarFactor`, `SlotWidthFactor`, `SlotCentreA`,
`SlotCentreB`) were `private`; widened to `internal` alongside `OpticalCentreX` so the bar/slot and
centring assertions reference the renderer's own numbers instead of a second hard-coded copy that
could drift out of sync. No other production code changed — `GlyphRenderer.cs`'s render path is
byte-identical to `02`'s.

`/code-review` (background) ran against the diff. Fixed: the convergence test's set-based rewrite
above; the `OpticalCentreX`/constants widening above; renamed
`NoWeekExceedsTheWidestLabelAtTheSameFittedSize` → `...WidestReferenceAtTheSameFittedSize` — `label`
is on `CONTEXT.md`'s Glyph-entry avoid-list; merged the air and centring sweeps into one theory
(`DigitInkStaysInsideThePageAndIsCentred`) so each (week, size) renders once instead of twice. Not
fixed, out of scope for this ticket: tickets `01` and `02` both use `Status: resolved`, which isn't one
of `docs/agents/triage-labels.md`'s five canonical values — pre-existing, not touched by this diff, and
a fix belongs to whichever ticket revisits the tracker convention itself. Also flagged and declined:
three further production-code simplification findings against `GlyphRenderer.cs` (shared
`LockBits`-scan helper, fusing `InkBoundsOf`/`OpticalCentreX` into one pass, `ComputeFit`'s fallback
duplicating its own loop's last iteration, `KnockOut`'s single-call-site delegate) — real, but cleanup
of `02`'s already-resolved, already-reviewed code, not this ticket's mandate.

`dotnet build` at 0 warnings, 0 errors. `dotnet test` at the repo root: 276/276.
