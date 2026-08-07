# 04 — Is NativeAOT viable for a WinForms + GDI+ tray applet on .NET 10?

Type: research
Status: resolved

## Question

Packaging was deliberately split (`01`, Q15): framework-dependent single-file for the prototype phase, with the shipped artifact's form left open rather than assumed. Settle it.

The three candidates and what each costs:

| Option | Size | Needs runtime installed | Risk |
| --- | --- | --- | --- |
| Framework-dependent single file | ~1 MB | Yes (.NET 10 Desktop) | None |
| Self-contained single file | ~70 MB | No | Absurd size for a tray glyph |
| NativeAOT | ~5–15 MB? | No | Trimming/reflection breakage |

70 MB to display four characters is hard to defend for something whose brief is "as minimal as possible", so NativeAOT is the interesting option — *if* it actually works here.

Answer:

1. **Does .NET 10 support NativeAOT for WinForms at all?** Support was being built out across .NET 8/9; establish the .NET 10 status from primary Microsoft sources rather than older blog posts.
2. **Does `System.Drawing.Common` survive AOT?** The applet's core loop is GDI+ text measurement, bitmap rendering and `Icon.FromHandle` — if any of that depends on trimmed-away reflection, AOT is dead for this project.
3. **Do the specific APIs this design needs survive?** `NotifyIcon`, `ContextMenuStrip`, `ShowBalloonTip`, `SystemEvents` (display/theme/session change), `Microsoft.Win32.Registry`, and `System.Text.Json` deserialisation of the config record. **`System.Text.Json` is the likeliest casualty** — reflection-based deserialisation is trim-hostile, and the fix is a `JsonSerializerContext` source generator, which is cheap but must be in the spec.
4. **What is the actual measured size and cold-start time** versus framework-dependent?

## Decision this unblocks

A single recommendation for the shipped artifact's packaging, with any source-generator or trimming requirements it imposes on the implementation — because those have to be designed in from the start, not retrofitted.

## Method

Microsoft's NativeAOT and WinForms deployment documentation for the .NET 10 status. Then **build it** — a throwaway minimal WinForms + GDI+ + `NotifyIcon` project published with `PublishAot`, since AOT problems surface at publish and runtime, not in documentation. The .NET 10 SDK (`10.0.301`) is installed.

This ticket does **not** depend on `05` — use a scratch project outside the repo rather than waiting for the real scaffold.

Record findings in this file under `## Answer`.

## Answer

**NativeAOT is not viable. Ship framework-dependent single-file.**

Not "risky" — *unsupported*. The .NET 10 SDK refuses the build, and the only way past the refusal is a
private, underscore-prefixed MSBuild property. And the size prize the option was chased for turns out
not to exist: forced-trim measured **36 MB**, and Microsoft's own AOT WinForms size issue reports
**54 MB**. The comparison table in the Question section was wrong on both rows — corrected below.

### 1. Does .NET 10 support NativeAOT for WinForms? No.

Four independent primary sources, all current:

- **The SDK itself** (`Microsoft.NET.Sdk/targets/Microsoft.NET.RuntimeIdentifierInference.targets:305`,
  SDK 10.0.301, read on disk) still emits a hard **`NetSdkError`**:
  ```xml
  <NetSdkError Condition="('$(UseWindowsForms)' == 'true') and ('$(PublishTrimmed)' == 'true') and ('$(_SuppressWinFormsTrimError)' != 'true')"
               ResourceName="TrimmingWindowsFormsIsNotSupported" />
  ```
  Our publish attempt produced exactly this:
  `error NETSDK1175: Windows Forms is not supported or recommended with trimming enabled.`
  `PublishAot` implies `PublishTrimmed`, so AOT is gated by the same condition.
- **[Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)**
  (page updated 2025-12-03, i.e. post-.NET-10-GA): *"almost no Windows Forms apps are runnable without
  built-in COM marshalling, so trimming support for Windows Forms apps is disabled in the .NET SDK
  currently."* The [Native AOT overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
  lists *"Windows: No built-in COM"* as a flat limitation — which is precisely WinForms' dependency.
