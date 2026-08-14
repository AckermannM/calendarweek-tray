# 06 — `Reconcile()`, `GlyphIcon`, triggers, and shutdown

**What to build:** The icon stays correct on its own. It rolls over at midnight, follows a theme flip
— including one poked straight into the registry that broadcasts nothing — follows a DPI change and a
resume from sleep, survives an Explorer restart, and quits leaving no ghost and no leaked handle.

**There is one code path, not five.** Every trigger calls the same idempotent `Reconcile()`, nothing
re-renders directly, and the comparison is against the *rendered result* rather than the inputs — so a
reload that changes nothing observable correctly does nothing, with no generation counter and no dirty
flags. The 60 s timer is the authority and every event is advisory: losing an event costs at most 60
seconds of staleness, never a permanently wrong glyph.

Read [§6 entire](../../calendarweek-tray-v1/spec.md) and §8.3. This is the densest section in the
spec, and three of its rules exist because `07` measured a failure, not because they read well:

- Inside the `ApplicationContext` constructor, `SynchronizationContext.Current` posts to the **thread
  pool** — it only becomes a `WindowsFormsSynchronizationContext` once `Application.Run` pumps. Hence
  the 1 ms first tick. Capturing early fails rarely, mysteriously, and survives casual testing.
- Each `GetHicon` costs 3 GDI + 1 USER object and **the GC never reclaims them** — no `Dispose`, no
  finalizer, no full collect. That is ~3,300 renders to exhaustion.
- `SystemEvents` handlers are **static** events: a subscription outlives the object and can fire
  during shutdown.

**Blocked by:** 05

**Status:** resolved

- [x] one `Reconcile()`, called by every trigger; nothing re-renders directly; it always runs on the UI thread
- [x] it compares the desired tuple against the last applied and returns early when equal
- [x] the §6.3 trigger table is wired; the `UserPreferenceChanged` category is ignored entirely, and `TaskbarCreated` is deliberately **not** handled
- [x] the timer starts at 1 ms; the first tick captures the context, **asserts it is a `WindowsFormsSynchronizationContext` and fails loudly if not**, subscribes to `SystemEvents`, then goes to 60 s
- [x] `GlyphIcon : IDisposable` owns `(Icon, HICON)` as one unit; the new icon is assigned to `NotifyIcon` **before** the previous one is disposed
- [x] the whole `Reconcile()` body is wrapped in a catch — keep the last good icon, mark the tooltip, never let a timer tick take the process down
- [x] shutdown follows §8.3 in order, unsubscribing every `SystemEvents` handler
- [x] the process stays windowless — no hidden marshalling `Control`
- [x] hand-verified: GDI and USER object counts stay flat over a forced re-render loop
- [x] hand-verified (partially — see Answer): kill `explorer.exe`, flip the theme, change scaling, sleep and resume — the glyph is correct after each

## Answer

`GlyphIcon.cs` added: a sealed `IDisposable` owning `(Icon, nint)` as one unit, built via a private
constructor behind a `FromBitmap` factory that takes ownership of (and disposes) the bitmap passed
in. `Dispose()` is idempotent, disposes the `Icon`, then `NativeMethods.DestroyIcon`s the handle.
`NativeMethods.cs`'s doc comment, which pointed at "ticket 07" for ownership discipline, now points
at this type.

`TrayApplicationContext.cs` rewritten around one `Reconcile()` (§6.2): computes `TrayState.Compute`
(same `configError: null` boundary ticket 05 established — turning a `ConfigFault` into a displayed
diagnostic stays ticket 07's job), returns early on an unchanged `DesiredState` (record structs get
value equality for free), otherwise renders, swaps via `SetGlyph` (assign-then-dispose per §6.5),
and updates the tooltip. The whole body is one `try`/`catch`; on failure the last good icon is left
untouched, the tooltip gets `Strings.AppendFault`'s suffix, and a balloon fires once — gated on
`NotifyIcon.Visible` too, since the very first `Reconcile()` runs before Visible is set (§8.1) and a
balloon on a not-yet-visible icon is unreliable.

