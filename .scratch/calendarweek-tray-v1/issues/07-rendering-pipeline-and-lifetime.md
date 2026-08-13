# 07 — Icon rendering pipeline, resource lifetime, and re-render triggers

Type: grilling
Status: resolved
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

## Answer

**The organising decision is that there is one code path, not five.** Every trigger calls the same
idempotent `Reconcile()`, which computes the glyph the applet *should* be showing and compares it to
the one it *is* showing. Nothing re-renders directly. That choice turned out to be load-bearing:
the experiments below found a theme change that arrives through no message at all, and a
`SynchronizationContext` that silently posts to the wrong thread — both of which this shape survives
and a set of per-event handlers would not.

### The three types

| type | kind | holds |
| --- | --- | --- |
| `GlyphRenderer.Render(GlyphSpec) → Bitmap` | pure static function | every rendering rule from `06`. No shell, no config, no state. |
| `GlyphIcon : IDisposable` | owns `(Icon, HICON)` as one unit | the ownership discipline, structurally |
| `TrayApplicationContext` | the only stateful thing | `NotifyIcon`, timer, subscriptions, last-applied state, `Reconcile()` |

`GlyphSpec` is `(int week, int sizePx, Color ink)` — **no text of any kind**. `06` removed the
prefix from the glyph, so `language`, `label` and `layout` no longer reach the renderer at all. The
config-schema consequences of that are **not** this ticket's and are graduated to `14`.

The seam exists so `06`'s rules have exactly one home, and because a pure bitmap-returning function
is measurable — which is what makes the map's testing fog answerable (graduated to `15`).

### `Reconcile()`

```
desired = (week, sizePx, ink, tooltip)
if desired == lastApplied: return          // the common case, ~every minute forever
render → GlyphIcon → assign to NotifyIcon → dispose the previous GlyphIcon
NotifyIcon.Text = tooltip (truncated to 127)
lastApplied = desired
```

**It compares the rendered result, not the inputs.** A config reload that changes nothing observable
produces an identical tuple and correctly does nothing; no generation counter, no dirty flags, and
double-fired events are provably harmless. It always runs on the UI thread. The whole body is
wrapped in a `catch`: on failure keep the last good icon, mark the tooltip, show one balloon the
first time only — a timer tick must never take the process down, and `01`/Q23 already chose the
tooltip as the diagnostic channel for exactly this.

### Trigger → mechanism → what changes

| trigger | mechanism | thread | what it catches |
| --- | --- | --- | --- |
| **every 60 s** | `System.Windows.Forms.Timer` | UI | week rollover (`01`/Q12), **theme changes that broadcast nothing**, any missed event, resume, clock drift |
| **theme flip** | `SystemEvents.UserPreferenceChanged` | **background → `Post`** | `ink` |
| **DPI / monitor change** | `SystemEvents.DisplaySettingsChanged` | background → `Post` | `sizePx` |
| **clock / timezone change** | `SystemEvents.TimeChanged` | background → `Post` | `week`, `tooltip` |
| **resume from sleep** | `SystemEvents.PowerModeChanged`, `Resume` only | background → `Post` | all |
| **config reload** | menu item | UI | `ink` (theme override), `tooltip` |
| **Explorer restart** | **`NotifyIcon`, unaided — we write nothing** | UI | nothing re-renders; the existing icon is re-added |

The timer is the safety net, not the mechanism: every event is advisory, and losing one costs at
most 60 seconds of staleness rather than a permanently wrong icon.

### The `SynchronizationContext` trap

Measured, not assumed: inside the `ApplicationContext` **constructor**, `SynchronizationContext.Current`
is a plain `SynchronizationContext` — which posts **to the thread pool**. It only becomes a
`WindowsFormsSynchronizationContext` once `Application.Run` pumps. Capturing it in the constructor
would marshal reconciles onto a pool thread and touch `NotifyIcon` cross-thread: a failure that is
rare, mysterious, and would survive every casual test.

**Therefore:** start the timer at `Interval = 1`; on its first tick — which cannot fire before the
pump exists — capture the context, **assert it is a `WindowsFormsSynchronizationContext` and fail
loudly if it is not**, subscribe to `SystemEvents`, then set `Interval = 60000`. This keeps the
applet windowless; the alternative (a hidden marshalling `Control`) adds a real window to a
deliberately windowless process.

### Resource lifetime — measured

A GDI/USER handle probe over 4,000 renders:

| | GDI objects | USER objects |
| --- | --- | --- |
| 3,000 renders, `DestroyIcon` on the replaced handle | **flat at 9** | **flat at 4** |
| 1,000 renders without it | **+3 per render** | **+1 per render** |
| after `GC.Collect()` + finalizers | **not reclaimed** | **not reclaimed** |

Three facts for the spec. Each `GetHicon()` costs **3 GDI + 1 USER** object (icon, colour bitmap,
mask). **The GC never saves you** — `Icon.FromHandle` does not own its handle, so no `Dispose`, no
finalizer and no full collect reclaims it. Against the 10,000-object default limit that is
**~3,300 renders to exhaustion**: harmless at one render per week, but a reconcile bug that
re-rendered every minute would take the process down in **about two and a half days**.

**Discipline:** assign the new icon to `NotifyIcon` **first**, then dispose the previous `GlyphIcon`,
which disposes the `Icon` and `DestroyIcon`s the handle together. Wrapping the pair in one type is
the point — a raw `nint` field shadowing a managed `Icon` is precisely the pairing that rots.

**Recorded gap, honestly:** assign-then-destroy is safe *by contract* — `Shell_NotifyIcon` is
synchronous and the shell copies the icon before returning — **not by measurement**. The probe
proves the registration survives, not the pixels. Proving the pixels needs a DPI-aware screen
capture rig, which `12` needs anyway; if it ever produces one, this is worth re-checking.

