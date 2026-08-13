using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CalendarWeekTray;

// ============================================================================================
//  PROTOTYPE — ticket 12. THROWAWAY CODE, same status as the 06 files it builds on.
//
//  12 asks whether the form 06 decided (FrameRings: page outline, notched binding bar, week
//  number in the body) needs a *reduced* variant at 16 px, where SM_CXSMICON is 16 and the
//  taskbar clock is 12 px type. 06 measured the frame's cost there: 8 px of digit inside it
//  against 10 px with no frame at all.
//
//  Two things this round adds that 06's sheets could not show:
//
//  1. The clock is IN the picture. Every earlier sheet showed the glyph alone on a flat swatch,
//     so "8 px against a 12 px clock" was a number in a table. Here each 1:1 cell is a strip of
//     real taskbar — 48 px tall at 100% scaling — with the glyph at its left and the two-line
//     Win11 clock at its right. The comparison the ticket is actually about is now visible.
//
//  2. A third option the ticket does not contain. The ticket offers keep-the-form or
//     drop-the-frame; but the frame's 2 px cost is two constants — a 3 px binding bar and 1 px
//     of air inside the outline — not the frame itself. Opening those up keeps one form at every
//     size, which is the cheap answer 08 would much rather have. Rendered here so it can lose on
//     looking rather than on argument.
// ============================================================================================

/// <summary>One thing to look at: a design plus the two frame constants it is drawn with.</summary>
internal readonly record struct Candidate(string Name, Design Design, float BarFactor, int BodyPad,
    string Note)
{
    /// <summary>What 06 decided, unchanged. Every other row is measured against this one.</summary>
    public static readonly Candidate Decided =
        new("06 as decided", Design.FrameRings, 0.17f, 1, "bar 3 px, 1 px air");

    public static readonly Candidate ThinBar =
        new("thinner bar", Design.FrameRings, 0.11f, 1, "bar 2 px, 1 px air");

    public static readonly Candidate NoPad =
        new("no inner air", Design.FrameRings, 0.17f, 0, "bar 3 px, 0 px air");

    public static readonly Candidate ThinBarNoPad =
        new("both opened up", Design.FrameRings, 0.11f, 0, "bar 2 px, 0 px air");

    /// <summary>The reduced form the ticket names: notched bar, no page.</summary>
    public static readonly Candidate BarOnly =
        new("reduced: no page", Design.BarWithRings, 0.13f, 0, "the ticket's option (b)");

    /// <summary>No calendar cue at all. The legibility ceiling, and 06 rejected it on meaning.</summary>
    public static readonly Candidate Bare =
        new("no cue at all", Design.NumberOnly, 0.17f, 0, "the ceiling — 06 rejected it");

    public static readonly Candidate[] All =
        [Decided, ThinBar, NoPad, ThinBarNoPad, BarOnly, Bare];
}

internal static class Prototype12Sheet
{
    private static readonly Color DarkTaskbar = Color.FromArgb(32, 32, 32);
    private static readonly Color LightTaskbar = Color.FromArgb(243, 243, 243);
    private static readonly Color DarkThemeGlyph = Color.White;
    private static readonly Color LightThemeGlyph = Color.FromArgb(26, 26, 26);

    /// <summary>The clock's face, from 02: Segoe UI Variable Small, Regular 400.</summary>
    private const string ClockFace = "Segoe UI Variable Small";

