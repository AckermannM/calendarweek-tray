# 05 — Strings, tooltip, and the pure desired-state function

**What to build:** Hovering the icon shows a real tooltip in the right language —
`Kalenderwoche 33 · 10.–16. August 2026` — and the glyph is painted in the right ink for the
machine's theme, including under high contrast.

Since `06` removed all text from the glyph, the tooltip is the **only** place the applet uses
language at all, and it doubles as the persistent diagnostic channel.

Read [§10](../../calendarweek-tray-v1/spec.md) for the strings, §4.2–4.3 for the week and the range,
§5.5 for ink, and §6.1 for `Compute`. Three of these are defect fixes rather than preferences and
must be built exactly as written:

- `SystemUsesLightTheme` reads `0` under **all four** stock contrast themes, so high contrast must
  win outright or High Contrast White gets a `#FFFFFF` glyph on a `#FFFAEF` taskbar at 1.04:1.
- The conditional in §5.5 cannot be collapsed — `SystemColors` does not track dark theme at all.
- `ISOWeek.GetYear`, never `DateTime.Year`: on 2026-12-31 the ISO week-year is 2027.

§10.3's elision rule is the only real logic in the localisation section, which is why the test table
covers all four of its branches in both languages rather than one representative week.

**Blocked by:** 02, 04

**Status:** ready-for-agent

- [ ] the §10.2 string table verbatim, both languages
- [ ] the warning marker is a bare `⚠` with **no** variation selector
- [ ] month names come from explicitly named cultures, never `CurrentUICulture`
- [ ] the §10.3 composition rule — one rule for both languages, dash spacing decided by whether the left side reduced to a bare day, day numbers unpadded
- [ ] `Compute` is pure with the §6.1 signature; every impure read happens in the caller and arrives as an argument
- [ ] the ink rule is §5.5 verbatim, and high contrast beats an explicit `theme: light` / `theme: dark`
- [ ] reads `SystemUsesLightTheme`, not `AppsUseLightTheme`; an absent key means light, meaning black ink
- [ ] the whole tooltip is composed in one language, never mixed, and is truncated at 127 characters
- [ ] the tooltip is visible on hover and correct for the configured language
- [ ] tests: all four range branches × both languages — eight strings, table-driven, including week 53
- [ ] tests: the ink rule across all six theme states, through `Compute`
