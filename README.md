# calendarweek-tray

A Windows 11 notification-area applet that displays the current ISO calendar week (`KW32`).

Windowless per-user background process. .NET 10, WinForms `NotifyIcon`, no third-party
dependencies. Right-click the icon for the menu; left-click does nothing.

## Build and run

```
dotnet build
dotnet run
```

## Configuration

`config.json` is optional — the applet runs correctly without it and never writes it.
Path and defaults are documented once the spec lands (ticket `08`).

## Status

Under design. Decisions live in `.scratch/calendarweek-tray-v1/`, indexed by `map.md`.
