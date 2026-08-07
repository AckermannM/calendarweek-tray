# 08 — Write the locked implementation spec

Type: task
Status: open
Blocked by: 06, 07, 09, 10, 11, 12, 13

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
- `07` — rendering pipeline, resource lifetime, re-render triggers

## Must contain

1. **Project shape** — target framework, project properties, manifest, dependency policy (BCL only).
2. **Config** — the four-key schema, resolution order (`%APPDATA%` then `~/.config`, first-found-wins), defaults, and the two distinct malformed-config behaviours (startup: fall back + report; reload: keep running config + report).
3. **Week computation** — `ISOWeek.GetWeekOfYear`, zero-padding, and the tooltip's date-range format per language.
4. **Rendering** — the pipeline and handle-ownership discipline from `07`.
5. **Interaction** — left-click inert, right-click menu with "Reload config" and "Quit", localised per `language`.
6. **Lifecycle** — single-instance mutex, autostart registration per `03`, clean shutdown.
7. **Diagnostics** — balloon tip plus tooltip error surfacing; no log file.
8. **Localisation** — `de`/`en` string tables, and how `label` overrides the prefix.
9. **README** — config path, defaults, and how to disable autostart.

## Explicitly out

Do not let the spec quietly re-open settled scope. The map's **Out of scope** section is binding: no service hosting, no non-ISO numbering, no extra languages, no `FileSystemWatcher`, no log file, no configurable font.

## Done when

The spec is written and the user agrees an agent could build v1 from it unaided. At that point the map's destination is reached and the map closes.

## Notes

Watch for decisions that were never actually made. Anything discovered missing while writing becomes a **new ticket**, not an assumption buried in the spec — a spec that quietly invents an answer is worse than one with an acknowledged gap.

Record the spec's location in this file under `## Answer`.
