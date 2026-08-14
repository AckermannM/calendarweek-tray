using System.Globalization;

namespace CalendarWeekTray;

internal readonly record struct DesiredState(int Week, int SizePx, Color Ink, string Tooltip);

/// <summary>
/// The pure desired-state function (spec §6.1), separable from the <see cref="NotifyIcon"/> it
/// drives, so the ink rule — 13's one pure-logic defect this map ever found, an invisible icon
/// undetectable from <c>SystemUsesLightTheme</c> alone — can be asserted across all six theme states
/// with no shell. Every impure read — the clock, <c>SystemInformation.SmallIconSize</c>,
/// <c>SystemInformation.HighContrast</c>, the <c>Personalize</c> registry value — happens in the
/// caller and arrives as an argument. <see cref="Compute"/> calls nothing that touches the machine.
/// </summary>
internal static class TrayState
{
    internal static DesiredState Compute(
        DateTime now,
        int sizePx,
        bool highContrast,
        bool? systemUsesLightTheme,
        AppConfig config,
        string? configError)
    {
        int week = ISOWeek.GetWeekOfYear(now);
        int isoYear = ISOWeek.GetYear(now); // NOT now.Year — the ISO week-year differs at year boundaries.
        DateTime monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday);
        DateTime sunday = monday.AddDays(6);

        Language language = ConfigLoader.ResolveLanguage(config.Language);
        Color ink = ResolveInk(highContrast, systemUsesLightTheme, config.Theme);
        string tooltip = Strings.ComposeTooltip(language, week, monday, sunday, configError);

        return new DesiredState(week, sizePx, ink, tooltip);
    }

    /// <summary>
    /// Spec §5.5, verbatim. The conditional cannot be collapsed — <see cref="SystemColors"/> does
    /// not track dark theme at all — and high contrast must win outright, because
    /// <c>SystemUsesLightTheme</c> reads 0 under all four stock contrast themes, including High
    /// Contrast White, whose taskbar is not black.
    /// </summary>
    private static Color ResolveInk(bool highContrast, bool? systemUsesLightTheme, Theme theme)
    {
        if (highContrast)
        {
            return SystemColors.MenuText;
        }

        // Absent registry key ⇒ light ⇒ black ink — the documented Windows default for a machine
        // where SystemUsesLightTheme was never toggled.
        bool lightTaskbar = theme switch
        {
            Theme.Light => true,
            Theme.Dark => false,
            _ => systemUsesLightTheme ?? true,
        };

        return lightTaskbar ? Color.Black : Color.White;
    }
}
