# 11 — Notification-area promotion: is it controllable, and what resets it?

Type: research
Status: resolved

## Question

Graduated from the map's fog by `05`, which put a real icon in a real tray and turned an unobservable question into an observable one.

`05` found that Windows 11 records each tray icon under `HKCU\Control Panel\NotifyIconSettings`, in a numerically-named subkey carrying an `ExecutablePath` value and an `IsPromoted` DWORD. After the scaffold's first run, `IsPromoted` read **1** — the icon was in the visible tray, not the overflow flyout.

That single observation raises more than it settles.

### Sub-questions

1. **What is the default for a never-before-seen executable?** The map's fog assumed Windows 11 hides new icons until the user drags them out. `05` observed a promoted icon but **cannot distinguish** a promoted-by-default shell from a user who dragged it during verification. Establish the actual default on current Windows 11 builds, ideally by first-running a fresh path and reading the key before touching the tray.
2. **Is there a supported way for an applet to promote itself?** Writing `IsPromoted` directly is an undocumented registry poke at a shell-owned store, and the shell may cache it. The likely answer is that no supported API exists — say so explicitly if that is what the evidence shows, because "we checked and there is no way" and "we never looked" are different spec outcomes.
3. **Should the applet do anything even if it can?** An applet that silently promotes itself past the user's tray preferences is badly behaved. A README line telling the user to drag the icon out may be the whole correct answer. Weigh against the standing minimalism preference.
4. **What does path-keyed state mean for install location?** Promotion is keyed by `ExecutablePath`, so moving the binary — `bin\Debug\` during development, then a real install directory — silently resets it and the user must drag the icon out again. **This feeds `10`'s sub-question 5 directly** and is a second, independent argument for recommending a canonical install location, alongside `03`'s stale-Run-value rule.

## Outcome

An answer to 1–3 precise enough for `08` to encode as either a spec behaviour or an explicit non-behaviour, plus a finding handed to `10` for sub-question 5. If the answer is "nothing to do but document it", that belongs in the map's **Out of scope** section rather than being silently dropped.

Use `/research`. Sub-question 1 is empirical and should be settled by observation on this machine, not by reading alone.

Record the findings in this file under `## Answer`.

## Answer

**The applet starts in the overflow flyout, it can promote itself, and it must not.** The one thing
this ticket changes about the product is a README paragraph — but a load-bearing one, because an
applet whose entire value is being readable at a glance is worth nothing hidden behind a chevron.

### 1. The default is *not promoted* — measured, and `05`'s observation was the user, not the shell

Published the scaffold to a never-before-seen path (`…\scratchpad\promo-a\CalendarWeekTray.exe`),
snapshotted `HKCU\Control Panel\NotifyIconSettings` (35 subkeys), launched, waited 6 s. One new
subkey appeared, `7887827385968263281`:

```
values=[UID, ExecutablePath, InitialTooltip, IconSnapshot]      IsPromoted = <ABSENT>
```

Then looked at the actual taskbar. The visible tray held one application icon (PowerToys) plus the
system status area and clock — **no week glyph**. Opening the overflow flyout showed the glyph as its
*first* entry, rendering `33`. So:

- `IsPromoted` **absent** is the initial state; the shell does not write the value at all until
  something changes it.
- Absent means **overflow**, confirmed visually, not inferred.

Cross-checks on the same machine, all consistent: `PowerToys.exe` (`IsPromoted=1`) is the one visible
app icon while its sibling `PowerToys.Awake.exe` (`IsPromoted=0`) is not; and four throwaway probes
left by earlier sessions — `TrayProbe.exe` and three `aottest.exe` builds, run once and never touched
— **all lack `IsPromoted` entirely**.

That settles the ambiguity this ticket was opened on. `05` saw `IsPromoted=1` for
`bin\Debug\…\CalendarWeekTray.exe` — the exact path `05` verified by hand in the tray. Had that been
the shell's default, the value would have been absent, as it was for every untouched probe.
**`05` recorded the user's own drag as if it were the system default.** The map's original fog
assumption was right and `05`'s observation was the misleading one.

