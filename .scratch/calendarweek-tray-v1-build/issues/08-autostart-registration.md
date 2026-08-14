# 08 — Autostart registration

**What to build:** A Release build registers itself to start at logon — once, ever — and a Debug build
never does. The entire footprint is one `REG_SZ` value.

**The applet registers; it never governs.** It writes no approval blob, deletes nothing, offers no
`--unregister` and no menu item, and reports nothing about its autostart state in any direction — not
a stale path, not an Autoruns disable, not a failed write. Task Manager is the only off-switch and
`03` proved it a complete one: the approval blob is keyed by value name and survives an identical
rewrite, a changed-path rewrite, and even delete-and-recreate, as a durable tombstone.

Read [§8.2](../../calendarweek-tray-v1/spec.md) — the guard is three registry reads and no written
state, and all six of its rules are binding. Two are traps rather than preferences: under single-file
publish `Assembly.Location` returns an **empty string**, and an unquoted path containing a space is
the classic silent autostart failure.

**Blocked by:** 01

**Status:** resolved

- [x] one `REG_SZ` under `HKCU\…\Run`, written at most once, **Release builds only**
- [x] all three guard reads — the `Run` value, the `StartupApproved` tombstone, and the Autoruns disabled location — and any one of them present means decline
- [x] `StartupApproved` is read for **presence only**; the blob is never decoded and never written
- [x] the path comes from `Environment.ProcessPath`, quoted unconditionally, with no arguments
- [x] the guard fails closed — any exception on any read aborts registration entirely
- [x] a failed write is caught and swallowed silently
- [x] an existing value is never overwritten, even a stale one pointing at a moved binary
- [x] no deregistration code of any kind exists in the tree
- [x] hand-verified: a Release run writes the value once and a second run leaves it untouched; a Debug run writes nothing

## Answer

New `src/CalendarWeekTray/Autostart.cs`, a static `Autostart.Register()` called as the very first
statement of `TrayApplicationContext`'s constructor (spec §8.1 step 1) — before the menu/config work,
so it runs unconditionally regardless of what config loading does afterward. The body is `#if !DEBUG`
only, matching spec §8.2's code block: three `Registry.GetValue` reads (`Run`, `StartupApproved`,
`Run\AutorunsDisabled`), all three absent required, then one `Registry.SetValue` of the quoted
`Environment.ProcessPath` under `Run`. One extra guard beyond the spec's literal listing: the write is
now also gated on `Environment.ProcessPath is string processPath` (a pattern match, not a null check
after the fact), because `"\"" + Environment.ProcessPath + "\""` would otherwise silently coerce a
`null` `ProcessPath` into the two-character string `""` rather than throw — a broken value the fail-
closed `catch` could never have caught, since string concatenation isn't a throwing operation. Every
read and the write both sit inside one `try`/`catch` that swallows silently, per rule 5.

No deregistration code exists anywhere in the tree — no `--unregister`, no menu item, nothing written
beyond the one `Run` value.

`dotnet build` (Debug and Release): 0 warnings, 0 errors. `dotnet test`: 299/299 (this guard is
explicitly on the "verified by hand, not by test" list — spec §11.4 — so no new test was added).
Hand-verified against the live registry: built Release and Debug. A Release run with no existing value
wrote `"...\bin\Release\net10.0-windows\CalendarWeekTray.exe"` under `Run`. With the value manually
overwritten to a stale `"C:\stale\moved.exe"`, a second Release run left it untouched — confirming rule
6 (never overwrite, even a moved-binary path) rather than just "runs twice without crashing". With the
value removed again, a Debug run wrote nothing. The registry was returned to its pre-test state
(`CalendarWeekTray` absent under `Run`) afterward.

`/code-review` ran against the diff. Fixed: the `Environment.ProcessPath`-could-be-`null` gap above.
Fixed: a misleading `// Step 1 of spec §8.1` comment on the `Autostart.Register()` call site, reworded
to state what it does without implying the surrounding constructor enumerates all five spec steps (it
doesn't — only steps 4 and 5 were ever labelled). Declined (pre-existing, out of scope for this
ticket): `SetGlyph`'s catch-block assumption about `nextIcon` (ticket 06), `GlyphIcon.FromBitmap`'s
narrow leak window on an `Icon.FromHandle` throw (ticket 02), `GlyphRenderer`'s triplicated pixel-scan
and per-render mask allocation (ticket 02), the two duplicated `ShowBalloonTip` call sites and the
redundant `timerCalibrated` field (ticket 06/07), and `Reconcile()` re-resolving `Language` twice per
call (ticket 07) — none of these touch autostart and belong to the tickets that wrote them.
