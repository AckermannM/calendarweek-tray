# 13 — Accessibility: text scaling, and high contrast

Type: prototype
Status: resolved
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
height. That is a direct trade against `06`'s decision.

**`12` has since closed that door.** It decided **one form at every size** — the user chose, on
looking, that the framed glyph reads acceptably at 16 px — so the reduced form is no longer
available to this ticket either. `12` also measured that the frame's cost is *irreducible*: opening
up its two constants recovers 1 px of the 2, and that 1 px fuses the digits into the outline. So
this ticket cannot reach for `BarWithRings`, and it cannot recover height by tuning. If it wants a
text-scaling response at all, it needs a mechanism neither `06` nor `12` has closed — and if there
isn't one, the answer is "deliberately nothing", which the ticket already names as legitimate.

Note this does **not** dispose of the high-contrast half below, which is untouched by `12` and is
the part `08` needs either way.

Doing nothing is a legitimate outcome. The applet would then be one of many tray icons that ignore
text scaling, which is the platform norm — but that should be a decision on the record, not an
omission.

## High contrast — folded in by `07`

`07` decided the ink is **pure white on dark, pure black on light**, read from `SystemUsesLightTheme`.
Under a **high contrast** theme that is potentially wrong in both directions: the taskbar is painted
from the high-contrast palette, which is neither, and a user running high contrast is by definition
the user least able to absorb a low-contrast glyph.

`07` deliberately did not answer it — it is the same question as text scaling (does this applet
respond to accessibility settings at all?) and answering half of it there would have split one
decision across two tickets.

The specific questions: does `SystemInformation.HighContrast` need to override the light/dark ink
choice, and if so with what — `SystemColors.WindowText` on `SystemColors.Window`, or something the
taskbar exposes more directly? And does the `theme` config key (`auto`|`light`|`dark`) need a fourth
value, or does high contrast simply win over `auto` and lose to an explicit override?

## Work

1. Read `TextScaleFactor` from `HKCU\Software\Microsoft\Accessibility` and confirm what it reads
   when unset, so the applet does not misread "absent" as 100%.
2. Raise the setting on this machine, look at the decided glyph beside real system text, and put it
   back.
3. Confirm whether `SM_CXSMICON` moves with it. The expectation is that it does not — verify.
4. Switch on a high contrast theme, look at the decided glyph in the tray, and put it back. Record
   what `SystemUsesLightTheme` reads under high contrast — if it still reads 0/1 unchanged, then
   `07`'s rule silently produces the wrong ink and the applet cannot detect it from that key alone.

## Outcome

Either "text scaling is ignored, deliberately, and here is why", or a described response precise
enough for `08` to encode — plus the high-contrast ink rule, which `08` needs either way.

Record the decision in this file under `## Answer`.

## Answer

**Text scaling is ignored, and high contrast is not.** The two halves of this ticket looked like one
question — "does this applet respond to accessibility settings at all?" — and they turn out to have
opposite answers, for the same reason: what matters is not what the *setting* does, it is what the
*taskbar* does.

- **Text scaling: deliberately nothing**, and not because a response was unavailable. The ticket's
  premise is false — the taskbar does not scale either.
