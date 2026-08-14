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

**Status:** ready-for-agent

- [ ] one `Reconcile()`, called by every trigger; nothing re-renders directly; it always runs on the UI thread
- [ ] it compares the desired tuple against the last applied and returns early when equal
- [ ] the §6.3 trigger table is wired; the `UserPreferenceChanged` category is ignored entirely, and `TaskbarCreated` is deliberately **not** handled
- [ ] the timer starts at 1 ms; the first tick captures the context, **asserts it is a `WindowsFormsSynchronizationContext` and fails loudly if not**, subscribes to `SystemEvents`, then goes to 60 s
- [ ] `GlyphIcon : IDisposable` owns `(Icon, HICON)` as one unit; the new icon is assigned to `NotifyIcon` **before** the previous one is disposed
- [ ] the whole `Reconcile()` body is wrapped in a catch — keep the last good icon, mark the tooltip, never let a timer tick take the process down
- [ ] shutdown follows §8.3 in order, unsubscribing every `SystemEvents` handler
- [ ] the process stays windowless — no hidden marshalling `Control`
- [ ] hand-verified: GDI and USER object counts stay flat over a forced re-render loop
- [ ] hand-verified: kill `explorer.exe`, flip the theme, change scaling, sleep and resume — the glyph is correct after each
