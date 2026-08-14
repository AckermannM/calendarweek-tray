# 15 — What does v1 actually test?

Type: grilling
Status: resolved
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

## Answer

**v1 ships a narrow suite of measured properties over the pure renderer, and nothing else.** xunit v3
in `Tests/`, run by `dotnet test`, asserting six things about the glyph plus config rejection, the
tooltip strings and the ink rule — no golden images, no CI, and no coverage of anything that would
need a production abstraction to reach.

### What reshaped the ticket

Three of its premises were false, and the third turns out to be a defect in the shipping binary.

- **The measurement primitives are not throwaway — they already ship.** The ticket's decisions 1 and 2
  both read as though tests would need new machinery. They don't. `06`'s centring loop calls
  `InkBoundsOf` and `OpticalCentreX` on **every render**, and the fit calls `MeasureInk`; `07` puts all
  of `06`'s rules inside `GlyphRenderer`, so pixel measurement is load-bearing production code whether
  or not v1 has a test. The marginal cost of assertions is **callers only**. That is most of the case
  for answering decision 1 "yes".
- **`09`'s "there is no CLI surface to test" is false as written.** `Program.cs` dispatched on `args`
  through three tickets — nine commands. A `WinExe` has no attached console, but it has argv. What
  `09` actually established is narrower: the `Run` value carries no arguments, so a CLI surface would
  never be exercised in production.
- **Those nine commands are in the Release build**, unguarded, including `experiment13`, which writes
  `HKCU\Software\Microsoft\Accessibility\TextScaleFactor`. A shipped v1 would carry a prototype
  harness that mutates the user's accessibility settings. This ticket owns the removal decision
  (below) because the harness *is* this repo's existing de facto test apparatus.

And one fact that removed a constraint before it could bind: xunit v3 `3.2.2` and
`Microsoft.NET.Test.Sdk` `18.8.1` are already in the local NuGet cache, so no option was ever gated on
network access.

### The dependency rule was deciding a question it was never argued for

