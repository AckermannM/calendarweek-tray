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

**Status:** resolved

- [x] two keys, `language` and `theme`, both optional, both defaulting to `auto`; the file itself is optional
- [x] `auto` resolves `language` from `CurrentUICulture.TwoLetterISOLanguageName`
- [x] the `JsonSerializerContext` source generator, always through the `JsonTypeInfo` overload, never `Deserialize<T>(string)`
- [x] comment handling and trailing commas configured on the attribute, never on a runtime options instance
- [x] generic `JsonStringEnumConverter<T>`; values matched case-insensitively
- [x] unmapped members rejected, so a typo'd key or value is reported rather than silently ignored
- [x] resolution order per §3.3, first found wins, no merging, and the second path is **not** tried when the first exists but fails to parse
- [x] startup falls back to `(auto, auto)` and never fails to start over a malformed file
- [x] the fault carries the JSON line number where available, remembering the property is 0-based
- [x] the applet never writes the file, or any file
- [x] tests: an unknown key is rejected, an unknown value is rejected, an absent file yields `(auto, auto)`

## Answer

`AppConfig.cs` and `ConfigLoader.cs` added to `src/CalendarWeekTray/`, both per spec §3 exactly:
`Language`/`Theme` enums, the `[JsonUnmappedMemberHandling(Disallow)]` record with the generic
`JsonStringEnumConverter<T>`, and the `ConfigJsonContext` source generator carrying
`ReadCommentHandling`/`AllowTrailingCommas` on the attribute — never a runtime
`JsonSerializerOptions` instance, and always the `JsonTypeInfo` overload
(`ConfigJsonContext.Default.AppConfig`), never `Deserialize<T>(string)`.

`ConfigLoader.Load()` walks the two §3.3 candidate paths (`%APPDATA%\calendarweek-tray\config.json`,
then `%USERPROFILE%\.config\calendarweek-tray\config.json`) and returns on the first one that exists,
success or failure — the second path is never opened once the first is found. A missing file, an
unreadable one, invalid JSON, an unknown key and an unknown value all fall back to `new AppConfig()`
(`auto`, `auto`); the last three also produce a `ConfigFault` carrying the message and
`JsonException.LineNumber` **unchanged, 0-based** — display formatting (the `+1`, the balloon/tooltip
string) is explicitly ticket 07's job, not this one's. `ConfigLoader.ResolveLanguage` turns `Auto`
into `De`/`En` from `CurrentUICulture.TwoLetterISOLanguageName`; nothing else consumes it yet. The
applet performs no file writes anywhere in the change. `Load()` also has an
`IEnumerable<string> candidatePaths` overload purely so tests never touch a developer's real profile
directories.

Nothing wired into `Program.cs`/`TrayApplicationContext.cs` — ticket 05 is blocked on this ticket and
owns consuming the result; `04`'s mandate stops at the config subsystem itself.

`test/CalendarWeekTray.Tests/ConfigLoaderTests.cs` covers the three cases §11.2 item 6 names (unknown
key rejected, unknown value rejected, absent file → `(auto, auto)`), plus a fourth happy-path case
(valid config with non-default, mixed-case values parses correctly) added after `/code-review`
flagged that the original three only ever exercised the fallback path. `dotnet build`: 0 warnings, 0
errors. `dotnet test` at the repo root: 280/280.

`/code-review` ran against the diff. Fixed: the missing happy-path test above; `ConfigLoader.LoadFrom`
collapsed from two sequential try/catch blocks into one try with two catch clauses (no behavioural
difference — `JsonException` never derives from `IOException` — just less code). Declined, out of
scope for this ticket: several `GlyphRenderer.cs`/`GlyphTests.cs` findings (shared pixel-scan helper,
`ReferenceCache`'s single-entry `ConcurrentDictionary`, `KnockOut`'s single-call-site delegate,
`DrawNumber`'s per-iteration `Graphics`/scan allocations, PascalCase constants vs. `.editorconfig`'s
field-naming rule, a `KnownDeadZone` doc-comment precision note) — all against tickets `02`/`03`'s
already-resolved, already-reviewed code, none of it touched by this diff. Also declined: the
`Status: resolved` vs. `docs/agents/triage-labels.md`'s five canonical values mismatch on tickets
`01`–`03` — pre-existing, not this ticket's mandate, and the same finding `03`'s own review already
raised and deferred.
