using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 06. THROWAWAY. Builds the contact sheets the decision is made from.

internal static class Prototype06Sheet
{
    private static readonly int[] Sizes = [16, 20, 24, 32];

    private static readonly Color DarkTaskbar = Color.FromArgb(32, 32, 32);
    private static readonly Color LightTaskbar = Color.FromArgb(243, 243, 243);
    private static readonly Color DarkThemeGlyph = Color.White;
    private static readonly Color LightThemeGlyph = Color.FromArgb(26, 26, 26);

    private static readonly Design[] Designs = Enum.GetValues<Design>();

    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);

        // The open question: how the digits should be centred, now that square corners are settled.
        string modes = Path.Combine(directory, "06r4-centring-modes.png");
        WriteCentringModeSheet(modes);
        Console.WriteLine(modes);

        string corners = Path.Combine(directory, "06r3-corner-tuning.png");
        WriteCornerSheet(corners);
        Console.WriteLine(corners);

        foreach (int size in Sizes)
        {
            string path = Path.Combine(directory, $"06r4-designs-{size}px.png");
            WriteDesignSheet(path, size, 11);
            Console.WriteLine(path);
        }
    }

    /// <summary>
    /// FrameRings under every corner treatment, at every size, magnified. This is the sheet the
    /// corner question is answered from.
    /// </summary>
    private static void WriteCornerSheet(string path)
    {
        FrameStyle[] styles = FrameStyle.All;
        const int zoom = 12, pad = 14, labelW = 290, headerH = 62;
        int magW = 32 * zoom;
        int rowH = magW + pad;

        int[] columns = new int[Sizes.Length];
        int x = labelW;
        for (int i = 0; i < Sizes.Length; i++)
        {
            columns[i] = x;
            x += (Sizes[i] * zoom) + pad;
        }

        using Bitmap sheet = new(x + pad, headerH + (styles.Length * rowH) + pad,
            PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 12f, FontStyle.Bold);

        g.DrawString("FrameRings — corner treatments  (week 44, the widest label)",
            header, Brushes.White, 8, 10);
        g.DrawString("a 1 px stroke on a straight edge is fully lit; swept round an arc it spreads "
            + "over two pixels and halves in density", small, Brushes.Gray, 8, 30);
        for (int i = 0; i < Sizes.Length; i++)
        {
            g.DrawString($"{Sizes[i]} px", small, Brushes.Gold, columns[i], 46);
        }

        FrameStyle original = PrototypeGlyph.Style;
        try
        {
            for (int r = 0; r < styles.Length; r++)
            {
                PrototypeGlyph.Style = styles[r];
                int y = headerH + (r * rowH);
                g.DrawString(styles[r].Name, label, Brushes.White, 8, y + (rowH / 2) - 8);

                for (int c = 0; c < Sizes.Length; c++)
                {
                    int box = Sizes[c];
                    using Bitmap glyph = PrototypeGlyph.Render(Design.FrameRings, box,
                        DarkThemeGlyph, 44, padded: false);
                    int side = box * zoom;
                    Cell(g, columns[c], y + ((magW - side) / 2), side, side, DarkTaskbar, glyph);
                }
            }
        }
        finally
        {
            PrototypeGlyph.Style = original;
        }

        sheet.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// The three centring rules side by side, on the weeks where they disagree. Unpadded, since
    /// that is now the preference — and a lone "1" is the hardest case for any of them.
    /// </summary>
    private static void WriteCentringModeSheet(string path)
    {
        int[] weeks = [1, 11, 14, 44, 32];
        Centring[] modes = [Centring.BoundingBox, Centring.OpticalMass, Centring.Blend];
        const int zoom = 14, pad = 14, labelW = 150, headerH = 62, box = 24;
        int magW = box * zoom;
        int rowH = magW + pad;

        using Bitmap sheet = new(labelW + ((magW + pad) * modes.Length) + pad,
            headerH + (weeks.Length * rowH) + pad, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 12f, FontStyle.Bold);

        g.DrawString("centring rules — FrameRings, square corners, unpadded, 24 px",
            header, Brushes.White, 8, 10);
        g.DrawString("red line is the icon's true centre. \"1\" is a stem with a flag off its "
            + "top-left: the flag widens the box but carries no weight.", small, Brushes.Gray, 8, 30);

        for (int m = 0; m < modes.Length; m++)
        {
            g.DrawString(modes[m].ToString(), small, Brushes.Gold, labelW + (m * (magW + pad)), 46);
        }

        Centring originalMode = PrototypeGlyph.Centre;
        try
        {
            for (int i = 0; i < weeks.Length; i++)
            {
                int y = headerH + (i * rowH);
                g.DrawString($"week {weeks[i]}", label, Brushes.White, 8, y + (rowH / 2) - 8);

                for (int m = 0; m < modes.Length; m++)
                {
                    PrototypeGlyph.Centre = modes[m];
                    using Bitmap glyph = PrototypeGlyph.Render(Design.FrameRings, box,
                        DarkThemeGlyph, weeks[i], padded: false);
                    CellWithCentreGuide(g, labelW + (m * (magW + pad)), y, magW, DarkTaskbar,
                        glyph, zoom);
                }
            }
        }
        finally
        {
            PrototypeGlyph.Centre = originalMode;
        }

        sheet.Save(path, ImageFormat.Png);
    }

    private static void WriteCentringSheet(string path)
    {
        int[] weeks = [32, 11, 44, 53, 8];
        const int zoom = 14, pad = 14, labelW = 150, headerH = 58, box = 24;
        int magW = box * zoom;
        int rowH = magW + pad;

        using Bitmap sheet = new(labelW + ((magW + pad) * 2) + pad,
            headerH + (weeks.Length * rowH) + pad, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 12f, FontStyle.Bold);

        g.DrawString("centring, 24 px — guides mark the icon's true centre", header, Brushes.White, 8, 10);
        g.DrawString("both columns are the fixed renderer: fitted to \"44\", centred by measurement",
            small, Brushes.Gray, 8, 30);
        g.DrawString("NumberOnly", small, Brushes.Gold, labelW, 44);
        g.DrawString("FrameRings", small, Brushes.Gold, labelW + magW + pad, 44);

        for (int i = 0; i < weeks.Length; i++)
        {
            int y = headerH + (i * rowH);
            g.DrawString($"week {weeks[i]}", label, Brushes.White, 8, y + (rowH / 2) - 8);

            using Bitmap plain = PrototypeGlyph.Render(Design.NumberOnly, box, DarkThemeGlyph,
                weeks[i], padded: true);
            using Bitmap framed = PrototypeGlyph.Render(Design.FrameRings, box, DarkThemeGlyph,
                weeks[i], padded: true);

            Rectangle ink = PrototypeGlyph.InkBoundsOf(plain);
            g.DrawString($"left {ink.X}  right {box - ink.Right}", small, Brushes.Gray, 8,
                y + (rowH / 2) + 8);

            CellWithCentreGuide(g, labelW, y, magW, DarkTaskbar, plain, zoom);
            CellWithCentreGuide(g, labelW + magW + pad, y, magW, DarkTaskbar, framed, zoom);
        }

        sheet.Save(path, ImageFormat.Png);
    }

    private static void WriteDesignSheet(string path, int box, int week)
    {
        const int zoom = 10, pad = 14, labelW = 250, oneToOneW = 80, headerH = 46;
        int magW = box * zoom;
        int rowH = Math.Max(magW, 52) + pad;
        int c0 = labelW, c1 = c0 + oneToOneW, c2 = c1 + oneToOneW, c3 = c2 + magW + pad;

        using Bitmap sheet = new(c3 + magW + pad, headerH + (Designs.Length * rowH) + pad,
            PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 18, 20));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using Font label = new("Consolas", 11f);
        using Font small = new("Consolas", 9f);
        using Font header = new("Consolas", 12f, FontStyle.Bold);

        g.DrawString($"{box} px  —  week {week}  —  {PrototypeGlyph.Style.Name}",
            header, Brushes.White, 8, 10);
        g.DrawString("1:1 dark", small, Brushes.Gold, c0, 30);
        g.DrawString("1:1 light", small, Brushes.Gold, c1, 30);
        g.DrawString($"x{zoom} dark", small, Brushes.Gold, c2, 30);
        g.DrawString($"x{zoom} light", small, Brushes.Gold, c3, 30);

        for (int i = 0; i < Designs.Length; i++)
        {
            Design d = Designs[i];
            int y = headerH + (i * rowH);
            int mid = y + (rowH / 2);

            using Bitmap dark = PrototypeGlyph.Render(d, box, DarkThemeGlyph, week, padded: false);
            using Bitmap light = PrototypeGlyph.Render(d, box, LightThemeGlyph, week, padded: false);

            g.DrawString(d.ToString(), label, Brushes.White, 8, mid - 16);
            g.DrawString($"digits {PrototypeGlyph.LastNumberInkHeight} px", small, Brushes.Gray, 8, mid + 4);

            Cell(g, c0, mid - (box / 2), box, box, DarkTaskbar, dark);
            Cell(g, c1, mid - (box / 2), box, box, LightTaskbar, light);
            Cell(g, c2, y, magW, magW, DarkTaskbar, dark);
            Cell(g, c3, y, magW, magW, LightTaskbar, light);
        }

        sheet.Save(path, ImageFormat.Png);
    }

    private static void Cell(Graphics g, int x, int y, int w, int h, Color background, Bitmap glyph)
    {
        using SolidBrush brush = new(background);
        g.FillRectangle(brush, x, y, w, h);
        g.DrawImage(glyph, new Rectangle(x, y, w, h));
    }

    /// <summary>As Cell, plus a hairline down the exact centre so a one-pixel bias is visible.</summary>
    private static void CellWithCentreGuide(Graphics g, int x, int y, int side, Color background,
        Bitmap glyph, int zoom)
    {
        Cell(g, x, y, side, side, background, glyph);
        using Pen guide = new(Color.FromArgb(150, 255, 64, 64));
        float centre = x + (side / 2f);
        g.DrawLine(guide, centre, y, centre, y + side);
    }
}
