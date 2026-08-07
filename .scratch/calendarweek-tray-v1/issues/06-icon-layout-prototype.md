# 06 — Prototype: `KW32` on one line or stacked?

Type: prototype
Status: resolved
Blocked by: 05

## Question

The brief was explicitly undecided here: `"KW32"` or `"KW"` above `"32"`. Neither the user nor the agent should settle this from a description — a 16×16 icon is small enough that intuition is unreliable.

Settle it by looking at it.

## Variables to render

- **Layout**: `single` (`KW32`) vs `stacked` (`KW` / `32`)
- **Padding**: `KW01` vs `KW1` (`01`, Q21 leans padded — confirm or falsify)
- **Size**: 16, 20, 24, 32px — the real `SM_CXSMICON` values across DPI scalings
- **Theme**: dark glyph on light taskbar, light glyph on dark taskbar
- **Optical variant**: `Segoe UI Variable Small` (what `02` established, and its recommended default) vs `Segoe UI Variable Text`. `02` deliberately left this open: at 12 epx the two are not separable by measurement, its shape-match actually ranked `Text` marginally closer to the captured clock, and it stated plainly that if the prototype finds `Text` reads better in a tray icon, no measurement contradicts that. **Render both.**
- **Weight**: Regular 400 is the documented answer. Worth also rendering Semibold, since a 16px glyph on a busy taskbar has different legibility needs than clock text with whitespace around it.

## Deliverable

Two things, because one isn't enough:

1. **A contact sheet PNG** — the full grid above, shown both 1:1 and magnified. 1:1 is the honest view; magnified is how you see *why* one option fails.
2. **The real thing in the tray.** Build the top candidates into the `05` harness and put them in the actual taskbar. An image viewer at 1:1 still lies — it doesn't have the taskbar's background, its neighbours, or its DPI.

## Recorded prior, to be falsified

**Stacked probably loses.** At 16px, two lines gives roughly 8px per line, which is below the legibility floor for Segoe UI Variable. If this turns out wrong, that's the more interesting result and worth capturing in the answer.

Watch for these while looking:

- Does `single` at 16px render `KW32` legibly at all, or does 4 characters in 16px force a font size where the glyphs turn to mush? **If both layouts fail at 16px**, that's a genuine finding that reopens the design — options would include dropping the `KW` prefix from the glyph and moving it to the tooltip, showing the number alone, or accepting that the applet only looks right at 125%+ scaling. Do not paper over this if it happens.
- Does the padded/unpadded choice actually matter visually, or is it invisible at tray size?

## Outcome

A decided default for the `layout` config key, a decided answer on padding, and — if the size findings warrant it — a new ticket for whatever the 16px result reopens.

Use `/prototype`. This is HITL: the user looks at the artifact and reacts. Do not decide it on the user's behalf.

Record the decision and link the contact sheet in this file under `## Answer`.

## Answer

**Decided, by the user, over four rounds of looking at it.** Both layouts this ticket set out to
choose between lost, and the winner is a form the ticket did not contain — which is what the
"if both layouts fail at 16px, that's a genuine finding" clause anticipated.

### The decision

| | |
| --- | --- |
| **Form** | A calendar page: 1 px outline, filled binding bar across the top, **two slots notched through the bar** as binding rings, week number in the body. Called `FrameRings` in the prototype. |
| **Corners** | **Square.** |
| **Padding** | **Unpadded** — `1`, not `01`. Reverses `01`/Q21's lean. |
| **Face** | **`Segoe UI Variable Text Semibold`.** Confirms `02`'s hint that `Text` might beat `Small`, and rejects Regular. |
| **Antialiasing** | **`TextRenderingHint.AntiAlias`** (smooth), not `AntiAliasGridFit`. |
| **Centring** | Blend of ink bounding box and optical centre of mass — see below. |

`KW` appears nowhere in the glyph. The prefix was tried on one line, stacked at equal size, stacked
at 1/3 height, stacked tiny in Regular, beside the number, and knocked out of the binding bar. All
of them lost. The frame carries the "calendar" meaning instead, which was the user's requirement:
a bare number is meaningless to anyone who was not told what it is.

### Why the original two options both lost

