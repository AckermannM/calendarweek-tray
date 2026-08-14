# 09 — README

**What to build:** The user-facing documentation is true and complete. `README.md` is the *entire*
deliverable of `10` and `11` — the install story, the update story and the first-run story are
documentation, not code, and there is nowhere else they exist.

Most of the file is already written and correct. This ticket's job is to **keep it true, not to
re-derive it**. Read [§13](../../calendarweek-tray-v1/spec.md) for the four changes, with §3 for the
configuration facts and §12.1 for the size figure.

The *First run* section is the one that matters most and it goes immediately after *Install*, not
under a limitations heading. Every other tray icon is an indicator you glance at; this one exists to
be read passively, so hidden behind the chevron it is not degraded, it is **pointless**.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] the *Configuration* stub is filled: both resolution paths in order, first-found-wins, the two keys with their values and defaults, a complete example file, the never-writes rule, and that an unknown key or value is reported rather than ignored
- [ ] *Uninstall* step 4's forward reference to that section resolves
- [ ] a *First run* section immediately after *Install*, using §13's wording
- [ ] *Build and run* is correct for the `src/` layout, including that `dotnet run` now needs an explicit project path
- [ ] the opening line no longer claims the applet displays `KW32` — `06` made that false
- [ ] the artifact size in *Requirements* is consistent with §12.1
- [ ] the documented failure modes stay documented: a locked-down `HKCU\…\Run` fails silently by design, and a missing runtime is explained by the apphost's own dialog
