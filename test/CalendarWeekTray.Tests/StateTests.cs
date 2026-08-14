using System.Globalization;
using Xunit;

namespace CalendarWeekTray.Tests;

/// <summary>
/// Spec §11.2 item 6's tooltip and ink assertions, via <see cref="TrayState.Compute"/> and
/// <see cref="Strings"/> directly. Raw ISO week arithmetic is out of scope (spec §11.4) — these
/// tests get their Monday/Sunday from <see cref="ISOWeek"/> itself, the same way production code
/// does, so only the formatting on top of it is under test.
/// </summary>
public class StateTests
{
    // --- date range: all four §10.3 branches, both languages, including week 53 -----------------
    //
    // xunit's [MemberData] requires a public source, and a public member cannot expose the
    // internal Language/Theme enums in its signature — so the theory data below carries plain
    // bool/string and each test converts locally.

    public static TheoryData<int, int, bool, string> DateRangeCases() =>
        new()
        {
            { 2026, 33, true, "10.–16. August 2026" },
            { 2026, 33, false, "10–16 August 2026" },
            { 2026, 27, true, "29. Juni – 5. Juli 2026" },
            { 2026, 27, false, "29 June – 5 July 2026" },
            { 2026, 1, true, "29. Dezember 2025 – 4. Januar 2026" },
            { 2026, 1, false, "29 December 2025 – 4 January 2026" },
            { 2026, 53, true, "28. Dezember 2026 – 3. Januar 2027" },
            { 2026, 53, false, "28 December 2026 – 3 January 2027" },
        };

    [Theory]
    [MemberData(nameof(DateRangeCases))]
    public void DateRangeMatchesTheSpecTable(int isoYear, int week, bool german, string expected)
    {
        DateTime monday = ISOWeek.ToDateTime(isoYear, week, DayOfWeek.Monday);
        DateTime sunday = monday.AddDays(6);
        Language language = german ? Language.De : Language.En;

        string actual = Strings.DateRange(language, monday, sunday);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TooltipIsComposedThroughComputeInTheConfiguredLanguage()
    {
        DateTime now = ISOWeek.ToDateTime(2026, 33, DayOfWeek.Wednesday);
        AppConfig config = new() { Language = Language.De };

        DesiredState state = TrayState.Compute(now, sizePx: 16, highContrast: false, systemUsesLightTheme: true, config, configError: null);

        Assert.Equal("Kalenderwoche 33 · 10.–16. August 2026", state.Tooltip);
        Assert.Equal(33, state.Week);
    }

    // --- ink: all six theme states, through Compute ------------------------------------------

    public static TheoryData<bool, bool?, string, Color> InkCases() =>
        new()
        {
            { true, true, "light", SystemColors.MenuText },   // high contrast wins outright
            { false, true, "light", Color.Black },
            { false, true, "dark", Color.White },
            { false, true, "auto", Color.Black },              // auto + SystemUsesLightTheme = true
            { false, false, "auto", Color.White },             // auto + SystemUsesLightTheme = false
            { false, null, "auto", Color.Black },              // auto + absent key ⇒ light
        };

    [Theory]
    [MemberData(nameof(InkCases))]
    public void InkFollowsSpecFiveFiveAcrossAllSixThemeStates(bool highContrast, bool? systemUsesLightTheme, string themeName, Color expected)
    {
        Theme theme = themeName switch
        {
            "light" => Theme.Light,
            "dark" => Theme.Dark,
            _ => Theme.Auto,
        };
        AppConfig config = new() { Theme = theme };

        DesiredState state = TrayState.Compute(DateTime.Now, sizePx: 16, highContrast, systemUsesLightTheme, config, configError: null);

        Assert.Equal(expected.ToArgb(), state.Ink.ToArgb());
    }

    // --- diagnostics (spec §9): the pure string helpers ticket 07 wires into Reconcile() ---------

    [Fact]
    public void ConfigFaultCarriesTheOneBasedLineNumberInBothLanguages()
    {
        Assert.Equal("⚠ config.json ungültig (Zeile 5)", Strings.ConfigFault(Language.De, lineNumber: 5));
        Assert.Equal("⚠ config.json invalid (line 5)", Strings.ConfigFault(Language.En, lineNumber: 5));
    }

    [Fact]
    public void ConfigFaultOmitsTheLineNumberWhenAbsent()
    {
        Assert.Equal("⚠ config.json ungültig", Strings.ConfigFault(Language.De, lineNumber: null));
        Assert.Equal("⚠ config.json invalid", Strings.ConfigFault(Language.En, lineNumber: null));
    }

    [Fact]
    public void AppendFaultAddsTheSeparatorOnlyWhenABaseTooltipExists()
    {
        string fault = Strings.RenderFault(Language.En);

        Assert.Equal("Calendar week 33 · ⚠ icon rendering failed", Strings.AppendFault("Calendar week 33", fault));
        Assert.Equal(fault, Strings.AppendFault(null, fault));
        Assert.Equal(fault, Strings.AppendFault(string.Empty, fault));
    }

    [Fact]
    public void BalloonBodyStripsTheLeadingWarningMarker()
    {
        Assert.Equal("icon rendering failed", Strings.BalloonBody(Strings.RenderFault(Language.En)));
        Assert.Equal("config.json invalid (line 5)", Strings.BalloonBody(Strings.ConfigFault(Language.En, lineNumber: 5)));
    }
}
