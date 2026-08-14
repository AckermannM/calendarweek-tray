using System.Globalization;
using System.Text.Json;

namespace CalendarWeekTray;

/// <summary>
/// A config problem this ticket captures but does not surface. <see cref="LineNumber"/> is
/// <see cref="JsonException.LineNumber"/> verbatim — 0-based — and it is ticket 07's job to add 1 and
/// build the displayed diagnostic string (spec §9).
/// </summary>
internal readonly record struct ConfigFault(string Message, long? LineNumber);

internal readonly record struct ConfigLoadResult(AppConfig Config, ConfigFault? Fault);

/// <summary>
/// Locates and deserialises <c>config.json</c> (spec §3.3–§3.4). A missing file is not a fault and
/// yields <c>(auto, auto)</c>; a present-but-malformed file — invalid JSON, an unknown key, an
/// unknown value, or an unreadable file — falls back to the same defaults rather than stopping
/// startup, and carries the fault forward instead of failing.
/// </summary>
internal static class ConfigLoader
{
    private const string DirectoryName = "calendarweek-tray";
    private const string FileName = "config.json";

    internal static ConfigLoadResult Load() => Load(CandidatePaths());

    /// <summary>Resolution over an explicit path list, so tests never touch the real profile.</summary>
    internal static ConfigLoadResult Load(IEnumerable<string> candidatePaths)
    {
        foreach (string path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return LoadFrom(path);
            }
        }

        return new ConfigLoadResult(new AppConfig(), Fault: null);
    }

    internal static Language ResolveLanguage(Language language) => language switch
    {
        Language.Auto => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? Language.De : Language.En,
        _ => language,
    };

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DirectoryName, FileName);
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", DirectoryName, FileName);
    }

    private static ConfigLoadResult LoadFrom(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            AppConfig config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig)
                ?? new AppConfig();
            return new ConfigLoadResult(config, Fault: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ConfigLoadResult(new AppConfig(), new ConfigFault(ex.Message, LineNumber: null));
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult(new AppConfig(), new ConfigFault(ex.Message, ex.LineNumber));
        }
    }
}
