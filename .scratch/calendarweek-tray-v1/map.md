# Map: calendarweek-tray v1

Type: wayfinder:map
Status: open

## Destination

A **locked implementation spec** for the v1 tray applet, plus a **visual prototype** that has settled the icon's rendered form. Reached when nothing remains to decide — an agent can build the whole thing in one session from the spec without asking a question.

## Notes

**Domain.** A Windows 11 notification-area applet showing the current ISO calendar week ("KW32"). .NET 10, WinForms `NotifyIcon` + GDI+ (`System.Drawing`) for glyph rendering. Runs as a windowless per-user background process — *not* a Windows Service, which cannot own a tray icon (session 0 isolation).

**Skills every session should consult.**

- HITL tickets: `/grilling` and `/domain-modeling`
- `wayfinder:prototype` tickets: `/prototype`
- `wayfinder:research` tickets: `/research`

**Standing preferences for this effort.**

- Minimal above all else. Every added key, file, or menu item must earn its place.
- **No third-party dependencies.** BCL + WindowsDesktop runtime only.
- No log file. Balloon tip for attention, hover tooltip for persistent state.
- The app **never writes** `config.json`. It runs correctly with no config file present.
- ISO 8601 week numbering is hard-wired (`ISOWeek.GetWeekOfYear`, never `Calendar.GetWeekOfYear`).

**This is a planning map.** Tickets produce decisions, not deliverables — with two deliberate exceptions: `05` scaffolds the project because nothing can be rendered without it, and `06` produces a prototype artifact because the visual question cannot be settled on paper.

**Environment facts already established.** .NET 10 SDK `10.0.301` and `Microsoft.WindowsDesktop.App 10.0.10` are installed. `Segoe UI Variable` ships as a single variable font (`SegUIVar.ttf`) but GDI+ enumerates named instances — `Segoe UI Variable Small` / `Text` / `Display`, each with Light/Semilight/Semibold. The repo now holds a **working scaffold** (`05`): one root-level `CalendarWeekTray.csproj`, exe `CalendarWeekTray.exe`, git on `master` with no commits yet.

## Decisions so far

