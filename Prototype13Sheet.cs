using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CalendarWeekTray;

// PROTOTYPE — ticket 13. THROWAWAY.
//
// 12 established the instrument: the decision gets taken from real taskbar strips, not from a
// table. This assembles the strips already captured into the one comparison the ticket turns on —
// 07's rule against the winning rule, in the contrast theme where they differ.

internal static class Prototype13Sheet
{
    private readonly record struct Row(string Caption, string Detail, string File);

    private static readonly Row[] Rows =
    [
        new("High Contrast Black — 07's rule as it stands",
            "ink #FFFFFF on #202020 — 16.29:1, fine", "13-hc-high-contrast-black-07-as-decided.png"),
        new("High Contrast Black — HC → MenuText",
            "ink #FFFFFF on #202020 — 16.29:1, identical", "13-hc-high-contrast-black-menutext.png"),
        new("High Contrast WHITE — 07's rule as it stands",
            "ink #FFFFFF on #FFFAEF — 1.04:1  *** INVISIBLE ***", "13-hc-high-contrast-white-07-as-decided.png"),
        new("High Contrast WHITE — HC → MenuText",
            "ink #000000 on #FFFAEF — 20.17:1, matches the shell's own ink", "13-hc-high-contrast-white-menutext.png"),
        new("dark theme — Text (regular), for the weight question",
            "the one lever 06 and 12 left open", "13-weight-Segoe-UI-Variable-Text.png"),
        new("dark theme — Text Semibold (06's decision)",
            "as shipped", "13-weight-Segoe-UI-Variable-Text-Semibold.png"),
        new("dark theme — Display Bold",
            "the heaviest instance available", "13-weight-Segoe-UI-Variable-Display-Bold.png"),
    ];

    public static void WriteAll(string directory)
    {
        const int zoom = 3;
        const int cropWidth = 430;
        const int labelWidth = 420;
        const int gap = 12;

        List<(Row R, Bitmap B)> loaded = [];
        foreach (Row row in Rows)
        {
            string path = Path.Combine(directory, row.File);
            if (File.Exists(path)) loaded.Add((row, new Bitmap(path)));
        }

        if (loaded.Count == 0) return;

        int stripHeight = loaded[0].B.Height;
        int rowHeight = Math.Max(stripHeight, stripHeight * zoom) + gap;
        int width = labelWidth + (cropWidth * zoom) + (gap * 2);

        using Bitmap sheet = new(width, (rowHeight * loaded.Count) + gap, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(38, 38, 42));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using Font caption = new("Segoe UI Semibold", 12f);
            using Font detail = new("Consolas", 10f);
            using SolidBrush white = new(Color.White);
            using SolidBrush grey = new(Color.FromArgb(175, 175, 182));

            int y = gap;
            foreach ((Row r, Bitmap b) in loaded)
            {
                g.DrawString(r.Caption, caption, white, 10, y + 6);
                g.DrawString(r.Detail, detail, grey, 10, y + 32);

                Rectangle crop = new(Math.Max(0, b.Width - cropWidth), 0, Math.Min(cropWidth, b.Width), b.Height);
                g.DrawImage(b, new Rectangle(labelWidth, y, crop.Width * zoom, crop.Height * zoom),
                    crop, GraphicsUnit.Pixel);

                y += rowHeight;
                b.Dispose();
            }
        }

        sheet.Save(Path.Combine(directory, "13-decision-sheet.png"), ImageFormat.Png);
    }
}
