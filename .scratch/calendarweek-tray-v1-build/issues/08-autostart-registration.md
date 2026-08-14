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

**Status:** ready-for-agent

- [ ] one `REG_SZ` under `HKCU\…\Run`, written at most once, **Release builds only**
- [ ] all three guard reads — the `Run` value, the `StartupApproved` tombstone, and the Autoruns disabled location — and any one of them present means decline
- [ ] `StartupApproved` is read for **presence only**; the blob is never decoded and never written
- [ ] the path comes from `Environment.ProcessPath`, quoted unconditionally, with no arguments
- [ ] the guard fails closed — any exception on any read aborts registration entirely
- [ ] a failed write is caught and swallowed silently
- [ ] an existing value is never overwritten, even a stale one pointing at a moved binary
- [ ] no deregistration code of any kind exists in the tree
- [ ] hand-verified: a Release run writes the value once and a second run leaves it untouched; a Debug run writes nothing
