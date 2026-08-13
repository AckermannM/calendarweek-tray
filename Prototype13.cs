using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 13 (accessibility: text scaling and high contrast). THROWAWAY.
//
// 12 could be decided from a contact sheet because icon size is a number we can pick. This one
// cannot: text scaling and high contrast are *live system state*, and the question is what the
// shell does to the taskbar around our glyph when they change. So the instrument is a tray lab
// that keeps running while the user flips settings, plus a screen sampler that reads the taskbar's
// actual painted colours back.
//
// The sampler is the point. 07 ruled out sampling the taskbar *at runtime* ("no API behind it"),
// which is a correctness argument about shipping code. For *deciding* the ink rule it is the only
// honest measurement available: it tells us what the shell itself paints under a high contrast
// theme, which is the thing our ink has to sit beside.

/// <summary>Everything about the machine this ticket cares about, sampled at one instant.</summary>
internal sealed record AccessibilityState(
    object? TextScaleRaw,
    int SmallIcon,
    int Dpi,
    bool HighContrast,
    int HcFlags,
    string HcScheme,
    object? SystemUsesLightTheme,
    object? AppsUseLightTheme)
{
    public static AccessibilityState Read()
    {
        using Bitmap probe = new(1, 1);
        using Graphics g = Graphics.FromImage(probe);

        return new AccessibilityState(
            TextScaleRaw: Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", null),
            SmallIcon: SystemInformation.SmallIconSize.Width,
            Dpi: (int)Math.Round(g.DpiX),
            HighContrast: SystemInformation.HighContrast,
            HcFlags: Registry.GetValue(
                @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast", "Flags", null)
                is string s && int.TryParse(s, out int f) ? f : -1,
            HcScheme: Registry.GetValue(
                @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                "High Contrast Scheme", "") as string ?? "",
            SystemUsesLightTheme: Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme", null),
            AppsUseLightTheme: Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", null));
    }

    /// <summary>
    /// The thing work-item 1 asks for: absent must not be readable as 100. It is a REG_DWORD that
    /// does not exist until the slider is moved off 100 — and the containing key exists anyway, so
    /// probing the *key* tells you nothing.
    /// </summary>
    public string TextScaleDescription => TextScaleRaw is null
        ? "ABSENT (no value under an existing key)"
        : $"{TextScaleRaw} ({TextScaleRaw.GetType().Name})";

    public int TextScalePercent => TextScaleRaw is int i ? i : 100;

    public string Summary =>
        $"text {TextScaleDescription} | box {SmallIcon}px | {Dpi}dpi | "
        + $"HC {(HighContrast ? "ON" : "off")} flags {HcFlags} \"{HcScheme}\" | "
        + $"SystemUsesLightTheme {SystemUsesLightTheme?.ToString() ?? "absent"} | "
        + $"AppsUseLightTheme {AppsUseLightTheme?.ToString() ?? "absent"}";
}

/// <summary>
/// The candidate ink rules. 07 decided pure white on dark / pure black on light from
/// <c>SystemUsesLightTheme</c>; this ticket has to say what happens when a high contrast theme
/// paints the taskbar from a palette that is neither.
/// </summary>
internal readonly record struct InkRule(string Name, string Note, Func<Color> Resolve)
{
    private static bool LightTaskbar => Registry.GetValue(
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        "SystemUsesLightTheme", 0) is int i && i != 0;

    /// <summary>07 as decided, with no accessibility awareness at all.</summary>
    private static Color SevenRule() => LightTaskbar ? Color.Black : Color.White;

    public static readonly InkRule[] All =
    [
        new("07 as decided", "SystemUsesLightTheme ? black : white", SevenRule),
        new("WindowText", "SystemColors.WindowText always", () => SystemColors.WindowText),
        new("ControlText", "SystemColors.ControlText always", () => SystemColors.ControlText),
        new("MenuText", "SystemColors.MenuText always", () => SystemColors.MenuText),
        new("HC wins over auto", "HighContrast ? WindowText : 07's rule",
            () => SystemInformation.HighContrast ? SystemColors.WindowText : SevenRule()),
        new("HC → ControlText", "HighContrast ? ControlText : 07's rule",
            () => SystemInformation.HighContrast ? SystemColors.ControlText : SevenRule()),

        // Added after the verify pass: MenuText is the only palette entry that equals the colour
        // the shell actually paints taskbar text with in all four stock contrast themes. WindowText
        // misses on High Contrast White (#3D3D3D against the shell's #000000) and ControlText is an
        // accent colour there — cyan under High Contrast #1, yellow under #2.
        new("HC → MenuText", "HighContrast ? MenuText : 07's rule",
            () => SystemInformation.HighContrast ? SystemColors.MenuText : SevenRule()),
    ];
}

/// <summary>
/// Reads the taskbar's painted pixels back off the screen. Answers two questions no registry key
/// does: what colour is the taskbar actually painted, and what colour is the shell's own text on
/// it — which is precisely the ink our glyph is trying to match.
/// </summary>
internal static partial class TaskbarSampler
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindow(string lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    internal readonly record struct Sample(
        Rectangle Bounds, Color Background, Color Ink, double Contrast, int DistinctColours);

    public static Sample? Grab()
    {
        nint tray = FindWindow("Shell_TrayWnd", null);
        if (tray == 0) return null;
        if (!GetWindowRect(tray, out Rect r)) return null;

        Rectangle bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        using Bitmap shot = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(shot))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        // The background is simply the most common colour on the bar — icons and text are a small
        // minority of its area at any scaling.
        Dictionary<int, int> histogram = [];
        for (int y = 0; y < shot.Height; y++)
        {
            for (int x = 0; x < shot.Width; x++)
            {
                int argb = shot.GetPixel(x, y).ToArgb();
                histogram[argb] = histogram.GetValueOrDefault(argb) + 1;
            }
        }

        KeyValuePair<int, int> top = histogram.MaxBy(kv => kv.Value);
        Color background = Color.FromArgb(top.Key);

        // The shell's ink is the colour furthest from the background that still covers enough
        // pixels to be real type rather than one antialiased fringe pixel.
        int threshold = Math.Max(20, shot.Width * shot.Height / 20000);
        Color ink = background;
        double best = -1;
        foreach ((int argb, int count) in histogram)
        {
            if (count < threshold) continue;
            Color c = Color.FromArgb(argb);
            double d = Distance(c, background);
            if (d > best) (best, ink) = (d, c);
        }

        return new Sample(bounds, background, ink, ContrastRatio(background, ink), histogram.Count);
    }

    private static double Distance(Color a, Color b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    private static double Luminance(Color c)
    {
        static double Channel(int v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    public static double ContrastRatio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>
    /// Which named <see cref="SystemColors"/> entry a sampled colour actually is. This is what
    /// turns "use WindowText, probably" into a measurement.
    /// </summary>
    public static string NameIt(Color sampled)
    {
        List<string> exact = [];
        (string Name, double D) nearest = ("", double.MaxValue);

        foreach (System.Reflection.PropertyInfo p in typeof(SystemColors).GetProperties())
        {
            if (p.PropertyType != typeof(Color)) continue;
            Color c = (Color)p.GetValue(null)!;
            double d = Distance(c, sampled);
            if (d < 0.5) exact.Add(p.Name);
            if (d < nearest.D) nearest = (p.Name, d);
        }

        return exact.Count > 0
            ? string.Join(", ", exact)
            : $"(none exact; nearest {nearest.Name} at distance {nearest.D:0.0})";
    }
}

/// <summary>The one-shot dump. Everything the ticket's work items 1, 3 and 4 want on the record.</summary>
internal static class Prototype13Probe
{
    public static string Compose()
    {
        AccessibilityState s = AccessibilityState.Read();
        StringBuilder sb = new();

        sb.AppendLine("=== ticket 13 — accessibility probe ===");
        sb.AppendLine();
        sb.AppendLine("--- text scaling (work items 1 and 3) ---");
        sb.AppendLine($"HKCU\\Software\\Microsoft\\Accessibility exists : "
            + $"{Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility") is not null}");
        sb.AppendLine($"  TextScaleFactor  : {s.TextScaleDescription}");
        sb.AppendLine($"  read as percent  : {s.TextScalePercent}");
        sb.AppendLine($"SM_CXSMICON        : {s.SmallIcon}");
        sb.AppendLine($"primary DPI        : {s.Dpi} ({s.Dpi * 100 / 96}% scaling)");
        sb.AppendLine($"icon box / dpi     : {s.SmallIcon * 96.0 / s.Dpi:0.00} px at 96 dpi");
        sb.AppendLine($"clock type size    : {PrototypeGlyph.ClockSizeFor(s.SmallIcon)} px (02's 12/16 ratio)");
        sb.AppendLine();

        sb.AppendLine("--- high contrast (work item 4) ---");
        sb.AppendLine($"SystemInformation.HighContrast : {s.HighContrast}");
        sb.AppendLine($"HighContrast\\Flags             : {s.HcFlags} (bit 0 = HCF_HIGHCONTRASTON)");
        sb.AppendLine($"High Contrast Scheme           : \"{s.HcScheme}\"");
        sb.AppendLine($"SystemUsesLightTheme           : {s.SystemUsesLightTheme?.ToString() ?? "absent"}");
        sb.AppendLine($"AppsUseLightTheme              : {s.AppsUseLightTheme?.ToString() ?? "absent"}");
        sb.AppendLine();

        sb.AppendLine("--- what each candidate ink rule resolves to right now ---");
        foreach (InkRule rule in InkRule.All)
        {
            Color c = rule.Resolve();
            sb.AppendLine($"  {rule.Name,-18} {Hex(c),-9} {rule.Note}");
        }

        sb.AppendLine();
        sb.AppendLine("--- system colours the rules might draw on ---");
        foreach (string name in (string[])
            ["Window", "WindowText", "Control", "ControlText", "Menu", "MenuText",
             "Highlight", "HighlightText", "Info", "InfoText", "ActiveCaption", "ActiveCaptionText"])
        {
            Color c = (Color)typeof(SystemColors).GetProperty(name)!.GetValue(null)!;
            sb.AppendLine($"  {name,-18} {Hex(c)}");
        }

        sb.AppendLine();
        sb.AppendLine("--- the taskbar as actually painted ---");
        TaskbarSampler.Sample? sample = TaskbarSampler.Grab();
        if (sample is not { } t)
        {
            sb.AppendLine("  Shell_TrayWnd not found or not capturable.");
        }
        else
        {
            sb.AppendLine($"  bounds            : {t.Bounds}");
            sb.AppendLine($"  distinct colours  : {t.DistinctColours}");
            sb.AppendLine($"  background        : {Hex(t.Background)}  = {TaskbarSampler.NameIt(t.Background)}");
            sb.AppendLine($"  shell's own ink   : {Hex(t.Ink)}  = {TaskbarSampler.NameIt(t.Ink)}");
            sb.AppendLine($"  contrast bg:ink   : {t.Contrast:0.00}:1");
            sb.AppendLine();
            sb.AppendLine("  each candidate rule's ink against that measured background:");
            foreach (InkRule rule in InkRule.All)
            {
                Color c = rule.Resolve();
                double ratio = TaskbarSampler.ContrastRatio(t.Background, c);
                string verdict = ratio < 1.5 ? "  <-- INVISIBLE" : ratio < 4.5 ? "  <-- weak" : "";
                sb.AppendLine($"    {rule.Name,-18} {Hex(c),-9} {ratio,6:0.00}:1{verdict}");
            }
        }

        return sb.ToString();
    }

    public static void WriteOnce(string directory)
    {
        Directory.CreateDirectory(directory);
        string text = Compose();
        File.WriteAllText(Path.Combine(directory, "13-probe.txt"), text);
        Console.WriteLine(text);
    }

    public static string Hex(Color c) =>
        $"#{c.R:X2}{c.G:X2}{c.B:X2}" + (c.A == 255 ? "" : $" a{c.A}");
}

/// <summary>
/// The tray lab. Sits there while the user flips text scaling and contrast themes in Settings,
/// re-renders the decided glyph on every change, and appends every distinct state it sees to a
/// log — so a theme flip that disturbs the session still leaves evidence behind.
/// </summary>
internal sealed class Prototype13Lab : ApplicationContext
{
    private static readonly string LogDirectory = Path.Combine(
        Directory.GetCurrentDirectory(), ".scratch", "calendarweek-tray-v1", "prototype-13");

    private static readonly string LogPath = Path.Combine(LogDirectory, "13-state-log.txt");

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _poll;
    private nint _iconHandle;

    private int _rule;
    private int _week = 44;
    private string _face = PrototypeGlyph.FaceDecided;
    private string _lastSummary = "";

    public Prototype13Lab()
    {
        Directory.CreateDirectory(LogDirectory);

        ContextMenuStrip menu = new();

        ToolStripMenuItem rules = new("Ink rule");
        for (int i = 0; i < InkRule.All.Length; i++)
        {
            int captured = i;
            rules.DropDownItems.Add($"{InkRule.All[i].Name} — {InkRule.All[i].Note}", null,
                (_, _) => { _rule = captured; Render("ink rule changed"); });
        }
        menu.Items.Add(rules);

        // The one text-scaling response 06 and 12 have not closed: more weight in the same box.
        ToolStripMenuItem weights = new("Digit weight (13's only unclosed lever)");
        foreach (string face in (string[])
            ["Segoe UI Variable Text", "Segoe UI Variable Text Semibold",
             "Segoe UI Variable Small Semibold", "Segoe UI Variable Display Bold"])
        {
            string captured = face;
            weights.DropDownItems.Add(captured, null,
                (_, _) => { _face = captured; Render("face changed"); });
        }
        menu.Items.Add(weights);

        ToolStripMenuItem weeks = new("Week shown");
        foreach (int week in (int[])[1, 11, 32, 44])
        {
            int captured = week;
            weeks.DropDownItems.Add($"KW{captured}", null,
                (_, _) => { _week = captured; Render("week changed"); });
        }
        menu.Items.Add(weeks);

        menu.Items.Add(new ToolStripSeparator());

        // Driving TextScaleFactor from here rather than from Settings is itself a measurement:
        // it tells us whether the applet would even be told, and whether SM_CXSMICON moves.
        ToolStripMenuItem scale = new("Set TextScaleFactor (writes HKCU, reversible)");
        foreach (int percent in (int[])[100, 125, 150, 200, 225])
        {
            int captured = percent;
            scale.DropDownItems.Add($"{captured}%", null, (_, _) => SetTextScale(captured));
        }
        scale.DropDownItems.Add(new ToolStripSeparator());
        scale.DropDownItems.Add("Delete the value (restore 'never set')", null, (_, _) => SetTextScale(null));
        menu.Items.Add(scale);

        menu.Items.Add("Sample the taskbar and dump state now", null, (_, _) => DumpNow());
        menu.Items.Add("Show the last dump", null, (_, _) =>
        {
            if (File.Exists(LogPath)) System.Diagnostics.Process.Start("notepad.exe", LogPath);
        });

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };

        // Polling rather than listening, on purpose: 07 measured that a theme write which
        // broadcasts nothing produces no event at all, and this lab must not miss a state change
        // just because the mechanism that reports it is the thing under test.
        _poll = new System.Windows.Forms.Timer { Interval = 1000 };
        _poll.Tick += (_, _) => PollForChange();
        _poll.Start();

        Append($"lab started — {AccessibilityState.Read().Summary}");
        Render("start");
    }

    private void PollForChange()
    {
        string summary = AccessibilityState.Read().Summary;
        if (summary == _lastSummary) return;
        Render("state changed");
        Append($"CHANGE  {summary}");
    }

    private void Render(string why)
    {
        AccessibilityState s = AccessibilityState.Read();
        _lastSummary = s.Summary;

        InkRule rule = InkRule.All[_rule];
        Color ink = rule.Resolve();

        PrototypeGlyph.Face = _face;
        using Bitmap bitmap = PrototypeGlyph.Render(Design.FrameRings, s.SmallIcon, ink, _week, padded: false);

        nint handle = bitmap.GetHicon();
        Icon? previous = _notifyIcon.Icon;
        nint previousHandle = _iconHandle;

        _notifyIcon.Icon = Icon.FromHandle(handle);
        _iconHandle = handle;
        previous?.Dispose();
        if (previousHandle != 0) NativeMethods.DestroyIcon(previousHandle);

        _notifyIcon.Text =
            $"KW{_week} | {rule.Name} = {Prototype13Probe.Hex(ink)}\n"
            + $"text {s.TextScaleDescription} | box {s.SmallIcon}px | {s.Dpi}dpi\n"
            + $"HC {(s.HighContrast ? "ON" : "off")} | SysLight {s.SystemUsesLightTheme?.ToString() ?? "absent"} "
            + $"| digits {PrototypeGlyph.LastNumberInkHeight}px vs clock {PrototypeGlyph.ClockSizeFor(s.SmallIcon)}px";

        // 07 measured the cap at 127; truncate rather than let the shell do it silently.
        if (_notifyIcon.Text.Length > 127) _notifyIcon.Text = _notifyIcon.Text[..127];

        Append($"render ({why}) — face \"{_face}\" rule \"{rule.Name}\" ink {Prototype13Probe.Hex(ink)} "
            + $"digits {PrototypeGlyph.LastNumberInkHeight}px — {s.Summary}");
    }

    private void SetTextScale(int? percent)
    {
        int boxBefore = SystemInformation.SmallIconSize.Width;

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility"))
        {
            if (percent is { } p) key.SetValue("TextScaleFactor", p, RegistryValueKind.DWord);
            else key.DeleteValue("TextScaleFactor", throwOnMissingValue: false);
        }

        Append($"WROTE TextScaleFactor = {percent?.ToString(CultureInfo.InvariantCulture) ?? "(deleted)"} "
            + $"— SM_CXSMICON before {boxBefore}, immediately after {SystemInformation.SmallIconSize.Width}");

        _notifyIcon.ShowBalloonTip(3000, "Text scale written",
            $"TextScaleFactor = {percent?.ToString(CultureInfo.InvariantCulture) ?? "deleted"}. "
            + "Watch the tooltip and the clock — does anything move?", ToolTipIcon.Info);
    }

    private void DumpNow()
    {
        string text = Prototype13Probe.Compose();
        File.WriteAllText(Path.Combine(LogDirectory, "13-probe.txt"), text);
        Append("--- full probe ---\n" + text);
        System.Diagnostics.Process.Start("notepad.exe", Path.Combine(LogDirectory, "13-probe.txt"));
    }

    private static void Append(string line)
    {
        // No timestamp helper in the prototype: DateTime is fine here, this is throwaway.
        File.AppendAllText(LogPath,
            $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _poll.Stop();
            _poll.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            if (_iconHandle != 0)
            {
                NativeMethods.DestroyIcon(_iconHandle);
                _iconHandle = 0;
            }

            PrototypeGlyph.Face = PrototypeGlyph.FaceDecided;
        }

        base.Dispose(disposing);
    }
}