    /// <summary>Win11's taskbar height at 100% scaling, where SM_CXSMICON is 16.</summary>
    private const int TaskbarHeight = 48;

    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);

        foreach (int box in new[] { 16, 24 })
        {
            string path = Path.Combine(directory, $"12-candidates-{box}px.png");
            WriteCandidateSheet(path, box, week: 44);
            Console.WriteLine(path);
        }

        string weeks = Path.Combine(directory, "12-week-sweep-16px.png");
        WriteWeekSweep(weeks, 16);
        Console.WriteLine(weeks);

        string debug = Path.Combine(directory, "12-fit-debug.txt");
        File.WriteAllText(debug, Dump());
        Console.WriteLine(debug);
    }

    /// <summary>
    /// Every candidate at one size: a real taskbar strip at 1:1 in both themes, then the same
    /// glyph magnified so the failure can be seen rather than guessed at.
    /// </summary>
    private static void WriteCandidateSheet(string path, int box, int week)
    {
        Candidate[] candidates = Candidate.All;
        const int zoom = 14, pad = 16, labelW = 310, headerH = 96;
        int stripW = 190;
        int magW = box * zoom;
        int rowH = Math.Max(magW, TaskbarHeight) + pad;
        int c0 = labelW, c1 = c0 + stripW + pad, c2 = c1 + stripW + pad, c3 = c2 + magW + pad;

        using Bitmap sheet = new(c3 + magW + pad, headerH + (candidates.Length * rowH) + pad,
            PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f, FontStyle.Bold);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 13f, FontStyle.Bold);

        g.DrawString($"{box} px icon  —  week {week} (widest label)  —  clock type is "
            + $"{PrototypeGlyph.ClockSizeFor(box)} px", header, Brushes.White, 8, 10);
        g.DrawString("the 1:1 columns are strips of real taskbar: glyph at the left, the Win11 "
            + "two-line clock at the right, same background, same scale.", small, Brushes.Gray, 8, 34);
        g.DrawString("VIEW THIS AT 100% ZOOM — an image viewer that scales the PNG destroys the "
            + "only thing the 1:1 columns are for.", small, Brushes.Gold, 8, 50);

        g.DrawString("1:1 dark taskbar", small, Brushes.Gold, c0, 78);
        g.DrawString("1:1 light taskbar", small, Brushes.Gold, c1, 78);
        g.DrawString($"x{zoom} dark", small, Brushes.Gold, c2, 78);
        g.DrawString($"x{zoom} light", small, Brushes.Gold, c3, 78);

        for (int i = 0; i < candidates.Length; i++)
        {
            Candidate c = candidates[i];
            int y = headerH + (i * rowH);
            int mid = y + (rowH / 2);

            using Bitmap dark = Render(c, box, DarkThemeGlyph, week);
            int digits = PrototypeGlyph.LastNumberInkHeight;
            using Bitmap light = Render(c, box, LightThemeGlyph, week);

            g.DrawString(c.Name, label, Brushes.White, 8, mid - 24);
            g.DrawString(c.Note, small, Brushes.Gray, 8, mid - 6);
            int clock = PrototypeGlyph.ClockSizeFor(box);
            g.DrawString($"digits {digits} px = {digits * 100 / clock}% of clock", small,
                digits >= clock ? Brushes.PaleGreen : Brushes.Gray, 8, mid + 10);

            TaskbarStrip(g, c0, mid - (TaskbarHeight / 2), stripW, DarkTaskbar, Color.White, dark, box);
            TaskbarStrip(g, c1, mid - (TaskbarHeight / 2), stripW, LightTaskbar,
                Color.FromArgb(26, 26, 26), light, box);
            Cell(g, c2, mid - (magW / 2), magW, magW, DarkTaskbar, dark);
            Cell(g, c3, mid - (magW / 2), magW, magW, LightTaskbar, light);
        }

        sheet.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// The two surviving frames across the weeks that break things: 1 and 11 for centring, 44 for
    /// width, 53 for the year end. If opening the constants up makes a digit touch the outline,
    /// this is where it shows.
    /// </summary>
    private static void WriteWeekSweep(string path, int box)
    {
        int[] weeks = [1, 8, 11, 32, 44, 53];
        Candidate[] candidates = [Candidate.Decided, Candidate.ThinBar, Candidate.ThinBarNoPad,
            Candidate.BarOnly];
        const int zoom = 14, pad = 14, labelW = 130, headerH = 62;
        int magW = box * zoom;
        int colW = magW + pad;

        using Bitmap sheet = new(labelW + (colW * candidates.Length) + pad,
            headerH + (weeks.Length * (magW + pad)) + pad, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 12f, FontStyle.Bold);

        g.DrawString($"every week that breaks something, at {box} px, magnified x{zoom}",
            header, Brushes.White, 8, 10);
        g.DrawString("1 and 11 are the centring cases (the flag off the stem); 44 is the widest "
            + "label; 53 is the year end.", small, Brushes.Gray, 8, 30);

        for (int c = 0; c < candidates.Length; c++)
        {
            g.DrawString(candidates[c].Name, small, Brushes.Gold, labelW + (c * colW), 46);
        }

        for (int i = 0; i < weeks.Length; i++)
        {
            int y = headerH + (i * (magW + pad));
            g.DrawString($"week {weeks[i]}", label, Brushes.White, 8, y + (magW / 2) - 8);

            for (int c = 0; c < candidates.Length; c++)
            {
                using Bitmap glyph = Render(candidates[c], box, DarkThemeGlyph, weeks[i]);
                Cell(g, labelW + (c * colW), y, magW, magW, DarkTaskbar, glyph);
            }
        }

        sheet.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// A slice of taskbar: the glyph where the shell would put it, and the Win11 clock — time over
    /// date, right-aligned — where the shell puts that. Both on one opaque background, so the size
    /// relationship the whole ticket turns on is a thing you look at rather than a table entry.
    /// </summary>
    private static void TaskbarStrip(Graphics g, int x, int y, int width, Color background,
        Color text, Bitmap glyph, int box)
    {
        using SolidBrush back = new(background);
        g.FillRectangle(back, x, y, width, TaskbarHeight);

        g.DrawImageUnscaled(glyph, x + 12, y + ((TaskbarHeight - box) / 2));

        int clockPx = PrototypeGlyph.ClockSizeFor(box);
        using Font clock = new(ClockFace, clockPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using SolidBrush ink = new(text);

        // Real taskbar text is ClearType on an opaque background. The glyph beside it deliberately
        // is not — it is an icon with an alpha channel, which is exactly 06's finding.
        TextRenderingHint previous = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        StringFormat right = new(StringFormat.GenericTypographic) { Alignment = StringAlignment.Far };
        float lineH = clockPx * 1.35f;
        float top = y + ((TaskbarHeight - (lineH * 2)) / 2f);
        g.DrawString("15:04", clock, ink, new RectangleF(x, top, width - 12, lineH), right);
        g.DrawString("13.08.2026", clock, ink, new RectangleF(x, top + lineH, width - 12, lineH), right);

        g.TextRenderingHint = previous;
    }

    private static void Cell(Graphics g, int x, int y, int w, int h, Color background, Bitmap glyph)
    {
        using SolidBrush brush = new(background);
        g.FillRectangle(brush, x, y, w, h);
        g.DrawImage(glyph, new Rectangle(x, y, w, h));
    }

    private static Bitmap Render(Candidate c, int box, Color colour, int week)
    {
        float bar = PrototypeGlyph.BarFactor;
        int pad = PrototypeGlyph.BodyPad;
        try
        {
            PrototypeGlyph.BarFactor = c.BarFactor;
            PrototypeGlyph.BodyPad = c.BodyPad;
            return PrototypeGlyph.Render(c.Design, box, colour, week, padded: false);
        }
        finally
        {
            PrototypeGlyph.BarFactor = bar;
            PrototypeGlyph.BodyPad = pad;
        }
    }

    // --- measurements ----------------------------------------------------------------------------

    private static string Dump()
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine("ticket 12 — is the decided form too small at 16 px?");
        sb.AppendLine();

        foreach (int box in new[] { 16, 20, 24, 32 })
        {
            int clock = PrototypeGlyph.ClockSizeFor(box);
            sb.AppendLine($"--- {box} px icon, clock type {clock} px ---");
            foreach (Candidate c in Candidate.All)
            {
                using Bitmap _ = Render(c, box, Color.White, 44);
                int h = PrototypeGlyph.LastNumberInkHeight;
                sb.AppendLine($"  {c.Name,-16} {c.Note,-24} digits {h,2} px  "
                    + $"({h * 100 / clock,3}% of the clock)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- does the digit ever touch the page outline? (16 px, gap in px) ---");
        sb.AppendLine("  the outline occupies row/column 0 and 15; a gap of 0 means they collide.");
        foreach (Candidate c in new[] { Candidate.Decided, Candidate.ThinBar, Candidate.NoPad,
            Candidate.ThinBarNoPad })
        {
            foreach (int week in new[] { 44, 8, 11 })
            {
                using Bitmap bare = RenderDigitsOnly(c, 16, week);
                Rectangle ink = PrototypeGlyph.InkBoundsOf(bare);
                sb.AppendLine($"  {c.Name,-16} week {week,2}  digits x={ink.X}..{ink.Right - 1} "
                    + $"y={ink.Y}..{ink.Bottom - 1}   left gap {ink.X - 1,2}  right gap "
                    + $"{15 - ink.Right,2}  bottom gap {15 - ink.Bottom,2}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("--- ring slots at 16 px: crisp or mush? alpha across the bar rows ---");
        sb.AppendLine("  06 snapped the slots to whole pixels; 12 was told to verify, not assume.");
        foreach (Candidate c in new[] { Candidate.Decided, Candidate.ThinBar, Candidate.BarOnly })
        {
            using Bitmap glyph = Render(c, 16, Color.White, 44);
            sb.AppendLine($"  {c.Name}  ({c.Note})");
            for (int y = 0; y < 4; y++)
            {
                System.Text.StringBuilder row = new();
                for (int x = 0; x < 16; x++)
                {
                    int a = glyph.GetPixel(x, y).A;
                    row.Append(a switch { 0 => " .", 255 => " #", _ => $"{a * 9 / 255,2}" });
                }
                sb.AppendLine($"    y={y}  {row}");
            }
            sb.AppendLine("    (# = opaque, . = clear, 1..8 = partial — a slot column must read '.')");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The candidate's glyph with the frame's own pixels erased, so the digits' bounding box can be
    /// measured without the outline polluting it. Erasing is crude on purpose — the frame occupies
    /// exactly the bar rows, the bottom row and the two side columns, and nothing else at these
    /// sizes is anywhere near them.
    /// </summary>
    private static Bitmap RenderDigitsOnly(Candidate c, int box, int week)
    {
        Bitmap digits = Render(c, box, Color.White, week);
        if (c.Design == Design.NumberOnly) return digits;

        int barHeight = Math.Max(c.Design == Design.BarWithRings ? 1 : 2,
            (int)Math.Round(box * c.BarFactor));

        using Graphics g = Graphics.FromImage(digits);
        g.CompositingMode = CompositingMode.SourceCopy;
        using SolidBrush clear = new(Color.FromArgb(0, 0, 0, 0));

        g.FillRectangle(clear, 0, 0, box, barHeight);
        if (c.Design is Design.FrameOutline or Design.FrameBar or Design.FrameRings)
        {
            g.FillRectangle(clear, 0, box - 1, box, 1);
            g.FillRectangle(clear, 0, 0, 1, box);
            g.FillRectangle(clear, box - 1, 0, 1, box);
        }

        return digits;
    }
}
