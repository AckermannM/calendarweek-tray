# calendarweek-tray v1 — implementation spec

Status: **locked**. The last open gap, §10's strings, was closed by
[`16`](issues/16-localisation-strings.md).

This is the destination of the wayfinder map at [`map.md`](map.md). Everything here was decided in a
ticket; the ticket reference in brackets is where the reasoning lives. **Read the ticket before
changing a decision** — several of the constants below were opened up deliberately, measured, and put
back.

An agent should be able to build v1 from this file alone. Where a rule looks arbitrary it is
usually measured; where it looks like an omission it is usually a decision, and says so.

---

## 0. What this is

A Windows 11 notification-area applet that draws the current ISO calendar week as a small calendar
page in the tray. Windowless per-user background process, .NET 10, WinForms `NotifyIcon` + GDI+.
No window, no console, no log file, no network, no installer.

Terminology is fixed by [`CONTEXT.md`](../../CONTEXT.md) — **glyph**, **ink**, **reconcile**,
**tooltip**, **autostart registration**, **measured property**. Use those words.

---

## 1. Project shape

### 1.1 Tree

**`src/` and `test/`, decided by the user and superseding `05`'s flat tree.**

```
.editorconfig                      code style — binding, see §1.4
.gitignore                         dotnet new gitignore, unmodified [05]
CalendarWeekTray.slnx              so bare `dotnet build` / `dotnet test` still work — see below
README.md                          the user-facing documentation — §13
src/
  CalendarWeekTray/
    CalendarWeekTray.csproj        the applet
    Program.cs                     [STAThread] Main — mutex, then Application.Run
    TrayApplicationContext.cs      the only stateful type: NotifyIcon, timer, Reconcile()
    TrayState.cs                   the pure desired-state function + DesiredState record
    GlyphRenderer.cs               pure static renderer — every rule from 06/12/13
    GlyphIcon.cs                   owns (Icon, HICON) as one unit
    AppConfig.cs                   record, enums, JsonSerializerContext, resolution + load
    Strings.cs                     de/en string tables and the date-range formatter
    Autostart.cs                   the one-shot guarded Run-value write
    NativeMethods.cs               DestroyIcon LibraryImport
test/
  CalendarWeekTray.Tests/
    CalendarWeekTray.Tests.csproj
    GlyphTests.cs
    ConfigTests.cs
    StateTests.cs
```

File-per-type is a convention here, not a rule; the split above is what the sections below assume.

**This overturns `05`, deliberately.** `05` put one project at the repo root with no `src/` and no
solution, so that bare `dotnet build` / `dotnet run` worked with no arguments, and `04`/`05` kept the
tree flat on minimalism grounds. That is now reversed by the maintainer's own layout preference,
which outranks a scaffolding convenience: v1 is the first point at which the repo holds two projects,
and `15` already conceded the flat tree was costing something (below).

Three consequences, none of which change a line of applet code:

- **`<Compile Remove="Tests/**" />` is no longer needed** and must **not** be added. `15` required it
  because a root-level project globs `**/*.cs` and would compile a sibling `Tests/` folder *into the
  applet*. With the applet in `src/CalendarWeekTray/`, its glob never reaches `test/`. `15` wrote that
  this price "would have been paid under any sibling-project option" — that is true only for a
  `Tests/` folder nested inside the applet's own directory, which this layout is not. The other line
  `15` mandated, `<InternalsVisibleTo>`, is about `internal` rather than layout and **still applies**.
- **A solution file is now load-bearing**, where `05` needed none. With both projects in
  subdirectories, a bare `dotnet build` or `dotnet test` at the repo root finds no project at all —
  and `15` chose `dotnet test` precisely because it is "the command a future agent will actually think
  to run". `CalendarWeekTray.slnx` (the XML solution format, supported by the .NET 10 SDK) restores
  both. It is ~5 lines against `.sln`'s ~30; fall back to `.sln` if any tooling balks. `dotnet run`
  still needs `--project src/CalendarWeekTray`, which is the one ergonomic loss and is accepted.
- **The `.editorconfig` rename is folded into the move, not carried as a separate risk.** `05`'s
  scaffold sources are being relocated and largely re-authored anyway (§1.3 deletes eight files
  beside them), so renaming `_notifyIcon`/`_iconHandle` to `this.notifyIcon`/`this.iconHandle` (§1.4)
  costs nothing next to the restructure it rides along with.

Nesting one directory deep — `src/CalendarWeekTray/` rather than `src/CalendarWeekTray.csproj` — is
the near-universal .NET convention and is what tooling and future agents expect. It is the one detail
here not dictated by the preference itself; flip it if the extra level grates.

### 1.2 `src/CalendarWeekTray/CalendarWeekTray.csproj`

Current properties stay, moved with the file. Two additions, both mandatory.

```xml
<Version>1.0.0</Version>                        <!-- 10: without it every build reports 1.0.0
                                                     and "replace the exe in place" has nothing
                                                     to verify against -->
<InternalsVisibleTo Include="CalendarWeekTray.Tests" />
                                                <!-- 15: 05 made everything internal and
                                                     GlyphRenderer follows. Making the renderer
                                                     public for a WinExe nothing consumes is
                                                     dishonest -->
```

`15`'s third mandated line, `<Compile Remove="Tests/**" />`, is **deliberately absent** — see §1.1.

Already present and **not** to be touched, each for a measured reason:

| property | why [ticket] |
| --- | --- |
| `<OutputType>WinExe</OutputType>` | no console window [`01`/Q2]. Consequence: the applet can never report anything from a command line [`09`] |
| `<TargetFramework>net10.0-windows</TargetFramework>` | |
| `<UseWindowsForms>true</UseWindowsForms>` | |
| `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` | |
| `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` | DPI awareness without an `app.manifest` [`05`]. `07` did not revise it |
| `<IsAotCompatible>true</IsAotCompatible>` | we do **not** ship AOT — `04` proved the SDK refuses it — but the analyzers are free, measured silent on this code [`05`], and fail the build the moment reflection-based JSON creeps back |
| `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` | `LibraryImport` fails `SYSLIB1062` without it [`04`] |
| `<InvariantGlobalization>false</InvariantGlobalization>` | **Never** flip this as a size optimisation. `16` measured two independent failures, not `04`'s "silent degradation": `GetCultureInfo("de-DE")` *throws*, and `language: "auto"` can never resolve to `de` again (§10.1) |

Two standing prohibitions [`04`]:

- **Never reference `ICommand`.** It drags WPF's `PresentationFramework` into a WinForms publish.
- **Never use reflection-based `System.Text.Json`.** See §3.2.

### 1.3 Delete the prototype harness [`15`]

This is not tidying. `Main` currently dispatches on `args` through nine commands, **unguarded in
Release**, one of which (`experiment13`) writes `HKCU\Software\Microsoft\Accessibility\TextScaleFactor`.
Today's shipping exe would hand a stranger a switch that mutates their accessibility settings.

Delete, in full:

```
Prototype06.cs   Prototype06Lab.cs   Prototype06Sheet.cs
Prototype12.cs
Prototype13.cs   Prototype13Experiment.cs   Prototype13Sheet.cs   Prototype13Verify.cs
```

Eight files, nine argv commands. (`15` says "nine files"; that is a miscount of the nine *commands* —
`sheet`, `debug`, `lab`, `sheet12`, `lab13`, `probe13`, `sheet13`, `verify13`, `experiment13`.)

`Main` reduces to a mutex check plus `ApplicationConfiguration.Initialize()` +
`Application.Run(new TrayApplicationContext())`, with **no argv handling at all** (§8.1).

**Not** part of this deletion: `MeasureInk`, `InkBoundsOf`, `OpticalCentreX`, `Fit`, `ReferenceFor`
and the centring loop. They are production code and move into `GlyphRenderer` (§5) — they are called
on **every render**, which is most of the reason the test suite is cheap [`15`].

Capture the harness on a throwaway `prototype/06-13` branch before deleting if the working history is
wanted; the tickets are the record, and `06`/`12`/`13`'s losing variants are their working.

