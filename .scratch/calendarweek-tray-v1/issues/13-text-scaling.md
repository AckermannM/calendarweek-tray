# 13 — Accessibility text scaling: does the applet respond to it at all?

Type: prototype
Status: open
Blocked by: 06

## Question

Graduated from the map's fog now that `06` has produced a glyph to look at.

`02` could not test `TextScaleFactor` because it is unset on this machine. The concern is
structural: a tray icon's dimensions come from `SM_CXSMICON`, which follows **DPI scaling** and
does **not** follow the accessibility text-size setting. A user who raises Windows' text size to
200% gets every label on their system larger — and a calendar-week applet whose number is unchanged.

Decide whether the applet should respond, and if so how, given that the canvas it draws into does
not grow.

## The tension to resolve

The icon box is fixed. So "respond to text scaling" can only mean making the number occupy more of
the same box — which means dropping the frame `06` chose, since the frame is what costs digit
height. That is a direct trade against `06`'s decision, so this ticket may end up proposing exactly
the reduced form `12` is deciding on, for a different reason. **Check `12` before designing
anything**; if the answers coincide, say so and let one mechanism serve both.

Doing nothing is a legitimate outcome. The applet would then be one of many tray icons that ignore
text scaling, which is the platform norm — but that should be a decision on the record, not an
omission.

## Work

1. Read `TextScaleFactor` from `HKCU\Software\Microsoft\Accessibility` and confirm what it reads
   when unset, so the applet does not misread "absent" as 100%.
2. Raise the setting on this machine, look at the decided glyph beside real system text, and put it
   back.
3. Confirm whether `SM_CXSMICON` moves with it. The expectation is that it does not — verify.

## Outcome

Either "text scaling is ignored, deliberately, and here is why", or a described response precise
enough for `08` to encode.

Record the decision in this file under `## Answer`.