Documented, and it has said so since Windows 7 — [Notifications and the Notification
Area](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area):

> When an icon is added to the notification area on Windows 7, it is added to the overflow section of
> the notification area by default. This area contains notification area icons that are active, but
> not visible in the notification area. **Only the user can promote an icon from the overflow to the
> notification area**, although in certain circumstances the system can temporarily promote an icon
> into the notification area as a short preview (under one minute).

Windows 11 changed the *store* (`NotifyIconSettings` replacing the old `TrayNotify` `IconStreams`
blob) without changing the *policy*.

### 2. No supported API — and the unsupported poke works, live

There is no promote flag anywhere in `NOTIFYICONDATA`. `dwState`/`dwStateMask` carry `NIS_HIDDEN`
(0x1) and `NIS_SHAREDICON` (0x2) — hiding has an API, showing does not. `Shell_NotifyIconGetRect`
reads position and cannot set it. The conceptual doc quoted above states promotion is the user's
alone. **We checked; there is no supported mechanism** — recording that explicitly, per the ticket.

The registry poke, however, works better than expected. Writing `IsPromoted` (`REG_DWORD` 1) into the
subkey moved the glyph from the flyout into the visible tray **within two seconds, with no app
restart and no `explorer.exe` restart** — the shell watches the key. Verified twice by screenshot.

Three measured facts make it a bad bet anyway:

- **The subkey name is a deterministic function of the executable path.** Deleted the key, relaunched
  the same exe, and the *identical* 64-bit name came back. But the hash is undocumented, so the
  applet cannot compute its own key name — it must scan the subkeys for its own `ExecutablePath`.
- **That scan is a race.** The key does not exist until the shell has processed the icon add; on a
  cold start it appeared within 6 s, but nothing bounds that.
