using System.Globalization;

namespace CalendarWeekTray;

/// <summary>
/// The spec §10.2 string table, verbatim in both languages, and the §10.3 date-range composition
/// rule. Pure string work only — month names come from <see cref="CultureInfo.GetCultureInfo(string)"/>
/// with an explicitly named culture, never <see cref="CultureInfo.CurrentUICulture"/> (spec §10.1:
/// <c>language</c> is picked by the config precisely so it can overrule the OS, and a tooltip that
/// half-followed the OS anyway would defeat the key's only purpose).
/// </summary>
internal static class Strings
{
    /// <summary>
    /// Bare <c>U+26A0</c>, with no <c>U+FE0F</c> variation selector. Segoe UI does not contain
    /// U+26A0 at all and renders it through fallback to Segoe UI Symbol as a clean monochrome
    /// triangle; adding the variation selector measured pixel-identical under GDI and risks pulling
    /// colour emoji into an otherwise monochrome tooltip (spec §10.2).
    /// </summary>
    internal const string WarningMarker = "⚠";

    private const string Separator = " · ";
    private const int MaxTooltipLength = 127;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    internal static string MenuReload(Language language) =>
        language == Language.De ? "Konfiguration neu laden" : "Reload configuration";

    internal static string MenuQuit(Language language) =>
        language == Language.De ? "Beenden" : "Quit";

    internal static string BalloonTitle => "CalendarWeekTray";

    internal static string RenderFault(Language language) =>
        language == Language.De ? $"{WarningMarker} Symbolfehler" : $"{WarningMarker} icon rendering failed";

    /// <summary>
    /// The balloon body is this string with the leading marker removed — the balloon already paints
    /// <see cref="ToolTipIcon.Warning"/> (spec §9).
    /// </summary>
    internal static string ConfigFault(Language language, long? lineNumber)
    {
        bool german = language == Language.De;
        return lineNumber switch
        {
            long line when german => $"{WarningMarker} {ConfigLoader.FileName} ungültig (Zeile {line})",
            long line => $"{WarningMarker} {ConfigLoader.FileName} invalid (line {line})",
            null when german => $"{WarningMarker} {ConfigLoader.FileName} ungültig",
            null => $"{WarningMarker} {ConfigLoader.FileName} invalid",
        };
    }

    /// <summary>
    /// The spec §10.3 composition rule: the right-hand date is emitted in full — day, month, year.
    /// The left-hand date drops every trailing component it shares with the right, and the en dash
    /// is unspaced iff that leaves the left side a bare day.
    /// </summary>
    internal static string DateRange(Language language, DateTime monday, DateTime sunday)
    {
        CultureInfo culture = language == Language.De ? German : English;
        bool bareDay = monday.Year == sunday.Year && monday.Month == sunday.Month;
        bool sameYear = monday.Year == sunday.Year;

        string left = bareDay
            ? FormatDay(monday.Day, language)
            : sameYear
                ? $"{FormatDay(monday.Day, language)} {MonthName(culture, monday)}"
                : $"{FormatDay(monday.Day, language)} {MonthName(culture, monday)} {YearOf(monday)}";

        string right = $"{FormatDay(sunday.Day, language)} {MonthName(culture, sunday)} {YearOf(sunday)}";
        string dash = bareDay ? "–" : " – ";

        return $"{left}{dash}{right}";
    }

    /// <summary>
    /// <c>prefix · range</c>, plus <c>· fault</c> when one is live (spec §10.4), truncated at 127
    /// characters — the real <see cref="NotifyIcon.Text"/> limit, not the 63 spec §4.3 originally
    /// assumed. The whole string is composed in one language, never mixed.
    /// </summary>
    internal static string ComposeTooltip(Language language, int week, DateTime monday, DateTime sunday, string? fault)
    {
        string prefix = language == Language.De ? $"Kalenderwoche {week}" : $"Calendar week {week}";
        string text = $"{prefix}{Separator}{DateRange(language, monday, sunday)}";
        return fault is null ? Truncate(text) : Truncate($"{text}{Separator}{fault}");
    }

    /// <summary>
    /// Appends a fault to an already-composed tooltip and truncates it the same way
    /// <see cref="ComposeTooltip"/> does. <c>Reconcile()</c>'s exception path (spec §6.2) has no
    /// week or date to recompose the tooltip from — only the last one that rendered successfully,
    /// which is <see langword="null"/> if the very first reconcile is what failed.
    /// </summary>
    internal static string AppendFault(string? tooltip, string fault) =>
        Truncate(string.IsNullOrEmpty(tooltip) ? fault : $"{tooltip}{Separator}{fault}");

    /// <summary>The balloon body is the matching fault string with the leading marker removed — the
    /// balloon already paints <see cref="ToolTipIcon.Warning"/> (spec §9).</summary>
    internal static string BalloonBody(string fault) =>
        fault.StartsWith($"{WarningMarker} ", StringComparison.Ordinal) ? fault[(WarningMarker.Length + 1)..] : fault;

    private static string Truncate(string text) => text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;

    private static string FormatDay(int day, Language language) =>
        language == Language.De
            ? $"{day.ToString(CultureInfo.InvariantCulture)}."
            : day.ToString(CultureInfo.InvariantCulture);

    private static string MonthName(CultureInfo culture, DateTime date) => culture.DateTimeFormat.MonthNames[date.Month - 1];

    private static string YearOf(DateTime date) => date.Year.ToString(CultureInfo.InvariantCulture);
}
