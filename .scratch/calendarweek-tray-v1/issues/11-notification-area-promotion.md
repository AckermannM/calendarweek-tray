# 11 — Notification-area promotion: is it controllable, and what resets it?

Type: research
Status: open

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