The map's Notes said flatly *"No third-party dependencies. BCL + WindowsDesktop runtime only."* Every
rationale this map has actually written for it is about the **shipped artifact** — `04`'s trimming
survey, `10`'s runtime-dependency story, the 194.3 KB figure. None of it touches a project that is
never published. Left as written it would have silently ruled out every test framework on the strength
of an argument made about something else, so it is **re-scoped to bind the shipped artifact, not the
repo** (the map's Notes are updated accordingly).

That mattered, because the zero-dependency alternative — a hand-rolled console runner with an exit
code, very much this repo's established style after nine such harnesses — had exactly one advantage,
and it was the one the rescoping dissolved. What remained was that `dotnet test` is the command a
future agent will actually think to run, which is precisely the failure mode ("tests nobody runs")
that argued against having tests at all.

**A self-test arg inside the applet was the third option and is out**: it ships test code in the
artifact `10` measured and keeps alive the very `Main` switch this ticket is deleting.

### `07`'s signature could not assert the bug this ticket exists for

The sharpest finding. `GlyphRenderer.Render` returns a `box × box` bitmap, so **digit overflow is
silently cropped and unobservable in the output** — the `"00"` bug, the ticket's headline
justification, would *not* have been caught by measuring the returned bitmap. Worse, once the frame is
drawn the page outline itself inks column 0 and `box-1`, so `InkBoundsOf` on the composite cannot see
the digits at all.

The metrics exist at render time and are simply not returned: `06`'s `DrawNumber` already draws the
digits onto their own layer precisely so "where did the ink go" stays answerable underneath a frame.

So `07`'s signature changes: **`Bitmap Render(GlyphSpec spec, out GlyphMetrics metrics)`**, with the
existing one-arg overload delegating to it. `GlyphMetrics` carries the fitted type size, the digit ink
rect, the body rect, the page rect, and whether the centring loop converged.

**A sibling pure `GlyphMetrics Measure(GlyphSpec)` was rejected, and this is the load-bearing part.**
It reads as the tidier design and it is a trap: a separate measure path is a *prediction* of what
`Render` will do, and predicting the draw offset instead of measuring where the ink actually landed is
the deepest error `06` found and fixed. A test must observe the real render or it re-introduces the
bug class it was written to catch. This is a return value, not a seam, so it does not reopen the
no-new-abstraction rule below.

### What is asserted

Over all **53 weeks × {16, 20, 24, 28, 32}** — the five sizes `SM_CXSMICON` reports at the five
standard scalings. All 53 weeks is non-negotiable: the `"00"` bug hit weeks 4, 14, 24, 34 and 40–49
specifically, so any week sample can miss it.

1. **Air preserved** — digit ink sits ≥ 1 px inside the page outline left, right and bottom, and ≥ 1 px
   below the binding bar. One assertion covering two bugs: it fails on overflow (`06`'s `"00"`) *and*
   on `BodyPad` being reclaimed (`12` measured 0 px there, where the `4`s visibly fuse into the side
   outline). This subsumes the ticket's separately-listed "no ink escapes the icon box", and is phrased
   against the rect `12` actually measured.
2. **Centring** — |left gap − right gap| ≤ 1 px. Not 0: the loop exits at sub-pixel drift (< 0.15 px),
   so integer margins can legitimately differ by one.
3. **Convergence** — the centring loop converged inside its 4-iteration cap.

Per size:

4. **The reference really is the widest** — no week's digit ink exceeds the reference label's at the
   same fitted size. This is what pins `"44"` over `"00"`.
5. **Bar geometry** — height `== max(2, round(box × 0.17))`, and alpha **exactly 0** at both slot
   centres. No tolerance: `12` measured the slots crisp at 16 px with not one partial-alpha pixel, and
   a tolerance would let that regress silently.

Off the glyph:

6. **Config** (`14`) — unknown key rejected, unknown *value* rejected, absent file → `(auto, auto)`.
   **Tooltip** — the `de` and `en` date-range string for one known week, this being the only place the
   applet uses language at all. **The ink rule** (`13`) — `HighContrast ? MenuText : light ? black :
   white` across all six theme states.

That last one is a requirement pushed back onto `08`, not an option: **the desired-tuple computation
must be specified as a pure function**, `(week, sizePx, highContrast, lightTaskbar, config) → (week,
sizePx, ink, tooltip)`. `07`/`15` had left this conditional on `08` choosing to, which is exactly the
shape `08`'s own Notes forbid. It earns the requirement because `13` caught **the only pure-logic
defect this map ever found** — `07`'s rule painting `#FFFFFF` on High Contrast White at **1.04:1**, an
invisible icon, undetectable from `SystemUsesLightTheme` alone. A rule that has already been wrong once
and has six theme states to get right is worth three lines of test.

### Measured properties, never golden images

A checked-in reference PNG per week × size is exact and catches everything. It is rejected because
`06` and `13` pinned the render to `Segoe UI Variable Text Semibold` **as installed**: a Windows font
update re-rasterises the glyph and breaks every golden with no bug present. That is a maintenance
obligation triggered by a third party — the category this map has now refused four times (winget,
signing, updater, installer). It is also the weaker signal: a broken golden reports *that* something
changed, never *what*, and every property above is a specific number.

Confirmed by the record: every bug `06` found and every fact `12` established came out of
`InkBoundsOf`/`MeasureInk`, not out of looking at the icon.

### What is deliberately not tested

**The autostart guard and the icon swap are out.** `09`'s note called the guard the strongest argument
yet for a test project — it is the one behaviour unreachable by running the app, since `09` made
registration Release-only. It still loses: covering three `Registry.GetValue` calls and one
`SetValue` means abstracting the registry behind an interface the applet has no other use for, a seam
bigger than the thing it wraps. The icon swap is the same shape, inseparable from `NotifyIcon`. This
map has consistently refused to let a secondary concern add structure to the applet (`09`'s
never-governs, `10`'s no-enforcement, `11`'s no-promotion); a suite that grows a production interface
is that move again. `09` itself notes the guard is "the part of the applet least likely to change once
written".

**Raw ISO week arithmetic is out** — `ISOWeek.GetWeekOfYear` is BCL code and testing it is testing
Microsoft. Our zero-padding and our date-range strings are covered above.

This list belongs in the **spec**, as a short "verified by hand, not by test" note — **not** in the
map's Out of scope. The autostart guard is squarely *in* v1's scope; it is simply covered by hand.
Conflating the two would misrepresent v1's surface to whoever builds it.

### Nothing enforces the suite, and that is deliberate

**No CI.** `10` already puts releases on GitHub so Actions was available, and it loses on a decisive
unknown: **`Segoe UI Variable` is a Windows 11 font and it is unverified whether GitHub's Windows
Server runners carry it.** If they do not, every measured property either fails or silently measures a
fallback face — worse than no CI, because it is a red suite that means nothing. Settling that is
itself a chunk of work, and a workflow is a recurring obligation on top.

Instead `dotnet test` becomes a documented gate in the release procedure `10` wrote into the README:
**quit, test, publish, zip, tag**. If the font question is ever worth settling, it is a fresh ticket,
not a v1 blocker.

### The prototype harness goes, entirely

All nine prototype files are deleted and `Main` is reduced to `ApplicationConfiguration.Initialize()`
+ `Application.Run(new TrayApplicationContext())` with **no argv handling at all** — which also closes
the Release-ships-`experiment13` defect above.

Nothing is relocated to `Tests/`. The primitives were never in question: `MeasureInk`, `InkBoundsOf`,
`OpticalCentreX`, `Fit` and the centring loop move into `GlyphRenderer` as `07` specifies, because
they are production code. What dies is the harness — the seven sheet/lab/probe/experiment classes,
`Design`'s six losing variants, `FrameStyle`'s six, `Centring`'s two, and `DumpFits()`, whose only
test-shaped value is superseded by the sweep above. The losing variants are `06` and `12`'s working;
the tickets are the record.

### The two lines this costs the shipping csproj

The applet's `.csproj` sits at the repo root, so it globs `**/*.cs` **including subdirectories** — a
`Tests/` folder is compiled into the applet unless told otherwise. That is the price of the flat tree
`04`/`05` chose, and it would have been paid under any sibling-project option. `08` must add both:

```xml
<Compile Remove="Tests/**" />
<InternalsVisibleTo Include="CalendarWeekTray.Tests" />
```

The second is because `05` made everything `internal` and `GlyphRenderer` follows. Making the renderer
types `public` is the alternative and it is dishonest for a `WinExe` that nothing consumes.

### One note for `08` that is not a decision

`Fit` is called per render and probes up to `box × 2` font sizes, allocating a canvas bitmap each
time. 265 renders will make the suite conspicuously slow unless the fit is cached per `(face, size)` —
it does not depend on the week.