The four survivors — `CalendarWeekTray.csproj`, `Program.cs`, `TrayApplicationContext.cs`,
`NativeMethods.cs` — move to `src/CalendarWeekTray/` (§1.1) and pick up the `.editorconfig` renames
(§1.4) on the way. Do the deletion and the move in one commit: a tree with eight prototype files
under `src/` never needs to exist.

### 1.4 Code style — `.editorconfig` is binding

The repo root carries an `.editorconfig` that encodes the maintainer's house style. **All code
written for v1 conforms to it.** The rules below are the ones that change what you would otherwise
type; the file is the authority for everything else.

| rule | consequence |
| --- | --- |
| `dotnet_style_qualification_for_field/property/method/event = true:error` | instance members are always `this.`-qualified: `this.notifyIcon`, `this.Reconcile()`. Static members are not (`GlyphRenderer.Render(…)`) |
| naming: fields must **not** start with `_`, camelCase (`IDE1006` = warning) | `_notifyIcon` → `this.notifyIcon`, `_iconHandle` → `this.iconHandle`. **The current `TrayApplicationContext.cs` violates this and must be renamed** |
| `csharp_style_var_*` = `false` (all three) | never `var` — explicit types everywhere, including `foreach` |
| `csharp_style_namespace_declarations = file_scoped:error` | `namespace CalendarWeekTray;` |
| `csharp_new_line_before_open_brace = all` | Allman braces, including for single-statement blocks (`csharp_prefer_braces = true`) |
| `dotnet_style_parentheses_in_arithmetic_binary_operators = always_for_clarity` | the glyph maths parenthesises sub-expressions explicitly: `area.X + ((area.Width - ink.Width) / 2f)` |
| `dotnet_style_readonly_field = true` | `private readonly NotifyIcon notifyIcon;` |
| `dotnet_style_require_accessibility_modifiers = for_non_interface_members` | explicit `private` / `internal` on every member |
| `csharp_using_directive_placement = outside_namespace`, `dotnet_sort_system_directives_first = false`, `dotnet_separate_import_directive_groups = false` | usings at the top of the file, one block, not System-first |
| `dotnet_style_namespace_match_folder = true` | `Tests/*.cs` are `namespace CalendarWeekTray.Tests;` |
| `csharp_style_prefer_primary_constructors = true`, `csharp_prefer_system_threading_lock = true` | suggestions — follow where natural |
| `[*.cs] end_of_line = crlf`, `insert_final_newline = false` | |
| `[*.csproj] indent_style = tab`, width 2 | **`CalendarWeekTray.csproj` is currently space-indented and must be re-tabbed**, as must the new test csproj |

Two consequences worth stating so they do not read as oversights:

- **`csharp_style_prefer_top_level_statements = true` is deliberately not followed in `Program.cs`.**
  A WinForms entry point must carry `[STAThread]`, which top-level statements cannot express. The
  rule is a suggestion, not an error; the explicit `Main` stands.
- **`<EnforceCodeStyleInBuild>` is not enabled.** The `.editorconfig` governs the editor and the
  author; the build stays about the applet. Turning it on would make style an error class in a
  release procedure (§14) whose gate is `dotnet test`, and would put the error-severity rules above
  in the same lane as a real defect. The AOT analyzers earn their place in the build because they
  catch a measured failure [`04`]; a missing `this.` does not.

`dotnet build` must remain at **0 warnings, 0 errors** [`05`].

---

## 2. Non-behaviours

Things v1 deliberately does not do. Each is a decision with a ticket, recorded here because every one
of them is indistinguishable from an oversight when read in the code.

| the applet never… | [ticket] |
| --- | --- |
| writes `config.json`, or any file, ever | `01`/Q18 |
| writes `HKCU\…\Explorer\StartupApproved` — not to enable, not to repair, not on uninstall | `09` |
| deletes or rewrites its own `Run` value; there is no `--unregister`, no menu item, no deregistration code | `09` |
| reports anything about its autostart state — not a stale path, not an Autoruns disable, not a failed write | `09` |
| reads or writes `HKCU\Control Panel\NotifyIconSettings` | `11` |
| reads `TextScaleFactor` or references `Windows.UI.ViewManagement` | `13` |
| checks where its exe lives, or complains about it | `10` |
| makes a network call, checks for updates, or reports a version | `10` |
| writes a log file | `01`/Q23 |
| watches `config.json` for changes (`FileSystemWatcher`) | `01`/Q14 |
| branches on `sizePx` — there is one glyph form and one weight at every size | `12`, `13` |
| uses `Calendar.GetWeekOfYear`, or any non-ISO week scheme | `01`/Q3 |
| uses `Win32_StartupCommand` as an "am I enabled?" oracle — it ignores `StartupApproved` | `03` |
| checks whether `Segoe UI Variable Text Semibold` is installed | §5.2 |

---

## 3. Configuration

### 3.1 Schema [`14`]

Two keys, both optional, both defaulting to `"auto"`. The file itself is optional and the applet runs
correctly with none present.

```json
{
  "language": "auto",
  "theme": "auto"
}
```

| key | values | default | governs |
| --- | --- | --- | --- |
| `language` | `auto` \| `de` \| `en` | `auto` | menu items and tooltip **only**. `auto` = `de` when `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` is `"de"`, else `en` |
| `theme` | `auto` \| `light` \| `dark` | `auto` | glyph ink. **Overridden outright by high contrast** (§5.5) |

`01`'s `label` and `layout` are **deleted** — `06` removed all text from the glyph and `12` decided one
form at every size, so neither key had anything left to name. **Nothing in the file reaches the
renderer**, whose signature stays `(week, sizePx, ink)`.

Values are matched **case-insensitively** (the `JsonStringEnumConverter` default), so `"Auto"` and
`"DE"` are accepted.

**Forward-compatibility cost, stated deliberately:** a config file written for a future version with a
third key trips `Disallow` (§3.2) on v1 and degrades to defaults-plus-warning. In an applet with no
updater and no migration story, being told is better than being ignored [`14`].

### 3.2 Deserialisation [`04`, `14`]

The `JsonSerializerContext` source generator is **mandatory** — reflection-based `System.Text.Json`
was the sole failure of `04`'s 22 trimming checks, and `IsAotCompatible` will fail the build if it
returns.

```csharp
internal enum Language { Auto, De, En }

internal enum Theme { Auto, Light, Dark }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AppConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter<Language>))]
    public Language Language { get; init; } = Language.Auto;

    [JsonConverter(typeof(JsonStringEnumConverter<Theme>))]
    public Theme Theme { get; init; } = Theme.Auto;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
                             ReadCommentHandling = JsonCommentHandling.Skip,
                             AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
```

Two traps, both measured:

- **`ReadCommentHandling` and `AllowTrailingCommas` go on the attribute, never on a runtime
  `JsonSerializerOptions` instance.** Passing an options instance to a source-generated call
  re-enters the reflection resolver and re-breaks it [`04`].
- **Always the `JsonTypeInfo` overload**, never `Deserialize<T>(string)`:
  `JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig)`.

Use the **generic** `JsonStringEnumConverter<T>`; the non-generic form is not source-generator
compatible [`14`].

### 3.3 Resolution [`01`/Q4, Q18]

In order:

1. `%APPDATA%\calendarweek-tray\config.json`
2. `%USERPROFILE%\.config\calendarweek-tray\config.json`

**First found wins, no merging.** "Found" means the file exists — if the first path exists but fails
to parse, that is the error reported (§9); the second path is **not** tried. A merged schema means a
key you cannot find in the file you are editing is silently coming from the other file.

Note the spelling: `calendar`, not `calender`.

### 3.4 Malformed config — two distinct behaviours [`01`/Q19]

| phase | behaviour |
| --- | --- |
| **startup** | fall back to defaults (`auto`, `auto`), report (§9), **never fail to start over a typo** |
| **reload** | **keep the running config**, report. Reverting a working icon to defaults because of a fat-fingered edit is worse than ignoring the edit |