- [01 — Charting decisions](issues/01-charting-decisions.md) — 23 design questions settled in one grilling session: hosting model, ISO numbering, config location and schema, click behaviour, DPI/theme handling, rollover, packaging, diagnostics.
- [02 — Taskbar clock font](issues/02-taskbar-clock-font-metrics.md) — the clock is **`Segoe UI Variable Small`, Regular 400, 12/16 epx** (the WinUI Caption ramp entry), and `Small` is correct for the tray glyph too. **GDI+ does not collapse the variable font's named instances** — verified by outline, pixel and advance-width diffs — so the "match the taskbar font" requirement survives. Three traps for the implementation: GDI+ **freezes the `opsz` axis** at the instance's value rather than varying it with size, so the optical variant must be chosen deliberately; **`new Font("Segoe UI Variable", …)` silently falls back to Microsoft Sans Serif** because the bare family the shell's XAML uses is not a GDI family; and `SPI_GETNONCLIENTMETRICS` reports legacy `Segoe UI` at every DPI, so the family must be hard-coded — though `lfMessageFont.lfHeight` does yield the correct DPI-tracking pixel size.
- [04 — NativeAOT viability](issues/04-nativeaot-winforms-viability.md) — **NativeAOT is unsupported, not merely risky**: `PublishAot` implies `PublishTrimmed`, which the .NET 10 SDK rejects with a hard `NETSDK1175` error for WinForms, escapable only via a private underscore-prefixed MSBuild property. The size prize doesn't exist either — forced-trim measured 36 MB and Microsoft's own issue reports 54 MB stock, against **195 KB** for framework-dependent single-file. Ship **framework-dependent single file**; this supersedes `01`/Q15's prototype-vs-ship split, since they turn out to be the same artifact. A forced-trim run proved the applet's whole API surface survives trimming — 21 of 22 checks passed, the sole failure being reflection-based `System.Text.Json`, fixed in-process by the source generator.
- [05 — Scaffold the project](issues/05-scaffold-project.md) — **the harness exists and works**: one root-level `CalendarWeekTray.csproj` (no `src/`, no `.sln`), `ApplicationContext` + `NotifyIcon` with no `Form`, building with **0 warnings** and confirmed live in the tray with a clean quit and no ghost icon. Four facts out: single-file publish measures **168.8 KB**, vindicating `04`; `IsAotCompatible` analyzers are **silent**, not noisy; PerMonitorV2 is declared via the `ApplicationHighDpiMode` MSBuild property with **no `app.manifest`**, which `07` may revise; and — the sharp one — **`TextRenderer` cannot draw onto a transparent bitmap**, because GDI text has no alpha, which collides directly with `07`'s lean toward `TextRenderer` for shell-matching fidelity.
- [06 — Icon layout prototype](issues/06-icon-layout-prototype.md) — the glyph is a **calendar page: square corners, 1 px outline, filled binding bar with two rings notched through it, week number in the body — unpadded, `Segoe UI Variable Text Semibold`, smooth `AntiAlias`**. `KW` appears nowhere: it lost on one line, stacked at four different sizes and weights, beside the number, and knocked out of the bar. Both layouts this ticket set out to choose between lost, and the ticket's recorded prior was backwards — `Single` collapsed, not `Stacked`. The unlock was measuring that digit size is constrained by **width, not height**, so a cue above the digits is nearly free. Four bugs fixed on the way, all of which would otherwise have reached the spec: `05`'s glyph was rendering with **no antialiasing at all**; `SystemDefault` is a trap that emits ClearType colour onto a transparent icon; the fit reference `"00"` was **not the widest label** (figures are proportional — `"44"` is, and weeks 4/14/24/34/40–49 were overflowing); and the number was off-centre both from banker's rounding and from the deeper error of predicting a draw offset instead of measuring where the ink landed. Also settles for `07`: **`GetHicon` preserves partial alpha**, so `05`'s alpha worry and its `TextRenderer` constraint are both moot.
- [03 — Run key / StartupApproved](issues/03-run-key-startupapproved.md) — rewriting our Run value **cannot** silently re-enable a Task-Manager-disabled entry: the approval blob is keyed by value name, is written only by GUI startup managers or the app itself, and survives identical rewrite, changed-path rewrite, and delete-and-recreate as a durable tombstone. Autostart strategy is **self-register once, guarded** by three registry reads and no written state. Two constraints for the spec: `Win32_StartupCommand` ignores `StartupApproved` and must never be used as an "am I enabled?" oracle, and Autoruns disables by moving the value to `Run\AutorunsDisabled` instead, which the guard must also check.

## Not yet specified

- **Testing strategy.** What's even worth testing in an applet whose output is a bitmap. ISO week arithmetic is the obvious testable core; the rest may be manual — but `06` turned up four rendering bugs that were only caught by measuring pixels, which suggests the glyph is more testable than "it's a bitmap" implies.

## Out of scope

- **Hosting as a Windows Service** — ruled out by fact, not preference: session 0 isolation makes tray icons impossible from a service.
- **Placing the icon adjacent to the clock** — that region is reserved for system status. Ruled out by the user in the original brief.
- **Non-ISO week numbering** (US broadcast week, etc.) — German business practice is ISO 8601 via DIN 1355-1, so ISO is the only scheme v1 needs. A different scheme would be a fresh effort.
- **Languages beyond `de` and `en`** — the `label` override makes arbitrary prefixes possible without new locales, which closes this permanently.
- **Auto-reload via `FileSystemWatcher`** — debouncing, partial-write races, and save-by-rename editors buy complexity the explicit "Reload config" menu item avoids.
- **A log file** — balloon tip plus hover tooltip is the entire diagnostic story.
- **Making the font configurable** — the taskbar font is a requirement of the brief, not a preference; exposing it invites breaking the one thing that makes the applet look native.
