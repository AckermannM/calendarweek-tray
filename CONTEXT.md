# Context: calendarweek-tray

A Windows 11 notification-area applet that displays the current ISO calendar week.

## Glossary

Use these terms exactly. Where a synonym is listed as avoided, it is avoided because it is actively misleading, not merely because it is different.

**Kalenderwoche (KW)** — the German term for the ISO 8601 calendar week. Not a separate numbering scheme: DIN 1355-1 was aligned to ISO 8601, so "KW" and "ISO week" denote the same number. The **word** is what the German tooltip says; the **abbreviation** `KW` appears nowhere in the applet, because since `06` the glyph carries no text.

**ISO week** — the week number from `System.Globalization.ISOWeek.GetWeekOfYear`. Monday-start; week 1 is the week containing the first Thursday. _Avoid_ computing this with `Calendar.GetWeekOfYear`, which is a different and wrong answer at year boundaries.

**Glyph** — the rendered image placed in the tray: a calendar page — outline, filled binding bar, two ring slots — carrying the zero-padded week number in its body and **no text of any kind**. Use "glyph" for the visual artifact and "icon" only for the `NotifyIcon`/`HICON` that carries it, so that "render the glyph" and "assign the icon" stay distinguishable. _Avoid_ **prefix**, **label** and **layout**: all three named parts of an earlier glyph that `06` and `12` replaced, and `14` deleted the config keys behind them. Nothing the applet renders has a prefix, and there is only ever one form.

**Tooltip** — the hover text (`Kalenderwoche 32 · 3.–9. August 2026`). Since the glyph carries no text, this is the **only** place the applet uses language at all, which is why it — not the glyph — is what the `language` key governs, and why it doubles as the persistent diagnostic channel.

**Ink** — the single colour the glyph is drawn in: pure white under a dark taskbar, pure black under a light one. Use "ink" rather than "foreground" or "text colour", because the frame, binding bar and number are all drawn in it and none of them are text.

**Reconcile** — the applet's one act: compute the glyph it *should* be showing and, only if that differs from what it *is* showing, render and swap. Every trigger reconciles; nothing re-renders directly. _Avoid_ "refresh" and "update", which both imply doing work unconditionally — the defining property is that reconciling is free when nothing has changed, which is what makes double-fired events and a once-a-minute poll harmless.

**Notification area** — the region of the taskbar holding application icons, colloquially "the system tray". The **system status area** (network, audio, battery, clock) is a distinct, reserved region this applet cannot enter.

**Autostart registration** — the applet's one-off creation of its `Run` entry, so Windows' own startup manager has something to list. The applet **registers; it never governs**: turning autostart on and off belongs to the user in Task Manager, and the applet honours that decision without ever recording, reflecting, or reporting it. _Avoid_ "autostart setting" and "autostart toggle", which both imply the applet holds state that `09` deliberately denied it, and _avoid_ describing registration as something that happens on startup — it is guarded, so on all but one launch it does nothing at all.

**Applet** — the whole program. It is a windowless per-user background process. _Avoid_ "service" and "daemon": a Windows Service runs in session 0 and cannot own a tray icon, so calling this a service describes something that is impossible to build.

**Config resolution** — locating `config.json` by checking `%APPDATA%\calendarweek-tray\` then `~/.config/calendarweek-tray/`, **first found wins**. _Avoid_ "merge" — no key-by-key combination happens, deliberately.

**Measured property** — the only form of automated assertion this project uses: a claim about a *number* read back out of a real render (ink bounds, gaps, alpha at a coordinate), never a comparison against a stored reference image. _Avoid_ **golden image** and **snapshot** as descriptions of what the suite does — a stored image asserts that nothing changed, which is a different and weaker claim, and one a Windows font update can falsify without a bug existing. The distinction is load-bearing: a measured property must be read from the render that actually happened, not predicted alongside it.

**Re-render trigger** — anything that prompts a **reconcile**: the one-minute poll, a theme flip, a DPI change, a clock change, resume from sleep, a config reload. Enumerated with its mechanism in ticket `07`. Note the deliberate asymmetry: **the poll is the authority and every event is advisory**, so a missed event costs staleness measured in seconds, never a permanently wrong glyph. `TaskbarCreated` is *not* in this set — the shell forgetting the icon is not the glyph becoming wrong, and `NotifyIcon` re-adds the existing icon itself.

## Decisions

Design decisions for v1 are recorded as wayfinder tickets under `.scratch/calendarweek-tray-v1/`, indexed by `map.md`. There are no ADRs under `docs/adr/` yet; promote a decision there when it outlives this effort.