- **High contrast: `07`'s ink rule is broken and must change.** `Ink = HighContrast ?
  SystemColors.MenuText : (SystemUsesLightTheme ? black : white)`. High contrast wins over
  everything, including an explicit `theme` override.
- **`06`'s `Segoe UI Variable Text Semibold` stands at every size, in every theme.** The one lever
  `06` and `12` had left open is closed here with nothing to spend it on.

### The reshaping finding: the taskbar is immune to text scaling

This ticket spent its whole framing — and `12`'s inherited constraint — on a trade it did not have
to make. The stated concern was that "a user who raises Windows' text size to 200% gets every label
on their system larger — and a calendar-week applet whose number is unchanged". **The second half
is true of the taskbar clock too**, so the applet is not the odd one out. It would *become* the odd
one out by responding.

Measured, not argued, and in two independent steps because the first one alone is ambiguous:

1. **The setting really applies.** Writing `HKCU\Software\Microsoft\Accessibility\TextScaleFactor`
   as a `REG_DWORD` moves `Windows.UI.ViewManagement.UISettings.TextScaleFactor` from `1` to `2`
   within three seconds — no Settings app, no sign-out, no explorer restart. So a null result in
   step 2 cannot be dismissed as "the write never took".
2. **The taskbar does not move one pixel.** An exact pixel diff of the whole `Shell_TrayWnd`
   capture, 2560×72 = **184,320 pixels**:

   | comparison | pixels differing |
   | --- | --- |
   | control: 100% → 100%, 4 s apart | **0** |
   | 100% → 200% | **0** |
   | 100% → 225% | **0** |

   The control row is what makes this a measurement rather than a coincidence: it establishes that
   the taskbar's own idle repainting contributes zero noise, so zero on the test rows means zero.
   `SM_CXSMICON` stays at 24 throughout, confirming work-item 3's expectation.

So there is no mechanism to reach for and, more importantly, **nothing to respond to**. The whole
design brief is "look native beside the clock"; scaling the glyph while the clock stayed put would
break exactly the requirement that motivates the applet's existence. `12` closed the door on
dropping the frame and on tuning its constants, and this ticket anticipated that the answer might
then have to be "deliberately nothing" — but as a concession. It is not a concession. Even with an
unlimited budget of digit height, spending it would be wrong.

`SM_CXSMICON` follows DPI and not text size, and that turns out to be **correct behaviour, not a
gap** — it keeps the applet in step with the surface it lives on.

**The one thing to be careful reading absent.** Work item 1 asked what `TextScaleFactor` reads when
unset, and the trap is sharper than expected: **the containing key `HKCU\Software\Microsoft\
Accessibility` exists on a machine that has never touched the setting**, with no values in it. So
probing for the *key* proves nothing; only the *value*'s absence means "never set", and absent means
100. This is moot for v1 since nothing reads it — recorded because it is the kind of thing that gets
re-derived wrongly later.

### High contrast: `07`'s rule is broken, and cannot know it

This half is a real defect caught before it shipped. `07` reads `SystemUsesLightTheme` and picks
pure white or pure black. **That key reads `0` under all four stock contrast themes** — including
*High Contrast White*, where the taskbar is painted `#FFFAEF`. `07`'s rule therefore paints
**`#FFFFFF` on `#FFFAEF`: a contrast ratio of 1.04:1**, an invisible icon, for the user least able
to absorb a low-contrast glyph. And the applet cannot detect this from `SystemUsesLightTheme` alone,
because that key does not move. `SystemInformation.HighContrast` is required.

Row 3 of the decision sheet is that failure in the real tray; row 4 is the fix.

**The winning ink is one this ticket did not propose.** The ticket suggested `SystemColors.WindowText`
on `SystemColors.Window`. Dumping the palette in every stock contrast theme and sampling what the
shell actually paints the taskbar with:

| theme | taskbar bg | shell's own ink | `MenuText` | `WindowText` | `ControlText` |
| --- | --- | --- | --- | --- | --- |
| High Contrast Black | `#202020` | `#FFFFFF` | `#FFFFFF` ✓ | `#FFFFFF` ✓ | `#FFFFFF` ✓ |
| High Contrast White | `#FFFAEF` | `#000000` | `#000000` ✓ | `#3D3D3D` ✗ | `#202020` ✗ |
| High Contrast #1 | `#2D3236` | `#FFFFFF` | `#FFFFFF` ✓ | `#FFFFFF` ✓ | `#B6F6F0` ✗ cyan |
| High Contrast #2 | `#000000` | `#FFFFFF` | `#FFFFFF` ✓ | `#FFFFFF` ✓ | `#FFEE32` ✗ yellow |

**`SystemColors.MenuText` matches the shell's measured taskbar ink 4 times out of 4.** `WindowText`
is legible everywhere (10.43:1 worst case) but is not what the shell uses; `ControlText` is the
accent colour in two themes and would put a cyan or yellow calendar page in the tray. `MenuText`
gives 20.17:1 in the worst case.

`SystemColors.Window` likewise equals the measured taskbar background in all four themes — recorded
in case it is ever wanted, though the glyph is transparent and never needs it.

**The rule must stay conditional, because `SystemColors` does not track dark theme.** On this machine
with dark mode fully on (`SystemUsesLightTheme=0`, `AppsUseLightTheme=0`) and high contrast off, the
palette is still the classic light Win32 one: `Window=#FFFFFF`, `MenuText=#000000`, against a
taskbar painted `#202020`. An unconditional `SystemColors.MenuText` would be **1.29:1 — invisible**,
i.e. the same bug, moved. So:

```csharp
Color Ink => SystemInformation.HighContrast
    ? SystemColors.MenuText
    : (systemUsesLightTheme ? Color.Black : Color.White);
```

This also confirms `07`'s non-high-contrast choice by measurement rather than by argument: the
shell's own taskbar ink on this dark theme is exactly `#FFFFFF`, and `07` picked pure white.

### High contrast wins over the `theme` config key — and no fourth value

`theme` keeps its three values `auto|light|dark`. High contrast is **detected, never configured**,
and it overrides all three.

It beats an explicit override, not only `auto`. The reasoning is that the key exists to correct a
mis-read of *what colour the taskbar is*; under a contrast theme that colour is known exactly from
the palette, so there is nothing left for the user to correct. Letting an explicit `theme: dark`
win would leave a reachable state where a config file produces an invisible icon — re-creating
through configuration the exact defect this ticket exists to remove.

This is not the applet *governing*, in `09`/`10`/`11`'s sense. Those rulings were about the applet
declining to decide things that belong to the user or the shell — where its icon lives, whether it
starts. This decides only what colour its own ink is, which was always its job.

### The weight lever, closed

`12` left exactly one mechanism open: more weight in the same box, which buys legibility without
asking the canvas to grow. It was rendered in the real tray at `Segoe UI Variable Text` (regular),
`Text Semibold` (`06`'s decision) and `Display Bold` — rows 5–7 of the decision sheet. Regular reads
visibly thinner; `Display Bold` is not better than Semibold at 24 px. With no text-scaling response
to attach it to, a second weight would be complexity `07` and `08` carry for nothing, so **`06`'s
Semibold stands unchanged, at every size and in every theme**. `12`'s "one form at every size" now
extends to one *weight* at every size.

### What `08` and `07` inherit

1. **The ink rule above**, verbatim, with `SystemInformation.HighContrast` as a third input to the
   desired-state tuple `07` defined. `07`'s existing triggers already cover it — a contrast theme
   change raises `SystemEvents.UserPreferenceChanged`, and the 60 s poll is the safety net for the
   case `07` measured where a theme write broadcasts nothing.
2. **A spec line recording the non-behaviour**: the applet never reads `TextScaleFactor`, never
   references `Windows.UI.ViewManagement`, and does not change with the accessibility text size.
   Recorded so it reads as a decision rather than an oversight — which is what this ticket was
   opened to guarantee.
3. **`SystemColors` is cached by .NET and refreshed on `WM_SYSCOLORCHANGE`.** The experiment
   harness pumps messages deliberately for this reason rather than sleeping. This is not a risk for
   the applet as designed — `Application.Run` means a live message loop by construction — but it is
   worth a line so nobody later reads the palette from a worker thread or a startup path that runs
   before the loop. *Provenance: known .NET behaviour that the harness was built to avoid, not
   isolate-tested here.*
4. **No config change.** `theme` stays `auto|light|dark` — a fact `14` can rely on rather than
   reopen.
5. `TextScaleFactor`'s containing key exists even when the value never has, per above.

### Assets

All in `.scratch/calendarweek-tray-v1/prototype-13/`:

| file | what it shows |
| --- | --- |
| `13-decision-sheet.png` | **the sheet the decision was taken from** — `07`'s rule against `HC → MenuText` on real taskbar strips in both contrast themes, plus the three weight candidates |
| `13-verify.txt` | the exact pixel diffs with their control, and the full palette + painted-taskbar dump for all four contrast themes |
| `13-experiment.txt` | the full matrix run: five text scales, three weights, six ink rules × two themes, with restore verification |
| `13-probe.txt` | the baseline single-shot probe |
| `13-hc-*.png`, `13-text-*.png`, `13-weight-*.png` | every individual taskbar strip behind the above |

Prototype code is `Prototype13.cs` (a live tray lab), `Prototype13Experiment.cs` (the unattended
matrix, restoring every setting in a `finally`), `Prototype13Verify.cs` (the exact-diff and palette
pass) and `Prototype13Sheet.cs`, plus `lab13`/`probe13`/`experiment13`/`verify13`/`sheet13`
switches in `Program.cs`. `06`'s `PrototypeGlyph.Face` became settable — defaulting to exactly what
`06` decided, so `06`'s and `12`'s sheets still reproduce. **All of it is throwaway and must not
ship**; `12` asked for it to be captured onto a `prototype/` branch once `13` was done, which is now.