**`Single` (`KW32` on one line) fails on measurement, not taste.** Four characters in a square icon
forces the type down to **6 px at 16 px** and **8–10 px at 24 px**, against a taskbar clock of 12
and 18 px respectively. It is grey mush at 1:1. It only becomes legible at 32 px (200% scaling),
where it fits at 12 px.

**`Stacked` at equal size is optically wrong, and the user identified why before I did.** Caps and
lining figures share a cap height, so equal type size is not equal visual weight — and `W`'s four
diagonals add mass the digits do not have. `KW` reads as taller and heavier than the number it
labels. Correcting it by shrinking `KW` worked optically and cost nothing (see below), but the user
rejected the whole stacked family regardless: it still reads as two things, not one.

**The recorded prior in this ticket was therefore wrong in an interesting way.** It predicted
stacked would lose at 16 px for want of vertical room. Stacked was in fact the *better* of the two,
and `Single` was the one that collapsed.

### The finding that unlocked the frame designs

Digit size in this icon is constrained by **width, not height**. At 24 px the number hits the box
edge horizontally before it runs out of vertical room, so vertical space above the digits is nearly
free. Measured digit ink height at 24 px:

| design | digit height | cost |
| --- | --- | --- |
| `NumberOnly` (bare) | 15 px | — |
| `BarOverNumber`, `BarWithRings` | 15 px | **free** |
| stacked with a 1/3-height `KW` | 15 px | **free** |
| `FrameRings`, `FrameBar`, `FrameOutline` | 13 px | −13% |
| stacked at equal size | 11 px | −27% |

So a calendar cue above the digits is nearly free, and the equal-size stack was paying 27% for the
privilege of looking wrong. The chosen `FrameRings` costs 2 px of digit height for a full page
outline — a trade the user took knowingly.

### Bugs found and fixed while prototyping

Four, all measured. Every one of them would otherwise have reached `08`'s spec as a hidden defect.

**1. `05`'s glyph was not antialiased at all.** `05` never assigned `Graphics.TextRenderingHint`,
and GDI+'s untouched default on a memory bitmap produces **zero partial-alpha pixels** — 24 inked,
0 partial. Combined with a ~9.9 px type size against a 12 px clock (ink mass 32.1 px² vs 50.2 for
Semibold at clock size) that is the whole of "small, thin, not antialiased, crappy".

**2. `TextRenderingHint.SystemDefault` is a trap, and is worse than leaving the hint alone.**
Explicitly assigning it makes GDI+ render **subpixel ClearType**: measured 38 of 46 inked pixels
carrying colour, at full alpha, on a bitmap about to be composited over a taskbar whose colour the
applet does not know. `AntiAlias` and `AntiAliasGridFit` are the only safe values. **`07` must
state this explicitly in the pipeline.**

**3. Type size was being fitted to `"00"`, which is not the widest label.** Segoe UI Variable's
figures are **proportional, not tabular** — `"11"` advances 18.60 px against `"32"`'s 25.54 at the
same size, and `4` inks 13 px against every other digit's 11. The widest week label of 01..53 is
therefore **`"44"` at 26 px**, not `"00"` at 24. Fitting to `"00"` silently overflowed the box for
weeks 4, 14, 24, 34 and 40–49 — a bug that would first have appeared in production, in October.
The reference is now computed as the widest digit doubled, not assumed.

**4. The number was off-centre, in two different ways.** First, `Math.Round` is banker's rounding,
so `Math.Round(0.5)` returns 0 and the leftover pixel landed on the same side every week. Second —
and this is the one worth carrying forward — **predicting a draw offset from a measurement taken at
a different origin does not work**, because the rasteriser's antialiasing spills differently
depending on the subpixel phase the glyph lands on. The ink that appears is not the ink that was
measured. The prototype now draws onto its own layer, measures where the ink actually went,
corrects, and repeats until it converges.

### The centring rule, which is subtler than it looks

Centring the ink **bounding box** is wrong for this typeface. Segoe's `1` is a bare stem with a thin
diagonal flag off its top-left and no foot serif. The flag widens the bounding box while carrying
almost none of the glyph's visual weight, so box-centring pushes the stems — what the eye actually
tracks — to the right. Weeks `1` and `11` are the visible cases; `44` is symmetric and unaffected.

