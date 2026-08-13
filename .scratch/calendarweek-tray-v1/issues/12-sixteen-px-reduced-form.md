# 12 — Does the icon need a reduced form at 16 px?

Type: prototype
Status: resolved
Blocked by: 06

## Question

`06` decided the glyph is a calendar page with a binding bar, rings notched through it, and the
week number in the body. That decision was taken at **24 px**, which is `SM_CXSMICON` at the 150%
scaling this machine runs. At **16 px** — 100% scaling, the most common configuration in the wild —
the same form is measurably tighter:

| | 16 px | 24 px |
| --- | --- | --- |
| taskbar clock type | 12 px | 18 px |
| digits inside `FrameRings` | **8 px** | 13 px |
| digits with no frame at all | 10 px | 15 px |

So at 100% scaling the number is **two thirds the height of the clock beside it**, and the frame is
what costs the difference. The outline is 1 px, the binding bar is 3 px, and each ring slot is a
single pixel wide.

Decide whether that is acceptable, or whether the applet needs a **reduced form** below some size
threshold — dropping the page outline and keeping only the notched bar (`BarWithRings`, which
measures 10 px of digit at 16 px, the same as no cue at all), or dropping the cue entirely.

## Watch for

- A single-pixel ring slot is either a crisp slot or grey mush; `06` pixel-aligned it, so verify
  rather than assume it survived.
- The bare number at 16 px is 10 px against a 12 px clock — still smaller than the clock even with
  no frame. If that also reads as too small, the problem is not the frame and this ticket should
  say so.
- Whether a size-dependent form is worth the complexity at all. Two forms means two things to spec
  in `08` and two things to keep correct in `07`.

## Outcome

Either "the decided form is used at every size" — which is the cheap answer and needs stating
explicitly so `08` can rely on it — or a threshold and a named reduced form.

This is HITL and it needs a real 100% display, or a scaling change on this one. `06`'s prototype
already renders every candidate at 16 px; reuse it rather than rebuilding.

Record the decision in this file under `## Answer`.

## Answer

**The form `06` decided is used at every size.** No threshold, no reduced variant, no second design —
the applet renders `FrameRings` at whatever `SM_CXSMICON` reports and nothing about the glyph is
size-dependent. This is the cheap answer the ticket hoped for, and it needed stating explicitly so
`08` can rely on it.

Decided by the user from the 1:1 contact sheet. The live 100%-scaling tray check was offered and
declined as unnecessary — the sheet puts the clock in the picture at the same scale (see below),
which was the thing the earlier sheets could not do.

### The third option the ticket did not contain, and why it is dead

The ticket framed this as keep-the-form or drop-the-frame. But the frame's 2 px cost is not the
frame — it is **two constants**: a 3 px binding bar and 1 px of air inside the outline. Opening
those up would have kept one form at every size *and* recovered the digits, so it was rendered
before either of the ticket's own options was considered.

It recovers 1 px of the 2, and that 1 px is unusable:

| candidate | bar / air | digits at 16 px | % of the 12 px clock |
| --- | --- | --- | --- |
| `06` as decided | 3 / 1 | 8 px | 66% |
| thinner bar | 2 / 1 | **8 px** | 66% |
| no inner air | 3 / 0 | 9 px | 75% |
| both opened up | 2 / 0 | 9 px | 75% |
| reduced: no page (`BarWithRings`) | — | 10 px | 83% |
| no cue at all (`NumberOnly`) | — | 10 px | 83% |

**The thinner bar buys literally nothing**, and the reason is `06`'s own finding cutting a second
way: digit size in this glyph is constrained by **width, not height**. `06` used that to conclude a
cue *above* the digits is nearly free; the corollary is that giving vertical space *back* is inert.
Only the inner air touches width at all.

And the inner air cannot be given up. Measured gaps between the digit ink and the outline at 16 px:

```
06 as decided    week 44   left gap  0   right gap  1
no inner air     week 44   left gap  0   right gap  0
both opened up   week 11   left gap  2   right gap  3   <- but the stems render grey
```

At `air = 0` the `4`s sit directly against the side outline with nothing between them, and in the
week sweep they visibly fuse into it — the glyph stops reading as a number in a box and starts
reading as a smear. Week 11 is worse than the numbers suggest: the extra shift changes the subpixel
phase its stems land on and they come back grey rather than white, which is the exact side effect
`06` rejected pure optical-mass centring for.

So the frame's cost is **irreducible**, and the ticket's binary really was binary.

### What the sheet showed that no earlier sheet could

Every sheet up to now put the glyph on a flat swatch, so "8 px against a 12 px clock" was a row in a
table. Each 1:1 cell here is a strip of real taskbar — 48 px tall, glyph at the left where the shell
puts it, the Win11 two-line clock right-aligned where the shell puts that, one background, one
scale. That is what the decision was taken from.

### Facts `08` and `13` inherit

- **The ring slots survive at 16 px.** The ticket said verify rather than assume, so the alpha across
  the bar rows was dumped: pure opaque and pure clear, **not one partial pixel**. `06`'s whole-pixel
  snapping holds at the size where a slot is a single pixel wide.
- **`bar = 0.17` and `air = 1 px` are load-bearing constants, not tuning.** `08` should record that
  they were opened up deliberately and put back. Anyone later "reclaiming" that 1 px of padding is
  reintroducing a measured defect.
- **The number never matches the clock at any size, framed or not.** Bare digits are 83% of the
  clock at 16 px and 83% at 32 px; the decided form runs 66 / 73 / 72 / 75% at 16 / 20 / 24 / 32 px.
  Parity with the clock was never on the table — the frame costs about 17 points of a gap that
  already starts at 17. 16 px is the worst case but only marginally, which is a large part of why
  one form at every size is defensible.
- **For `13`:** the reduced form is no longer available as a response to text scaling. `13` asked
  whether raising Windows' text size should make the number occupy more of the same box, and noted
  that the only way to do that is to drop the frame — which is exactly what this ticket has now
  ruled out, for a form the user chose on looking. `13` therefore cannot reach for `BarWithRings`;
  if it wants a text-scaling response at all it needs a mechanism this ticket has not closed, and
  otherwise its answer is "deliberately nothing". The two answers coincide in the direction `13`
  anticipated, but as a constraint rather than a shared mechanism.

### Assets

All in `.scratch/calendarweek-tray-v1/prototype-12/`:

| file | what it shows |
| --- | --- |
| `12-candidates-16px.png` | the six candidates on real taskbar strips beside the clock, 1:1 and ×14, both themes — **the sheet the decision was made from** |
| `12-candidates-24px.png` | the same at this machine's actual `SM_CXSMICON`, as the control |
| `12-week-sweep-16px.png` | weeks 1, 8, 11, 32, 44, 53 across four candidates — where `air = 0` visibly fuses |
| `12-fit-debug.txt` | every measurement quoted above, including the ring-slot alpha dump |

Prototype code is `Prototype12.cs` plus a `sheet12` switch in `Program.cs`, and two tuning levers
(`BarFactor`, `BodyPad`) added to `06`'s `PrototypeGlyph` — both defaulting to exactly what `06`
decided, so `06`'s sheets still reproduce. `Prototype06Lab` gained a "Frame tuning" menu for the
live check that was ultimately not needed. **All of it is throwaway and must not ship.** It stays in
the working tree because `13` is also a prototype ticket and needs the lab; capture onto a
`prototype/` branch when `13` is done.
