# 09 — How does autostart get turned off, and does the app ever author an approval blob?

Type: grilling
Status: resolved

## Question

Graduated from the map's fog once `03` settled how `StartupApproved` actually behaves.

`03` recommended an `--unregister` path that deletes the Run value **and** writes a `03` disabled tombstone, following the Docker Desktop precedent it found on this machine. That recommendation quietly merges two different intentions, and they need separating before either is specified.

### The distinction to force

**(a) "Stop starting at login, but stay listed in Task Manager."**
Keep the Run value, write a disabled blob to `StartupApproved\Run`. This is exactly what Docker Desktop does — its Run value is present and its blob is `03` with a zeroed FILETIME, driven by `AutoStart = False` in its own settings file. The entry stays visible and the user can flip it back on from Task Manager.

**(b) "Remove yourself entirely."**
Delete the Run value. Task Manager then shows nothing at all, so the tombstone is pointless for visibility — though `03` proved it lingers as a durable tombstone regardless, which means a later reinstall's guard would see it and correctly decline to re-register.

These want different mechanisms and probably different triggers. Decide which the applet needs — possibly both, possibly neither.

### Questions

1. **Does deregistration exist at all in v1?** The alternative is that Task Manager is the *only* off-switch and the app never removes its own Run value. That is the smallest possible surface and consistent with this effort's standing preference for minimalism.
2. **If it exists, what triggers it?** A CLI flag (`--unregister`), a third context-menu item, or a documented manual `reg delete`. Note that a menu item would be the **third** growth of a menu the original brief specified as "only be able to terminate it" — Reload config was the first. Weigh that against convenience honestly rather than waving it through.
3. **Should the app ever write to `StartupApproved`?** This is the load-bearing question. Arguments against: the format is **undocumented by Microsoft** — `03`'s strongest source is an archived MSDN forum post — so the app would be writing a reverse-engineered binary structure into another application's registry territory, and a future Windows change makes that garbage. Arguments for: a live third-party precedent exists, and it is the only way to achieve semantic (a).
4. **What about `Run\AutorunsDisabled`?** If Autoruns has moved our value there, deregistration should not recreate confusion. Decide whether to detect and report, or ignore.
5. **Uninstall generally.** Beyond autostart: the config file (which the app never wrote, so arguably never removes), and whether an uninstall story is even in scope for a single-exe applet with no installer. This partly depends on `04`'s packaging answer.

## Recommended starting position, to be argued with

**Deregistration exists as a CLI flag only, and it uses semantic (b): delete the Run value, write nothing.** No menu item — the menu stops growing. No authoring of `StartupApproved` — the applet does not write undocumented binary formats into the registry to save the user one click in a UI they already have open. Semantic (a) is already available to the user directly in Task Manager, which is precisely where `01`/Q9 decided the control surface should live.

The counter worth taking seriously: this leaves the app unable to reflect its own "should I autostart?" state anywhere the user can see it, and if a future config key ever wants to govern autostart, semantic (a) becomes necessary.

## Outcome

A decision on 1–5, precise enough for `08` to encode. If deregistration is ruled out entirely, record that as a scope decision on the map rather than silently dropping it.

Use `/grilling`. Do not decide question 3 on the user's behalf — writing an undocumented registry format is a commitment with a real failure mode and deserves an explicit call.

Record the decision in this file under `## Answer`.

## Answer

**Deregistration does not exist in v1, and the applet never writes `StartupApproved` in any code
path.** The app's entire autostart footprint is one `REG_SZ` value it creates at most once. It
registers; it never governs. Governance is Task Manager's, permanently and exclusively.

### The finding that reshaped the ticket

The ticket's own recommended position — semantic (b), *delete the `Run` value, write nothing* — **is
self-defeating**, and neither the ticket nor `03` noticed. `03`'s guard registers when the `Run`
value is **absent**. So `--unregister` deletes the value and the **next launch puts it straight
back, enabled**. Semantic (b) only holds if the app never runs again — i.e. only as the last act
before the exe is deleted.

That is not a refinement of the (a)/(b) split; it dissolves it. The real choice was never (a) vs
(b). It was **write the tombstone, or have no deregistration at all** — and once `StartupApproved`
authoring was ruled out, only the second survived.

### Decisions

**1. Does deregistration exist at all?** **No.** Task Manager is the only off-switch, and it is a
complete one: a TM disable writes the tombstone, `03`'s guard rule 2 reads it, and the app never
re-registers — durably, across reinstalls and moved binaries, as `03` proved empirically. There is
no gap for a deregistration feature to fill.

The two candidate mechanisms were rejected on their merits, not by preference:

- **A `--unregister` CLI flag** does exactly what a documented `reg delete` does, and buys an
  argument parser to do it. Worse, `OutputType` is **`WinExe`**: the process has no console, so the
  flag can report **nothing** — PowerShell's prompt returns before the process even runs. A silent
  flag whose effect is invisible is not a feature.
- **A third context-menu item** was never seriously in play. The brief specified a menu that could
  "only terminate it"; Reload config was already the first growth. Deregistration does not clear the
  bar that Reload config cleared.

**2. Trigger.** Not applicable — see 1. The residue statement for `10` is below.

**3. Does the app ever write `StartupApproved`?** **Never. No exception, including uninstall.**

This was the load-bearing call and it was taken explicitly, as the ticket demanded. The format is
undocumented by Microsoft; `03`'s evidence for the *byte semantics* is an archived MSDN forum post,
and `03` graded that sub-question only **medium-high** while grading everything else high. That soft
link is precisely the part a *writer* depends on — a reader keying off value **presence** (which is
what the guard does) is insensitive to it. So the one place the research is weakest is the one place
writing would rely on it.