"Malformed" covers all of: invalid JSON, an unknown key (`"them"`), an unknown value (`"drak"`), and
an unreadable file. All four route to the same channel; none crashes.

Both paths produce the diagnostic string in §9, which carries the JSON line number where one is
available (`JsonException.LineNumber` is **0-based** — add 1 for display).

---

## 4. Week and tooltip

### 4.1 The number [`01`/Q3, Q21 as reversed by `06`]

```csharp
int week = ISOWeek.GetWeekOfYear(DateTime.Now);
```

**Never `Calendar.GetWeekOfYear`** — it is a different and wrong answer at year boundaries.

**The glyph is unpadded**: week 1 renders `1`, not `01`. This reverses `01`/Q21, which leaned padded
on the reasoning that a fixed character count stops the glyph reflowing — `06` found the type size is
fitted to a constant reference label instead (§5.3), which delivers that stability without the extra
digit. The tooltip is likewise unpadded.

### 4.2 The date range

The Monday–Sunday span of the displayed week:

```csharp
int isoYear = ISOWeek.GetYear(today);              // NOT today.Year
DateTime monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday);
DateTime sunday = monday.AddDays(6);
```

`ISOWeek.GetYear` rather than `DateTime.Year` is load-bearing: on 2026-12-31 the ISO week-year is
2027, and pairing week 53/1 with the wrong year silently yields a range a week or a year out.

Formatting is language-dependent and is specified in §10.

### 4.3 The tooltip [`01`/Q16, `07`]

`NotifyIcon.Text`, e.g. `Kalenderwoche 32 · 3.–9. August 2026`. **Truncate at 127 characters** —
`07` measured the real .NET 10 limit as 127, not the 63 `01`/Q16 recorded. The worst realistic
string measures 71, so the truncation is a safety net, not a budget.

Since `06` removed all text from the glyph, **the tooltip is the only place the applet uses language
at all**, and it doubles as the persistent diagnostic channel (§9).

---

## 5. Rendering

### 5.1 The three types [`07`]

| type | kind | holds |
| --- | --- | --- |
| `GlyphRenderer` | pure static | every rendering rule from `06`/`12`/`13`. No shell, no config, no state |
| `GlyphIcon : IDisposable` | owns `(Icon, HICON)` as one unit | the handle-ownership discipline, structurally |
| `TrayApplicationContext` | the only stateful thing | `NotifyIcon`, timer, subscriptions, last-applied state, `Reconcile()` |

```csharp
internal readonly record struct GlyphSpec(int Week, int SizePx, Color Ink);

internal readonly record struct GlyphMetrics(
    int TypeSizePx,        // the fitted integer type size
    Rectangle DigitInk,    // ink bounds of the digits, measured on their own layer
    Rectangle Body,        // the rect the digits were fitted into
    Rectangle Page,        // the page outline's rect
    bool Converged);       // did the centring loop settle inside its 4-iteration cap
```

**Signature** [`07` as amended by `15`]:

```csharp
internal static Bitmap Render(GlyphSpec spec, out GlyphMetrics metrics);
internal static Bitmap Render(GlyphSpec spec) => Render(spec, out _);   // delegates
```

**Why the `out` parameter, and why a sibling `Measure()` is forbidden.** `Render` returns a
`box × box` bitmap, so digit overflow is silently **cropped**, and once the frame is drawn the page
outline itself inks column 0 and `box-1` — so `InkBoundsOf` on the returned bitmap cannot see the
digits at all. The `"00"` bug this whole suite exists to catch (§5.3) was therefore *unobservable in
the output*. The metrics already exist at render time and were simply not returned.

A pure `GlyphMetrics Measure(GlyphSpec)` reads as the tidier design and **is a trap**: a separate
measure path *predicts* what `Render` will do, and predicting the draw instead of measuring where the
ink landed is the deepest error `06` found and fixed. A test must observe the real render or it
re-introduces the bug class it was written to catch. This is a return value, not a seam.

### 5.2 The form [`06`, `12`, `13`]

A calendar page: **square corners**, 1 px outline, filled binding bar across the top with **two slots
notched through it** as binding rings, week number in the body. `KW` appears nowhere — the frame
carries the "calendar" meaning, which is what makes a bare number mean something.

| | value | why |
| --- | --- | --- |
| face | `"Segoe UI Variable Text Semibold"` — spelled exactly | 31 chars, under GDI's `LF_FACESIZE`. The sibling `"Segoe UI Variable Small Semibold"` is 32 and truncates; the **bare family `"Segoe UI Variable"` silently becomes Microsoft Sans Serif** [`02`, `06`] |
| weight | Semibold, at every size and in every theme | `13` rendered Regular and Display Bold in the real tray: Regular reads thinner, Display Bold is not better |
| corners | square (radius 0) | a 1 px stroke swept round an arc spreads its coverage diagonally across two pixels and halves in density — the corners read as *missing*. Square corners have no arcs [`06`] |
| stroke | 1.0 px, single strike | |
| antialiasing | `TextRenderingHint.AntiAlias` + `SmoothingMode.AntiAlias` + `PixelOffsetMode.HighQuality` | see the trap below |
| bitmap | `PixelFormat.Format32bppArgb`, cleared to `Color.Transparent` | genuine alpha; `GetHicon` preserves partial alpha exactly, verified by `GetDIBits` against a hand-built `CreateIconIndirect` DIB [`06`] |

**`TextRenderingHint` must be assigned, and must never be `SystemDefault`.** This is the single
easiest way to reintroduce `05`'s bug [`06`, `07`]:

- left **unset**, GDI+ renders with **zero partial-alpha pixels** — 24 inked, 0 partial. That, plus a
  ~10 px type size, is the whole of "small, thin, not antialiased".
- set to **`SystemDefault`**, GDI+ renders **subpixel ClearType**: 38 of 46 inked pixels carrying
  *colour*, at full alpha, onto an icon about to be composited over a taskbar whose colour the applet
  does not know.
- only `AntiAlias` and `AntiAliasGridFit` are safe. `06` chose `AntiAlias` by eye.

**No font-availability check.** If the face is absent GDI+ falls back silently and the glyph is
wrong-looking but functional. v1 targets Windows 11, where it ships. Adding a check would need a
message surface the applet does not have, and would be *governing* in `09`'s sense. (`15` records the
adjacent open question — whether GitHub's Windows runners carry the font — as a fresh ticket if CI is
ever wanted.)

### 5.3 Constants — measured, not tuned

```
bar height   = max(2, round(box × 0.17))     // the binding bar
inner air    = 1 px                          // between the page outline and the digits
slot width   = max(1, round(box × 0.08))     // each ring slot
slot centres = 0.32 and 0.68 of the box width
```

**`bar = 0.17` and `air = 1 px` are load-bearing constants, not tuning** [`12`]. Both were opened up
deliberately and put back:

| candidate | bar / air | digits at 16 px |
| --- | --- | --- |
| as decided | 3 / 1 | 8 px |
| thinner bar | 2 / 1 | **8 px — buys nothing** |
| no inner air | 3 / 0 | 9 px, and the `4`s **fuse into the side outline** (measured gap 0 both sides on week 44) |

The thinner bar is inert because digit size in this glyph is constrained by **width, not height** —
`06`'s finding cutting a second way. Anyone later "reclaiming" that 1 px of padding is reintroducing a
measured defect; week 11's stems additionally shift onto a subpixel phase that renders them grey.

**Pixel-alignment rules — every filled edge lands on an integer pixel boundary** [`06`, `07`]:

- the **binding bar fills from the icon edge in whole pixels** (`y = 0`), not from `page.Y`. `page.Y`
  is a *half* pixel, because a centred 1 px stroke sits at 0.5, and filling from there leaves the
  bar's bottom edge straddling a pixel row — a grey seam under an otherwise solid bar. The bar
  overlaps the outline's top stroke; they share a colour, so the overlap is invisible.
