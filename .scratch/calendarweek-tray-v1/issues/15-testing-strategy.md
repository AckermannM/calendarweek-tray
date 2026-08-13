# 15 — What does v1 actually test?

Type: grilling
Status: open
Blocked by: 07

## Question

Graduated from the map's fog by `07`. The fog patch asked "what's even worth testing in an applet
whose output is a bitmap" and could not be sharpened while the code had no seams. `07` gave it two:

- **`GlyphRenderer.Render(GlyphSpec) → Bitmap` is a pure static function** — no shell, no config, no
  state. It can be called from a test and its output measured pixel by pixel.
- **`Reconcile()` compares a `(week, sizePx, ink, tooltip)` tuple** — pure decision logic, separable
  from the `NotifyIcon` it drives.

That matters because **every bug `06` found was found by measuring pixels, and none of them would
have been caught by looking at the icon** — the `"00"` fit reference that overflowed for weeks 4,
14, 24, 34 and 40–49 would first have appeared in production, in October.

### The decisions

1. **Is there a test project in v1 at all?** It is the first thing in this repo that is not the
   applet, and `04`/`05` kept the tree deliberately flat (one root-level `.csproj`, no `src/`, no
   `.sln`). A test project changes that shape.
2. **What is worth asserting?** Candidates, roughly in descending confidence:
   - ISO week arithmetic at year boundaries (`ISOWeek` is BCL code — is testing it testing *us*?)
   - the fit reference really is the widest label across weeks 1–53 at every size the applet renders
   - no glyph ink escapes the icon box, for all 53 weeks × every `SM_CXSMICON` the applet can see
     (16/20/24/32). `12` decided one form at every size, so this is one property over a size
     parameter rather than a per-form suite — and it is the assertion that would have caught `06`'s
     `"00"` fit bug in October
   - the digits never touch the page outline: `12` measured a **1 px** gap on the widest label and
     **0 px** with the inner air removed, so this property is what pins that constant down
   - `Reconcile()` re-renders on a changed tuple and does nothing on an unchanged one
   - the centring loop converges within its 4-iteration cap for all 53 weeks
3. **Golden-image tests, or measurements?** A checked-in reference PNG per week is exact but breaks
   on any deliberate design change and on any font update. Measured properties (ink stays in the
   box, digit height within tolerance) survive redesign but assert less.
4. **What is deliberately *not* tested**, and is that written down? The GDI handle discipline was
   verified by a throwaway probe this map does not keep; the shell integration was verified by
   killing Explorer by hand.

## Note from `09` (resolved) — a third seam, and one thing made untestable by hand

`09` settled autostart and left this ticket a candidate it did not have before, plus a constraint:

- **Autostart registration is unreachable in a debug run.** `09` decided registration happens in
  **Release builds only**, so that running the exe out of `bin\Debug\` does not leave a dead `Run`
  value on every dev machine. The consequence is that the one behaviour where the applet writes to
  another program's territory **cannot be exercised by running the app** — if it is to be covered at
  all, it needs a unit-level seam over the three guard reads.
- **There is no CLI surface to test.** `09` removed deregistration entirely and noted `OutputType`
  is `WinExe` — no console, no flags, nothing a test could invoke from a command line.
- The two cases worth asserting, if this ticket decides autostart is in scope: the guard **fails
  closed** (any exception on any of the three reads aborts registration — the asymmetry being that
  registering can override user intent while declining never can), and the `Run` value's path is
  **quoted** and sourced from `Environment.ProcessPath` rather than `Assembly.Location`, which
  returns an empty string under single-file publish.

Weigh this against decision 1 honestly: three registry reads and one write is a small surface, and
covering it is the strongest argument yet for a test project existing — but it is also the part of
the applet least likely to change once written.

## Outcome

A decision on whether v1 ships tests, and if so what the test project asserts — precise enough for
`08` to encode. "No automated tests, and here is why" is an acceptable answer if it is argued.

Use `/grilling` and `/domain-modeling`.
