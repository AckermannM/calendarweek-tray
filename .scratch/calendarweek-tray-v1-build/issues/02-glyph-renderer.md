# 02 — The real glyph: `GlyphRenderer`

**What to build:** The tray shows the decided calendar page instead of the placeholder — square
corners, 1 px outline, filled binding bar with two ring slots notched through it, the unpadded week
number in the body, and nothing that says `KW`. Look at it beside the real clock; that is the
verification this ticket has.

The code exists. `PrototypeGlyph` on the `prototype/06-13` branch (ticket 01) already renders this
correctly, wrapped in the variant machinery `06`/`12`/`13` used to choose between candidates —
the `Design` and `Centring` enums, `FrameStyle.All`, the rounded-rect paths, the badge layout. Extract
the decided path and drop the rest.

Read [§5 entire](../../calendarweek-tray-v1/spec.md) plus §4.1. Every constant in §5.3 was opened up,
measured, and put back — read the ticket behind a rule before changing it.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] `GlyphRenderer` is pure static — no shell, no config, no state; `GlyphSpec` and `GlyphMetrics` per §5.1
- [ ] `Render(spec, out metrics)` plus the delegating single-argument overload, and **no sibling `Measure()`** — §5.1 explains why that tidier-looking design is the trap
- [ ] the fit reference is computed by probing `'0'`..`'9'` and doubling the widest, never hard-coded
- [ ] everything is fitted to the reference, never to the week being displayed
- [ ] `TextRenderingHint` is assigned and is never `SystemDefault`
- [ ] the binding bar fills from the icon edge at `y = 0`, ring slots snap to whole pixels, type sizes are integers
- [ ] knockouts subtract alpha and never paint a background colour
- [ ] the blend centring loop per §5.4 — capped at 4 iterations, digits drawn on their own layer, no reliance on banker's rounding
- [ ] `Font`, `Pen` and `Brush` created and disposed per render; the only cache is the fit result keyed on `(face, box)` (§5.7)
- [ ] the week number is unpadded — week 1 renders `1` (§4.1)
- [ ] the glyph renders correctly at the machine's current scaling and reads cleanly beside the clock
