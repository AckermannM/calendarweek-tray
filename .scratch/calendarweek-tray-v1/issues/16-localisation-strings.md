# 16 — What exactly do the German and English strings say?

Type: grilling
Status: resolved
Blocked by: —

## Question

Surfaced while writing the spec (`08`). Everything else in v1 traces to a ticket; **the strings do
not.**

`01`/Q16 fixed exactly one string — `Kalenderwoche 32 · 3.–9. August 2026` — as an illustrative
example of a *same-month* German tooltip. Nothing else was ever decided:

- the **English tooltip** has no decided form at all;
- the **date range across a month boundary** (week 27: 29 June → 5 July) and **across a year
  boundary** (week 1: 29 December → 4 January) have no decided form in either language;
- the **German menu strings** were never written down — `01`/Q7 gives the menu items in English
  ("Reload config", "Quit") because that is the language the ticket was written in, which is not the
  same as deciding them;
- the **diagnostic suffix** (`⚠ config.json invalid (line 4)`) has no German form;
- which **culture** supplies month names — an explicitly named `de-DE`/`en-GB`, or
  `CurrentUICulture` — is a real fork, because `14` decided the `language` key **overrules** the OS.

This is small but it is not trivial, and three things make it worth a ticket rather than an
assumption:

1. **`15` asserts both strings exactly.** The `de` and `en` tooltip for one known week is one of the
   six things v1 tests, so a placeholder becomes a test fixture.
2. **Since `06` the tooltip is the only place the applet uses language at all.** The glyph is a bare
   number in a frame; the menu is two items a user must go looking for. The tooltip is the product's
   entire voice.
3. **`14` deleted the `label` key, so there is no escape hatch.** Whatever these strings say is what
   every user gets, permanently — a French user gets the English ones and cannot change them.

## Decisions needed

1. **German menu items.** `Konfiguration neu laden` / `Beenden`? Or shorter — `Neu laden` /
   `Beenden`? The menu is two items in a tray context menu, so length is nearly free, but the brief's
   register matters more than the width.
2. **The English tooltip prefix.** `Calendar week 32`, or `Week 32`, or `KW 32` even in English —
   note `06` removed `KW` from the *glyph*, which does not by itself settle whether the word survives
   in English prose.
3. **Date order in English.** `3–9 August 2026` (British, matches the German element order) or
   `August 3–9, 2026` (American). The maintainer is German; the applet is for a German-business
   concept.
4. **The two boundary cases, in both languages.** Proposed in the spec as
   `29. Juni – 5. Juli 2026` and `29. Dezember 2025 – 4. Januar 2026`; confirm or replace, including
   whether the German same-month form really drops the second month (`3.–9. August 2026`) and whether
   the dash is spaced.
5. **The German diagnostic suffix.** `⚠ config.json ungültig (Zeile 4)`? And whether the warning
   glyph `⚠` (U+26A0) renders acceptably in a `NotifyIcon.Text` tooltip at all — worth *looking* at,
   not just deciding.
6. **Month-name culture.** Explicit `de-DE`/`en-GB` (proposed) versus `CurrentUICulture`. The
   proposal's argument: `14` made `language` overrule the OS, so a tooltip that half-follows the OS
   would defeat the key's only purpose — a German user on an en-US laptop is the case `14` kept the
   key for.

## Outcome

A complete pair of string tables and date-range rules, precise enough to paste into
[`spec.md`](../spec.md) §10 and into `15`'s test fixtures.

The spec already carries a **proposed** table for all of the above, marked as such. A legitimate
outcome is "the proposal is right, ship it" — but it should be looked at rather than inherited,
because it is the only part of v1 that reached the spec without a decision behind it.

Use `/grilling`. This is HITL and it is specifically the *user's* call: these are user-facing strings
in the maintainer's own language, and the agent that proposed them is not the authority on how they
read.

## Done when

`spec.md` §10 loses its warning banner and reads as decided, and the map's destination is reached.

Record the decision in this file under `## Answer`.

## Answer

**The proposal was mostly right, and the three places it was wrong were all found by measuring rather
than arguing.** The strings are now [`spec.md`](../spec.md) §10, which has lost its warning banner.
Every one of the ticket's six decisions was taken; two gaps the ticket did not know about were found
and closed as well.

### The six decisions

1. **German menu items** — `Konfiguration neu laden` / `Beenden`, the long form. `Neu laden` was
   rejected: width is nearly free in a tray context menu, and in a two-item menu whose other entry is
   `Beenden` it fails to say *what* is reloaded. The English side moved to match — `Reload config`
   became **`Reload configuration`**, because the two languages otherwise disagreed in register, with
   German spelling out and English abbreviating the same idea.
2. **English tooltip prefix** — `Calendar week {week}`. `KW 32` in English is an abbreviation of a
   German word and meaningless to an English reader; `Week 32` leaves "week of what?" faintly open.
   The tooltip's job is to name the kind of number the glyph shows, and `Calendar week` mirrors
   `Kalenderwoche` so the two read as one sentence in two languages.
3. **Date order in English** — **British**, and the culture is therefore `en-GB`. American order forks
   the composition logic and adds a comma before the year with no German counterpart, to serve an
   en-US constituency this applet does not have.
4. **The boundary cases** — confirmed, but they collapsed into **one rule**, which is the useful part
   of this ticket. See below.
