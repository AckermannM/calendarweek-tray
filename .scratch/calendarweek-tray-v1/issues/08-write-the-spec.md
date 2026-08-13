# 08 — Write the locked implementation spec

Type: task
Status: open
Blocked by: 06, 07, 09, 10, 11, 12, 13, 14, 15

## Question

Fold every resolved decision into a single spec an agent can build from in one session without asking a question. This is the map's destination.

## Source material

Everything closed by then, in particular:

- `01` — the 23 charting decisions (hosting, config, interaction, rollover, diagnostics)
- `03` — autostart registration strategy, and the two constraints it imposes (`Win32_StartupCommand` is not a valid enabled-check; the guard must also check `Run\AutorunsDisabled`)
- `09` — autostart deregistration and whether the app authors its own approval blob
- `04` — packaging, plus **six binding implementation constraints** it measured: mandatory `JsonSerializerContext` source generator (with `ReadCommentHandling`/`AllowTrailingCommas` on the *attribute*, never a runtime `JsonSerializerOptions` instance, which re-enters the reflection resolver); `IsAotCompatible` analyzers on; `AllowUnsafeBlocks` for `LibraryImport`; mandatory `DestroyIcon`; never reference `ICommand`; `InvariantGlobalization` stays `false`
- `10` — distribution and install shape
- `06` — the decided icon form
- `12` — that the form is **size-independent**, and that its bar/air constants are load-bearing
- `07` — rendering pipeline, resource lifetime, re-render triggers
- `14` — the surviving config schema (`07` orphaned three of `01`'s four keys)
- `15` — whether v1 ships tests, and what they assert

## Must contain

1. **Project shape** — target framework, project properties, manifest, dependency policy (BCL only). Include a **`<Version>` property** (`10`): the csproj has none, so every build reports `1.0.0` and `10`'s manual update procedure has nothing to verify against. Quote the publish command and the artifact size **`10` measured — 194.3 KB**, not `05`'s 168.8 KB.
2. **Config** — the schema `14` settles (**not** `01`'s four keys — `06` and `07` orphaned three of them), resolution order (`%APPDATA%` then `~/.config`, first-found-wins), defaults, and the two distinct malformed-config behaviours (startup: fall back + report; reload: keep running config + report).
3. **Week computation** — `ISOWeek.GetWeekOfYear`, zero-padding, and the tooltip's date-range format per language.
4. **Rendering** — the pipeline and handle-ownership discipline from `07`, and the glyph's form from `06`. State plainly that there is **one form at every size** (`12`) — no threshold, no reduced variant, nothing branching on `sizePx` — because that is a decision that looks like an omission. Record the binding bar (0.17 of the box) and the 1 px of inner air as **measured constants that must not be reclaimed**: `12` opened both up and put them back, and at 0 px air the digits fuse into the side outline.
5. **Interaction** — left-click inert, right-click menu with "Reload config" and "Quit", localised per `language`.
6. **Lifecycle** — single-instance mutex, autostart registration per `03`, clean shutdown.
7. **Diagnostics** — balloon tip plus tooltip error surfacing; no log file.
8. **Localisation** — `de`/`en` string tables for menu and tooltip. **The glyph carries no text at all** (`06`), so there is no prefix for `label` to override unless `14` says otherwise.
9. **README** — config path, defaults, and how to disable autostart. `10` already wrote the **Requirements / Install / Update / Uninstall** sections; the spec's job is to keep them true, not to re-derive them. Two known drift points: uninstall step 4 defers the config path to the *Configuration* section, which `14` may still move, and the Requirements section rounds the artifact to "roughly 200 KB".

## Explicitly out

Do not let the spec quietly re-open settled scope. The map's **Out of scope** section is binding: no service hosting, no non-ISO numbering, no extra languages, no `FileSystemWatcher`, no log file, no configurable font.

## Done when

The spec is written and the user agrees an agent could build v1 from it unaided. At that point the map's destination is reached and the map closes.

## Notes

Watch for decisions that were never actually made. Anything discovered missing while writing becomes a **new ticket**, not an assumption buried in the spec — a spec that quietly invents an answer is worse than one with an acknowledged gap.

Record the spec's location in this file under `## Answer`.