- **ring slots snap to whole pixels.** At 16 px a slot is one pixel wide; at a fractional x it
  renders as grey mush. `12` verified the snapping survives at 16 px: the alpha across the bar rows
  is pure opaque and pure clear, **not one partial pixel**.
- **integer type sizes only.** Fractional sizes at these dimensions land stems between pixels.

**Knockouts subtract alpha; they never paint a background colour.** The ring slots multiply the
target's alpha by the inverse of a mask (lock both bitmaps, `a * (255 - maskAlpha) / 255`). Painting
"the background colour" is not available to this applet — the background is a taskbar whose colour it
does not know, and that is almost certainly what made the ugly calendar-frame icons in the wild look
the way they do.

**The fit reference is the widest digit doubled — `"44"` — and never `"00"`.** Segoe UI Variable's
figures are **proportional, not tabular**: measured at 23 px, `4` inks 13 px against 11 px for every
other digit, so the widest label of 1..53 is `"44"` at 26 px against `"00"`'s 24. Fitting to `"00"`
silently overflowed the box for weeks 4, 14, 24, 34 and **40–49** — a bug that would first have
appeared in production, in October. Compute the reference by probing `'0'`..`'9'` at a large size and
doubling the widest; do not hard-code `"44"`, which is a property of the installed face.

Everything is fitted to the reference, **never to the week being displayed**, so the glyph does not
resize as the year goes on. Vertical placement likewise comes from the reference's ink — specifically
the converged, cached `y` §5.4 derives against it — so digits do not shift baseline between weeks.

### 5.4 The centring rule [`06`]

Centring the ink **bounding box** is wrong for this typeface: Segoe's `1` is a bare stem with a thin
diagonal flag off its top-left and no foot serif, so the flag widens the box while carrying almost
none of the visual weight, pushing the stems — what the eye tracks — to the right. Weeks 1 and 11 are
the visible cases.

Centring the **alpha-weighted centre of mass** fixes that and over-corrects: shifting each week by a
different subpixel amount changes the phase its stems land on, and a stem straddling two columns
renders wider and softer, so some weeks read as a *larger* number than others.

**The rule is the blend of the two**, arrived at by iteration, not prediction:

```
draw the digits onto their own box×box layer
  actual   = InkBoundsOf(layer)
  centre   = ((actual.X + actual.Width/2) + OpticalCentreX(layer)) / 2
  drift    = targetCentre - centre
  if |drift| < 0.15 px: done
  x += drift; repeat, at most 4 times, taking the last result if it has not converged
composite the layer onto the glyph
```

Two things this shape buys, both of which were bugs:

- **Predicting a draw offset from a measurement taken at a different origin does not work** — the
  rasteriser's antialiasing spills differently depending on the subpixel phase the glyph lands on, so
  the ink that appears is not the ink that was measured.
- **`Math.Round` is banker's rounding**, so `Math.Round(0.5) == 0` and the leftover pixel landed on
  the same side every week. Round deliberately where a whole pixel is wanted, and let the loop own
  the horizontal placement.

The **separate digit layer** is not an implementation detail: it is what keeps "where did the ink go"
answerable underneath a frame, and it is what `GlyphMetrics.DigitInk` reports (§5.1).

**Vertical placement mirrors this exactly**, converging `y` instead of `x`:

```
draw the reference ink ("44") onto its own box×box layer at a starting y
  actual   = InkBoundsOf(layer)
  centre   = ((actual.Y + actual.Height/2) + OpticalCentreY(layer)) / 2
  drift    = body's own vertical centre - centre
  if |drift| < 0.15 px: done
  y += drift; repeat, at most 4 times, taking the attempt with the smallest |drift| if none converge
```

Three differences from the horizontal loop, all deliberate:

- **The target is always `body`'s own vertical centre, never `page`'s.** The binding bar's visual
  weight pulls the eye down by roughly 2.5–3.5 px depending on box size — far more than "digits sit
  low by up to 1 px" describes — and blending toward `page` to compensate for it was considered and
  rejected: it conflates two different problems (the digit's placement within its own box vs. the
  binding bar's visual weight) behind one number.
- **The loop runs once per `(face, box)`, against the reference ink only — never per week.** Running
  it per week, against each week's own digits, would mirror the horizontal loop more literally, but it
  reverses the "digits do not shift baseline between weeks" decision above: a per-week vertical
  convergence would let e.g. `"1"`'s thin ink land at a different `y` than `"44"`'s. The result is
  cached the same way `FitCache` already keys the fit result on `(face, box)`, so only the very first
  render at a given box size ever runs this loop; every week after that reuses the cached `y`.
- **On non-convergence, the loop keeps the best of its (at most 4) tried attempts, not simply the
  last.** The horizontal loop can get away with "last" because it composites whatever `layer` last
  drew; this loop hands back a bare `y` for a draw that happens later, so "last" risks handing back
  the worse of an oscillating pair. Tracking the minimum `|drift|` seen is not a new tuning knob — the
  cap and tolerance are unchanged — it only decides which already-computed candidate to report.

Re-derived from the shipped loop (design's throwaway diagnostic estimated 1.00 / 0.50 / 0.00 / 0.50 /
1.00 px and was, as its own notes warned, not to be trusted as a fixture): the actual correction moves
the digits **0.54 px higher at 16 px, 0.00 px at 20 px, 0.65 px at 24 px, 0.24 px at 28 px, and 0.66 px
at 32 px** than the old static formula (`y = round(body centre − referenceInk.Height/2) −
referenceInk.Y`) placed them — that formula had no correction step, so it landed wherever the
reference ink's raw bounding box happened to fall, not where the blend of box-centre and mass-centre
actually converges.

This converges cleanly at 16/24/28/32 px. At **20 px, "44" sits in the vertical equivalent of the
horizontal loop's known GDI+ two-phase dead zone**: the drift oscillates between ~0.175 px and
~0.225 px and neither lands inside the 0.15 px band, confirmed stable even at 40 iterations of the
identical loop — a property of the rasteriser at that exact size, not a bug in the loop. The "best of
attempts" rule above already returns the closer of the two states (0.175 px) rather than leaving it to
iteration-count parity, but that is still outside 0.15 px, so `GlyphTests.cs`'s vertical convergence
test holds every size to the real 0.15 px band except 20 px, which gets one narrow, explicitly named
allowance (0.2 px) — not a general exemption list, and not a change to the cap or tolerance the
production loop itself uses.

### 5.5 Ink [`07`, `13`]

```csharp
Color ink = highContrast
    ? SystemColors.MenuText
    : (lightTaskbar ? Color.Black : Color.White);
```

Verbatim. Each branch is measured:

- **Pure `#FFFFFF` on dark, pure `#000000` on light.** Shell tray icons are monochrome white in dark
  theme and matching that is what makes the applet look native; sampling the real taskbar colour has
  no API behind it. `13` confirmed by measurement that the shell's own taskbar ink on a dark theme is
  exactly `#FFFFFF`.
- **High contrast wins over everything**, including an explicit `theme: light` / `theme: dark`. This
  is a defect fix, not a preference: **`SystemUsesLightTheme` reads `0` under all four stock contrast
  themes**, including *High Contrast White*, whose taskbar is `#FFFAEF` — so `07`'s original rule
  painted `#FFFFFF` on `#FFFAEF` at **1.04:1, an invisible icon**, for the user least able to absorb
  one, and the applet could not detect it from that key. `SystemInformation.HighContrast` is
  required. Letting an explicit `theme` win would leave a reachable state where a config file
  produces an invisible icon.
- **`SystemColors.MenuText`, not `WindowText` or `ControlText`.** `MenuText` matches the shell's
  measured taskbar ink **4 themes out of 4** (worst case 20.17:1). `WindowText` misses on High
  Contrast White (`#3D3D3D`); `ControlText` is the accent colour under High Contrast #1 and #2 and
  would put a **cyan or yellow** calendar page in the tray.
- **The conditional cannot be collapsed.** `SystemColors` does not track dark theme at all — on a
  fully dark machine with high contrast off, `MenuText` is `#000000` against a `#202020` taskbar:
  **1.29:1**, the same bug moved.

`lightTaskbar` resolves as:

| `theme` | `lightTaskbar` |
| --- | --- |
| `light` | `true` |
| `dark` | `false` |
| `auto` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` → `SystemUsesLightTheme` (`REG_DWORD`) ≠ 0. **Absent key ⇒ light ⇒ black ink**, the documented Windows default for a machine where it was never toggled |

Read `SystemUsesLightTheme`, **not** `AppsUseLightTheme` — the taskbar's own setting is the one that
matters [`01`/Q11].

One note, not a risk as designed: **`SystemColors` is cached by .NET and refreshed on
`WM_SYSCOLORCHANGE`**, so it must never be read from a worker thread or from a startup path that runs
before `Application.Run` pumps. The design already guarantees a live message loop [`13`].

### 5.6 Size, and one form at every size

`sizePx` is `SystemInformation.SmallIconSize.Width` (`SM_CXSMICON`) for the **primary monitor's DPI,
always** [`07`]. A single `NotifyIcon` yields a single `HICON`, so per-monitor correctness is not
available at any price; a differently-scaled secondary taskbar gets a shell-scaled glyph, and that is
a documented limitation rather than a bug.

**There is one form at every size** [`12`] — no threshold, no reduced variant, nothing branching on
`sizePx`. This is a decision that looks like an omission, which is why it is stated. `12` decided it
from a contact sheet of the glyph on real taskbar strips beside the real clock. Related measured
facts, so nobody re-opens it hoping for a win:

- the number **never** matches the clock at any size — bare digits are 83% of the clock height, the
  decided form runs 66 / 73 / 72 / 75% at 16 / 20 / 24 / 32 px. Parity was never on the table, and
  16 px is the worst case only marginally.
- **text scaling is ignored, deliberately** [`13`]. The premise that the applet would be the odd one
  out is false: an exact pixel diff of the whole taskbar (2560×72 = 184,320 px) shows **0 pixels
  differing** at 200% and at 225% text scale, against a **0-pixel control** — the taskbar is immune,
  so the clock does not grow either. Responding would make the applet the only thing on the bar that
  did. `SM_CXSMICON` never moves either, and that is correct behaviour, not a gap.

### 5.7 Allocation discipline [`07`]

`Font`, `Pen` and `Brush` are **created and disposed per render**. No caching: a `Font` cached at
24 px is simply wrong after a DPI change, and a cache is a second source of truth that goes stale. At
dozens of renders per year the cost is unmeasurable.

**One exception, and it is safe for the opposite reason:** the fit result may be cached in a static
dictionary keyed by `(face, box)` — both of which are *inputs* to the render, so the cache cannot go
stale the way a cached `Font` can; a DPI change changes `box` and therefore the key. The reference
label may be cached per face on the same argument. This matters only for the test suite, which does
265 renders and would otherwise probe up to `box × 2` font sizes each time, allocating a canvas
bitmap per probe [`15`].

---

## 6. Reconcile, triggers, and resource lifetime

### 6.1 The pure desired-state function [required by `15`]

The desired state is computed by a **pure static function**, separable from the `NotifyIcon` it
drives, so `13`'s ink rule can be asserted across all six theme states without a shell:

```csharp
internal readonly record struct DesiredState(int Week, int SizePx, Color Ink, string Tooltip);

internal static DesiredState Compute(
    DateTime now,
    int sizePx,
    bool highContrast,
    bool? systemUsesLightTheme,   // null == the registry value is absent
    AppConfig config,
    string? configError);         // null == config is fine
```

Every impure read — the clock, `SystemInformation.SmallIconSize`, `SystemInformation.HighContrast`,
the `Personalize` registry value — happens in the caller and arrives as an argument. `Compute` calls
nothing that touches the machine.

This is a **requirement**, not a preference. `13` caught the only pure-logic defect this map ever
found — an invisible icon at 1.04:1, undetectable from `SystemUsesLightTheme` alone. A rule that has
already been wrong once and has six theme states to get right is worth three lines of test.

(`15` phrased the signature as `(week, sizePx, highContrast, lightTaskbar, config)`. It takes `now`
rather than `week` so that the tooltip's date range is inside the pure boundary, and `configError` so
that the diagnostic suffix is too — both are pure string work and both are asserted.)

### 6.2 `Reconcile()` [`07`]

**There is one code path, not five.** Every trigger calls the same idempotent `Reconcile()`; nothing
re-renders directly.

```
desired = Compute(…)
if desired == lastApplied: return          // the common case, ~every minute forever
render → GlyphIcon → assign to NotifyIcon → dispose the previous GlyphIcon
NotifyIcon.Text = desired.Tooltip (truncated to 127)
lastApplied = desired
```

**It compares the rendered result, not the inputs.** A config reload that changes nothing observable
produces an identical tuple and correctly does nothing — no generation counter, no dirty flags, and
double-fired events are provably harmless. It always runs on the UI thread.

The whole body is wrapped in a `catch`: on failure **keep the last good icon**, mark the tooltip, and
show one balloon **the first time only**. A timer tick must never take the process down.

This shape is load-bearing rather than tidy — `07`'s experiments found a theme change that arrives
through no message at all, and a `SynchronizationContext` that silently posts to the wrong thread.
One idempotent reconcile survives both; a set of per-event handlers would not.

### 6.3 Trigger → mechanism [`07`]

| trigger | mechanism | thread | what it catches |
| --- | --- | --- | --- |
| **every 60 s** | `System.Windows.Forms.Timer` | UI | week rollover, **theme changes that broadcast nothing**, any missed event, resume, clock drift |
| theme / contrast flip | `SystemEvents.UserPreferenceChanged` | background → `Post` | `Ink` |
| DPI / monitor change | `SystemEvents.DisplaySettingsChanged` | background → `Post` | `SizePx` |
| clock / timezone change | `SystemEvents.TimeChanged` | background → `Post` | `Week`, `Tooltip` |
| resume from sleep | `SystemEvents.PowerModeChanged`, `Resume` only | background → `Post` | all |
| config reload | menu item | UI | `Ink`, `Tooltip`, and the menu strings (§7) |
| **Explorer restart** | **`NotifyIcon`, unaided — we write nothing** | — | nothing re-renders; the existing icon is re-added |

**The timer is the authority and every event is advisory.** Losing an event costs at most 60 seconds
of staleness, never a permanently wrong glyph. Concretely measured:

- **A registry theme write that broadcasts nothing produces no event at all** — not
  `UserPreferenceChanged`, not `WM_SETTINGCHANGE`. Theme switchers and dark-mode schedulers that poke
  the registry directly are invisible to every event mechanism. **Only the poll catches them.**
- **`SystemEvents.UserPreferenceChanged` fires on a background thread** with the uninformative
  `category=General`. Marshalling is mandatory; **filtering by category is pointless**, so
  `Reconcile()` ignores the category entirely.
- **`NotifyIcon` genuinely re-adds its icon on `TaskbarCreated`** — proven live against a
  hand-registered control icon that never came back. There is a ~4 second window where the icon is
  really gone (Explorer killed 11:05:19, broadcast 11:05:24.3, re-registered by 11:05:25); nothing
  can be done about it and nothing needs to be. **`NotifyIcon._added` stays `True` throughout that
  window — it is not a health oracle.**

**Known gap, accepted:** if an Explorer restart coincides with a DPI change, `NotifyIcon` re-adds the
*previous* glyph at the old size and the poll corrects it within 60 seconds. Accepted over adding a
second top-level window to a deliberately windowless process.

### 6.4 The `SynchronizationContext` trap [`07`]

Measured, not assumed: **inside the `ApplicationContext` constructor,
`SynchronizationContext.Current` is a plain `SynchronizationContext`, which posts to the thread
pool.** It only becomes a `WindowsFormsSynchronizationContext` once `Application.Run` pumps.
Capturing it in the constructor would marshal reconciles onto a pool thread and touch `NotifyIcon`
cross-thread — a failure that is rare, mysterious, and survives every casual test.

**Therefore:**

1. start the timer at `Interval = 1`;
2. on its first tick — which cannot fire before the pump exists — capture the context, **assert it is
   a `WindowsFormsSynchronizationContext` and fail loudly if it is not**, subscribe to `SystemEvents`,
   then set `Interval = 60000`;
3. reconcile.

This keeps the applet windowless. The alternative — a hidden marshalling `Control` — adds a real
window to a process whose whole shape is not having one.

### 6.5 Handle ownership [`04`, `07`]

`Icon.FromHandle` does **not** own its `HICON`. Measured over 4,000 renders:

| | GDI objects | USER objects |
| --- | --- | --- |
| 3,000 renders with `DestroyIcon` on the replaced handle | **flat at 9** | **flat at 4** |
| 1,000 renders without it | **+3 per render** | **+1 per render** |
| after `GC.Collect()` + finalizers | **not reclaimed** | **not reclaimed** |

Each `GetHicon()` costs **3 GDI + 1 USER** object (icon, colour bitmap, mask), and **the GC never
saves you** — no `Dispose`, no finalizer and no full collect reclaims them. Against the 10,000-object
default limit that is **~3,300 renders to exhaustion**: harmless at one render a week, but a
reconcile bug re-rendering every minute takes the process down in **about two and a half days**.

**Discipline:**

```csharp
internal sealed class GlyphIcon : IDisposable
{
    // holds (Icon, nint) together, created from a Bitmap via GetHicon + Icon.FromHandle.
    // Dispose(): dispose the Icon, then NativeMethods.DestroyIcon(handle).
}
```

**Assign the new icon to `NotifyIcon` first, then dispose the previous `GlyphIcon`.** Wrapping the
pair in one type is the point — a raw `nint` field shadowing a managed `Icon` is precisely the
pairing that rots.

**Recorded gap, honestly:** assign-then-destroy is safe *by contract* — `Shell_NotifyIcon` is
synchronous and the shell copies the icon before returning — **not by measurement**. The probe proved
the registration survives, not the pixels.

---

## 7. Interaction [`01`/Q7]

- **Left-click does nothing.** Instant-quit-on-left-click was rejected as a footgun.
- **Right-click opens a `ContextMenuStrip` with exactly two items**: *Reload configuration* and
  *Quit*, in that order, localised per `language` (§10.2).

The menu carries two items rather than the original brief's one ("only be able to terminate it").
That growth was accepted knowingly and **the constraint must not drift further** — `09` and `11` both
declined a third item on exactly this ground.

**Reload configuration** re-runs §3.3 resolution and load, applies §3.4's *reload* behaviour on failure,
**re-applies the menu item texts** (a `language` change is not visible in `Reconcile()`'s tuple, so
the reload path applies it directly), then calls `Reconcile()`.

**Quit** calls `ExitThread()`, which runs §8.3.

---

## 8. Lifecycle

### 8.1 Startup order [`07`, `01`/Q20]

```
[STAThread] Main:
  1. acquire Local\CalendarWeekTray mutex; if not acquired, return immediately and silently
  2. ApplicationConfiguration.Initialize()
  3. Application.Run(new TrayApplicationContext())
```

The mutex is `Local\`-scoped (per-session, per-user) and must be **held for the process lifetime** —
keep it in a field/local that outlives `Application.Run` and `GC.KeepAlive` it. A second instance
**exits silently, with no dialog**: a background applet that pops a message box at login is one you
uninstall.

There is **no argv handling whatsoever** (§1.3).

Inside the constructor, in order:

1. autostart registration (§8.2) — a one-shot act, **not** part of `Reconcile()`;
2. load config (§3);
3. build the `NotifyIcon` and menu;
4. render and assign the first icon, **then** set `Visible = true`. Setting `Visible` before an icon
   exists shows a blank frame;
5. start the 1 ms timer (§6.4).

### 8.2 Autostart registration [`03`, `09`]

**The applet registers; it never governs.** The entire autostart footprint is one `REG_SZ` value
written at most once.

```
#if !DEBUG        // Release only — see below
try
{
    if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
                          "CalendarWeekTray", null) is null
     && Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                          "CalendarWeekTray", null) is null
     && Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\AutorunsDisabled",
                          "CalendarWeekTray", null) is null)
    {
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
                          "CalendarWeekTray",
                          "\"" + Environment.ProcessPath + "\"",
                          RegistryValueKind.String);
    }
}
catch { /* swallowed — see rule 5 */ }
#endif
```

Six binding rules:

1. **Release builds only.** Without the `#if`, running out of `bin\Debug\` leaves a `Run` value
   pointing at build output `dotnet clean` deletes — and since the applet reports nothing about its
   autostart state (rule 6), every dev machine would silently acquire a dead startup entry, exactly
   once, permanently. Path-sniffing for `bin\Debug` is a heuristic; a first-run opt-in contradicts
   `01`/Q9.
