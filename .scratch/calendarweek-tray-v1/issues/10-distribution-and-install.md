# 10 — How does the applet get onto a machine?

Type: grilling
Status: open

## Question

Graduated from the map's fog once `04` settled the artifact's shape.

The thing being distributed is now known precisely: a framework-dependent single-file exe, **measured at 168.8 KB** by `05` against `04`'s 195 KB estimate, that requires the .NET 10 Desktop Runtime to be present. That is a very different distribution problem from a 110 MB self-contained binary, and it is what makes this question answerable now.

### Sub-questions

1. **Is distribution in scope for v1 at all?** The map's destination is a locked spec plus a validated prototype. A legitimate answer is "build it from source, `dotnet publish`, done" — which rules most of this out of scope rather than resolving it. Say so explicitly if that's the call.
2. **The runtime prerequisite.** 195 KB is only 195 KB because .NET 10 Desktop is assumed present. A zip cannot express that dependency; a winget manifest can declare it; nothing else will tell a user why the exe silently fails to start. How is this communicated — README, winget dependency, or a runtime check with a clear message?
3. **Code signing.** An unsigned exe from the internet gets a SmartScreen warning, and a certificate is a recurring cost and an ongoing commitment. In scope, or explicitly accepted as out?
4. **Updates.** Any mechanism at all, or manual replacement? A self-updater is a large surface for an applet this small, and it conflicts with the standing minimalism preference.
5. **Install location — this one has a cross-ticket consequence.** `03` decided the app never overwrites an existing Run value, even a stale one pointing at a moved binary, and surfaces the mismatch instead. That rule only behaves well if there's a *conventional* place the exe lives. If it can sit anywhere the user drops it, "stale Run value" becomes a routine state rather than an anomaly. Decide whether a canonical location (e.g. `%LOCALAPPDATA%\Programs\calendarweek-tray\`) is recommended, and reconcile the answer with `03`'s guard and `09`'s deregistration.

   **A second, independent argument for the same conclusion** arrived from `05`: Windows keys notification-area promotion by `ExecutablePath` under `HKCU\Control Panel\NotifyIconSettings`, so a moved binary silently loses whatever tray-visibility state the user had set. `11` is chasing the details; the install-location consequence is this ticket's to weigh.

## Recommended starting position, to be argued with

**Rule most of this out of scope for v1.** Ship source plus a documented `dotnet publish` one-liner and a zipped release; recommend — but do not enforce — a canonical install directory so `03`'s stale-value rule stays meaningful. No winget manifest, no code signing, no updater.

The reasoning is that signing and winget are *ongoing* commitments, not one-time work, and this is a personal applet whose entire brief is minimalism. The counter worth weighing: if this is ever meant for colleagues who want their KW in the tray, "clone and run `dotnet publish`" is a wall, and winget is the only answer that clears it without a certificate.

## Outcome

A decision on 1–5. If most of it is ruled out, record those as **Out of scope** entries on the map rather than leaving them as silent omissions from the spec — the distinction between "decided against" and "never considered" is the whole point of that section.

Sub-question 5 must produce an answer that `08` can reconcile with `03` and `09`; the three cannot contradict each other on where the binary lives and what a Run value pointing elsewhere means.

Use `/grilling`.

Record the decision in this file under `## Answer`.
