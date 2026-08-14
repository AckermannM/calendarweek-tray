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

**Status:** ready-for-agent

- [ ] exactly two items, in the order §7 gives, localised per the configured language
- [ ] left-click does nothing
- [ ] *Reload configuration* re-runs resolution and load, **keeps the running config** on failure, re-applies the menu item texts directly, then calls `Reconcile()`
- [ ] *Quit* exits through the §8.3 shutdown path
- [ ] a balloon is shown **once per distinct error** and never repeated on the 60 s poll
- [ ] the balloon title is the product name in both languages, and the body is the fault string with the leading `⚠` removed
- [ ] the fault is appended to the tooltip with the §10.2 separator, and the whole string stays in one language
- [ ] the only conditions reaching either channel are config problems and a `Reconcile()` exception — everything else stays silent
