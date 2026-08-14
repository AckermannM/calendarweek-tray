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

**Status:** resolved

- [x] the §10.2 string table verbatim, both languages
- [x] the warning marker is a bare `⚠` with **no** variation selector
- [x] month names come from explicitly named cultures, never `CurrentUICulture`
- [x] the §10.3 composition rule — one rule for both languages, dash spacing decided by whether the left side reduced to a bare day, day numbers unpadded
- [x] `Compute` is pure with the §6.1 signature; every impure read happens in the caller and arrives as an argument
- [x] the ink rule is §5.5 verbatim, and high contrast beats an explicit `theme: light` / `theme: dark`
- [x] reads `SystemUsesLightTheme`, not `AppsUseLightTheme`; an absent key means light, meaning black ink
- [x] the whole tooltip is composed in one language, never mixed, and is truncated at 127 characters
- [x] the tooltip is visible on hover and correct for the configured language
- [x] tests: all four range branches × both languages — eight strings, table-driven, including week 53
- [x] tests: the ink rule across all six theme states, through `Compute`

## Answer

`Strings.cs` and `TrayState.cs` added to `src/CalendarWeekTray/`. `Strings` carries the §10.2 table
verbatim (menu items, tooltip prefix, the U+00B7 separator, both config-fault variants, the render
fault, the balloon title) plus `DateRange`, which implements §10.3's single elision rule: the
right-hand date is always emitted in full via `culture.DateTimeFormat.MonthNames` against explicitly
named `de-DE`/`en-GB` cultures (never `CurrentUICulture`), the left-hand date drops every trailing
component it shares with the right, and the en dash is unspaced only when that leaves a bare day.
`TrayState.Compute` matches the §6.1 signature exactly — `now`, `sizePx`, `highContrast`,
`systemUsesLightTheme`, `config`, `configError` — and calls nothing that touches the machine: it
derives `week`/`monday`/`sunday` from `ISOWeek` (`GetYear`, never `.Year`), resolves ink via §5.5's
verbatim conditional (high contrast wins outright over an explicit `theme`, and the absent-key case
falls through to light), and hands the language-resolved pieces to `Strings.ComposeTooltip`, which
truncates at 127.

`TrayApplicationContext`'s constructor now does one real (non-placeholder) render: `ConfigLoader.Load()`,
`SystemInformation.HighContrast`, `SystemInformation.SmallIconSize.Width`, and a `Personalize`\
`SystemUsesLightTheme` registry read feed `TrayState.Compute`, whose `DesiredState` drives both
`NotifyIcon.Text` and `GlyphRenderer.Render`. `configError` is hard-coded `null` here — turning a
`ConfigFault` into a displayed diagnostic is explicitly ticket 07's job (`AppConfig.cs`'s own doc
comment on `ConfigFault.LineNumber` says so), and the two-item localised menu with the reload trigger
belongs to ticket 07 ("menu and diagnostics") too, so the menu here still reads a bare `"Quit"`.

`test/CalendarWeekTray.Tests/StateTests.cs` covers the two required properties: all four §10.3
branches × both languages (2026-W33 same month, W27 across months, W01 across years backwards, **W53**
across years forwards — 8 cases, table-driven off `ISOWeek.ToDateTime` directly rather than raw
week/year arithmetic, per §11.4's "testing ISOWeek is testing Microsoft") and the ink rule's six theme
states through `TrayState.Compute` (high contrast; explicit light; explicit dark; auto+light;
auto+dark; auto+absent-key). A ninth test asserts the full tooltip assembly end-to-end through
`Compute`. `dotnet build`: 0 warnings, 0 errors. `dotnet test` at the repo root: 295/295.

`/code-review` ran against the diff (2 verifier agents, phase 2 verification). Fixed: `NotifyIcon.Visible`
was set `true` in the object initializer, before the first icon/tooltip existed — spec §8.1 requires
render-then-`Visible`, and the malformed-order window is now materially longer than the placeholder it
replaced (registry read + config load + the font-fit/centring loop), so this was moved to after the
real render. Also fixed: `Strings.ConfigFault` repeated the literal `"config.json"` four times instead
of reusing `ConfigLoader`'s existing filename constant, which is now `internal` (the repo's established
pattern for widening a `private const` for exactly this kind of cross-file reuse — see
`GlyphRenderer`'s `Stroke`/`BodyPad`/etc.). Declined, out of scope for this ticket: the constructor
still has no exception handling around `ConfigLoader.Load()`/the registry read/`GlyphRenderer.Render`,
and a startup throw would abandon an already-`Visible` `NotifyIcon` with no cleanup path — both are
real, but the catch-wrap-and-keep-the-last-good-icon discipline is explicitly `Reconcile()`'s job (spec
§6.2), which ticket 06 owns and which does not exist yet. Also declined: `configError` being discarded
on first launch (ticket 07's mandate, as above); `GlyphRenderer.ComputeFit`'s linear fit search
(pre-existing, ticket 02's code, untouched by this diff); the `label` glossary-avoid-term in
`GlyphRenderer`'s doc comment (same); and three tracker-convention findings against tickets `01`/`03`/`04`
(`Status: resolved` vs. the five canonical labels, `## Answer` vs. `## Comments`, and the cross-feature-slug
`spec.md` link) — all pre-existing, already raised and deferred by `03`'s and `04`'s own reviews, and
none of it this ticket's mandate or files.