2. **The path comes from `Environment.ProcessPath`, never `Assembly.Location`** — under single-file
   publish `Location` returns an **empty string**, which would write an empty `Run` value. This is a
   trap, not a preference.
3. **Quoted unconditionally, with no arguments.** The recommended install directory has no spaces,
   but the user can drop the exe anywhere, and an unquoted path containing a space is the classic
   silent autostart failure. No `--autostart` argument: nothing in the design needs to know it was
   launched at logon.
4. **The guard fails closed.** Any exception on any of the three reads aborts registration entirely.
   The risk is asymmetric — registering can override a user's expressed intent; declining is always
   recoverable and never surprising.
5. **A failed write is caught and swallowed silently**, and documented in `README.md` instead.
   `HKCU\…\Run` is writable by default but corporate policy can lock it, in which case the write
   fails at *every* launch — a balloon there is a nag, not a diagnostic.
6. **Never overwrite an existing `Run` value**, even a stale one pointing at a moved binary, and
   **report nothing** about any of it. `03`'s "surface a stale path" recommendation is **retired** by
   `09`: a running instance narrating its autostart state is telling the user about something that is
   not wrong right now.

Why this guard and not something simpler: `03` proved empirically that the `StartupApproved` blob is
keyed by value **name** only and survives an identical rewrite, a changed-path rewrite, and even
delete-and-recreate as a **durable tombstone** — so a Task Manager disable holds permanently and the
malware-shaped failure the design feared cannot occur. Read `StartupApproved` for **presence** only;
do not decode the blob (`03` graded the byte semantics only *medium-high*, off an archived forum
post, and the guard is deliberately insensitive to it). `Run\AutorunsDisabled` is read because
Sysinternals Autoruns disables by **moving** the value there and leaves no `StartupApproved` trace.

**Task Manager is the only off-switch, and it is a complete one.** There is no deregistration code in
v1 — a `--unregister` flag was rejected because `WinExe` has no console and could report nothing, and
because `03`'s guard would put the value straight back on the next launch unless a tombstone landed
too, which `09` ruled out separately.

### 8.3 Shutdown [`05`, `07`]

In order:

1. `NotifyIcon.Visible = false` — **before** anything else. Without it the shell keeps drawing the
   icon until the user hovers over it;
2. **unsubscribe every `SystemEvents` handler.** These are *static* events: a subscription outlives
   the object and can fire during shutdown — the same class of bug as the `HICON`, and it bites only
   after the object should have been dead;
3. stop and dispose the timer;
4. dispose the `GlyphIcon` (which `DestroyIcon`s the handle) and the `NotifyIcon`;
5. release the mutex.

---

## 9. Diagnostics [`01`/Q23]

**No log file.** Two channels, and they are complementary rather than redundant:

- **Balloon tip** (`NotifyIcon.ShowBalloonTip`) — the attention channel, **shown once per distinct
  error**, never repeated on the 60 s poll. Balloons are unreliable *by design*: the `timeout`
  argument has been ignored since Vista, Windows 10+ renders them as toasts, they obey Do Not Disturb
  and can be disabled per-app. Its title and body are §10.2's; the body drops the `⚠`, because the
  balloon already paints `ToolTipIcon.Warning`.