5. **German diagnostic suffix** — `⚠ config.json ungültig (Zeile {n})` verbatim. The *render*-fault
   pair was replaced: `⚠ Symbol konnte nicht gezeichnet werden` / `⚠ could not draw the icon` was a
   full passive clause against a three-word fragment, so the two diagnostics did not read as the same
   product. Now `⚠ Symbolfehler` / `⚠ icon rendering failed` — short noun phrases in both.
6. **Month-name culture** — explicit `de-DE` / `en-GB`, as proposed. But see the measurement below,
   which turned this from a preference into a hard constraint.

### The one rule that replaced the range table

> Emit the right-hand date in full — day, month, year. From the left-hand date, **drop every trailing
> component it shares with the right**. The en dash is **unspaced** iff the left side reduced to a
> bare day, and **spaced** otherwise.

Same month drops month and year, across-months drops the year, across-years drops nothing. The
proposal's three table rows are all consequences of it, and German differs from English only by the
`.` after the day number. This matters because it is the **only real logic in §10** — the rest is
literals — so it is the only part `15` can meaningfully assert.

### Three things measured, not decided

- **`⚠` renders, and the variation selector must not be used.** The ticket asked for this to be
  *looked* at, and it was. `Segoe UI` — the tooltip font — **does not contain U+26A0 at all**; it
  arrives through fallback to `Segoe UI Symbol` as a clean monochrome triangle at 9 pt with no
  `.notdef` box. Rendering `U+26A0 U+FE0F` beside it produced a **pixel-identical** result under GDI,
  so the selector buys nothing here and its only possible effect elsewhere is to pull colour emoji out
  of `Segoe UI Emoji` into a monochrome tooltip. Ship the bare codepoint.
- **`InvariantGlobalization=true` is not merely a degradation — it throws, and it breaks a key.** A
  hard-coded 12 + 12 month table was weighed as a third option (the ticket offered only culture vs
  `CurrentUICulture`), on the grounds that we compose the range by hand anyway and a table would make
  `15`'s fixtures immune to ICU changes. Its only real prize would have been flipping the flag, and
  measurement killed that twice over: `CurrentUICulture` collapses to `''` / `'iv'`, so §3.1's
  `language: "auto"` can **never** resolve to `de` again and every German user silently gets English;
  and `GetCultureInfo("de-DE")` raises `CultureNotFoundException` outright, because invariant mode
  implies `PredefinedCulturesOnly=true`. **This corrects `04`**, which recorded the flag as something
  that "would silently degrade German formatting" — it fails loudly, and the `auto` breakage is a
  second, independent reason `04` never saw. The prohibition in §1.2 now cites both.
- **The development machine's `CurrentUICulture` is `en-US`.** So `"auto"` resolves to `en` right
  here. `14` kept the `language` key for "a German user on an en-US corporate laptop" and that turns
  out to be the maintainer's own machine, not an edge case. Also confirmed: `de-DE`'s
  `MonthGenitiveNames` are identical to its `MonthNames`, so the genitive trap that bites Russian,
  Czech and Greek does not apply.

### Two gaps the ticket did not list

- **The balloon tip had no strings at all.** `ShowBalloonTip` takes a mandatory `tipTitle` *and*
  `tipText`, and §9 specified neither — a second instance of exactly the omission this ticket exists
  for. Decided: title is `CalendarWeekTray`, **untranslated in both languages**, because a translated
  title makes the toast look like a different application; body is the matching fault string with the
  leading `⚠ ` stripped, because the balloon already paints `ToolTipIcon.Warning` and the marker would
  be doubled.
- **§9's worked example was mixed-language** — `Kalenderwoche 33 · 10.–16. August 2026 · ⚠
  config.json invalid (line 4)`, German prefix and date with an English fault. Harmless as prose,
  dangerous as the thing someone copies into a fixture. Fixed, and §10 now states that the tooltip is
  composed in one language and never mixes.

### What `15` asserts, widened

`15` had "the `de` and `en` tooltip for one known week" — two strings. The composition rule has four
branches and a single-week fixture exercises only the one hardest to get wrong. Now **four cases ×
both languages**, one table-driven test over a pure function of `now`:

| case | week | `de` | `en` |
| --- | --- | --- | --- |
| same month | 2026-W33 | `10.–16. August 2026` | `10–16 August 2026` |
| across months | 2026-W27 | `29. Juni – 5. Juli 2026` | `29 June – 5 July 2026` |
| across years, backwards | 2026-W01 | `29. Dezember 2025 – 4. Januar 2026` | `29 December 2025 – 4 January 2026` |
| across years, forwards | 2026-W53 | `28. Dezember 2026 – 3. Januar 2027` | `28 December 2026 – 3 January 2027` |

All four spans verified against `ISOWeek`. The fourth row is the one the proposal had no example for,
and it is reachable in the spec's own reference year: **2026 has 53 weeks**.

### Spec changes

§10 rewritten as §10.1–§10.4 with the banner removed; §1.2's `InvariantGlobalization` rationale
corrected; §7's menu item renamed to *Reload configuration*; §9 gains the balloon title/body rule and
its example de-mixed; §11.2's tooltip assertion widened to eight strings; the document header no
longer declares an acknowledged gap.
