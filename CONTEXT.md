# Context: calendarweek-tray

A Windows 11 notification-area applet that displays the current ISO calendar week.

## Glossary

Use these terms exactly. Where a synonym is listed as avoided, it is avoided because it is actively misleading, not merely because it is different.

**Kalenderwoche (KW)** — the German term for the ISO 8601 calendar week. Not a separate numbering scheme: DIN 1355-1 was aligned to ISO 8601, so "KW" and "ISO week" denote the same number. `KW` is the German-language **prefix**; `CW` is the English one.

**ISO week** — the week number from `System.Globalization.ISOWeek.GetWeekOfYear`. Monday-start; week 1 is the week containing the first Thursday. _Avoid_ computing this with `Calendar.GetWeekOfYear`, which is a different and wrong answer at year boundaries.

**Glyph** — the rendered image placed in the tray: prefix plus zero-padded week number, e.g. `KW01`. Use "glyph" for the visual artifact and "icon" only for the `NotifyIcon`/`HICON` that carries it, so that "render the glyph" and "assign the icon" stay distinguishable.

**Prefix** — the letters preceding the number (`KW`, `CW`). Derived from `language` unless overridden by `label`.

**Label** — the config key that overrides the prefix outright with an arbitrary string. The escape hatch that removes any need for further locales.

**Layout** — how the glyph arranges its parts: `single` (`KW32` on one line) or `stacked` (`KW` over `32`). _Avoid_ "orientation" and "format".

**Notification area** — the region of the taskbar holding application icons, colloquially "the system tray". The **system status area** (network, audio, battery, clock) is a distinct, reserved region this applet cannot enter.

**Applet** — the whole program. It is a windowless per-user background process. _Avoid_ "service" and "daemon": a Windows Service runs in session 0 and cannot own a tray icon, so calling this a service describes something that is impossible to build.

**Config resolution** — locating `config.json` by checking `%APPDATA%\calendarweek-tray\` then `~/.config/calendarweek-tray/`, **first found wins**. _Avoid_ "merge" — no key-by-key combination happens, deliberately.

**Re-render trigger** — an event that obliges the glyph to be redrawn: week change, theme flip, DPI change, config reload, `TaskbarCreated`. Enumerated in ticket `07`.

## Decisions

Design decisions for v1 are recorded as wayfinder tickets under `.scratch/calendarweek-tray-v1/`, indexed by `map.md`. There are no ADRs under `docs/adr/` yet; promote a decision there when it outlives this effort.