The Docker Desktop precedent was weighed and **rejected as non-transferable**. Docker writes the
blob because it has an in-app **AutoStart checkbox** and must keep the OS in agreement with its own
`settings-store.json`. It is syncing two sources of truth. We have no such toggle and the standing
minimalism preference says we never will — so there is nothing to sync, and writing the blob would
*create* a second source of truth rather than reconcile one.

This supersedes `03`'s "Follow-on for other tickets" recommendation.

**4. `Run\AutorunsDisabled`.** **Detect, never report.** `03`'s guard rule 3 stands unchanged — the
key is read so Autoruns' disable is honoured. Nothing is surfaced to the user.

**5. Uninstall generally.** **Out of `09`'s scope; it belongs to `10`** as the inverse of install.
`10` already owns install location and packaging, and its sub-question 5 already has to reconcile
with this ticket; reasoning about uninstall in both guarantees `08` inherits a contradiction. `09`
discharges its duty by handing `10` the exact residue — see *Follow-on* below.

### Additional decisions this ticket settles

**6. The app reports nothing about its own autostart state — and `03`'s stale-path surfacing rule is
retired.** `03` recommended that a `Run` value pointing at a *different* copy of the binary be
surfaced via "tooltip / balloon" (it never picked which). That rule is **overturned by explicit user
decision**. A *running* instance narrating its autostart state is telling the user about something
that is not wrong right now — they launched it, it is running. The tooltip carries persistent state
about the glyph; there is no log file by design. Both the Autoruns case and the stale-path case are
detected for the guard and are otherwise silent.

**7. Registration happens in Release builds only** (`#if !DEBUG`, or equivalent). Without this,
running the exe out of `bin\Debug\` registers a `Run` value pointing at build output that
`dotnet clean` deletes — and after decision 6, the app will never mention it. Every dev machine
would silently acquire a dead autostart entry, exactly once, permanently. The alternatives were
worse: path-sniffing for `bin\Debug` is a heuristic, and a first-run opt-in contradicts `01`/Q9.

**8. The `Run` value's exact shape.** Name `CalendarWeekTray`, type `REG_SZ`, data = the full
executable path **wrapped in double quotes**, **no arguments**.

- **Quote unconditionally**, even though the install directory `10` will recommend has no spaces —
  the user can drop the exe anywhere, and an unquoted path containing a space is the classic silent
  autostart failure.
- **No `--autostart` argument.** Nothing in the design needs to know it was launched at logon, and
  an unused argument is a key that has not earned its place.
- The path **must** come from **`Environment.ProcessPath`**, never `Assembly.Location` — under
  single-file publish (which `04` selected and `05` measured at 168.8 KB) `Location` returns an
  **empty string**, which would write an empty `Run` value. This is a trap, not a preference.

**9. A failed `Run` write is caught and swallowed silently** — and documented in the repo-root
`README.md` as a known limitation. `HKCU\...\Run` is writable by default but corporate policy can
lock it. The applet remains fully functional in that state; it simply will not autostart. A balloon
would fire at **every** launch on a policy-locked machine about a condition the user cannot fix —
that is a nag, not a diagnostic. The README entry is where the honesty lives instead.

**10. The guard fails closed.** Any exception on any of the three guard reads (`Run`,
`StartupApproved\Run`, `Run\AutorunsDisabled`) **aborts registration entirely**. The risk is
asymmetric: registering is the action that can override a user's expressed intent, whereas declining
to register is always recoverable and never surprising. When the app cannot tell what the user
wanted, it does nothing.

### Shape for `08`

Registration is a **one-shot startup act, not part of `07`'s `Reconcile()`**. It runs once during
construction and never again; it is not a re-render trigger and has no place in the reconcile loop.

The complete autostart specification is: on startup, in a Release build, inside a `try` that
swallows everything, read three registry values; if **all three are absent**, write one quoted
`REG_SZ` at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CalendarWeekTray`. There is no other
autostart code. No write to `StartupApproved`, no deregistration, no reporting, no repair of a stale
value.

### Follow-on for other tickets

- **`10`** — the residue an uninstall leaves is **exactly one value**:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CalendarWeekTray`. Removing it is one line:
  `reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v CalendarWeekTray /f`.
  If the user ever disabled the entry in Task Manager, a `StartupApproved\Run\CalendarWeekTray`
  tombstone also exists — `03` proved it survives deletion of the `Run` value and nothing collects
  it. Deliberately left behind: it is 12 bytes, it is Explorer's to own, and if the app is ever
  reinstalled it correctly causes the guard to decline to re-register. `10` decides whether any of
  this gets documented and where.
- **`15`** — registration is unreachable in a debug run (decision 7), so it **cannot** be covered by
  running the app. It needs a unit-level seam over the three guard reads. The fail-closed rule
  (decision 10) and the quoting rule (decision 8) are the cases worth testing; the `WinExe`
  no-console fact means there is no CLI surface to test at all.
- **`03`** — its stale-path surfacing recommendation and its `--unregister` follow-on are both
  superseded here; notes added to that file so `08` does not encode retired rules.

### Known unknown, deliberately not chased

How Task Manager's *Startup apps* tab displays an entry whose exe has been deleted was **not**
established. All six `Run` entries on this machine point at live binaries, so there was no local
evidence, and confirming it needs a human clicking. It affects only how untidy the left-behind value
looks after an uninstall — which is `10`'s call, not a blocker here.
