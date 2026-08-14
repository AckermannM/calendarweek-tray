using Microsoft.Win32;

namespace CalendarWeekTray;

/// <summary>
/// Spec §8.2: the applet registers itself to start at logon, once, ever — it never governs. The
/// entire footprint is one <c>REG_SZ</c> value, and there is no deregistration code anywhere in
/// the tree; Task Manager is the only off-switch.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string AutorunsDisabledKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\AutorunsDisabled";
    private const string ValueName = "CalendarWeekTray";

    /// <summary>
    /// Release builds only — a Debug run out of <c>bin\Debug</c> would otherwise write a
    /// <c>Run</c> value pointing at build output <c>dotnet clean</c> deletes, and since this
    /// reports nothing about its own outcome, every dev machine would silently acquire one dead
    /// startup entry, exactly once, permanently.
    /// </summary>
    public static void Register()
    {
#if !DEBUG
        try
        {
            // Environment.ProcessPath, never Assembly.Location — under single-file publish
            // Location returns an empty string, which would write an empty Run value. A null
            // ProcessPath is guarded here too: string concatenation would silently turn it into
            // "" rather than throw, which the catch below could not have caught.
            if (Environment.ProcessPath is string processPath
             && Registry.GetValue(RunKey, ValueName, defaultValue: null) is null
             && Registry.GetValue(StartupApprovedKey, ValueName, defaultValue: null) is null
             && Registry.GetValue(AutorunsDisabledKey, ValueName, defaultValue: null) is null)
            {
                // Quoted unconditionally, with no arguments: an unquoted path containing a space
                // is the classic silent autostart failure, and nothing here needs to know it was
                // launched at logon.
                Registry.SetValue(RunKey, ValueName, "\"" + processPath + "\"", RegistryValueKind.String);
            }
        }
        catch
        {
            // Fails closed on a read and swallows a failed write, both silently (documented in
            // README.md instead): HKCU\...\Run is writable by default but corporate policy can
            // lock it, in which case the write fails at every launch, and a balloon there would
            // be a nag, not a diagnostic. An existing value — even a stale one pointing at a
            // moved binary — is never overwritten, and none of this is ever reported.
        }
#endif
    }
}