### Rendering path — confirmed, not re-litigated

`06` settled this empirically and it stands: `Graphics.DrawString` with
**`TextRenderingHint.AntiAlias`**, `Bitmap.GetHicon()`, genuine alpha channel, no
`CreateIconIndirect`, no `TextRenderer`. `05`'s "GDI text has no alpha" constraint is moot.

**`TextRenderingHint` must be assigned and must never be `SystemDefault`** — unset renders with zero
partial alpha (`05`'s jagged glyph), and `SystemDefault` renders subpixel ClearType, writing
*coloured* fringes at full alpha onto an icon composited over an unknown taskbar. Only `AntiAlias`
and `AntiAliasGridFit` are safe.

**All of `06`'s rules live inside `GlyphRenderer` and nowhere else**: integer type sizes only; the
fit reference is the widest digit doubled (`"44"`, because the figures are proportional) and never
`"00"`; every filled edge lands on an integer pixel boundary; the binding bar fills from the icon
edge in whole pixels; ring slots snap to the grid; knockouts subtract alpha rather than painting a
background colour; the face is `"Segoe UI Variable Text Semibold"` spelled exactly (31 chars, under
`LF_FACESIZE`; the bare family silently becomes Microsoft Sans Serif).

**Centring** is the blend of ink bounding box and alpha-weighted centre of mass, arrived at by
drawing, measuring where the ink actually landed, correcting, and repeating — **capped at 4
iterations, last result taken if it has not converged**. No cache and no precompute: at dozens of
renders per year the cost is unmeasurable, and a cache is a second source of truth that goes stale
on a DPI change. For the same reason **`Font`, `Pen` and `Brush` are created and disposed per
render** — a `Font` cached at 24 px is simply wrong after a DPI change.

### Colour and theme

**Pure `#FFFFFF` ink on dark, pure `#000000` on light.** Shell tray icons are monochrome white in
dark theme and matching that is what makes the applet look native; sampling the real taskbar colour
has no API behind it.

The source is **`SystemUsesLightTheme`** (the taskbar's own setting, per `01`/Q11), not
`AppsUseLightTheme`. **Absent key ⇒ light ⇒ black ink**, that being the documented Windows default
for a machine where it was never toggled. The `theme` config key overrides. **High contrast is not
07's** — it is the same accessibility question as `13`'s text scaling and is folded in there.

### Multi-monitor

**Render for the primary monitor's DPI, always.** A single `NotifyIcon` yields a single `HICON`, so
per-monitor correctness is not available at any price; a differently-scaled secondary taskbar gets a
shell-scaled glyph, and that is a documented limitation rather than a bug.

### Startup and shutdown

Startup: **mutex first** (`01`/Q20 — a second instance must never touch the tray), then render, then
assign, **then `Visible = true`** — setting `Visible` before an icon exists shows a blank frame.

Shutdown: `Visible = false` before exit (`05`'s ghost icon), **unsubscribe every `SystemEvents`
handler**, dispose the `GlyphIcon`. `SystemEvents` are *static* events: a subscription outlives the
object and can fire during shutdown — the same class of bug as the `HICON`, and it bites only after
the object should have been dead.

### Facts established by experiment

Explorer was restarted live, with a hand-registered `Shell_NotifyIcon` icon alongside as a control
that deliberately never re-adds itself — without it, "the icon is still there" would not have been
evidence of anything.

- **`NotifyIcon` genuinely re-adds its icon on `TaskbarCreated`; we need to write nothing.** The
  control icon never came back for the rest of the run, so the test was sensitive.
- **There is a ~4-second window where the icon is really gone** (Explorer killed at `11:05:19`,
  broadcast at `11:05:24.303`, re-registered by `11:05:25`). Nothing can be done about it and
  nothing needs to be.
- **`NotifyIcon._added` stayed `True` throughout that window — it is not a health oracle.** The
  honest check is `Shell_NotifyIcon(NIM_MODIFY)`, which returns `False` when the shell has forgotten
  the icon.
- **`SystemEvents.UserPreferenceChanged` fires on a background thread** (thread 4 against UI thread
  2) with the uninformative `category=General`. Marshalling is mandatory; filtering by category is
  pointless, which is why `Reconcile()` ignores the category entirely.
- **A registry theme write that broadcasts nothing produces no event at all** — not
  `UserPreferenceChanged`, not `WM_SETTINGCHANGE`. Theme switchers and dark-mode schedulers that
  poke the registry directly are therefore invisible to every event mechanism. **Only the poll
  catches them.** (Whether the shell itself repaints in that case was not established — a screen
  capture attempt was invalidated by PowerShell's DPI-unawareness and is not evidence.)
- **`NotifyIcon.Text`'s real limit on .NET 10 is 127 characters, not the 63 recorded in `01`/Q16.**
  The worst-case tooltip — `"Kalenderwoche 33 · 10.–16. August 2026 · ⚠ config.json invalid (line 4)"`
  — measures 71, comfortable now but over a 63-char budget. Truncate at 127 as a safety net.
- **The installed runtime is `Microsoft.WindowsDesktop.App 10.0.11`**, not the 10.0.10 the map
  records.

Probe sources are throwaway and live outside the repo, in this session's scratchpad
(`gdiprobe`, `trayprobe`, `textprobe`).

### Known gaps carried forward

1. If an Explorer restart coincides with a DPI change, `NotifyIcon` re-adds the **previous** glyph
   at the old size, and the poll corrects it within 60 seconds. Accepted over adding a second
   top-level window to observe `TaskbarCreated` ourselves.
2. Destroy-ordering pixel safety is reasoned, not measured (above).
3. Secondary taskbars at a different scale get a shell-scaled glyph (above).
