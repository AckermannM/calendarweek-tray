# 05 — Scaffold the repo and a runnable .NET 10 project

Type: task
Status: resolved

## Question

Nothing can be rendered or looked at until a project exists. The repo is currently **completely empty** — no git, no source, no solution.

This is a `task` ticket in the strict wayfinder sense: it decides nothing, but the prototype decision (`06`) is blocked until it's done.

## Work

1. **`.gitignore` via `dotnet new gitignore`.** The SDK ships the canonical .NET template — do not hand-roll one.

   **Git is already initialised** (branch `master`, no commits yet). Do **not** run `git init`.

   Verified against SDK 10.0.301: the generated file is 385 lines, covers `[Bb]in/` and `[Oo]bj/`, and contains **no pattern matching `scratch`** — so this repo's issue tracker is committed as-is and needs no un-ignore rule. Confirm with `git status` after generating rather than trusting this note. **Never add a `.scratch/` rule**: the tracker lives there and must be committed.
2. Create a **.NET 10 WinForms project** targeting `net10.0-windows`, with:
   - `<UseWindowsForms>true</UseWindowsForms>`
   - `<OutputType>WinExe</OutputType>` — no console window (`01`, Q2)
   - `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
   - PerMonitorV2 DPI awareness (`01`, Q10) — via `app.manifest` or `ApplicationHighDpiMode`, whichever the research in `07` prefers; a reasonable default now is fine, `07` may revise it.

   Plus four properties `04` established by measurement — set them **now**, because retrofitting them is exactly the rework that ticket exists to prevent:
   - `<IsAotCompatible>true</IsAotCompatible>` — we do *not* ship AOT (`04` proved the SDK refuses it), but the analyzers are free and will fail the build if reflection-based JSON ever creeps back in.
   - `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — required for `LibraryImport`; without it the build fails `SYSLIB1062`. Needed for the mandatory `DestroyIcon` P/Invoke.
   - `<InvariantGlobalization>false</InvariantGlobalization>` — `de-DE` depends on it. Do not let this get flipped later as a size optimisation.
   - **Never reference `ICommand`** — it drags WPF's `PresentationFramework` into the publish output.
3. **No `Form`.** The applet has no window — `ApplicationContext` plus a `NotifyIcon` is the shape. A default `Form1.cs` should not survive scaffolding.
4. Confirm it **builds and runs**, showing *any* placeholder tray icon, and that it exits cleanly from a context menu. This is the harness `06` renders into, so it must actually work.
5. A minimal `README.md` stub — the config path and defaults get written later by `08`.

## Done when

`dotnet build` succeeds, `dotnet run` puts an icon in the tray with no console window and no taskbar button, and right-click → exit terminates the process cleanly with no orphan icon left behind.

## Notes

The stale-tray-icon-after-exit problem is real and worth noticing here rather than at the end: an icon whose owner died stays visible until the user hovers over it. `NotifyIcon.Visible = false` before exit is the fix. Flag it for `07` if the scaffold reproduces it.

Do **not** design the rendering pipeline here — that is `07`. This ticket only needs *an* icon, not the right one.

Record what was done, and any facts later tickets depend on, in this file under `## Answer`.

## Answer

Done. The harness `06` renders into exists, builds clean, and was confirmed working in a live tray.

### What was created

> **Superseded by the user during [`08`](08-write-the-spec.md).** The root-level placement below is
> reversed: v1 uses **`src/CalendarWeekTray/` and `test/CalendarWeekTray.Tests/`** with a
> `CalendarWeekTray.slnx` at the root, per spec §1.1. Everything else in this answer stands — the
> project's properties, the DPI decision, the `DestroyIcon` reference implementation and the traps
> for `07` are unaffected by where the file sits.

A **single project at the repo root** — no `src/` directory and no `.sln`. One project needs neither, and root placement means bare `dotnet build` / `dotnet run` work from the repo root with no arguments. Five files:

| File | Purpose |
| --- | --- |
| `.gitignore` | `dotnet new gitignore`, unmodified |
| `CalendarWeekTray.csproj` | the project |
| `Program.cs` | `[STAThread] Main` → `ApplicationConfiguration.Initialize()` → `Application.Run(new TrayApplicationContext())` |
| `TrayApplicationContext.cs` | the `ApplicationContext` + `NotifyIcon`. No `Form` was ever generated |
| `NativeMethods.cs` | the `DestroyIcon` `LibraryImport` |
| `README.md` | stub; config path and defaults deferred to `08` |

Assembly and exe name are **`CalendarWeekTray`**; root namespace `CalendarWeekTray`. This is the name that will appear in Task Manager's *Startup apps* tab, so `03`/`09`/`10` should assume `CalendarWeekTray.exe`.

Nothing has been committed — `git` is initialised on `master` with **still no commits**. The working tree is entirely untracked.

### Verification against "Done when"

- `dotnet build` — **succeeds, 0 warnings, 0 errors**.
- Launched exe — process alive, `MainWindowHandle == 0` (no window, therefore no taskbar button), no `conhost` child (no console window).
- Tray icon — **confirmed visible by the user**, menu opened, **Quit terminated the process cleanly**.
- Stale icon after exit — **did not reproduce**. `NotifyIcon.Visible = false` in the `Dispose(true)` override was written in from the start and the icon vanished on the instant. Note for `07`: this is confirmation the discipline works, *not* evidence the problem is absent — it was never given a chance to occur.

### Facts later tickets depend on

**`.gitignore` (confirmed, not assumed).** 385 lines; **no pattern matching `scratch`**. Post-generation `git status` shows `bin/` and `obj/` gone and `.scratch/` still listed as untracked. The issue tracker is committable as-is.

**DPI awareness is declared via MSBuild, not a manifest.** `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`, which the WinForms source generator turns into a `SetHighDpiMode` call inside `ApplicationConfiguration.Initialize()`. There is **no `app.manifest` in the project**. `07` may still want one — a manifest declares awareness before managed code runs, which the MSBuild route cannot — but adding it later is a file, not a refactor.

**`IsAotCompatible=true` is not noisy.** Zero trim/AOT/single-file analyzer warnings against this code. The fear that the analyzers would drown a WinForms project in noise does not materialise at this size; it can stay on as `04` intended.

**The `DestroyIcon` `LibraryImport` compiles** under `AllowUnsafeBlocks=true` and is already wired into the scaffold's icon swap, so `07` inherits a working reference implementation of the ownership dance rather than a blank page.

**Framework-dependent single-file publish measures 168.8 KB** (`-c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`), against `04`'s 195 KB estimate. `04`'s conclusion holds with room to spare. `10` can quote 169 KB.

**A trap for `07`, found while writing the placeholder.** The placeholder uses `Graphics.DrawString`, *not* `TextRenderer.DrawText` — because `TextRenderer` is GDI and GDI text drawing has **no alpha channel**, so it cannot draw onto the transparent bitmap `01`/Q10 requires. This collides head-on with `07`'s open question, which leans toward `TextRenderer` precisely because it matches shell rendering. `07` cannot simply pick `TextRenderer`; it must decide how to get GDI text onto a surface with genuine alpha (render to an opaque bitmap and reconstruct alpha, use a 32-bit DIB section, or accept GDI+ hinting). **This is a real constraint, not a preference.**

**Windows recorded the applet under `HKCU\Control Panel\NotifyIconSettings`**, keyed by a numeric id, with `ExecutablePath` pointing at the `bin\Debug\...` exe and `IsPromoted = 1`. Two consequences: promotion state is **keyed by executable path**, so moving the binary mints a fresh entry and loses whatever the user had set — which sharpens `10`'s sub-question 5 considerably — and this session cannot distinguish a default of promoted from a user action. Graduated into `11`.

### Placeholder glyph — settles nothing

The glyph is the bare two-digit ISO week number in white, `Segoe UI` at 62% of `SM_CXSMICON`, centred. Deliberately crude and deliberately **not** `KW32`: prefix, layout, font, optical size and padding are all `06`'s to decide, and the placeholder was kept ugly so it cannot be mistaken for a proposal.
