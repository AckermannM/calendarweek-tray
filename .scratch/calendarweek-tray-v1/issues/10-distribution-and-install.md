# 10 — How does the applet get onto a machine?

Type: grilling
Status: resolved

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

## Note from `09` (resolved) — uninstall is this ticket's, and here is the residue

`09` ruled **general uninstall out of its own scope and into this one**, as the inverse of install: exe removal, config-file removal, and whether a documented uninstall procedure exists at all are all yours. `09` owns only the autostart footprint, and hands you a precise statement of it:

- The residue is **exactly one value**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CalendarWeekTray`. One line removes it:
  `reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v CalendarWeekTray /f`
- If the user ever disabled the entry in Task Manager, a `StartupApproved\Run\CalendarWeekTray` tombstone also exists. `03` proved it survives deletion of the `Run` value and nothing garbage-collects it. `09` **deliberately leaves it**: 12 bytes, Explorer's to own, and on a reinstall it correctly makes the guard decline to re-register.
- **There is no `--unregister` flag and no deregistration code at all** — `09` removed it from v1. Any uninstall procedure you document must therefore be a manual `reg delete`, not an app invocation. Note `OutputType` is `WinExe`: the applet has no console and can never report anything from a command line.

Two consequences for **sub-question 5** specifically:

- `09` **retired** `03`'s "surface a stale Run value" rule — the applet now reports *nothing* about its autostart state. That removes the safety net that made a non-canonical install location tolerable: a moved binary now leaves a dead Run value that nothing anywhere will ever mention. The argument for recommending a canonical directory is correspondingly **stronger** than when this ticket was written.
- One known unknown `09` chose not to chase, because it is yours: how Task Manager's *Startup apps* tab displays an entry whose exe has been deleted. All six `Run` entries on this machine point at live binaries, so there was no local evidence to read. It bears only on how untidy the left-behind value looks.

Use `/grilling`.

Record the decision in this file under `## Answer`.

## Answer

**Distribution is a zipped exe on a GitHub Release, and everything else is documentation.** No
winget manifest, no code signing, no updater, no installer. The canonical install directory is
**recommended and never enforced**; uninstall is a documented manual procedure. The whole of this
ticket's surface is `README.md` plus a `<Version>` property.

### The finding that reshaped the ticket

**Sub-question 2's premise is false, and it was testable.** The ticket asserts that "nothing else
will tell a user why the exe silently fails to start". It does not silently fail. Measured against
the actual ship artifact — single-file, framework-dependent, `win-x64` — with `DOTNET_ROOT` pointed
at an empty directory so the host can find no framework at all:

```
Architecture: x64          App host version: 10.0.9
Learn more:    https://aka.ms/dotnet/app-launch-failed
Download link: https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=win-x64&os=win10&apphost_version=10.0.9&gui=true
[ Download it now ]  [ &Close ]
```

A GUI task dialog with a **one-click "Download it now" button** landing on the correct .NET
download page. The `gui=true` in that URL is the apphost detecting the WINDOWS subsystem: the
console-less `WinExe` that `09` correctly noted "can never report anything from a command line"
still gets a *dialog*, because this error is raised by the native host **before** any managed code
exists and before the subsystem constraint applies. The same test against a wrong-version framework
(runtimeconfig hand-edited to `99.0.0` on a non-single-file publish) names the requirement exactly:
`Required: 'Microsoft.NETCore.App', version '99.0.0' (x64)`.

So the runtime prerequisite is **self-communicating, loudly and actionably, with no code from us**.
That collapses sub-question 2 from a design problem to a single README line, and it removes the
strongest argument for a winget manifest — winget's declared dependency was going to be the only
thing that explained the failure, and it turns out the failure explains itself.

An in-app runtime check was never possible in any case: managed code cannot run in order to detect
that the managed runtime is absent.

### Decisions

