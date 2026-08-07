# 12 — Does the icon need a reduced form at 16 px?

Type: prototype
Status: open
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