- **[dotnet/winforms#4649 "Epic - Make WinForms trim compatible"](https://github.com/dotnet/winforms/issues/4649)** —
  still **open**, milestone **`Future`** (not .NET 10, not .NET 11), last updated 2026-02-25. Their
  TrimTest project still generates **2,535 trim warnings**.
- **[What's new in WinForms for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100)**
  (updated 2026-02-10) does not mention trimming or AOT **at all**. Dark mode and async forms graduated
  out of preview in .NET 10; trimming did not, because it was never in preview.

The `_SuppressWinFormsTrimError` escape hatch still exists, but an underscore-prefixed MSBuild property
*is* the support boundary. Shipping across it means owning every WinForms trimming bug yourself, in a
project whose entire brief is minimalism.

### 2 & 3. Do `System.Drawing.Common` and the specific APIs survive trimming? Yes — all but one, as predicted.

This is the genuinely new information, and it is **measured, not read**. NativeAOT could not be linked
on this machine (see "toolchain" below), so as the closest available proxy the same project was
published with `PublishTrimmed=true` + `_SuppressWinFormsTrimError=true`, self-contained, and **run**.
`PublishTrimmed` applies the identical ILLink trimming and the identical WinForms feature switches that
AOT would; it differs only in that codegen stays JIT. If the applet's API surface were going to be
trimmed away, it would fail here.

It very nearly didn't fail at all. **21 of 22 checks passed.**

Passing under trimming: `NotifyIcon` (created, `Visible`, `MouseClick`/`DoubleClick`) · `ContextMenuStrip`
with `ToolStripMenuItem`/`ToolStripSeparator`/`CheckOnClick` · `NotifyIcon.ShowBalloonTip(3000)` ·
`Application.Run(ApplicationContext)` with **no Form**, pumping a real message loop and exiting cleanly ·
`Graphics.MeasureString` · `TextRenderer.MeasureText` · `Font("Segoe UI Variable Text", 9f)` ·
`Graphics.FromImage`/`Clear`/`DrawString`/`DrawRectangle` onto a 32bppArgb `Bitmap` ·
`Bitmap.GetHicon` + **`Icon.FromHandle`** + `Clone` · `DestroyIcon` via `LibraryImport` ·
`Bitmap.Save(PNG)` (codec enumeration) · `FontFamily.Families` (353 families) ·
`SystemEvents.UserPreferenceChanged`/`DisplaySettingsChanged`/`SessionEnding`/`TimeChanged` ·
`Microsoft.Win32.Registry` create/write/read/enumerate/delete incl. the real HKCU Run key ·
`ISOWeek.GetWeekOfYear` · `CultureInfo.GetCultureInfo("de-DE")`.

**The single failure was exactly the one this ticket predicted:**

```
FAIL  System.Text.Json REFLECTION-based Deserialize<AppConfig>
   -> System.InvalidOperationException: Reflection-based serialization has been disabled for this
      application. Either use the source generator APIs or explicitly configure the
      'JsonSerializerOptions.TypeInfoResolver' property.

PASS  System.Text.Json SOURCE-GENERATED Deserialize<AppConfig> -> Label=KW, Font=Segoe UI Variable Text, ...
```

Both ran in the same process, same build. **Hypothesis confirmed and fix verified.**

Only **18 IL2xxx warnings** (8 unique) were emitted, zero IL3xxx. None touch this applet's code paths —
they are drag-drop-as-JSON, NRBF clipboard deserialisation, COM `TypeDescriptor`, and
`AccessibleObject.IReflect.InvokeMember`. The one warning in *our* code was the deliberate
reflection-JSON call. So WinForms is far closer to trim-clean for a tray-only surface than the 2,535-warning
epic suggests — the blockers are policy and toolchain, not this applet's code.

### 4. Measured sizes and cold start

Built and run on this machine: SDK **10.0.301**, `Microsoft.WindowsDesktop.App` **10.0.10** (FDD) /
**10.0.9** (self-contained), `net10.0-windows`, `win-x64`, Release. Timings are the median-ish spread of
**3 warm runs each**; `host-init` is process start → `Main` entry (host + runtime init), `to-loop` is
process start → message loop running with the tray icon live.

| Variant | Size on disk | host-init | to-loop | Functional result |
| --- | --- | --- | --- | --- |
| **Framework-dependent single file** | **195 KB** (+24 KB pdb) | **58–79 ms** | **223–304 ms** | **22/22 pass** |
| Self-contained single file, compressed | 49.19 MB | 169–305 ms | 293–425 ms | n/a |
| Self-contained single file, uncompressed | 110.61 MB | — | — | n/a |
| Self-contained **trimmed** (forced, unsupported) | 36.22 MB | 185–220 ms | 401–474 ms | 21/22 — only STJ reflection |
| **NativeAOT** | **could not be produced** | — | — | blocked twice, see below |

**Corrections to the table in the Question section:**
- Self-contained is **110 MB**, not ~70 MB — or 49 MB with `EnableCompressionInSingleFile`. Worse than assumed.
- NativeAOT is **not ~5–15 MB**. [dotnet/winforms#9911 "Size of WinForms with PublishAot"](https://github.com/dotnet/winforms/issues/9911)
  (open, updated 2025-10-14) reports **54 MB** for a stock WinForms template, dropping to 15.2 MB only
  after manually excluding `PresentationFramework` (pulled in via `ICommand`), networking, XML, and
  designer attributes. The ~7 MB figure is an aspiration, not a result. Our own forced-trim build landing
  at 36 MB is consistent with that. **AOT would have been ~200x larger than the option we're recommending**,
  not smaller.

The framework-dependent build is the smallest *and* the fastest. There is no trade to make.

### Why NativeAOT could not be linked here (secondary blocker)

After suppressing NETSDK1175, the publish failed again:

```
error : Platform linker not found. Ensure you have all the required prerequisites documented at
https://aka.ms/nativeaot-prerequisites, in particular the Desktop Development for C++ workload.
```

This machine has Visual Studio 2026 Professional 18.7.2 with workloads `ManagedDesktop`, `NetWeb`,
`NetCrossPlat`, `CoreEditor` — **`Microsoft.VisualStudio.Workload.NativeDesktop` ("Desktop development
with C++") is not installed.** `link.exe` exists at `VC\Tools\MSVC\14.51.36231` but ships only a
`onecore` lib dir; there is no `C:\Program Files (x86)\Windows Kits\10` (only `NETFXSDK\4.8`) and no
`KitsRoot10` registry key, so the ucrt/um import libraries ILC needs are absent.

Per the ticket's instruction, the multi-GB C++ workload was **not** installed. This is a secondary
finding only — it does not change the recommendation, because blocker #1 (unsupported, `NetSdkError`)
is decisive on its own, and the forced-trim run already answered the interesting question about whether
the APIs survive.

### Recommendation

**Ship framework-dependent, single-file, win-x64.** 195 KB, fastest cold start, fully supported,
zero trimming risk. Prerequisite is the .NET 10 Desktop Runtime, which is a reasonable ask and is
obtainable via `winget install Microsoft.DotNet.DesktopRuntime.10` or Windows Update. This also means
the prototype-phase packaging from `01` Q15 **is** the shipping packaging — nothing changes at ship time.

If the runtime prerequisite is ever judged unacceptable, the fallback is self-contained single-file with
`EnableCompressionInSingleFile` (49 MB), **not** AOT. Revisit only if dotnet/winforms#4649 leaves the
`Future` milestone.

### Constraints this imposes on the implementation

Design these in from the start. Only the first is strictly required by the recommendation; the rest are
near-free and are what keep the AOT door open should #4649 ever land.

1. **`System.Text.Json` must use a `JsonSerializerContext` source generator.** Required regardless — it is
   also faster, allocation-free at startup, and removes the only measured failure. Non-negotiable, because
   retrofitting it after the config record grows is exactly the kind of rework this ticket exists to prevent.
   ```csharp
   [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
                                ReadCommentHandling = JsonCommentHandling.Skip,
                                AllowTrailingCommas = true)]
   [JsonSerializable(typeof(AppConfig))]
   internal partial class ConfigJsonContext : JsonSerializerContext;

   // always the JsonTypeInfo overload, never Deserialize<T>(string)
   var cfg = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
   ```
   `ReadCommentHandling`/`AllowTrailingCommas` belong on the attribute, not on a runtime
   `JsonSerializerOptions` — passing an options instance to a source-generated call re-enters the
   reflection resolver and re-breaks it.
2. **Keep the AOT analyzers on** even though we don't AOT. They cost nothing and fail the build the moment
   someone reintroduces reflection-based JSON:
   ```xml
   <IsAotCompatible>true</IsAotCompatible>   <!-- turns on all four analyzers -->
   <TrimmerSingleWarn>false</TrimmerSingleWarn>
   ```
3. **`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` if using `LibraryImport`.** Measured: the build fails
   with `SYSLIB1062: LibraryImportAttribute requires unsafe code` without it. Use plain `DllImport` instead
   if that's unwelcome — but see #4, we do need one P/Invoke.
4. **`Bitmap.GetHicon` leaks — `DestroyIcon` is mandatory.** `Icon.FromHandle` does not own the handle.
   A long-running applet re-rendering the glyph on every week rollover, DPI change and theme change will
   exhaust GDI handles otherwise. Pattern that was verified working: `GetHicon` → `Icon.FromHandle` →
   `Clone` the icon → `DestroyIcon` the original handle → assign the clone to `NotifyIcon.Icon` → dispose
   the *previous* icon after reassignment.
5. **Do not reference `ICommand`.** It is what drags WPF's `PresentationFramework` into WinForms
   publishes (per #9911). Irrelevant to size for FDD, but it is the single biggest own-goal available and
   costs nothing to avoid.
6. **`InvariantGlobalization` must stay `false`.** `CultureInfo.GetCultureInfo("de-DE")` is required for
   the `de`/`en` locale story from `01`. Verified working; just don't let anyone flip it on as a size
   optimisation, since it would silently degrade German formatting.

### Follow-on for the map

`map.md`'s "Distribution beyond a local build" open item was waiting on this. The artifact is a **single
195 KB exe with a .NET 10 Desktop Runtime prerequisite** — so distribution means a winget manifest with a
runtime dependency, or a plain zip, rather than an installer that carries a runtime. (Not editing `map.md`
from this ticket.)

### Reproduction

Throwaway harness (22 checks, self-reporting to `aottest-results.txt`) at
`C:\Users\AckermannM\AppData\Local\Temp\claude\C--src-calendarweek-tray\108715fc-30b1-4795-a483-814f23944b29\scratchpad\aottest\`.
Scratchpad only — nothing was added to the repo, and no git commands were run.
