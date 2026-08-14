# 08 — Write the locked implementation spec

Type: task
Status: resolved
Blocked by: 06, 07, 09, 10, 11, 12, 13, 14, 15
<!-- all blockers resolved as of 15 — this is the frontier, and the map's last ticket. -->

## Question

Fold every resolved decision into a single spec an agent can build from in one session without asking a question. This is the map's destination.

## Source material

Everything closed by then, in particular:

- `01` — the 23 charting decisions (hosting, config, interaction, rollover, diagnostics)
- `03` — autostart registration strategy, and the two constraints it imposes (`Win32_StartupCommand` is not a valid enabled-check; the guard must also check `Run\AutorunsDisabled`)
- `09` — autostart deregistration and whether the app authors its own approval blob
- `04` — packaging, plus **six binding implementation constraints** it measured: mandatory `JsonSerializerContext` source generator (with `ReadCommentHandling`/`AllowTrailingCommas` on the *attribute*, never a runtime `JsonSerializerOptions` instance, which re-enters the reflection resolver); `IsAotCompatible` analyzers on; `AllowUnsafeBlocks` for `LibraryImport`; mandatory `DestroyIcon`; never reference `ICommand`; `InvariantGlobalization` stays `false`
- `10` — distribution and install shape
- `06` — the decided icon form
- `12` — that the form is **size-independent**, and that its bar/air constants are load-bearing
- `07` — rendering pipeline, resource lifetime, re-render triggers
- `14` — the surviving config schema (`07` orphaned three of `01`'s four keys)
- `15` — whether v1 ships tests, and what they assert

## Must contain

1. **Project shape** — target framework, project properties, manifest, dependency policy (BCL only **in the shipped artifact** — `15` re-scoped this; `Tests/` may take NuGet packages). Include a **`<Version>` property** (`10`): the csproj has none, so every build reports `1.0.0` and `10`'s manual update procedure has nothing to verify against. Quote the publish command and the artifact size **`10` measured — 194.3 KB**, not `05`'s 168.8 KB. Two lines `15` makes mandatory, both on the *applet* csproj: `<Compile Remove="Tests/**" />` — without it the root-level project globs the test sources **into the applet** — and `<InternalsVisibleTo Include="CalendarWeekTray.Tests" />`.
1b. **Delete the prototype harness** (`15`). All nine `Prototype*.cs` files go, and `Main` is reduced to `ApplicationConfiguration.Initialize()` + `Application.Run(new TrayApplicationContext())` with **no argv handling**. This is not tidying: the current `Main` switch is unguarded in Release, so today's shipping exe would expose `experiment13`, which writes `HKCU\Software\Microsoft\Accessibility\TextScaleFactor`. The measurement primitives (`MeasureInk`, `InkBoundsOf`, `OpticalCentreX`, `Fit`, the centring loop) are **not** part of this deletion — they are production code and move into `GlyphRenderer` per `07`.
2. **Config** — the schema `14` settles (**not** `01`'s four keys — `06` and `07` orphaned three of them), resolution order (`%APPDATA%` then `~/.config`, first-found-wins), defaults, and the two distinct malformed-config behaviours (startup: fall back + report; reload: keep running config + report).
3. **Week computation** — `ISOWeek.GetWeekOfYear`, zero-padding, and the tooltip's date-range format per language.
4. **Rendering** — the pipeline and handle-ownership discipline from `07`, and the glyph's form from `06`. **`15` amends `07`'s signature** to `Bitmap Render(GlyphSpec spec, out GlyphMetrics metrics)` (fitted type size, digit ink rect, body rect, page rect, converged flag), with the one-arg overload delegating to it — because `Render` returns a `box × box` bitmap, so digit overflow is silently cropped and the `"00"` bug is **unobservable in the output**. Record why a sibling `Measure()` is forbidden: a separate measure path predicts the draw rather than measuring the ink, which is the error `06` fixed. **`15` also requires the desired-tuple computation to be a pure function** — `(week, sizePx, highContrast, lightTaskbar, config) → (week, sizePx, ink, tooltip)` — separable from the `NotifyIcon` swap, so `13`'s ink rule can be asserted across all six theme states. State plainly that there is **one form at every size** (`12`) — no threshold, no reduced variant, nothing branching on `sizePx` — because that is a decision that looks like an omission. Record the binding bar (0.17 of the box) and the 1 px of inner air as **measured constants that must not be reclaimed**: `12` opened both up and put them back, and at 0 px air the digits fuse into the side outline.
5. **Interaction** — left-click inert, right-click menu with "Reload config" and "Quit", localised per `language`.
6. **Lifecycle** — single-instance mutex, autostart registration per `03`, clean shutdown.
7. **Diagnostics** — balloon tip plus tooltip error surfacing; no log file.
7b. **Tests** — the six properties `15` settles, in `Tests/` under xunit v3, swept over 53 weeks × {16,20,24,28,32}. Include the **"verified by hand, not by test"** list (autostart guard, icon swap, GDI handle discipline, shell integration) — in the spec, deliberately *not* in the map's Out of scope, because those are in v1's scope and merely covered manually. `dotnet test` is a gate in `10`'s release procedure — **quit, test, publish, zip, tag** — and there is no CI.
8. **Localisation** — `de`/`en` string tables for menu and tooltip. **The glyph carries no text at all** (`06`), so there is no prefix for `label` to override unless `14` says otherwise.
9. **README** — config path, defaults, and how to disable autostart. `10` already wrote the **Requirements / Install / Update / Uninstall** sections; the spec's job is to keep them true, not to re-derive them. Two known drift points: uninstall step 4 defers the config path to the *Configuration* section, which `14` may still move, and the Requirements section rounds the artifact to "roughly 200 KB".

## Explicitly out

Do not let the spec quietly re-open settled scope. The map's **Out of scope** section is binding: no service hosting, no non-ISO numbering, no extra languages, no `FileSystemWatcher`, no log file, no configurable font.

## Done when

The spec is written and the user agrees an agent could build v1 from it unaided. At that point the map's destination is reached and the map closes.

## Notes

Watch for decisions that were never actually made. Anything discovered missing while writing becomes a **new ticket**, not an assumption buried in the spec — a spec that quietly invents an answer is worse than one with an acknowledged gap.

Record the spec's location in this file under `## Answer`.

## Answer

**The spec is [`.scratch/calendarweek-tray-v1/spec.md`](../spec.md)**, fourteen sections, every
decision traced to the ticket that made it. One gap was found while writing and is ticketed rather
than assumed: **[`16` — the localisation strings](16-localisation-strings.md)**.

### The one thing that was never decided

Everything in v1 traces to a ticket except the **strings**. `01`/Q16 fixed exactly one — the German
same-month tooltip, `Kalenderwoche 32 · 3.–9. August 2026` — as an illustration. The English tooltip,
the German menu items, the German diagnostic suffix, the cross-month and cross-year range forms, and
which culture supplies month names were all never settled. `01`/Q7 states the menu items in English,
but that is the language the ticket was written in, not a decision.

It earns a ticket rather than an author's call on three grounds: `15` **asserts both strings exactly**
in the suite, so a placeholder becomes a test fixture; since `06` the tooltip is the **only place the
applet uses language at all**, so it is the product's entire voice; and `14` deleted `label`, so
there is **no escape hatch** — whatever the strings say is permanent for every user. §10 of the spec
carries a complete proposal under a warning banner so the spec is buildable today, and `16` is where
the user's decision lands.

### Decisions made while writing, that no ticket had taken

Small, mechanical, and recorded here so they do not read as invention:

- **`Fit` may be cached, keyed by `(face, box)`.** `15` flagged the 265-render suite would be slow and
  called it "not a decision"; `07` had forbidden caching. They do not conflict: `07` rejected a cached
  `Font` because it survives a DPI change **wrongly**, and a cache keyed on inputs cannot — a DPI
  change changes `box` and therefore the key. Spec §5.7.
- **`Compute` takes `now` and `configError`**, not `15`'s literal `(week, sizePx, highContrast,
  lightTaskbar, config)`. Both additions pull pure string work — the date range and the diagnostic
  suffix — inside the pure boundary, and both are asserted. Spec §6.1.
- **Menu strings are re-applied by the reload path, not by `Reconcile()`.** A `language` change is not
  visible in `07`'s `(week, sizePx, ink, tooltip)` tuple, so reconciling alone would leave a
  half-translated menu. Spec §7.
- **`dotnet test` is run against `Tests\CalendarWeekTray.Tests.csproj` explicitly.** `15` said
  "run by `dotnet test`"; with no `.sln`, a bare `dotnet test` at the repo root resolves the *applet*
  project and fails. Spec §11.1.
- **No font-availability check**, stated as a non-behaviour rather than left silent. Spec §5.2.
- **Mutex name `Local\CalendarWeekTray`**, continuing `10`'s one-string convention (folder, exe and
  `Run` value are already the same word). Spec §8.1.

### The `src/` + `test/` tree, decided by the user

**Supersedes `05`'s flat root-level project** (a note is on that ticket so nobody builds from it).
`05` chose the root so bare `dotnet build` / `dotnet run` needed no arguments, and `04`/`05` kept the
tree flat on minimalism grounds; the maintainer's layout preference outranks a scaffolding
convenience, and v1 is the first point the repo holds two projects at all. Spec §1.1.

The interesting part is what it does to `15`'s two mandated csproj lines, which were **the price of
the flat tree** in `15`'s own words:

- **`<Compile Remove="Tests/**" />` is refunded** and must not be added. It existed because a
  root-level project globs `**/*.cs` and would compile a sibling `Tests/` folder *into the applet*.
  With the applet in `src/CalendarWeekTray/`, its glob never reaches `test/`. `15` wrote that this
  price "would have been paid under any sibling-project option" — that holds only for a `Tests/`
  folder nested inside the applet's own directory, which this layout is not.
- **`<InternalsVisibleTo>` still applies** — it is about `internal`, not about layout.

And it creates one obligation `05` did not have: **a solution file becomes load-bearing.** With both
projects in subdirectories a bare `dotnet build` or `dotnet test` at the root finds nothing, and `15`
chose `dotnet test` over a hand-rolled runner *precisely* because it is "the command a future agent
will actually think to run". `CalendarWeekTray.slnx` restores it. `dotnet run` still needs
`--project src/CalendarWeekTray`; that is the one ergonomic loss and it is accepted. Nesting a
directory deep (`src/CalendarWeekTray/` over `src/CalendarWeekTray.csproj`) is the .NET convention
and is the only detail here not dictated by the preference itself.

The user's accompanying point is recorded because it disposes of the one objection this spec had
raised: **the `.editorconfig` field renames are no longer a separate cost.** `05`'s sources are being
relocated and largely re-authored regardless, so `_notifyIcon` → `this.notifyIcon` rides along with a
move that was happening anyway.

### Three corrections to inherited facts

- **`15` says "all nine prototype files".** There are **eight** `Prototype*.cs`; nine is the count of
  **argv commands** (`sheet`, `debug`, `lab`, `sheet12`, `lab13`, `probe13`, `sheet13`, `verify13`,
  `experiment13`). The deletion list in spec §1.3 is by filename, so it cannot be miscounted.
- **`README.md`'s opening line still says the applet displays `KW32`**, which `06` made false. Added
  to the spec's README drift list (§13) alongside the two `10` already flagged.
- **`15`'s "`<Compile Remove>` would have been paid under any sibling-project option" is false** for a
  `src/`+`test/` layout — see above. It was true for the option `15` had in front of it.

### The `.editorconfig` the user added

Now binding, and it is spec §1.4 — the rules that change what an agent would otherwise type, not a
restatement of the file. Three of them have teeth beyond formatting:

- **Fields must not start with `_` and instance members are `this.`-qualified at `error` severity**,
  so the existing `TrayApplicationContext.cs` (`_notifyIcon`, `_iconHandle`) **violates the style and
  must be renamed** as part of the v1 build.
- **`csproj` files are tab-indented**, and `CalendarWeekTray.csproj` is currently space-indented.
- **`csharp_style_prefer_top_level_statements = true` is deliberately not followed** in `Program.cs`:
  a WinForms entry point must carry `[STAThread]`, which top-level statements cannot express. It is a
  suggestion, not an error.

`<EnforceCodeStyleInBuild>` is **not** enabled, and the spec says why: the AOT analyzers earn a place
in the build because they catch a measured failure (`04`), and a missing `this.` does not belong in
the same lane as a real defect in a release procedure whose gate is `dotnet test`.

### Acceptance

The ticket's *Done when* is the user agreeing an agent could build v1 unaided. The spec is written and
complete; that read is outstanding, and `16` is the one section it should not be judged on yet.
