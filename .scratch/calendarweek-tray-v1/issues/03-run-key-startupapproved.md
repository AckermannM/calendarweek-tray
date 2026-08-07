# 03 — Does rewriting our Run key defeat the user's Task Manager disable?

Type: research
Status: resolved

## Question

The chosen autostart design (`01`, Q9) is: the app writes an `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value on first run so that Task Manager's *Startup apps* tab has something to show, and the user governs enable/disable from there.

There is an unresolved risk in this. When a user disables a startup entry in Task Manager, Windows does **not** delete the Run value — it records the disabled state in a separate binary blob under `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run`, keyed by the Run value's name.

**If the app rewrites its Run value on a later launch, does that reset the approval state and silently re-enable itself against the user's explicit wish?**

An applet that resurrects its own autostart after being told not to is malware behaviour. This must be settled before the design is locked.

Sub-questions:

1. Is the `StartupApproved\Run` entry keyed by value **name** only, or does it also encode a hash/timestamp of the value **data** that a rewrite would invalidate?
2. What does the blob's byte layout mean (the leading DWORD distinguishing enabled from disabled, and the trailing timestamp)?
3. Does writing an *identical* value preserve approval, while writing a *changed* path resets it? This matters because the app's path changes when it's moved or reinstalled.
4. Does deleting and re-creating the value behave differently from overwriting in place?

## Decision this unblocks

Choose one:

- **Self-register once, guarded** — write the Run value only when absent, and additionally check `StartupApproved` so a disabled-then-moved binary is never silently re-enabled.
- **Self-register only on genuine first run** — track "we have registered before" in the config directory rather than inferring it from the registry.
- **Never self-register** — ship a documented one-line command or a Startup-folder shortcut the user creates deliberately, so the app never writes to its own autostart at all.

Recommend one with reasoning; the spec (`08`) will encode it.

## Method

`StartupApproved` is not formally documented by Microsoft, so treat blog posts and reverse-engineering write-ups as secondary and **verify empirically on this machine**: create a throwaway Run entry, disable it in Task Manager, inspect the blob, rewrite the value, and observe whether it re-enables. Clean up the throwaway entry afterwards.

Record findings in this file under `## Answer`.

## Answer

**No. Rewriting our Run value does not reset the approval and cannot silently re-enable us.**
The `StartupApproved\Run` blob is keyed by value **name** only, is written **exclusively** by the
GUI startup managers (Task Manager / Settings) or by the application itself, and is never touched,
recomputed or garbage-collected in response to writes to the `Run` key. A disabled entry stays
disabled across an identical rewrite, a changed-path rewrite, and even a delete-and-recreate.

### Evidence

Sources are graded. `StartupApproved` has **no Microsoft reference documentation**; the closest to
primary is an archived MSDN forum answer from Microsoft community support ("Drake_Wu"), which is
*semi-official at best*. Everything else is **secondary** (reverse-engineering blog posts, forum
reports). The load-bearing evidence is therefore the **local empirical test**, which is primary.

**A. Live registry on this machine (primary).** `HKCU\...\Explorer\StartupApproved` contains `Run`
and `StartupFolder`; **`Run32` does not exist** here. Every blob is exactly **12 bytes**:

| Key | Value name | Blob | Reading |
|---|---|---|---|
| `Run` | `OneDrive`, `Microsoft.Lists`, `Teams`, `Adobe Acrobat Synchronizer`, `MicrosoftEdgeAutoLaunch_…` | `02 00 00 00` + 8 zero bytes | enabled |
| `Run` | `Docker Desktop` | `03 00 00 00` + 8 **zero** bytes | disabled, no timestamp |
| `StartupFolder` | `Send to OneNote.lnk` | `03 00 00 00 6B C2 4A 1D 1E 0D DD 01` | disabled, FILETIME = **2026-07-06T08:04:48Z** |

The `Send to OneNote.lnk` trailer decodes as a clean, plausible local FILETIME, which confirms
*trailing QWORD = UTC FILETIME of the moment the entry was disabled*, zeroed when enabled.

The `Docker Desktop` row is independently informative: `%APPDATA%\Docker\settings-store.json`
contains `AutoStart = False`, and Docker's blob carries the disabled flag with a **zeroed**
timestamp — unlike the Task-Manager-written one. That is a live, third-party precedent for an
application writing its own `StartupApproved` blob from an in-app toggle, and it shows apps that
do so tend not to bother filling the FILETIME.

**B. Local probe experiment (primary).** A throwaway `HKCU\...\Run\zz-claude-research-probe` value
was created, given a fabricated Task-Manager-style disabled blob (`03` + FILETIME-now), then
subjected to every mutation the app could plausibly perform. The blob was re-read after each step:

| Step | Run value data afterwards | `StartupApproved\Run` blob afterwards |
|---|---|---|
| create Run value | `C:\probe\a.exe --one` | *absent* — nothing auto-creates it |
| fabricate disable | `C:\probe\a.exe --one` | `03 00 00 00 67 70 AB 0E 61 26 DD 01` |
| **rewrite identical data** | `C:\probe\a.exe --one` | **unchanged** |
| **rewrite changed path** | `D:\elsewhere\b.exe --two` | **unchanged** |
| **delete Run value** | *absent* | **unchanged** (tombstone survives) |
| **re-create Run value** | `D:\elsewhere\b.exe --two` | **unchanged** |
| enumerate via `Win32_StartupCommand` | `D:\elsewhere\b.exe --two` | **unchanged** |

Both throwaway values were deleted afterwards and the six real `Run` / six real `StartupApproved\Run`
entries were verified byte-identical to the pre-test snapshot. **No real application's entries were
modified at any point.**

Note the WMI row: `Win32_StartupCommand` happily reported the probe while it was flagged disabled.
It reads the `Run` key and **ignores `StartupApproved`** — so it is useless as a "am I enabled?"
oracle and must not be used by the app.

**C. Secondary sources, consistent with the above.**
- Harlan Carvey's reverse-engineering write-ups: `02` = enabled, `03` = disabled, remaining QWORD is
  the FILETIME of disabling; entries appear **only** when created by a GUI startup manager or an
  installer; deleting the `Run` value leaves the `StartupApproved` entry behind; adding a `Run` value
  via `regedit`/`reg.exe` creates no `StartupApproved` entry and defaults to enabled.
  ([pt II](http://windowsir.blogspot.com/2022/07/startupapprovedrun-pt-ii.html),
  [Does "Autostart" Really Mean "Autostart"?](http://windowsir.blogspot.com/2022/07/does-autostart-really-mean-autostart.html))
- Archived MSDN forum, Microsoft community support: *"we usually set the first binary bit to
  0x02 (enable) / 0x03 (disable)"*, and the least-significant bit of the first byte is the
  on/off bit — i.e. **odd = disabled**, which is why `06`/`07` also appear in the wild.
  ([thread](https://learn.microsoft.com/en-us/archive/msdn-technet-forums/b4c9f990-ecb5-46e1-9711-df72bb896f31))
- Sysinternals forum, answered by MarkC(MSFT): confirms the flag is genuinely **enforced at logon**
  for `HKCU\...\Run` — *"After disabling an application via the Task Manager … the program isn't
  executed at startup"* — while the `Run` value itself remains untouched. Also documents that
  **Autoruns disables differently**: it *moves* the value into a `Run\AutorunsDisabled` subkey and
  leaves `StartupApproved` alone.
  ([thread](https://learn.microsoft.com/en-us/archive/msdn-technet-forums/f2a2b59b-aa59-46de-922c-342fbdaf6d8c))
- [renenyffenegger.ch](https://renenyffenegger.ch/notes/Windows/registry/tree/HKEY_CURRENT_USER/Software/Microsoft/Windows/CurrentVersion/Explorer/StartupApproved/Run/index) — keyed by value name, `02`/`06` enabled, other values disabled. Explicitly hedged ("seems to indicate").

### Sub-questions, answered

1. **Name only.** The 12-byte blob is a flag plus a timestamp. There is no hash, no length, no copy
   of the value data — there is not enough room for one, and the changed-path rewrite left the blob
   bit-identical. A rewrite cannot invalidate it.
2. **Layout: `DWORD flags` + `FILETIME disabledAtUtc`.** `0x02` = enabled, `0x03` = disabled; the
   real discriminator is **bit 0 (odd = disabled)**, which is why `0x06`/`0x07` occur. The trailing
   FILETIME is the UTC instant of disabling and is all-zero when enabled (and, per the Docker
   Desktop observation, sometimes all-zero even when disabled, if a third-party app wrote it).
   **Treat "value present and `blob[0] & 1` set" as disabled; do not test for `0x03` exactly.**
3. **Both preserve approval.** Identical rewrite and changed-path rewrite are indistinguishable to
   `StartupApproved`. A moved or reinstalled binary keeps the user's disable.
4. **No difference.** Delete-and-recreate behaves identically to overwrite-in-place. The
   `StartupApproved` entry is a **durable tombstone**: it outlives deletion of the `Run` value
   entirely, and nothing observed garbage-collects it.

### Consequence for the design

The premise that motivated this ticket is false, and one important corollary falls out of it:
**a Task Manager disable does not delete the `Run` value.** So a rule of "create the `Run` value only
when it is absent" is *by itself* already immune to the malware behaviour the ticket feared — after
a disable, the value is still present, so the app never rewrites it, and even if it did, the disable
would hold.

Two residual holes remain, and neither is closed by `StartupApproved` alone:

- **Autoruns disable** moves the value to `HKCU\...\Run\AutorunsDisabled` and leaves no
  `StartupApproved` trace. A naive "absent ⇒ register" app would resurrect itself, enabled.
  (`Run\AutorunsDisabled` does not currently exist on this machine.)
- **A user deleting the `Run` value in regedit** having never touched Task Manager leaves no recorded
  intent at all. Nothing can distinguish that from a genuine first run.

## Recommendation

**Adopt "Self-register once, guarded" — the first option — with the guard specified as three registry
reads and no written state outside the `Run` key.**

On startup, create `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CalendarWeekTray` **only if
all three hold**:

1. `HKCU\...\Run\CalendarWeekTray` is absent, **and**
2. `HKCU\...\Explorer\StartupApproved\Run\CalendarWeekTray` is absent
   (present ⇒ the user has governed this entry before; honour it, whatever the flag says), **and**
3. `HKCU\...\Run\AutorunsDisabled\CalendarWeekTray` is absent (respects Sysinternals Autoruns).

And these absolute rules:

- **Never write to `StartupApproved` on startup.** Not to enable, not to "repair". The app reads it
  and never authors it during normal operation.
- **Never overwrite an existing `Run` value** at startup, even a stale one pointing at a moved
  binary. If the value exists but its path is not ours, surface it (tooltip / balloon) — do not
  silently correct it. Silent correction is exactly the behaviour that makes this class of app
  distrusted, and the correction is unnecessary: the running instance already knows where it is.

Reasoning against the alternatives:

- **"Only on genuine first run" via a marker file** is *worse*, not safer. It adds written state to a
  project whose standing preference is that the app never writes files, and it desyncs immediately —
  wipe the config dir and the app resurrects; keep it and reinstall, and the app never registers
  again. The registry already holds a more durable and more honest record of user intent
  (`StartupApproved` survives deletion of the `Run` value, as proven above), so a second copy of
  that state buys a liability and no information.
- **"Never self-register"** is defensible and is the only fully-zero-risk option, but it discards the
  decision already taken in `01`/Q9: the entire point of the `Run` value is that Task Manager's
  *Startup apps* tab has something to show, giving the user a native, discoverable governance
  surface. Pushing that onto a documented manual step means most users never get it. Now that the
  resurrect risk is measured and absent, the cost of the guarded option is three registry reads.

Confidence: **high** on sub-questions 1, 3 and 4 and on the recommendation — these rest on a
reproducible local experiment, not on the undocumented format holding. **Medium-high** on the exact
bit semantics of sub-question 2 (bit 0 = disabled), which rests on the semi-official MSDN answer plus
corroborating live data; the recommendation is deliberately insensitive to it, since the guard keys
off *presence* of the value rather than its contents.

### Follow-on for other tickets

The **uninstall story** (open in `map.md`) now has a clean shape: a `--unregister` switch deletes the
`Run` value **and** writes a disabled tombstone `03 00 00 00` + zero FILETIME into
`StartupApproved\Run` — precisely what Docker Desktop does on this machine. That makes unregistering
stick against guard rule 2 on the next launch, with a 12-byte footprint and no files. This is the
*only* place the app should ever write `StartupApproved`, and it is a deliberate, user-initiated act.

## Outstanding human verification

Everything above is settled by the local experiment except one link that cannot be exercised without
clicking: that Task Manager, when *it* writes the disable, does not additionally do something to the
`Run` value, and that the disable visibly survives our rewrite in Task Manager's own UI. Expected
result is "nothing changes"; this is a confirmation, not an open question.

**Part 1 — read-only sanity check on the decode (20 seconds, changes nothing).**

1. Open Task Manager → **Startup apps**.
2. Confirm **Docker Desktop** shows **Disabled** and **OneDrive**, **Teams**, **Microsoft Lists**,
   **Adobe Acrobat Synchronizer** show **Enabled**.
   *This validates `03` = disabled / `02` = enabled against this exact machine.* If Docker Desktop
   shows Enabled, the whole decode is wrong and this ticket must be reopened.

**Part 2 — the rewrite test (about 90 seconds).** Both commands below use real, harmless binaries
(`rundll32.exe` with no arguments does nothing), and the entry is disabled before it could ever run.

3. Run in PowerShell:
   ```powershell
   reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v zz-claude-research-probe /t REG_SZ /d "C:\Windows\System32\rundll32.exe" /f
   ```
4. Open Task Manager → **Startup apps**. Find **zz-claude-research-probe** (it will show as
   *Enabled*). Right-click → **Disable**. Leave Task Manager open.
5. Run in PowerShell — this rewrites the `Run` value with a **different path**, i.e. simulates the
   app being moved or reinstalled and re-registering itself:
   ```powershell
   reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v zz-claude-research-probe /t REG_SZ /d "C:\Windows\SysWOW64\rundll32.exe" /f
   reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" /v zz-claude-research-probe
   ```
6. **What to look for — two things:**
   - The `reg query` output must print a blob beginning **`03000000`**. If it prints `02000000`, or
     the value is reported as not found, the rewrite reset the approval and **this ticket's answer is
     wrong**.
   - Switch back to Task Manager, press **F5** (or close and reopen it) and confirm
     **zz-claude-research-probe still shows *Disabled*.**
7. Clean up — run both lines, then confirm neither prints anything but "not found" on a re-query:
   ```powershell
   reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v zz-claude-research-probe /f
   reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" /v zz-claude-research-probe /f
   ```