**1. Is distribution in scope for v1?** **Partly — a zipped exe on a GitHub Release, and nothing
more.** The ticket's recommended position ("rule most of this out of scope; ship source plus a
`dotnet publish` one-liner") was rejected as *slightly* too austere, on evidence already in the
repo: `origin` is a public GitHub remote, and `README.md`'s Known-limitations section is written to
a reader who is not the author ("If you expect the applet to start automatically and it does not,
check whether the `Run` value above exists"). That is documentation for a stranger, and the source
-only answer contradicts it.

The deciding property is that a Release zip costs **once** and never again — a `dotnet publish` and
a drag-and-drop. Every option above it (winget, signing, updater) converts this into a *maintained*
package with a recurring obligation, which is exactly what the standing minimalism preference
exists to refuse. Every option below it makes a .NET SDK the price of entry for a 194 KB applet.

**2. The runtime prerequisite.** **README states it and names the winget one-liner; the apphost
dialog is the safety net.** `winget install Microsoft.DotNet.DesktopRuntime.10` — verified present
in the winget catalogue at `10.0.11`, the same build `07` measured on this machine — is a
copy-pasteable fix, strictly better than the dialog's download-page detour for anyone who reads
first. The dialog catches everyone who doesn't.

One nuance for `08`: if a user has the .NET 10 **console** runtime but not **Desktop**, the dialog
names `Microsoft.WindowsDesktop.App` rather than `Microsoft.NETCore.App`. Both paths self-describe
correctly, so the README does not need to enumerate the cases.

**3. Code signing.** **Out of scope, and documented rather than silently omitted.** Mark of the Web
propagates from the downloaded zip into the extracted exe, so a colleague gets *"Windows protected
your PC"* → **More info** → **Run anyway**.

Rejected on merit, not on cost alone: an OV certificate is a recurring annual charge **and** still
has to accrue SmartScreen reputation across downloads before the warning stops — reputation an
applet with three users will never earn. Only an EV certificate clears it immediately, at a higher
price and with a hardware token. Paying yearly for a warning that persists anyway is the worst
available outcome. The mitigation that actually works is one README line naming the exact
click-path: a reader who is *told* to expect "More info → Run anyway" is not blocked by it; a reader
who is not, is.

**4. Updates.** **No mechanism at all. Manual replacement, documented.**

A self-updater is a version check, a download path, a file-swap that must survive the exe holding
its own lock, and a failure mode on each — bolted onto an applet whose brief is minimalism. The
middle option considered and also rejected was a **passive** check (fetch the latest release tag,
mention it in the tooltip or a balloon): it would give the applet a network dependency it currently
does not have, in order to report a version the user would still install by hand. New category of
behaviour, no reduction in manual work.

The procedure is three steps, and step 2 is load-bearing:

1. **Quit** from the tray menu — the running exe holds a file lock on itself.
2. Replace the exe **in place**, at the same path.
3. Run it.

"In place" is where decision 5 pays off. The same path preserves both the `Run` value and the
`NotifyIconSettings` promotion `11` is chasing. **An update that moves the binary silently breaks
both** — which makes the canonical directory an update-correctness rule, not merely tidiness.

**5. Install location.** **`%LOCALAPPDATA%\Programs\CalendarWeekTray\`, recommended and never
enforced.**

*Why a canonical location at all* — two independent silent-failure arguments, neither of which the
applet will ever mention:

- `09` **retired** `03`'s "surface a stale Run value" rule. The applet now reports **nothing** about
  its autostart state, so a moved binary leaves a dead `Run` value that nothing anywhere will ever
  surface. The safety net that made a free-floating install tolerable is gone.
- `11`: notification-area promotion is keyed by `ExecutablePath` under
  `HKCU\Control Panel\NotifyIconSettings`, so a moved exe silently loses whatever tray-visibility
  state the user set by hand.

*Why recommended and not enforced.* Enforcement means the applet reads `Environment.ProcessPath` at
startup and complains, or refuses, when it is elsewhere. That is **governing**, and `09` established
the opposite posture in the term the glossary now carries: *the applet registers, it never governs*.
Enforcement would also need a message surface and would add a startup failure mode to an applet
whose entire diagnostic story is a balloon tip. A user who drops the exe on the Desktop gets a
working applet with a fragile `Run` value, and that is their business.

*Why that path.* `%LOCALAPPDATA%\Programs` is per-user and needs no elevation, which matches the
per-user `Run` key (`03`) and per-user config resolution. It is not hypothetical: 12 applications on
this machine already install there (VS Code, Obsidian, KeePass, Git, Bruno, …). `CalendarWeekTray`
over the repo slug `calendarweek-tray` so that the **folder name, the exe name, and the `Run` value
name are one string** — a user hunting the residue reads the same word three times.

*Reconciliation with `03` and `09`, as the ticket demanded.* The three do not contradict:

- `03` — guard on three registry reads, write at most once, never overwrite an existing value.
- `09` — no deregistration code exists; `StartupApproved` is never written; the applet reports
  nothing about its autostart state.
- `10` — a recommended path makes "the `Run` value points somewhere else" the **anomaly** it was
  assumed to be, rather than the routine state it becomes if the exe can live anywhere. Nothing in
  the applet's code changes as a result. The convention lives entirely in `README.md`, which is
  precisely why it does not reopen `03` or `09`.

**6. Uninstall.** **A documented manual procedure in `README.md`.**

`09` handed this over with a precise residue statement, and the reason to document it is that
**the residue is invisible**. Deleting a folder is self-evident; a `Run` value is the one thing a
user can neither guess at nor see, and after the exe is deleted it becomes a startup entry pointing
at nothing. The procedure:

1. **Quit** from the tray menu.
2. Delete `%LOCALAPPDATA%\Programs\CalendarWeekTray\`.
3. `reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v CalendarWeekTray /f`
4. Delete `config.json` if you created one — `%APPDATA%\calendarweek-tray\` or
   `~/.config/calendarweek-tray/` (`14` may still move this).

There is no `--unregister` flag and no deregistration code (`09` deleted the feature), and
`OutputType` is `WinExe`, so step 3 **must** be a manual `reg delete` rather than an app invocation.

The `StartupApproved\Run\CalendarWeekTray` tombstone — present only if the entry was ever disabled
in Task Manager — is **deliberately left**, per `09`: 12 bytes, Explorer's to own, and on a
reinstall it correctly makes the guard decline to re-register. The README says so explicitly, so its
survival reads as intent rather than as a bug.

*On `09`'s known unknown* — how Task Manager renders a `Run` entry whose exe has been deleted. Still
unmeasured, and **defused rather than answered**: the documented procedure removes the value, so the
question only bites a user who skipped step 3. Establishing the fact would have required writing a
bogus `Run` value to the user's live hive to look at it, which is not worth it for a cosmetic
detail on a path we now tell people not to take.

**7. What a release is, concretely.**

- **Zip contains `CalendarWeekTray.exe` alone.** The publish also emits a **22.9 KB `.pdb`** — drop
  it. There is no log file (`01`), no crash reporting, and no support channel to send a stack trace
  to, so shipping symbols serves nobody; the matching build is available locally if it is ever
  needed. The README is excluded too: it is one click away on the repo the user just downloaded
  from, and a stale copy inside a zip is worse than no copy.
- **Add `<Version>` to the csproj and tag releases `v1.0.0`.** The project currently has **no
  `Version` property**, so the exe reports `1.0.0` by default and every release would be
  indistinguishable in file properties. Decision 4's "replace the exe in place" is a procedure with
  no way to verify you already performed it. `<Version>` is the smallest change that makes it
  checkable — right-click the exe → Details.

### Facts measured while resolving this

- **The single-file artifact measures 194.3 KB**, not `05`'s 168.8 KB — published as
  `-c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`. `04`'s whole
  framework-dependent-vs-AOT argument rested on that order of magnitude and is unaffected (194 KB vs
  36–54 MB), but `08` should quote the number it can reproduce. The gap is presumably a publish-flag
  difference between the two runs; not chased, because nothing depends on which is right.
- **`Microsoft.DotNet.DesktopRuntime.10` is in the winget catalogue at `10.0.11`** — the exact build
  `07` recorded. Relevant even though we ship no manifest: it makes the README's install line a
  verified copy-paste rather than a guess.
- **The missing-runtime dialog is raised by the native apphost**, before managed code exists — which
  is why a `WinExe` still gets a GUI surface. Worth recording because `09` established that the
  applet itself can never report from a command line, and that constraint does **not** extend to
  host-level launch failures.

### Follow-ons for `08`

`README.md` is the entire deliverable of this ticket, and it owes four sections beyond what `08`
already lists at *Must contain* item 9:

1. **Prerequisite** — .NET 10 Desktop Runtime, with `winget install Microsoft.DotNet.DesktopRuntime.10`.
2. **Install** — download the zip from Releases, extract to
   `%LOCALAPPDATA%\Programs\CalendarWeekTray\`, expect *"Windows protected your PC" → More info →
   Run anyway*.
3. **Update** — quit, replace in place, run; with the reason the path must not change.
4. **Uninstall** — the four steps above, including the deliberately-left tombstone.

Also: add `<Version>` to `CalendarWeekTray.csproj`, and quote **194.3 KB** (or a re-measured figure)
rather than `05`'s 168.8 KB.
