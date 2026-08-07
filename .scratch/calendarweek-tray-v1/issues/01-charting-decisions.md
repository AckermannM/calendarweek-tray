# 01 — Charting decisions

Type: grilling
Status: resolved

## Question

What is this effort's destination, and which design decisions can be settled up front before any ticket is worked?

## Answer

Settled across three grilling rounds. Grouped by area; `Qn` refs are to the charting session.

### Destination and shape

- **Q1** — The map ends at a locked spec plus a validated visual prototype. The visual is the only thing that can't be decided on paper, so it is settled inside the map; everything else becomes a spec to hand off.
- **Q2** — A plain windowless per-user background process. Single-instance guarded. **A Windows Service was ruled out by fact**: services run in session 0 and cannot place icons in an interactive desktop's tray.
- **Q6** — WinForms `NotifyIcon` + GDI+ for glyph rendering. **No third-party dependencies** — WPF was rejected because it would drag in a tray library for no gain when there is no window.
- **Q15** — Framework-dependent single-file build for the prototype phase. NativeAOT deferred to research (`04`) rather than assumed to work.

### Week numbering

- **Q3** — ISO 8601, hard-wired. German business practice *is* ISO 8601 (DIN 1355-1 was aligned to it): Monday start, week 1 contains the first Thursday. Use `System.Globalization.ISOWeek.GetWeekOfYear`; **never** `Calendar.GetWeekOfYear`, which gets this subtly wrong.
- **Q21** — Zero-padded: `KW01`, not `KW1`. Fixed glyph width stops the icon reflowing between 3 and 4 characters at the year turn. To be eyeballed in `06` alongside layout, since the two interact.

### Autostart

- **Q9** — The app self-registers an `HKCU\...\Run` entry on first run, then never touches it again. Task Manager's *Startup apps* tab becomes the enable/disable surface. **Task Scheduler was ruled out** — its entries never appear in Task Manager. An unresolved risk around `StartupApproved` is carried by `03`.

### Configuration

- **Q4** — `config.json`, resolved from `%APPDATA%\calendarweek-tray\` first, then `~/.config/calendarweek-tray/` (`~` via `USERPROFILE`). The user's original path had "calender" misspelled; "calendar" wins. The repo directory was already correct — nothing to rename.
- **Q18** — **First found wins**, no merging. A merged schema means a key you can't find in the file you're editing is silently coming from the other file. The app **never writes** the config; the README documents path and defaults.
- **Q5 / Q17** — Four keys, all defaulted, file optional:
  ```json
  {
    "language": "de",
    "label": null,
    "layout": "single",
    "theme": "auto"
  }
  ```
  `language`: `de` | `en`, governs both the prefix and the menu strings. `label`: overrides the prefix outright; `null` derives it from `language`. `layout`: `single` | `stacked`. `theme`: `auto` | `light` | `dark`.
- **Q13** — Ships `de` and `en` only. The `label` escape hatch permanently closes "please add language X".
- **Q17** — **Font is deliberately not configurable.** The taskbar font is a requirement of the brief; exposing it invites breaking the applet's native look.
- **Q14** — Reload is a menu item only. No `FileSystemWatcher`.

### Interaction

- **Q7** — Left-click does nothing. Right-click opens a context menu. Instant-quit-on-left-click was rejected as a footgun.
- **Addition** — The menu carries **two** items: "Reload config" and "Quit". This grows past the original brief's "only be able to terminate it"; accepted knowingly, and the constraint should not drift further.
- **Q16** — Hover tooltip shows more than the glyph can, e.g. `Kalenderwoche 32 · 3.–9. August 2026`. `NotifyIcon.Text` has a 63-character limit on older shells — keep it short.

### Rendering

- **Q10** — Render on demand at `SM_CXSMICON` for the current DPI, declare PerMonitorV2 awareness, re-render on DPI change. Rendering one 16px icon and letting Windows upscale is how these applets end up blurry.
- **Q11** — Follow the system theme: read `SystemUsesLightTheme` under `HKCU\...\Themes\Personalize`, watch for changes, re-render on flip. Without this the glyph is invisible for half of users. The `theme` config key overrides.
- **Q8** — `single` vs `stacked` is **not** decided here; it goes to prototype `06`. Prior recorded for falsification: stacked probably loses, because ~8px per line is below the legibility floor for Segoe UI Variable.

### Rollover

- **Q12** — A one-minute timer that recomputes the week and re-renders only when the number differs. Immune to sleep/wake, manual clock changes and timezone changes all at once. Scheduling an exact next-Monday-midnight timer is more elegant and has more ways to be wrong.

### Lifecycle and diagnostics

- **Q20** — Named `Mutex` (`Local\`, per-user). A second instance exits silently — no dialog, because a background applet that pops a message box at login is one you uninstall.
- **Q19** — Malformed config behaves **differently by phase**. At startup: fall back to defaults, show one balloon tip, never fail to start over a typo. On reload: **keep the running config** and report the error — reverting a working icon to defaults because of a fat-fingered edit is worse than ignoring the edit.
- **Q23 + amendment** — No log file. Balloon tips (`NotifyIcon.ShowBalloonTip`) are the attention channel, but they are **unreliable by design**: the `timeout` argument has been ignored since Vista, Windows 10+ renders them as toasts, they obey Do Not Disturb, and they can be disabled per-app. So config errors are **additionally reflected in the hover tooltip** (e.g. `Kalenderwoche 32 · ⚠ config.json invalid (line 4)`), which is always visible, can't be disabled, and is already being built for Q16. Toast tells you now; tooltip tells you whenever you look.