- **And the scan's anchor is not reliable.** After deleting the key out from under a running shell,
  the shell recreated **only `IconSnapshot`** — `ExecutablePath` never came back. Not after 100 s of
  polling at 10 s intervals, and not after quitting and relaunching the applet. The shell holds its
  record in memory and re-materialises the persisted form only when it first *learns* of an exe. So
  the one value a self-promoting applet needs to find itself can legitimately be missing, with no
  event to wait for and no way to force it.

  (Promotion still worked when poked into that stripped key — so the numeric name is the identity and
  `ExecutablePath` is decorative metadata for Settings' benefit.)

An implementation would therefore be: poll an undocumented shell-owned store on a timer, string-match
a value that may never appear, and write a flag whose semantics Microsoft has never published. Against
one line of README.

### 3. It must not — this is `09`'s posture again

**Ruled out of scope.** Four reasons, in descending weight:

1. It is the user's decision, stated as a guideline on the same page:
   > The user should have the final say on which icons they want to see in their notification area.
   > Before installing a non-transient icon in the notification area, the user should be asked for
   > permission.
2. It is *governing* — precisely the posture [`09`](09-autostart-deregistration.md) denied the applet
   over autostart. "The applet registers, it never governs" generalises cleanly: the applet shows an
   icon, it never decides where the icon lives.
3. The mechanism is undocumented and, per §2, unreliable in exactly the case it would need to work.
4. Standing minimalism. This buys a poll loop, a registry scan and a failure mode, for something the
   user does once with two clicks.

The whole answer is documentation — but it is not a footnote. Every other applet in the tray is an
*indicator*: you glance at it when you want to know something. This one exists to be read passively.
Hidden behind the chevron it is not degraded, it is pointless. So the README instruction is
first-run-critical and belongs immediately after **Install**, not under a limitations heading.

Wording for `08` to encode:

> ## First run
>
> The icon starts hidden in the overflow flyout — the `^` chevron by the clock. Windows puts *every*
> new tray icon there, and only you can move it out.
>
> Drag it from the flyout onto the taskbar, or use **Settings → Personalization → Taskbar → Other
> system tray icons** and switch **calendarweek-tray** on. Windows remembers the choice, keyed to
> where the exe lives — see **Update** before you move it.

And a spec line recording the **non-behaviour**: *the applet never reads or writes
`HKCU\Control Panel\NotifyIconSettings`.* Worth stating explicitly so a later reader does not
rediscover the poke and assume nobody thought of it.

### 4. Path-keyed state → straight into `10`, and it confirms what `10` already chose

Directly observed, not argued: `bin\Debug\net10.0-windows\CalendarWeekTray.exe` and
`…\promo-a\CalendarWeekTray.exe` — same binary, same filename — hold **two separate subkeys**, and
the second started unpromoted while the first was promoted. Moving the exe silently sends the icon
back to the flyout, with nothing said and nothing logged.

[`10`](10-distribution-and-install.md) had already recorded that replacing the exe **in place** is
what "preserves both the `Run` value and `11`'s `NotifyIconSettings` promotion". That is now measured
rather than assumed, and it is a second independent argument for the canonical install location
alongside `03`'s stale-`Run`-value rule. **Nothing in `10` needs to change** — the README it specified
already carries both sentences.

Two details `10` could not have known:

- **A versioned install path would have been quietly corrosive.** Because the key is path-derived,
  any app installing to `…\App_1.2.3\` gets a fresh, unpromoted entry on every update — visible in
  the dump as the `WindowsApps\MSTeams_26198.304.4946.9672_x64__…` and
  `Microsoft.CommandPalette_0.10.11181.0_x64__…` entries. `10`'s versionless
  `%LOCALAPPDATA%\Programs\CalendarWeekTray\` avoids this by accident; it is now avoided on purpose.
- **`%LOCALAPPDATA%` paths are stored fully expanded**, username and all, while machine-wide known
  folders are tokenized (`{6D809377-…}` = Program Files, `{1AC14E77-…}` = System32,
  `{F38BF404-…}` = Windows). Harmless — but it means the entry is per-machine and per-user in the
  literal sense, and nothing about it roams.

One documented clause deliberately **not** acted on: `NOTIFYICONDATA`'s troubleshooting section says
icon settings survive a moved binary if both the old and new files are Authenticode-signed by the
same company. That clause is about `NIF_GUID` icon registration — WinForms `NotifyIcon` identifies by
`hWnd`+`uID` and never registers a GUID — and I did not test it against Windows 11's
`NotifyIconSettings` store. It does not reopen `10`'s code-signing ruling: an unsigned applet at a
recommended stable path never moves, so the exception has nothing to sell.

### What resets promotion

| Reset | Verified |
| --- | --- |
| Moving or renaming the exe (different path → different key → default) | yes, two coexisting keys for the same binary |
| Deleting the subkey | yes — glyph returned to the flyout |
| A version-bearing install directory, on every update | inferred from the path-keying, corroborated by the store's own `WindowsApps` entries |
| A new user profile (`HKCU`) | not tested; follows from the hive |

**Not** a reset: restarting the applet, or overwriting the exe in place.

Untested and left untested: whether a Windows feature update clears the store. It would change no
decision here — the applet does nothing either way, and the recovery is the same two clicks as first
run.

### Method and cleanup

All measurements on this machine, Windows 11 build 10.0.26200, 150% scaling. Screenshots had to be
taken at *physical* pixel coordinates — a DPI-unaware capture process reads the framebuffer directly,
so logical coordinates from `Screen.PrimaryScreen.Bounds` land in the middle of the screen instead of
on the taskbar. Noted only because it cost a wrong screenshot.

The test process was killed and subkey `7887827385968263281` deleted; `NotifyIconSettings` is back to
its 35-key baseline. The pre-existing `bin\Debug` entry from `05` was left alone. The four stale probe
entries from earlier sessions were left alone too — they point at deleted temp directories and are
harmless, but they are also the evidence for §1 and are worth keeping until `08` lands.
