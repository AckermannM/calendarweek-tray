# Glyph vertical centring — spec

Status: ready-for-agent

Terminology is fixed by [`CONTEXT.md`](../../CONTEXT.md) — **glyph**, **ink**, **box**, **measured
property**. Use those words. This spec assumes the reader has [`GlyphRenderer.cs`](../../src/CalendarWeekTray/GlyphRenderer.cs)
and [`calendarweek-tray-v1/spec.md`](../calendarweek-tray-v1/spec.md) §5.3–5.4 open — it extends, and
does not restate, the constants and the horizontal centring rule decided there.

---

## Problem Statement

The week number in the tray glyph reads as vertically bottom-aligned rather than centred in its body
— most visible at the 16px box, the size most users actually see day to day. The digits are, by the
existing formula, centred against the *reference* ink's bounding box, but real renders show the drawn
digits sitting up to 1px below the body's own geometric centre, because that formula is a one-shot
calculation with no correction step, unlike the horizontal placement which draws, measures, and
adjusts until it converges.

## Solution

Give vertical placement the same draw → measure → blend → correct convergence loop the horizontal
rule already uses (§5.4), instead of a static formula. Run it once against the reference ink ("44"),
not per week, and cache the resulting y per `(face, box)` — so every week at a given box size still
shares one baseline, exactly as today, and the digits move up by the amount the real geometry
actually requires at each rendered size (measured during design: 1.00px @16px, 0.50px @20px, 0.00px
@24px, 0.50px @28px, 1.00px @32px) rather than by a hand-picked constant.

## User Stories

1. As a Windows user glancing at the notification area, I want the week number visually centred in
   its calendar-page glyph, so it doesn't read as cramped against the bottom edge.
2. As a user on a display where Windows reports a larger `SM_CXSMICON` (20/24/28/32px), I want the
   same vertical correction applied at whatever size my glyph actually renders at, so the glyph looks
   right regardless of DPI/scaling, not just at 16px.
3. As a user, I want the correction to never make one week's number sit at a different height than
   another week's, so the glyph doesn't visibly bob between reconciles.
4. As a maintainer reading `GlyphRenderer.cs`, I want the vertical centring rule documented with its
   "why" in the same style as the existing §5.3/§5.4 constants, so a future edit doesn't quietly
   reintroduce the low bias without realizing it's undoing a measured decision.
5. As a maintainer, I want the vertical correction derived from the same measured blend mechanism as
   the horizontal one (bounding-box centre blended with alpha-weighted mass centre), not a
   hand-authored pixel offset, so it keeps holding if `BarFactor`, `BodyPad`, or the face itself ever
   change, without anyone remembering to also retune a magic number.
6. As the test suite, I want a measured property confirming the digit's blended vertical centre sits
   within tolerance of the body's own vertical centre at every rendered box size, so a regression here
   is caught automatically instead of requiring another manual pixel inspection.
7. As a future contributor, I want it obvious from the code and spec that the vertical loop
   deliberately targets the body's own centre and deliberately runs against the reference ink only —
   not the page's centre, and not per-week — so those two rejected alternatives aren't quietly
   re-attempted later without the context for why they were ruled out.

## Implementation Decisions

- Add `GlyphRenderer.OpticalCentreY(Bitmap)`, mirroring the existing `OpticalCentreX` (`GlyphRenderer.cs:362`):
  the alpha-weighted vertical centre of mass of a bitmap's ink.
- Replace the static vertical placement formula at `GlyphRenderer.cs:126`
  (`y = MathF.Round(area.Y + ((area.Height - referenceInk.Height) / 2f)) - referenceInk.Y`) with a
  convergence loop structurally identical to the existing horizontal one (`GlyphRenderer.cs:137-167`):
  draw the digits to their own layer, measure `InkBoundsOf`, blend the ink's bounding-box vertical
  centre with `OpticalCentreY`, compute drift against a fixed target, adjust `y`, repeat. Same cap (4
  iterations) and same tolerance (0.15px) as the horizontal loop — no new tuning knobs.
- The convergence target is `body`'s own vertical centre — an exact mirror of how the horizontal loop
  targets `body`'s own horizontal centre (`GlyphTests.cs:82-85` confirms the existing target is `body`,
  never `page`). Do not blend toward `page`'s centre or add any correction for the binding bar's
  visual weight (see Out of Scope).
