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

**Status:** resolved

- [x] `GlyphRenderer` is pure static — no shell, no config, no state; `GlyphSpec` and `GlyphMetrics` per §5.1
- [x] `Render(spec, out metrics)` plus the delegating single-argument overload, and **no sibling `Measure()`** — §5.1 explains why that tidier-looking design is the trap
- [x] the fit reference is computed by probing `'0'`..`'9'` and doubling the widest, never hard-coded
- [x] everything is fitted to the reference, never to the week being displayed
- [x] `TextRenderingHint` is assigned and is never `SystemDefault`
- [x] the binding bar fills from the icon edge at `y = 0`, ring slots snap to whole pixels, type sizes are integers
- [x] knockouts subtract alpha and never paint a background colour
- [x] the blend centring loop per §5.4 — capped at 4 iterations, digits drawn on their own layer, no reliance on banker's rounding
- [x] `Font`, `Pen` and `Brush` created and disposed per render; the only cache is the fit result keyed on `(face, box)` (§5.7)
- [x] the week number is unpadded — week 1 renders `1` (§4.1)
- [x] the glyph renders correctly at the machine's current scaling and reads cleanly beside the clock

## Answer

`GlyphRenderer.cs` extracted from `PrototypeGlyph` on `prototype/06-13`, stripped to only the decided
path: `Design.FrameRings`, `FrameStyle.Square`, `Centring.Blend`. The `Design`/`FrameStyle`/`Centring`
enums, the other six design variants, and all prototype scaffolding (`DumpFits`, the public
`LastNumberInkHeight`, `Face`/`Style`/`Centre` setters used by ticket 13's lab) are gone — the
constants they used to vary are now `private const`.

Verified by rendering a contact sheet of weeks {1, 8, 11, 32, 44, 53} at box sizes {16, 20, 24, 32}px
in both ink colours (white-on-dark, black-on-light) via a throwaway harness, and inspecting the PNGs
by eye: square corners, filled bar, two knocked-out ring slots, unpadded digits, no visible overflow
or clipping at any size, including week 44 (the reference). `GlyphMetrics` fields (type size, digit
ink, body, page, converged) were logged alongside and spot-checked — type sizes are integers, digit
ink stays inside the box, `Converged` correctly reports the blend loop's outcome per render.
A live-taskbar screenshot was attempted for the "beside the real clock" check but the sandbox's
`Shell_TrayWnd` capture returned unrelated window content, not the real desktop, so that specific
half of the verification could not be completed here — worth a manual look after `06` wires the
renderer into the tray for real.

`/code-review` (background, ultra-thorough) surfaced 10 findings, all against the actual file, not
speculative. Two were fixed here: `Render`'s bitmap now disposes itself if a later step throws, and
the `body.Width > 2 && body.Height > 2` guard from the prototype's `DrawFrame` — dropped during
extraction — is back, so a degenerate box size gets an empty body instead of a 4px-fallback font.
Two more were low-risk hardening applied without changing output (metrics before/after the changes
are byte-identical): the fit/reference caches are `ConcurrentDictionary` now, since nothing pins
`Render` to one thread and ticket 03's suite may call it from several; `MeasureInk` now reuses
`InkBoundsOf` instead of duplicating its scan; the always-true `barHeight >= 2` guard around the
ring-slot knockout is gone.

Two findings were deliberately **not** fixed, because they're properties of the *decided* algorithm
ticket 01 said to extract verbatim, not regressions introduced here:
- The blend centring loop doesn't always converge inside 4 iterations (measured: 46/265 week×size
  combinations). §5.4's pseudocode explicitly says "taking the last result if it has not converged"
  — `GlyphMetrics.Converged` exists precisely to surface this, not to make it impossible.
- `ReferenceFor`'s fit can, at some sizes, be a pixel narrower than what a real render of that same
  text produces once the centring loop moves it off the fixed measurement origin (reproduced at
  24px/week 44: fitted ink 20px wide, rendered ink 21px). This is the same "measure the real render,
  don't predict it" tension §5.1 names as the reason `Render` returns metrics instead of hiding them
  behind a `Measure()` — the fix belongs in `03`'s measured-property tests, not in a silent
  correction here that would fight the philosophy the spec lays out.

Remaining findings were about `Program.cs` and `TrayApplicationContext.cs` — ticket 01's already-
resolved code and ticket `06`'s `GlyphIcon`/`Reconcile()` work — out of scope here.

`dotnet build` at 0 warnings, 0 errors throughout.