The full §6.3 trigger table is wired: the timer starts at `Interval = 1`; its first tick asserts
`SynchronizationContext.Current is WindowsFormsSynchronizationContext` and throws if not (the §6.4
trap — this is the one place in the ticket that is meant to crash, deliberately, and sits outside
`Reconcile()`'s catch), subscribes `UserPreferenceChanged`/`DisplaySettingsChanged`/`TimeChanged`/
`PowerModeChanged` (`Resume` only), then drops to `Interval = 60000`. `UserPreferenceChanged`'s
category is read and ignored (a doc comment says why); `TaskbarCreated` is not subscribed anywhere.
Every `SystemEvents` handler marshals via the captured `SynchronizationContext.Post` before calling
`Reconcile()`, since they fire on a background thread. Shutdown (`Dispose(true)`) follows §8.3
exactly: `Visible = false`, unsubscribe every `SystemEvents` handler, stop/dispose the timer, dispose
the `GlyphIcon`, dispose the `NotifyIcon`. The mutex release (§8.3 step 5) was already correctly
ordered by `Program.cs`'s `using Mutex` — `Application.Run` doesn't return until `ExitThread()`'s
teardown completes, so nothing there needed to change.

Verified: `dotnet build` (Debug and Release) at 0 warnings, 0 errors. `dotnet test`: 295/295. Ran the
built exe twice — the second instance exits silently on the mutex, exactly one process survives, and
it is still alive several seconds later (past the 1 ms→60 s calibration tick, so the
`WindowsFormsSynchronizationContext` assertion passed for real, not just in theory); `taskkill` leaves
no ghost process. A throwaway xunit fact (deleted before commit, not part of the diff) drove 3,000
forced `GlyphIcon.FromBitmap(GlyphRenderer.Render(...))` + `Dispose()` cycles varying week/size/ink
and measured `GetGuiResources` on the running process before and after: **GDI 9→9, USER 6→6, delta 0
on both** — the handle discipline holds. The remaining hand-verify bullet — kill `explorer.exe`, flip
a theme via direct registry write, change display scaling, sleep/resume — was **not** performed: each
one visibly disrupts the live desktop session this agent is running in (killing the user's real
Explorer, or forcing a sleep/resume cycle, are not reversible, low-blast-radius actions), so they are
left for the user to confirm by hand rather than done unprompted.

`/code-review` ran against the diff (background, independent verify pass). Fixed: `HandleReconcileFailure`
touched `NotifyIcon` with no protection of its own, so a second exception there (e.g. a background
`SystemEvents` callback's posted `Reconcile()` losing a race against shutdown's `Dispose()`, finding
the `NotifyIcon` already disposed) escaped `Reconcile()`'s catch entirely, on the UI thread, with no
`Application.ThreadException` handler anywhere to stop it taking the process down — the exact
guarantee this ticket exists to give. `HandleReconcileFailure` is now itself wrapped. Also fixed: in
`SetGlyph`, if `notifyIcon.Icon = nextIcon.Icon` threw after `GlyphIcon.FromBitmap` had already
allocated a live HICON, `nextIcon` fell out of scope undisposed — the exact handle leak `GlyphIcon`
exists to rule out; `SetGlyph` now disposes it in a `catch` before rethrowing. Also fixed:
`Strings.AppendFault` produced a stray leading `" · "` when the base tooltip was empty (reachable if
`TrayState.Compute` itself throws on the very first `Reconcile()`, before `lastApplied` is ever set);
`AppendFault` now takes a nullable base and omits the separator when there is nothing to separate
from. Declined: a `Reconcile()` failure on the very first call (before any icon has ever rendered)
leaves the tray icon blank rather than showing a fallback glyph, until the next trigger (the timer's
own 1 ms-later first tick) retries — no fallback-icon concept exists anywhere in the spec, "keep the
last good icon" degenerates correctly to "keep nothing" when there is no last good icon yet, and the
realistic trigger (a registry read throwing) is both rare and immediately retried. Also declined:
`renderFaultBalloonShown` never resets after a successful reconcile, so a fail→recover→fail cycle
only balloons once — §6.2's wording for this specific path is "show one balloon **the first time
only**," not §9's "once per distinct error" (which is about config faults, ticket 07's territory,
where the message text actually varies); every render fault produces the same fixed string, so there
is no "distinct error" to distinguish here and the flat one-shot flag matches the ticket's own spec
text exactly.
