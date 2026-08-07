# 09 — How does autostart get turned off, and does the app ever author an approval blob?

Type: grilling
Status: open

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