- **Hover tooltip** — the persistent channel. The error is appended to the normal tooltip:

  ```
  Kalenderwoche 33 · 10.–16. August 2026 · ⚠ config.json ungültig (Zeile 4)
  ```

  Always visible, cannot be disabled, and is being built anyway. Toast tells you now; tooltip tells
  you whenever you look.

  This example was mixed-language until `16` — a German prefix and date with an English fault. The
  whole tooltip is **one language**, never composed from both.

The line number comes from `JsonException.LineNumber + 1` (the property is 0-based) and is omitted
when unavailable. Truncate the whole string at 127 characters (§4.3); the worst realistic case
measures 71.

The **only** conditions that reach either channel are config problems (§3.4) and a `Reconcile()`
exception (§6.2). Everything else is silent by decision — see §2.

---

## 10. Localisation [`16`]

Two languages, `de` and `en`, chosen by §3.1's `language` key. There is **no locale escape hatch of
any kind** in v1: a French user gets English strings and cannot change them [`14`]. That is
affordable because `06` made the **glyph locale-free in every language on earth** — a bare number in
a calendar frame — so what stays untranslated is a hover tooltip and a two-item menu.

The whole tooltip is composed in **one** language. Never mix.

### 10.1 Month names come from an explicitly named culture

`CultureInfo.GetCultureInfo("de-DE")` and `CultureInfo.GetCultureInfo("en-GB")` — **never
`CurrentUICulture`**. `14` made `language` overrule the OS, so a tooltip that half-followed the OS
would defeat the key's only purpose. `en-GB` rather than `en-US` because §10.3 uses British element
order.

Read `MonthNames[m - 1]` and compose by hand. `de-DE`'s `MonthGenitiveNames` are **identical** to its
`MonthNames` (measured), so the genitive-form trap that bites Russian, Czech and Greek does not apply.

A hard-coded 12 + 12 month table was weighed and rejected. Its only real prize would be flipping
`InvariantGlobalization` to `true`, and that is **not available at any price** — two measured
failures, one of which `04` did not have:

- `CurrentUICulture.Name` becomes `''` and `TwoLetterISOLanguageName` becomes `'iv'`, so §3.1's
  `language: "auto"` can **never** resolve to `de` again. Every German user silently gets English.
- `GetCultureInfo("de-DE")` **throws `CultureNotFoundException`** — invariant mode implies
  `PredefinedCulturesOnly=true`. This corrects `04`, which recorded that the flag "would silently
  degrade German formatting": it fails loudly.

Note for the `auto` path: `CurrentUICulture` on the development machine is **`en-US`**, so `"auto"`
resolves to `en` there. `14`'s "German user on an en-US corporate laptop" is not a hypothetical — it
is the maintainer's own machine, and it is why the `language` key exists.

### 10.2 String table

| | `de` | `en` |
| --- | --- | --- |
| menu — reload | `Konfiguration neu laden` | `Reload configuration` |
| menu — quit | `Beenden` | `Quit` |
| tooltip prefix | `Kalenderwoche {week}` | `Calendar week {week}` |
| separator | ` · ` (U+00B7, spaced both sides) | ` · ` |
| config fault | `⚠ config.json ungültig (Zeile {n})` | `⚠ config.json invalid (line {n})` |
| config fault, no line number | `⚠ config.json ungültig` | `⚠ config.json invalid` |
| render fault | `⚠ Symbolfehler` | `⚠ icon rendering failed` |
| balloon title | `CalendarWeekTray` | `CalendarWeekTray` |

`config.json` is a filename and stays untranslated in both. The balloon **title** is the product name
in both languages — a translated title would make the toast look like a different application. The
balloon **body** is the matching fault string with the leading `⚠ ` removed (§9).

**The warning marker is a bare `⚠`**, with **no** `U+FE0F` variation selector. Measured rather
than assumed, because `Segoe UI` — the tooltip font — **does not contain U+26A0 at all**: it renders
through fallback to `Segoe UI Symbol` as a clean monochrome triangle at 9 pt, with no `.notdef` box.
Adding `U+FE0F` produced a **pixel-identical** result under GDI, so it buys nothing and its only
possible effect elsewhere is to pull colour emoji from `Segoe UI Emoji` into a monochrome tooltip.
`·` (U+00B7) and `–` (U+2013) are both native to Segoe UI and need no fallback.

Both fault strings are short noun phrases in both languages. The earlier proposal's
`Symbol konnte nicht gezeichnet werden` / `could not draw the icon` was a full passive clause against
a three-word fragment — two diagnostics that did not read as the same product.

### 10.3 The date range — one composition rule

Both languages share **one** rule, and it is the only real logic in this section:

> Emit the right-hand date in full — day, month, year. From the left-hand date, **drop every trailing
> component it shares with the right**. The en dash (U+2013) is **unspaced** iff the left side
> reduced to a bare day, and **spaced** otherwise.

German writes `3.` where English writes `3`; that punctuation is the *only* difference between the two
languages here. Day numbers are **unpadded** — `3.`, never `03.` — consistent with §4.1's unpadded
glyph and tooltip.

The rule generates every case; the table below is worked examples, not additional rules. All four
spans were verified against `ISOWeek`:

| case | week | `de` | `en` |
| --- | --- | --- | --- |
| same month | 2026-W33 | `10.–16. August 2026` | `10–16 August 2026` |
| across months | 2026-W27 | `29. Juni – 5. Juli 2026` | `29 June – 5 July 2026` |
| across years, backwards | 2026-W01 | `29. Dezember 2025 – 4. Januar 2026` | `29 December 2025 – 4 January 2026` |
| across years, forwards | 2026-W53 | `28. Dezember 2026 – 3. Januar 2027` | `28 December 2026 – 3 January 2027` |

**2026 has 53 weeks**, so the fourth row is reachable in the spec's own reference year — the earlier
proposal listed only three cases and had no forwards cross-year example.

### 10.4 Assembling the tooltip

```
prefix  ·  range                        (normal)
prefix  ·  range  ·  fault              (a fault is live, §9)
```

Truncate the assembled string at 127 characters (§4.3). The worst realistic case measures 71.

---

## 11. Tests [`15`]

**A narrow suite of measured properties over the pure renderer, and nothing else.** xunit v3 in
`Tests/`, run by `dotnet test`, no golden images, no CI.

### 11.1 The project

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net10.0-windows</TargetFramework>
		<UseWindowsForms>true</UseWindowsForms>
		<OutputType>Exe</OutputType>          <!-- xunit v3 test projects are executables -->
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<RootNamespace>CalendarWeekTray.Tests</RootNamespace>
		<AssemblyName>CalendarWeekTray.Tests</AssemblyName>
		<IsPackable>false</IsPackable>
		<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="xunit.v3" Version="3.2.2" />
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
	</ItemGroup>
	<ItemGroup>
		<ProjectReference Include="..\..\src\CalendarWeekTray\CalendarWeekTray.csproj" />
	</ItemGroup>
