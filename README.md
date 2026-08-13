# calendarweek-tray

A Windows 11 notification-area applet that displays the current ISO calendar week (`KW32`).

Windowless per-user background process. .NET 10, WinForms `NotifyIcon`, no third-party
dependencies. Right-click the icon for the menu; left-click does nothing.

## Requirements

The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). The applet ships
as a framework-dependent single file of roughly 200 KB, which is only that small because the runtime
is assumed present.

```
winget install Microsoft.DotNet.DesktopRuntime.10
```

If it is missing, the applet does not fail silently — Windows shows a dialog naming the runtime it
needs, with a **Download it now** button.

## Install

Download the zip from Releases and extract `CalendarWeekTray.exe` to:

```
%LOCALAPPDATA%\Programs\CalendarWeekTray\
```

That directory is a **recommendation, not a requirement** — the applet never checks where it is
running from. But two things are keyed to the exe's path and both break silently if it moves: the
autostart entry (see *Known limitations*), and the notification-area setting that controls whether
the icon is always shown or hidden in the overflow flyout. Keeping the exe at one stable path is
what keeps those working.

The exe is not code-signed, so the first run shows **"Windows protected your PC"**. Choose
**More info** → **Run anyway**.

## Build and run

From source, if you have the .NET 10 SDK:

```
dotnet build
dotnet run
```

## Configuration

`config.json` is optional — the applet runs correctly without it and never writes it.
Path and defaults are documented once the spec lands (ticket `08`).

## Update

There is no updater and no version check — the applet makes no network calls at all.

1. **Quit** from the tray menu. The running exe holds a lock on itself.
2. Replace `CalendarWeekTray.exe` **in place**, at the same path.
3. Run it.

Step 2 matters: extracting the new version to a different folder is the "moved binary" case above,
and it silently loses both your autostart entry and your notification-area setting. To check which
version you have, right-click the exe → **Properties** → **Details**.

## Uninstall

There is no uninstaller, and the applet has no command-line interface to invoke.

1. **Quit** from the tray menu.
2. Delete `%LOCALAPPDATA%\Programs\CalendarWeekTray\`.
3. Remove the autostart entry:
   ```
   reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v CalendarWeekTray /f
   ```
4. Delete `config.json` if you created one (see *Configuration*).

Step 3 is the only part you cannot see: deleting the folder leaves a startup entry pointing at
nothing, and the applet will never mention it.

If you ever disabled the applet in Task Manager, Windows also holds a small approval record under
`StartupApproved`. **Leave it.** It belongs to Explorer, it is a few bytes, and it is what makes a
later reinstall correctly stay disabled rather than switching itself back on.

## Known limitations

**Autostart may silently fail on a managed machine.** On first run the applet adds itself to
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` so it appears in Task Manager's *Startup apps*
tab, where you can enable or disable it. That key is writable by default, but corporate policy can
lock it — and when the write fails, the applet **says nothing**. It still works normally; it just
will not start at logon, and there is no message telling you so.

The silence is deliberate: on a locked-down machine the write fails on *every* launch, and a warning
you cannot act on shown every time you log in is a nag rather than a diagnostic. If you expect the
applet to start automatically and it does not, check whether the `Run` value above exists.

Turning autostart off is Task Manager's job — the applet has no uninstall or unregister command, and
never removes its own entry.

## Status

Under design. Decisions live in `.scratch/calendarweek-tray-v1/`, indexed by `map.md`.