Centring the **alpha-weighted centre of mass** fixes that but over-corrects: shifting each week by a
different subpixel amount changes the phase its stems land on, and a stem straddling two pixel
columns renders wider and softer than one sitting on a single column — so some weeks read as a
*larger* number than others. The user caught this.

**The decision is the blend of the two**, which keeps the flag correction without the phase side
effect. `08` should specify the rule, not just the outcome.

### Pixel-alignment rules the form depends on

Square corners were chosen partly because a hairline outline's **arcs cannot be drawn at full
density**: a 1 px stroke on a straight edge lands square on one row of pixels at full alpha, but
swept around a curve it spreads its coverage diagonally across two pixels and halves in density.
That is why the rounded corners read as *missing* rather than as thin. Re-stroking the same path
compounds alpha and fixes it without changing radius or weight — straight edges are already
saturated so they do not change — and that remains the fallback if a later round wants rounded
corners back. Square corners avoid the problem outright.

With the arcs gone, two more half-pixel artefacts became visible and are fixed:

- The binding bar was filled from `page.Y`, which is a **half** pixel because a centred 1 px stroke
  sits at 0.5. Its bottom edge straddled a pixel row and left a grey seam under an otherwise solid
  bar. It now fills from the icon edge in whole pixels and overlaps the outline's top stroke, which
  is invisible because they share a colour.
- The ring slots sat at fractional x. At 16 px a slot is one pixel wide, so that rendered as grey
  mush rather than a slot. Now snapped to the pixel grid.

**General rule for `07`: every filled edge in this glyph must land on an integer pixel boundary.**

### Facts `07` and `08` inherit

- **`Bitmap.GetHicon()` preserves partial alpha exactly.** Verified against a hand-built 32-bpp
  `CreateIconIndirect` DIB by reading both icons' colour bits back with `GetDIBits`: identical
  alpha histograms across three rendering hints. **`05`'s worry was misplaced and `07` does not need
  the `CreateIconIndirect` route** — which also means `05`'s "`TextRenderer` cannot draw onto a
  transparent bitmap" constraint is moot, because GDI+ `DrawString` with `AntiAlias` produces a
  genuine alpha channel and looks right. `07` should confirm rather than re-litigate.
- **Knockouts must subtract alpha, not paint a background colour.** The ring slots and the badge
  variant multiply the target's alpha by the inverse of a mask. Painting "the background colour" is
  not available to this applet — that is almost certainly what made the ugly calendar-frame icon
  the user saw online look the way it did.
- **`"Segoe UI Variable Text Semibold"` is exactly 31 characters and resolves correctly.** Its
  sibling `"Segoe UI Variable Small Semibold"` is 32 and exceeds GDI's `LF_FACESIZE`, resolving to
  the truncated `"...Semibol"` — which measurement confirms is the same physical face, not a
  fallback. `02`'s warning stands for the bare family: `"Segoe UI Variable"` silently becomes
  Microsoft Sans Serif, re-confirmed here.
- **Integer type sizes only.** Fractional sizes at these dimensions land stems between pixels.
- **Digit height at 16 px is 8 px, against a 12 px clock.** The form survives at 100% scaling but
  is noticeably smaller than the clock. Graduated into `12`.

### Assets

All in `.scratch/calendarweek-tray-v1/prototype-06/`:

| file | what it settles |
| --- | --- |
| `06r4-designs-{16,20,24,32}px.png` | the seven surviving designs at every DPI, both themes, 1:1 and ×10 |
| `06r4-centring-modes.png` | the three centring rules on the weeks where they disagree |
| `06r3-corner-tuning.png` | seven corner treatments; the square row is the decision, the x1 row is the bug |
| `fit-debug.txt` | the measurements this answer quotes |

Prototype code is `Prototype06.cs`, `Prototype06Sheet.cs`, `Prototype06Lab.cs` plus a `sheet` /
`lab` / `debug` switch in `Program.cs`. **It is throwaway and must not ship** — `07` designs the
real pipeline and `08` writes the spec. It is uncommitted; capturing it on a `prototype/06-icon`
branch and stripping it from the working tree is still to be done.