</Project>
```

Both package versions are already in the local NuGet cache [`15`], so a restore needs no network.

**`dotnet test` at the repo root is the command**, resolved through `CalendarWeekTray.slnx` (§1.1).
That is the whole reason the solution file exists: `15` chose `dotnet test` over a hand-rolled runner
because it is "the command a future agent will actually think to run", and a layout where the bare
command finds nothing would have given that back.

The applet keeps a **NuGet-free shipped artifact**; the dependency rule binds the *published exe*,
not the repo, which is what admits xunit at all [`15`].

### 11.2 What is asserted

Swept over **all 53 weeks × {16, 20, 24, 28, 32} px** — the five sizes `SM_CXSMICON` reports at the
five standard scalings. All 53 weeks is **non-negotiable**: the `"00"` bug hit weeks 4, 14, 24, 34
and 40–49 specifically, so any sample can miss it.

Per (week, size), from `Render(spec, out metrics)`:

1. **Air preserved** — `metrics.DigitInk` sits **≥ 1 px inside** `metrics.Page` left, right and
   bottom, and ≥ 1 px below the binding bar. One assertion covering two bugs: it fails on `06`'s
   `"00"` overflow *and* on `12`'s `BodyPad` being reclaimed.
2. **Centring** — `|left gap − right gap| ≤ 1 px`. Not 0: the loop exits at sub-pixel drift
   (< 0.15 px), so integer margins can legitimately differ by one.
3. **Convergence** — `metrics.Converged` is true.

Per size:

4. **The reference really is the widest** — no week's digit ink exceeds the reference label's at the
   same fitted size. This is what pins `"44"` over `"00"`.
5. **Bar geometry** — height `== max(2, round(box × 0.17))`, and alpha **exactly 0** at both slot
   centres. **No tolerance** — `12` measured the slots crisp at 16 px with not one partial-alpha
   pixel, and a tolerance would let that regress silently.

Off the glyph:

6. **Config** — an unknown key is rejected, an unknown *value* is rejected, an absent file yields
   `(auto, auto)` [`14`].
   **Tooltip** — §10.3's rule has four branches, so assert **all four × both languages**: 2026-W33
   (same month), W27 (across months), W01 (across years backwards) and **W53** (across years
   forwards). Eight strings, one table-driven test. `16` widened this from `15`'s single week
   deliberately: a one-week fixture exercises only the branch that is hardest to get *wrong*, and the
   elision rule is the sole piece of logic in §10.
   **Ink** — `HighContrast ? MenuText : light ? black : white` across **all six theme states**
   [`13`], via `TrayState.Compute` (§6.1).

### 11.3 Measured properties, never golden images

A checked-in reference PNG per week × size is exact and catches everything, and is **rejected**: the
render is pinned to `Segoe UI Variable Text Semibold` **as installed**, so a Windows font update
re-rasterises the glyph and breaks every golden **with no bug present**. That is a maintenance
obligation triggered by a third party — the category this effort has refused four times (winget,
signing, updater, installer). It is also the weaker signal: a broken golden reports *that* something
changed, never *what*, and every property above is a specific number.

### 11.4 Verified by hand, not by test

These are **in v1's scope** and simply covered manually. This list belongs here and deliberately
**not** in the map's Out of scope, which would misrepresent v1's surface.

| behaviour | how it is checked | why it is not automated |
| --- | --- | --- |
| the autostart guard (§8.2) | inspect `HKCU\…\Run` after a Release run | covering three `Registry.GetValue` calls means abstracting the registry behind an interface the applet has no other use for — a seam bigger than the thing it wraps [`15`] |
| the icon swap and GDI handle discipline (§6.5) | Task Manager's GDI/USER object columns over a forced re-render loop | inseparable from `NotifyIcon`; already measured over 4,000 renders in `07` |
| shell integration — Explorer restart, DPI change, theme flip, resume | kill `explorer.exe`; change scaling; flip theme; sleep/resume | needs a real shell |
| the glyph beside the real clock | look at it | this is what `06`/`12`/`13` were for |

**Raw ISO week arithmetic is out** — `ISOWeek.GetWeekOfYear` is BCL code and testing it is testing
Microsoft. What is ours — the unpadded rendering (§4.1) and the date-range strings — is covered by 6.

### 11.5 No CI, deliberately

`10` already puts releases on GitHub so Actions was available. It loses on a decisive unknown:
**`Segoe UI Variable` is a Windows 11 font and it is unverified whether GitHub's Windows Server
runners carry it.** If they do not, every measured property either fails or silently measures a
fallback face — worse than no CI, because it is a red suite that means nothing. Instead, `dotnet
test` is a **documented gate in the release procedure** (§14). If the font question is ever worth
settling, it is a fresh ticket, not a v1 blocker.

---

## 12. Build, publish, release

### 12.1 Publish

```
dotnet publish src/CalendarWeekTray -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The explicit project path is required by the `src/` layout (§1.1) — publishing the solution is not
the same thing, and would try to publish the test project too.

Produces `CalendarWeekTray.exe` at **194.3 KB** (`10`'s re-measurement; `05` measured 168.8 KB with a
different flag set — quote a figure you have reproduced), plus a **22.9 KB `.pdb` which is dropped**.

**Framework-dependent single file, not NativeAOT, not self-contained** [`04`]. NativeAOT is
*unsupported*, not merely risky: `PublishAot` implies `PublishTrimmed`, which the .NET 10 SDK rejects
with a hard `NETSDK1175` for WinForms, escapable only via a private underscore-prefixed MSBuild
property. And the size prize does not exist — forced-trim measured **36 MB** and Microsoft's own issue
reports **54 MB**, against 194 KB here. Framework-dependent is the smallest *and* the fastest cold
start (58–79 ms host-init, 223–304 ms to a live tray icon). Revisit only if
[dotnet/winforms#4649](https://github.com/dotnet/winforms/issues/4649) leaves the `Future` milestone.

### 12.2 Release procedure [`10`, `15`]

**Quit → test → publish → zip → tag.**

1. quit any running instance (it holds a lock on its own exe);
2. `dotnet test` — the gate;
3. publish as above;
4. zip **`CalendarWeekTray.exe` alone** — no `.pdb` (there is no log file, no crash reporting and no
   support channel to send a stack trace to, so 22.9 KB of symbols serves nobody), no `README.md` (it
   is one click away on the repo the user just downloaded from, and a stale copy inside a zip is
   worse than none);
5. tag `v1.0.0`, matching `<Version>`, and attach the zip to a GitHub Release.

**No winget manifest, no code signing, no updater, no installer** — each ruled out on the map with
reasoning; see `10` before reopening any of them.

---

## 13. README

`README.md` is the entire user-facing deliverable of `10` and `11`. The existing sections
**Requirements / Install / Build and run / Update / Uninstall / Known limitations** are already
written and correct; this spec's job is to **keep them true, not to re-derive them**. Three changes:

1. **Fill the *Configuration* section**, currently a stub. It owes: both resolution paths in order,
   first-found-wins, the two keys with their values and defaults, a complete example file, the
   never-writes rule, and that an unknown key or value is reported rather than ignored. Uninstall
   step 4 forward-references this section, so it must name the paths.
2. **Add a *First run* section immediately after *Install*** [`11`] — not under a limitations
   heading. Every other tray icon is an *indicator* you glance at; this one exists to be read
   passively, so hidden behind the chevron it is not degraded, it is **pointless**. Wording from
   `11`:

   > ## First run
   >
   > The icon starts hidden in the overflow flyout — the `^` chevron by the clock. Windows puts
   > *every* new tray icon there, and only you can move it out.
   >
   > Drag it from the flyout onto the taskbar, or use **Settings → Personalization → Taskbar → Other
   > system tray icons** and switch **calendarweek-tray** on. Windows remembers the choice, keyed to
   > where the exe lives — see **Update** before you move it.

3. **Fix *Build and run* for the `src/` layout** (§1.1). `dotnet build` still works from the repo
   root via the solution, but `dotnet run` no longer does:

   ```
   dotnet build
   dotnet run --project src/CalendarWeekTray
   dotnet test
   ```

4. **Two drift fixes**: the *Requirements* section rounds the artifact to "roughly 200 KB" (fine, but
   keep it consistent with §12.1), and the opening line still says the applet displays the week as
   `KW32`, which `06` made false — the glyph is a bare number in a calendar frame.

---

## 14. Scope boundary

The map's **Out of scope** section is binding and this spec must not quietly re-open it: no Windows
Service hosting, no non-ISO week numbering, no languages beyond `de`/`en`, no `FileSystemWatcher`, no
log file, no configurable font, no autostart deregistration, no `StartupApproved` authoring, no
winget manifest, no code signing, no update mechanism, no installer, no self-promotion out of the
notification-area overflow, no `.pdb` in the release zip.

Each has a recorded reason in [`map.md`](map.md). "Decided against" and "never considered" are
different things, and the difference is the whole point of that section.
