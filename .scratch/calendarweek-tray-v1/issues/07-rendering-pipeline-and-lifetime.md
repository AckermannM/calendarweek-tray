# 07 — Icon rendering pipeline, resource lifetime, and re-render triggers

Type: grilling
Status: open
Blocked by: 05, 06

## Question

Once `06` has decided *what* the icon looks like, decide *how* it gets produced and kept correct for a process that runs for months without restarting.

### Rendering path

Text → `Bitmap` → `HICON` → `NotifyIcon.Icon`. Decide the specifics:

- Text measurement and drawing: `TextRenderer` (GDI, ClearType, matches shell rendering) vs `Graphics.DrawString` (GDI+, different hinting). These do **not** produce the same output at small sizes, and matching the taskbar argues for one of them specifically.
- Antialiasing and hinting mode, and whether pixel-grid alignment matters at 16px.
- Transparency: the icon must sit on an unknown taskbar background, so the bitmap needs a genuine alpha channel, not a colour-keyed one.

**Largely settled by `06` — confirm rather than re-litigate.** `05` flagged a tension here: `TextRenderer` is GDI, GDI text drawing produces no alpha, so it cannot draw onto a transparent bitmap. `06` resolved it empirically and the tension turns out not to bite:

- **`Graphics.DrawString` with `TextRenderingHint.AntiAlias` produces a genuine alpha channel and looks right.** The user picked smooth AA over grid-fit by eye. GDI+ hinting is therefore the accepted answer, and no alpha-reconstruction mechanism is needed.
- **`Bitmap.GetHicon()` preserves partial alpha exactly** — verified by reading both routes' colour bits back with `GetDIBits` against a hand-built 32-bpp `CreateIconIndirect` DIB; identical alpha histograms across three hints. `05`'s worry was misplaced; the `CreateIconIndirect` route is not required.
- **`TextRenderingHint` must be assigned, and must never be `SystemDefault`.** Left unset, GDI+ renders with *zero* partial alpha — that is what made `05`'s glyph look jagged. Explicitly set to `SystemDefault`, it renders subpixel ClearType and writes **coloured** fringes at full alpha onto an icon that will be composited over an unknown taskbar. Only `AntiAlias` and `AntiAliasGridFit` are safe. **State this in the pipeline; it is the single easiest way to reintroduce `05`'s bug.**

What is left for this ticket on the rendering path: **pixel-grid alignment**, which `06` found matters more than expected. Every filled edge must land on an integer boundary — a bar filled from a half-pixel origin leaves a grey seam, and a one-pixel ring slot at a fractional x renders as mush. `06` also established that type sizes must be integers, that the fit reference is the widest label (`"44"`, because the figures are proportional) and not `"00"`, and that horizontal centring must be done by measuring where the ink actually landed rather than by predicting it. Those rules are specified in `06`'s answer and `08` will encode them; `07` needs to decide where they live in the pipeline.

### Resource lifetime — the actual bug risk

`Icon.FromHandle` does **not** own the `HICON`. Every re-render leaks a GDI handle unless `DestroyIcon` is P/Invoked on the previous one. This applet re-renders on a one-minute timer, on theme flips, and on DPI changes — a leak here is unbounded over a month-long uptime, and GDI handles are a per-process limited resource (default 10,000).

Decide the ownership discipline and where it lives, and confirm the old icon isn't destroyed while the shell is still using it.

### Re-render triggers

Enumerate the complete set and the mechanism for each:

- Week changed — one-minute timer, re-render only on difference (`01`, Q12)
- System theme flipped — watch `HKCU\...\Themes\Personalize\SystemUsesLightTheme`; decide between `RegistryKeyValueChanged`/`WaitForSingleObject` and `SystemEvents.UserPreferenceChanged`
- DPI changed — docking a laptop or moving between monitors
- **`TaskbarCreated`** — when Explorer restarts (it does), every tray icon vanishes. The shell broadcasts a registered `TaskbarCreated` window message and applications are expected to re-add themselves. WinForms' `NotifyIcon` may already handle this; **verify rather than assume**, because the failure mode is "the icon silently disappeared hours ago" and it is easy to miss in testing.
- Config reloaded via the menu

### Shutdown

Confirm `NotifyIcon.Visible = false` before exit, so no ghost icon survives the process (see `05`'s note).

## Outcome

A described rendering pipeline and a table of trigger → mechanism → what re-renders, precise enough for `08` to encode without further decisions.

Use `/grilling` and `/domain-modeling`. Where a question is empirical rather than a matter of preference (does `NotifyIcon` handle `TaskbarCreated`? do the two text renderers differ at 16px?), **find out** rather than putting it to the user.

Record the decisions in this file under `## Answer`.
