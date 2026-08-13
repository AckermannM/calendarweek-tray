using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 13. THROWAWAY.
//
// The scripted half. The lab (Prototype13.cs) is for the user to drive by hand; this walks the
// whole matrix unattended, captures the real taskbar at each state, and — the part that matters —
// restores every system setting it touched in a finally block, so a crash mid-run cannot leave the
// machine at 225% text with a high contrast theme on.
//
// Everything here measures the taskbar as painted rather than as configured. That is deliberate:
// the question this ticket has to answer is not "what does Windows say the text scale is" but
// "what happens to the things our glyph sits beside".

internal static partial class Sys
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct HighContrastInfo
    {
        public uint cbSize;
        public uint dwFlags;
        public nint lpszDefaultScheme;
    }

    internal const uint SPI_GETHIGHCONTRAST = 0x0042;
    internal const uint SPI_SETHIGHCONTRAST = 0x0043;
    internal const uint HCF_HIGHCONTRASTON = 0x00000001;
    internal const uint SPIF_UPDATEINIFILE = 0x0001;
    internal const uint SPIF_SENDCHANGE = 0x0002;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfo(
        uint uiAction, uint uiParam, ref HighContrastInfo pvParam, uint fWinIni);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint SendMessageTimeout(
        nint hWnd, uint msg, nuint wParam, string lParam, uint flags, uint timeout, out nuint result);

    internal static void BroadcastSettingChange(string what)
    {
        // HWND_BROADCAST = 0xFFFF, WM_SETTINGCHANGE = 0x001A, SMTO_ABORTIFHUNG = 0x0002
        SendMessageTimeout(0xFFFF, 0x001A, 0, what, 0x0002, 1000, out _);
    }

    /// <summary>Reads the live high contrast state and the scheme name Windows has selected.</summary>
    internal static (bool On, string Scheme) ReadHighContrast()
    {
        nint buffer = Marshal.AllocHGlobal(256 * sizeof(char));
        try
        {
            HighContrastInfo info = new()
            {
                cbSize = (uint)Marshal.SizeOf<HighContrastInfo>(),
                lpszDefaultScheme = buffer,
            };

            if (!SystemParametersInfo(SPI_GETHIGHCONTRAST, info.cbSize, ref info, 0))
                return (false, "(SPI_GETHIGHCONTRAST failed)");

            string scheme = info.lpszDefaultScheme == 0 ? "" : Marshal.PtrToStringUni(info.lpszDefaultScheme) ?? "";
            return ((info.dwFlags & HCF_HIGHCONTRASTON) != 0, scheme);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static bool SetHighContrast(bool on, string scheme)
    {
        nint name = Marshal.StringToHGlobalUni(scheme);
        try
        {
            HighContrastInfo info = new()
            {
                cbSize = (uint)Marshal.SizeOf<HighContrastInfo>(),
                dwFlags = on ? HCF_HIGHCONTRASTON : 0,
                lpszDefaultScheme = name,
            };

            return SystemParametersInfo(SPI_SETHIGHCONTRAST, info.cbSize, ref info,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
        finally
        {
            Marshal.FreeHGlobal(name);
        }
    }
}

/// <summary>One captured state: what the settings said, and what the taskbar actually looked like.</summary>
internal sealed record Observation(
    string Label,
    int TextScalePercent,
    bool TextScaleValuePresent,
    int SmallIcon,
    bool HighContrast,
    string HcScheme,
    object? SystemUsesLightTheme,
    Color TaskbarBackground,
    Color TaskbarInk,
    int ClockInkHeight,
    string TaskbarHash,
    string StripFile);

internal static class Prototype13Experiment
{
    private static string _directory = "";
    private static readonly StringBuilder Report = new();
    private static NotifyIcon? _icon;
    private static nint _iconHandle;

    /// <summary>The right-hand slice of the taskbar: our glyph, its neighbours, and the clock.</summary>
    private const int StripWidth = 900;

    public static void Run(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        object? originalTextScale = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", null);
        (bool hcWasOn, string hcOriginalScheme) = Sys.ReadHighContrast();

        Say($"restore point: TextScaleFactor = {originalTextScale?.ToString() ?? "(absent)"}, "
            + $"high contrast = {hcWasOn} scheme \"{hcOriginalScheme}\"");

        _icon = new NotifyIcon { Visible = true, Text = "ticket 13 experiment" };
        SetGlyph(PrototypeGlyph.FaceDecided, Color.White, 44);
        Settle(2000);

        List<Observation> observations = [];

        try
        {
            observations.AddRange(TextScalePhase());
            RestoreTextScale(originalTextScale);
            Settle(2500);
            observations.AddRange(HighContrastPhase(hcWasOn, hcOriginalScheme));
        }
        finally
        {
            RestoreTextScale(originalTextScale);
            Sys.SetHighContrast(hcWasOn, hcOriginalScheme);
            Settle(2000);

            (bool hcNow, string schemeNow) = Sys.ReadHighContrast();
            object? tsNow = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", null);

            Say("");
            Say("=== restore verification ===");
            Say($"TextScaleFactor : {tsNow?.ToString() ?? "(absent)"}   "
                + $"(was {originalTextScale?.ToString() ?? "(absent)"}) "
                + $"{(Equals(tsNow, originalTextScale) ? "OK" : "*** MISMATCH ***")}");
            Say($"high contrast   : {hcNow} \"{schemeNow}\"   (was {hcWasOn} \"{hcOriginalScheme}\") "
                + $"{(hcNow == hcWasOn ? "OK" : "*** MISMATCH ***")}");

            if (_icon is not null)
            {
                _icon.Visible = false;
                _icon.Icon?.Dispose();
                _icon.Dispose();
            }

            if (_iconHandle != 0) NativeMethods.DestroyIcon(_iconHandle);
        }

        WriteMatrix(observations);
        WriteSheet(observations);
        File.WriteAllText(Path.Combine(directory, "13-experiment.txt"), Report.ToString());
    }

    // --- phase 1: text scaling -------------------------------------------------------------------

    private static List<Observation> TextScalePhase()
    {
        Say("");
        Say("=== phase 1 — text scaling ===");
        Say("The claim under test is the ticket's own premise: that raising Windows' text size makes");
        Say("'every label on their system larger' while our glyph stays put. Each row below captures");
        Say("the real taskbar, so the clock beside our glyph is measured rather than assumed.");
        Say("");

        List<Observation> results = [];

        foreach (int? percent in (int?[])[null, 125, 150, 200, 225])
        {
            ApplyTextScale(percent);
            Settle(3500);

            string label = percent is null ? "text 100% (value absent)" : $"text {percent}%";
            results.Add(Observe(label, $"13-text-{percent?.ToString(CultureInfo.InvariantCulture) ?? "absent"}.png"));
        }

        // The one lever 06 and 12 left open: more weight in the same box. Captured at 200%, which
        // is where a text-scaling response would have to justify itself if it existed.
        Say("");
        Say("--- weight candidates, captured at 200% text scale ---");
        foreach (string face in (string[])
            ["Segoe UI Variable Text", "Segoe UI Variable Text Semibold", "Segoe UI Variable Display Bold"])
        {
            ApplyTextScale(200);
            Settle(1500);
            SetGlyph(face, InkNow(), 44);
            Settle(1200);
            results.Add(Observe($"weight: {face.Replace("Segoe UI Variable ", "")}",
                $"13-weight-{face.Replace(' ', '-')}.png"));
        }

        SetGlyph(PrototypeGlyph.FaceDecided, InkNow(), 44);
        return results;
    }

    private static void ApplyTextScale(int? percent)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility");
        if (percent is { } p) key.SetValue("TextScaleFactor", p, RegistryValueKind.DWord);
        else key.DeleteValue("TextScaleFactor", throwOnMissingValue: false);

        Sys.BroadcastSettingChange("Accessibility");
        Sys.BroadcastSettingChange("WindowMetrics");
    }

    private static void RestoreTextScale(object? original)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility");
        if (original is int i) key.SetValue("TextScaleFactor", i, RegistryValueKind.DWord);
        else key.DeleteValue("TextScaleFactor", throwOnMissingValue: false);

        Sys.BroadcastSettingChange("Accessibility");
    }

    // --- phase 2: high contrast ------------------------------------------------------------------

    private static List<Observation> HighContrastPhase(bool wasOn, string originalScheme)
    {
        Say("");
        Say("=== phase 2 — high contrast ===");
        Say("07 reads SystemUsesLightTheme and picks pure white or pure black. Under a contrast theme");
        Say("the taskbar is painted from a third palette, so the question is whether that key still");
        Say("reports something our ink rule can trust.");
        Say("");

        List<Observation> results = [];

        foreach (string scheme in (string[])["High Contrast Black", "High Contrast White"])
        {
            if (!Sys.SetHighContrast(true, scheme))
            {
                Say($"SPI_SETHIGHCONTRAST failed for \"{scheme}\" "
                    + $"(win32 error {Marshal.GetLastWin32Error()}) — skipping.");
                continue;
            }

            Settle(5000);
            (bool on, string active) = Sys.ReadHighContrast();
            if (!on)
            {
                Say($"asked for \"{scheme}\" but high contrast did not come on (active \"{active}\") — skipping.");
                continue;
            }

            Say($"high contrast ON, scheme \"{active}\"");

            foreach (InkRule rule in InkRule.All)
            {
                SetGlyph(PrototypeGlyph.FaceDecided, rule.Resolve(), 44);
                Settle(1200);
                results.Add(Observe($"{active} — ink: {rule.Name}",
                    $"13-hc-{Slug(active)}-{Slug(rule.Name)}.png"));
            }
        }

        Sys.SetHighContrast(wasOn, originalScheme);
        Settle(4000);
        return results;
    }

    // --- observing ------------------------------------------------------------------------------

    private static Observation Observe(string label, string stripFile)
    {
        AccessibilityState s = AccessibilityState.Read();
        TaskbarSampler.Sample? sample = TaskbarSampler.Grab();

        Bitmap strip = CaptureStrip();
        strip.Save(Path.Combine(_directory, stripFile), ImageFormat.Png);

        int clock = ClockInkHeight(strip, sample?.Background ?? Color.Black);
        string hash = HashOf(strip);
        strip.Dispose();

        Observation o = new(
            label,
            s.TextScalePercent,
            s.TextScaleRaw is not null,
            s.SmallIcon,
            s.HighContrast,
            s.HcScheme,
            s.SystemUsesLightTheme,
            sample?.Background ?? Color.Empty,
            sample?.Ink ?? Color.Empty,
            clock,
            hash,
            stripFile);

        Say($"  {label,-42} box {o.SmallIcon,2}  clock-ink {o.ClockInkHeight,2}px  "
            + $"bg {Prototype13Probe.Hex(o.TaskbarBackground)}  ink {Prototype13Probe.Hex(o.TaskbarInk)}  "
            + $"SysLight {o.SystemUsesLightTheme?.ToString() ?? "absent"}  hash {o.TaskbarHash}");

        return o;
    }

    /// <summary>
    /// The tallest run of non-background ink in the right-hand quarter of the strip — which is the
    /// clock. Crude, but it is the number the whole text-scaling question turns on: if raising text
    /// scale does not move this, the clock does not scale either and matching it means doing nothing.
    /// </summary>
    private static int ClockInkHeight(Bitmap strip, Color background)
    {
        int best = 0;
        for (int x = strip.Width * 3 / 4; x < strip.Width - 4; x++)
        {
            int top = -1, bottom = -1;
            for (int y = 0; y < strip.Height; y++)
            {
                Color c = strip.GetPixel(x, y);
                int d = Math.Abs(c.R - background.R) + Math.Abs(c.G - background.G) + Math.Abs(c.B - background.B);
                if (d <= 90) continue;
                if (top < 0) top = y;
                bottom = y;
            }

            if (top >= 0) best = Math.Max(best, bottom - top + 1);
        }

        return best;
    }

    private static string HashOf(Bitmap bmp)
    {
        // Not cryptographic — just "did these pixels change at all", which is the claim being tested.
        unchecked
        {
            ulong h = 1469598103934665603;
            for (int y = 0; y < bmp.Height; y += 2)
            {
                for (int x = 0; x < bmp.Width; x += 2)
                {
                    h = (h ^ (uint)bmp.GetPixel(x, y).ToArgb()) * 1099511628211;
                }
            }

            return h.ToString("x16", CultureInfo.InvariantCulture)[..8];
        }
    }

    private static Bitmap CaptureStrip()
    {
        TaskbarSampler.Sample? sample = TaskbarSampler.Grab();
        Rectangle bounds = sample?.Bounds ?? new Rectangle(0, 0, 1920, 48);
        int width = Math.Min(StripWidth, bounds.Width);
        Rectangle region = new(bounds.Right - width, bounds.Top, width, bounds.Height);

        Bitmap bmp = new(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.Location, Point.Empty, region.Size);
        return bmp;
    }

    // --- the glyph in the tray -------------------------------------------------------------------

    private static Color InkNow() => InkRule.All[0].Resolve();

    private static void SetGlyph(string face, Color ink, int week)
    {
        if (_icon is null) return;

        PrototypeGlyph.Face = face;
        int box = SystemInformation.SmallIconSize.Width;
        using Bitmap bitmap = PrototypeGlyph.Render(Design.FrameRings, box, ink, week, padded: false);

        nint handle = bitmap.GetHicon();
        Icon? previous = _icon.Icon;
        nint previousHandle = _iconHandle;

        _icon.Icon = Icon.FromHandle(handle);
        _iconHandle = handle;
        previous?.Dispose();
        if (previousHandle != 0) NativeMethods.DestroyIcon(previousHandle);
    }

    /// <summary>
    /// Waits while pumping messages. Not optional: .NET caches SystemColors and only invalidates it
    /// on WM_SYSCOLORCHANGE, so a Thread.Sleep here would read a stale palette straight through the
    /// high contrast phase and quietly invent the wrong answer.
    /// </summary>
    private static void Settle(int milliseconds)
    {
        int end = Environment.TickCount + milliseconds;
        while (Environment.TickCount < end)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }
    }

    // --- output ----------------------------------------------------------------------------------

    private static void WriteMatrix(List<Observation> observations)
    {
        Say("");
        Say("=== the matrix ===");
        Say("");
        Say($"{"state",-42} {"box",3} {"clock",5} {"SysLight",8} {"bg",-9} {"shell ink",-9} {"hash",8}");
        Say(new string('-', 96));
        foreach (Observation o in observations)
        {
            Say($"{o.Label,-42} {o.SmallIcon,3} {o.ClockInkHeight,5} "
                + $"{o.SystemUsesLightTheme?.ToString() ?? "absent",8} "
                + $"{Prototype13Probe.Hex(o.TaskbarBackground),-9} {Prototype13Probe.Hex(o.TaskbarInk),-9} {o.TaskbarHash,8}");
        }
    }

    /// <summary>
    /// 12's instrument: real taskbar strips stacked, 1:1 and zoomed, so the decision is taken from
    /// pixels rather than from a table.
    /// </summary>
    private static void WriteSheet(List<Observation> observations)
    {
        const int zoom = 5;
        const int labelWidth = 360;
        const int gap = 10;

        List<(Observation O, Bitmap Strip)> rows = [];
        foreach (Observation o in observations)
        {
            string path = Path.Combine(_directory, o.StripFile);
            if (File.Exists(path)) rows.Add((o, new Bitmap(path)));
        }

        if (rows.Count == 0) return;

        // Only the tray end gets zoomed — the clock is at the far right of it, which is exactly the
        // comparison this ticket needs in one glance.
        int cropWidth = Math.Min(300, rows[0].Strip.Width);
        int rowHeight = Math.Max(rows[0].Strip.Height, cropWidth == 0 ? 1 : rows[0].Strip.Height * zoom / zoom);
        int zoomHeight = rows[0].Strip.Height * zoom;
        int totalHeight = rows.Sum(_ => Math.Max(rowHeight, zoomHeight) + gap) + gap;
        int totalWidth = labelWidth + rows[0].Strip.Width + gap + (cropWidth * zoom) + (gap * 3);

        using Bitmap sheet = new(totalWidth, totalHeight, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(48, 48, 52));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using Font font = new("Segoe UI", 11f);
            using Font mono = new("Consolas", 9f);
            using SolidBrush white = new(Color.White);
            using SolidBrush grey = new(Color.FromArgb(170, 170, 175));

            int y = gap;
            foreach ((Observation o, Bitmap strip) in rows)
            {
                g.DrawString(o.Label, font, white, 8, y + 4);
                g.DrawString(
                    $"box {o.SmallIcon}px · clock ink {o.ClockInkHeight}px · SysLight "
                    + $"{o.SystemUsesLightTheme?.ToString() ?? "absent"}\nbg {Prototype13Probe.Hex(o.TaskbarBackground)}"
                    + $" · shell ink {Prototype13Probe.Hex(o.TaskbarInk)} · hash {o.TaskbarHash}",
                    mono, grey, 8, y + 26);

                g.DrawImageUnscaled(strip, labelWidth, y);

                Rectangle crop = new(strip.Width - cropWidth, 0, cropWidth, strip.Height);
                g.DrawImage(strip,
                    new Rectangle(labelWidth + strip.Width + gap, y, cropWidth * zoom, strip.Height * zoom),
                    crop, GraphicsUnit.Pixel);

                y += Math.Max(rowHeight, zoomHeight) + gap;
                strip.Dispose();
            }
        }

        sheet.Save(Path.Combine(_directory, "13-contact-sheet.png"), ImageFormat.Png);
    }

    private static string Slug(string s) =>
        string.Concat(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-');

    private static void Say(string line) => Report.AppendLine(line);
}
