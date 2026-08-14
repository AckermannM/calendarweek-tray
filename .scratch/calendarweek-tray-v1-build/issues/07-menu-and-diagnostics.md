# 07 — The context menu and the diagnostic channels

**What to build:** Right-click gives exactly two localised items — *Reload configuration* and *Quit*.
Left-click does nothing. And when the config is bad, the user finds out: a balloon once, and a `⚠`
suffix on the hover tooltip that stays there for as long as the fault does.

The two channels are complementary rather than redundant. Balloons are unreliable *by design* — the
timeout argument has been ignored since Vista, they obey Do Not Disturb, and they can be disabled
per-app — so the toast tells you now and the tooltip tells you whenever you look. There is **no log
file**.

Read [§7](../../calendarweek-tray-v1/spec.md) and §9, with §3.4 for the reload-versus-startup
asymmetry: reverting a working icon to defaults because of a fat-fingered edit is worse than ignoring
the edit.

The menu carries two items rather than the original brief's one. That growth was accepted knowingly
and **must not drift further** — `09` and `11` both declined a third item on exactly this ground.

**Blocked by:** 06

**Status:** resolved

- [x] exactly two items, in the order §7 gives, localised per the configured language
- [x] left-click does nothing
- [x] *Reload configuration* re-runs resolution and load, **keeps the running config** on failure, re-applies the menu item texts directly, then calls `Reconcile()`
- [x] *Quit* exits through the §8.3 shutdown path
- [x] a balloon is shown **once per distinct error** and never repeated on the 60 s poll
- [x] the balloon title is the product name in both languages, and the body is the fault string with the leading `⚠` removed
- [x] the fault is appended to the tooltip with the §10.2 separator, and the whole string stays in one language
- [x] the only conditions reaching either channel are config problems and a `Reconcile()` exception — everything else stays silent

## Answer

`TrayApplicationContext.cs`'s two-item `ContextMenuStrip` is built from named `ToolStripMenuItem`
fields (`reloadMenuItem`, `quitMenuItem`) rather than the old bare `"Quit"` string, added in §7's
order. Left-click still has no handler at all — nothing to add. *Quit* stays an inline lambda over
`ExitThread()`; *Reload configuration* is `OnReloadConfiguration`: it re-runs `ConfigLoader.Load()`,
keeps `this.config` on a fault per §3.4 ("keep the running config"), records the fault either way,
calls `ApplyMenuText()` (a language change is invisible in `Reconcile()`'s `DesiredState` tuple, so
the reload path is the one place that re-applies menu text directly, per §7's own wording), then
`Reconcile()`.

`configFault` (set at startup and by reload) is threaded into `Reconcile()` as `Compute`'s
`configError` argument via `Strings.ConfigFault`, closing the boundary tickets 05/06 both left open
on purpose ("turning a `ConfigFault` into a displayed diagnostic stays ticket 07's job"). The 0-based
`JsonException.LineNumber` becomes 1-based through lifted `long?` addition. A new
`MaybeShowConfigFaultBalloon` runs on every `Reconcile()` call — *before* the tuple-equality early
return, not after — tracking the last-shown fault *string* rather than a flat one-shot flag: this
gives "once per distinct error, never repeated on the 60 s poll" (§9) exactly, and running before the
early return means a fault present since before `NotifyIcon.Visible` was set still gets its balloon
once the icon actually appears, rather than being silently skipped forever (the same problem
`HandleReconcileFailure`'s existing `Visible` guard already solved for render faults, mirrored here).
Clearing the fault resets the tracked text, so a fault that recurs after being fixed balloons again —
deliberately different from the render-fault flag, which never resets, because ticket 06's own answer
already established why: render faults are one fixed string with no "distinct error" to distinguish,
config faults vary by message and line.

`test/CalendarWeekTray.Tests/StateTests.cs` gained four table-driven tests over the pure string
helpers this ticket wires in: `Strings.ConfigFault` with and without a line number in both languages,
`Strings.AppendFault`'s separator-only-when-a-base-exists rule, and `Strings.BalloonBody` stripping
the leading marker from both fault kinds.

`dotnet build` (Debug and Release): 0 warnings, 0 errors. `dotnet test`: 299/299. Hand-verified: built
the Release exe, ran it, confirmed a second instance exits silently on the mutex leaving exactly one
process (§8.1); wrote a malformed `config.json` (`{ "theme": "drak" }`) to
`%APPDATA%\calendarweek-tray\`, restarted the exe, and confirmed it starts and stays alive well past
the 1 ms→60 s timer calibration tick rather than failing over the typo (§3.4). Visually confirming the
menu text, the tooltip's `⚠` suffix, and the balloon toast itself was not done — each needs an
interactive right-click / hover / notification-center check on the live desktop session this agent is
running in, the same category of hand-verify §11.4 already carves out for shell integration.

`/code-review` ran against the diff (8 finder agents, 5 targeted verifiers). Fixed: a prior render
fault's `⚠` tooltip suffix never cleared once the icon started rendering successfully again, because
`HandleReconcileFailure` sets `NotifyIcon.Text` directly without touching `lastApplied`, so a
recovered-but-otherwise-unchanged `DesiredState` matched `lastApplied` and hit `Reconcile()`'s early
return before ever reaching the line that would restore the clean tooltip — a new `lastReconcileFailed`
flag now forces one extra reapplication after any failure, clearing the stale suffix for good. Also
fixed: the constructor's `ConfigLoader.Load()` call had no exception handling and runs before
`Application.Run` pumps, so an escaping exception there — unlike everywhere else, which goes through
`Reconcile()`'s own catch — has no `ThreadException` handler to catch it and would crash the process
silently before any icon ever appeared, violating §3.4's "never fail to start over a typo"; a new
`LoadConfigSafely()` wraps the call the same way `Reconcile()` wraps everything else. Declined:
`SetGlyph`'s catch disposing `nextIcon` even after it has already been assigned into
`this.glyphIcon`, if `previousIcon.Dispose()` itself throws — real, but pre-existing ticket-06 code
this ticket does not touch. Declined: `HandleReconcileFailure` labelling any exception caught by
`Reconcile()`'s try — including one from `MaybeShowConfigFaultBalloon`'s own `ShowBalloonTip` call —
as a render fault; the checklist's own boundary is "a `Reconcile()` exception", not "a render
exception", so this is the designed catch-all, not a mislabel. Declined: `ApplyMenuText()` not being
called from `Reconcile()` or a `SystemEvents` trigger — this is §7's design as written ("a language
change is not visible in `Reconcile()`'s tuple, so the reload path applies it directly"), not a gap.
Declined: the two different one-shot-balloon mechanisms (a flat bool for render faults, a
last-shown-string for config faults) reading as accidental duplication — ticket 06's own answer
already states why they differ. Declined: no debouncing of bursty `SystemEvents` triggers — pre-existing
ticket-06 trigger-plumbing scope, not menu-and-diagnostics. Declined: threading a single resolved
`Language` through every call site instead of re-resolving it — `TrayState.Compute`'s signature is
spec-pinned (§6.1) and cannot take a pre-resolved language, and `ConfigLoader.ResolveLanguage` is a
cheap switch, not worth a parameter-threading abstraction for.
