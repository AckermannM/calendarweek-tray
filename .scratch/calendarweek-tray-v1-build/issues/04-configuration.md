# 04 — Configuration

**What to build:** The applet reads an optional `config.json` with two optional keys, and runs
correctly when there is no file at all. A typo in that file never stops it starting — it falls back
to defaults and carries the fault forward for ticket 07 to surface.

Read [§3 entire](../../calendarweek-tray-v1/spec.md). Two things there are traps rather than
preferences: passing a runtime `JsonSerializerOptions` instance to a source-generated call re-enters
the reflection resolver and re-breaks trimming, and the non-generic `JsonStringEnumConverter` is not
source-generator compatible.

`01`'s `label` and `layout` keys are **deleted** — `06` removed all text from the glyph and `12`
decided one form at every size, so neither had anything left to name. Nothing in the config file
reaches the renderer.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] two keys, `language` and `theme`, both optional, both defaulting to `auto`; the file itself is optional
- [ ] `auto` resolves `language` from `CurrentUICulture.TwoLetterISOLanguageName`
- [ ] the `JsonSerializerContext` source generator, always through the `JsonTypeInfo` overload, never `Deserialize<T>(string)`
- [ ] comment handling and trailing commas configured on the attribute, never on a runtime options instance
- [ ] generic `JsonStringEnumConverter<T>`; values matched case-insensitively
- [ ] unmapped members rejected, so a typo'd key or value is reported rather than silently ignored
- [ ] resolution order per §3.3, first found wins, no merging, and the second path is **not** tried when the first exists but fails to parse
- [ ] startup falls back to `(auto, auto)` and never fails to start over a malformed file
- [ ] the fault carries the JSON line number where available, remembering the property is 0-based
- [ ] the applet never writes the file, or any file
- [ ] tests: an unknown key is rejected, an unknown value is rejected, an absent file yields `(auto, auto)`
