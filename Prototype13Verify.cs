using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 13. THROWAWAY.
//
// The matrix run left two claims resting on a subsampled hash, and both are load-bearing enough
// that the answer should not rest on an inference:
//
//   A. "The taskbar is completely immune to text scaling." Proved properly by an exact pixel diff
//      against a same-state control diff, so clock ticks cannot be mistaken for a real change.
//   B. "SystemColors follows high contrast even though it does not follow dark theme." The matrix
//      inferred this from icons that hashed the same; here the palette is simply dumped in each
//      scheme, alongside the taskbar as painted.

internal static class Prototype13Verify
{
    private static readonly StringBuilder Report = new();
    private static string _directory = "";

    public static void Run(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        object? originalTextScale = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", null);
        (bool hcWasOn, string hcOriginalScheme) = Sys.ReadHighContrast();

        try
        {
            VerifyTextScaling();
            VerifyHighContrast();
        }
        finally
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility"))
            {
                if (originalTextScale is int i) key.SetValue("TextScaleFactor", i, RegistryValueKind.DWord);
                else key.DeleteValue("TextScaleFactor", throwOnMissingValue: false);
            }

            Sys.BroadcastSettingChange("Accessibility");
            Sys.SetHighContrast(hcWasOn, hcOriginalScheme);
            Pump(3000);

            (bool hcNow, _) = Sys.ReadHighContrast();
            object? tsNow = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "TextScaleFactor", null);

            Say("");
            Say("=== restore verification ===");
            Say($"TextScaleFactor : {tsNow?.ToString() ?? "(absent)"} "
                + $"{(Equals(tsNow, originalTextScale) ? "OK" : "*** MISMATCH ***")}");
            Say($"high contrast   : {hcNow} {(hcNow == hcWasOn ? "OK" : "*** MISMATCH ***")}");
        }

        File.WriteAllText(Path.Combine(_directory, "13-verify.txt"), Report.ToString());
    }

    // --- A: is the taskbar really immune to text scaling? ----------------------------------------

    private static void VerifyTextScaling()
    {
        Say("=== A — exact pixel diff of the taskbar across a text scale change ===");
        Say("");
        Say("A control diff at an unchanged setting comes first, so that whatever the taskbar does on");
        Say("its own — a clock tick, a repaint — is measured before it can be read as a response.");
        Say("");

        SetTextScale(null);
        Pump(2500);

        using Bitmap control1 = Capture();
        Pump(4000);
        using Bitmap control2 = Capture();
        (int controlDiff, int controlTotal) = Diff(control1, control2);
        Say($"control  100% -> 100% (4s apart) : {controlDiff} of {controlTotal} pixels differ "
            + $"({100.0 * controlDiff / controlTotal:0.000}%)");

        SetTextScale(200);
        Pump(4000);
        using Bitmap at200 = Capture();
        (int diff200, int total200) = Diff(control2, at200);
        Say($"test     100% -> 200% (4s apart) : {diff200} of {total200} pixels differ "
            + $"({100.0 * diff200 / total200:0.000}%)");

        at200.Save(Path.Combine(_directory, "13-taskbar-at-200.png"), ImageFormat.Png);

        SetTextScale(225);
        Pump(4000);
        using Bitmap at225 = Capture();
        (int diff225, int total225) = Diff(control2, at225);
        Say($"test     100% -> 225% (8s apart) : {diff225} of {total225} pixels differ "
            + $"({100.0 * diff225 / total225:0.000}%)");

        SetTextScale(null);
        Pump(3000);

        Say("");
        Say($"WinRT UISettings.TextScaleFactor was independently measured to move 1 -> 2 -> 1 across");
        Say($"exactly this registry write, so the setting is unquestionably being applied system-wide.");
        Say($"SM_CXSMICON throughout: {SystemInformation.SmallIconSize.Width}");
        Say("");
    }

    private static (int Differing, int Total) Diff(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return (-1, 0);

        int differing = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) differing++;
            }
        }

        return (differing, a.Width * a.Height);
    }

    // --- B: what does the palette actually say in each contrast theme? ---------------------------

    private static readonly string[] Interesting =
        ["Window", "WindowText", "Control", "ControlText", "Menu", "MenuText",
         "Highlight", "HighlightText", "Info", "InfoText", "GrayText", "HotTrack"];

    private static void VerifyHighContrast()
    {
        Say("=== B — the palette and the painted taskbar, per contrast theme ===");
        Say("");

        DumpPalette("no high contrast (dark theme, as the machine sits)");

        foreach (string scheme in (string[])
            ["High Contrast Black", "High Contrast White", "High Contrast #1", "High Contrast #2"])
        {
            if (!Sys.SetHighContrast(true, scheme)) { Say($"-- \"{scheme}\": SPI call failed, skipped"); continue; }

            Pump(6000);
            (bool on, string active) = Sys.ReadHighContrast();
            if (!on) { Say($"-- \"{scheme}\": did not come on, skipped"); continue; }

            DumpPalette($"high contrast ON — \"{active}\"");
        }

        Sys.SetHighContrast(false, "");
        Pump(4000);
    }

    private static void DumpPalette(string label)
    {
        Say($"--- {label} ---");

        object? sysLight = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", null);

        Say($"  SystemInformation.HighContrast : {SystemInformation.HighContrast}");
        Say($"  SystemUsesLightTheme           : {sysLight?.ToString() ?? "absent"}   "
            + "<-- 07's only input");

        StringBuilder palette = new("  palette:");
        foreach (string name in Interesting)
        {
            Color c = (Color)typeof(SystemColors).GetProperty(name)!.GetValue(null)!;
            palette.Append(CultureInfo.InvariantCulture, $" {name}={Prototype13Probe.Hex(c)}");
        }

        Say(palette.ToString());

        TaskbarSampler.Sample? sample = TaskbarSampler.Grab();
        if (sample is not { } t)
        {
            Say("  taskbar: not capturable");
            Say("");
            return;
        }

        Say($"  taskbar as painted             : background {Prototype13Probe.Hex(t.Background)} "
            + $"({TaskbarSampler.NameIt(t.Background)}), shell ink {Prototype13Probe.Hex(t.Ink)} "
            + $"({TaskbarSampler.NameIt(t.Ink)})");

        Say("  candidate ink rules against that background:");
        foreach (InkRule rule in InkRule.All)
        {
            Color ink = rule.Resolve();
            double ratio = TaskbarSampler.ContrastRatio(t.Background, ink);
            string verdict = ratio < 1.5 ? "   *** INVISIBLE ***" : ratio < 4.5 ? "   (weak)" : "";
            Say($"    {rule.Name,-18} {Prototype13Probe.Hex(ink),-9} {ratio,6:0.00}:1{verdict}");
        }

        Say("");
    }

    // --- plumbing ---------------------------------------------------------------------------------

    private static void SetTextScale(int? percent)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility");
        if (percent is { } p) key.SetValue("TextScaleFactor", p, RegistryValueKind.DWord);
        else key.DeleteValue("TextScaleFactor", throwOnMissingValue: false);

        Sys.BroadcastSettingChange("Accessibility");
        Sys.BroadcastSettingChange("WindowMetrics");
    }

    private static Bitmap Capture()
    {
        TaskbarSampler.Sample? sample = TaskbarSampler.Grab();
        Rectangle bounds = sample?.Bounds ?? new Rectangle(0, 0, 1920, 48);
        Bitmap bmp = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return bmp;
    }

    private static void Pump(int milliseconds)
    {
        int end = Environment.TickCount + milliseconds;
        while (Environment.TickCount < end)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }
    }

    private static void Say(string line) => Report.AppendLine(line);
}
