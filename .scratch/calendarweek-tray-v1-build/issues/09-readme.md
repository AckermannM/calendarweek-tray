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

**Status:** resolved

- [x] the *Configuration* stub is filled: both resolution paths in order, first-found-wins, the two keys with their values and defaults, a complete example file, the never-writes rule, and that an unknown key or value is reported rather than ignored
- [x] *Uninstall* step 4's forward reference to that section resolves
- [x] a *First run* section immediately after *Install*, using §13's wording
- [x] *Build and run* is correct for the `src/` layout, including that `dotnet run` now needs an explicit project path
- [x] the opening line no longer claims the applet displays `KW32` — `06` made that false
- [x] the artifact size in *Requirements* is consistent with §12.1
- [x] the documented failure modes stay documented: a locked-down `HKCU\…\Run` fails silently by design, and a missing runtime is explained by the apphost's own dialog

## Answer

`README.md` updated, no code changes — this ticket is documentation-only.

Opening line rewritten to describe the actual glyph (`06`'s bare number in a calendar frame)
instead of the retired `KW32` text form. Added a *First run* section immediately after *Install*,
verbatim from §13/`11`. *Build and run* now shows `dotnet run --project src/CalendarWeekTray` plus
`dotnet test`, with a line explaining why the bare command no longer resolves now that the root
holds a solution spanning two projects. *Configuration* filled in from spec §3.1/§3.3/§3.4: both
resolution paths in explicit first-found-wins order, the two-key example file, the default table,
and the "reported, not ignored" rule for an unknown key or value — which also resolves *Uninstall*
step 4's forward reference, since that section now has real content to point to.

Two items already true and left untouched: *Requirements*' "roughly 200 KB" was already consistent
with §12.1's 194.3 KB figure, and *Known limitations* already documented both failure modes (a
locked-down `Run` key failing silently, a missing runtime surfaced by the apphost's own dialog) —
nothing there contradicted the spec.

No build or test run needed — no `.cs` file changed.

`/code-review` ran against the tree. All eight findings it returned are in files this ticket never
touched — `TrayApplicationContext.cs`, `GlyphIcon.cs`, `GlyphRenderer.cs`, `GlyphTests.cs` — pre-
existing issues from `06`/`07` (a missing exception guard on config reload, a balloon-shown flag
that never resets, a `SetGlyph` catch-block disposal ordering bug, an `Icon.FromHandle` handle-leak
window, a tooltip-only change forcing a full icon rebuild, a hard-coded per-week dead-zone list in
the tests, a bolted-on `lastReconcileFailed` flag, and redundant `Graphics` allocation in the
centring loop). Declined here as out of scope, same as `08`'s precedent — none touch autostart or
the README, and they belong to the tickets that wrote that code.