- This loop runs **once, against the reference ink ("44") only, per `(face, box)`** — not per week.
  Cache the resulting `y` the same way `FitCache` already keys its result on `(face, box)`
  (`GlyphRenderer.cs:42-43`). Every week at a given box size reuses the cached value, so
  the existing "digits do not shift baseline between weeks" decision (`calendarweek-tray-v1/spec.md:472`)
  is preserved by construction, not by convention.
- Update `calendarweek-tray-v1/spec.md` §5.4 ("The centring rule") to document the vertical mirror
  alongside the horizontal rule it mirrors, and revise the sentence at
  `calendarweek-tray-v1/spec.md:472` ("Vertical placement... comes from the reference's ink, so digits
  do not shift baseline between weeks") to describe the converged, cached reference `y` rather than
  the raw `referenceInk.Y` / `referenceInk.Height` it currently names.
- `GlyphMetrics`'s shape does not need to change for this. `Converged` remains specific to the
  per-render horizontal loop; the vertical convergence is a one-time-per-`(face, box)` cache-population
  event, not a per-render outcome, so it has no natural per-render field to report through.

## Testing Decisions

- Reuse the existing seam: `GlyphRenderer.Render(GlyphSpec) → (Bitmap, GlyphMetrics)`, asserted via
  measured properties — the same seam ticket 03 established in
  [`GlyphTests.cs`](../../test/CalendarWeekTray.Tests/GlyphTests.cs). No new seam.
- Extend `DigitInkStaysInsideThePageAndIsCentred` (or add a sibling immediately beside it) with a
  vertical-axis version of the existing horizontal-axis check (`GlyphTests.cs:68-89`): crop to
  `metrics.DigitInk`, recompute the blend of bounding-box centre and `GlyphRenderer.OpticalCentreY`
  from the bitmap that actually rendered, and compare against `body`'s vertical centre computed
  structurally from `Stroke`, `BarFactor`, `BodyPad` — the same technique the existing test already
  uses for `bodyLeft`/`bodyRight`. Never reimplement the renderer's own algorithm a second time inside
  the test.
- Sweep the same `Sizes = [16, 20, 24, 28, 32]` the suite already sweeps (`GlyphTests.cs:14`) — the
  correction is expected to differ by size (measured during design: 1.00 / 0.50 / 0.00 / 0.50 / 1.00px).
- No golden images — per this project's "measured property" convention (`CONTEXT.md`), every
  assertion reads a number back out of a render that actually happened.
- No new dead-zone-style exemption list is expected, unlike `TheCentringLoopConvergesOutsideTheKnownDeadZone`
  for the horizontal loop: the vertical loop only ever runs once per box size (5 times total, against
  one fixed reference string), not 265 times against every real week's digits. If any of those 5 fail
  to converge within the existing iteration/tolerance budget, that should fail the build, not be
  silently accepted as a known dead zone.

## Out of Scope

- Any correction that also pulls the vertical target toward `page`'s centre to account for the binding
  bar's visual weight — measured at roughly 2.5–3.5px depending on box size, well beyond what "shift
  one pixel row" describes, and with no precedent in the existing horizontal rule. Explicitly
  considered and rejected during design.
- Per-week vertical jitter — running the convergence loop against each week's own actual digits rather
  than the reference only, which would match the horizontal loop more literally but reverses the
  documented "no baseline shift between weeks" decision. Explicitly considered and rejected.
- Any change to `BarFactor`, `BodyPad`, `Stroke`, or any other §5.3 "measured, not tuned" constant —
  this fix changes only where, within the existing geometry, the digits land; not the geometry itself.
- Any change to the horizontal centring rule, the widest-reference computation, or the binding-bar /
  ring-slot geometry.

## Further Notes

- The per-size offsets quoted above (1.00 / 0.50 / 0.00 / 0.50 / 1.00px) were measured directly
  against this codebase during design, via a throwaway diagnostic xunit test that rendered week "44"
  at each box size and compared its blended ink centre to `body`'s structural centre. That test was
  deleted afterward and is not checked in — the implementing agent should expect to re-derive and
  re-verify these numbers from the real convergence loop once it exists, not trust them as fixtures.
- This spec was synthesized from a `/grilling` session (`mattpocock-skills:grilling`). The full
  reasoning trail — why a fixed-pixel constant and a proportional `BarFactor`-style constant were both
  considered and rejected in favour of mirroring the existing blend-and-converge mechanism — lives in
  that conversation and is not restated in full here.
