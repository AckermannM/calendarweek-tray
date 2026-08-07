using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace CalendarWeekTray;

// ============================================================================================
//  PROTOTYPE — ticket 06. THROWAWAY CODE. Do not promote any of this to the shipping applet;
//  07 designs the real pipeline and 08 writes the spec.
//
//  Round 1 settled: KW32 on one line is unreadable; the glyph must be antialiased (05 was not);
//  type size must be fitted to a fixed reference or the glyph resizes week to week.
//  Round 2 settled: a bare number does not say "calendar week", and stacking KW above the digits
//  does not work at any size or weight — the calendar signal has to come from the frame.
//  Round 3 — this file — fixes two measured bugs and tunes how the frame's corners render.
// ============================================================================================

internal enum Design
{
    /// <summary>No calendar cue at all. Kept as the legibility reference.</summary>
    NumberOnly,

    /// <summary>Calendar page outline around the number.</summary>
    FrameOutline,

    /// <summary>Calendar page with a filled binding bar along the top.</summary>
    FrameBar,

    /// <summary>The favourite: page plus a bar notched twice into binding rings.</summary>
    FrameRings,

    /// <summary>Only the binding bar, no page — keeps the digits at full size.</summary>
    BarOverNumber,

    /// <summary>Notched bar without the page.</summary>
    BarWithRings,

    /// <summary>Solid rounded chip with the number knocked out.</summary>
    BadgeKnockout,
}

/// <summary>
/// How the page outline is stroked. This exists because a hairline outline's corners read as
/// missing: a 1 px stroke on a straight edge lands square on one row of pixels at full alpha,
/// but the same stroke swept round an arc spreads its coverage across two pixels diagonally, so
/// the corners come out at roughly half the density of the sides. The eye reads that as a gap.
/// </summary>
internal readonly record struct FrameStyle(string Name, float RadiusFactor, float Stroke, int Strikes)
{
    /// <summary>What round 2 shipped, and what looked broken at the corners.</summary>
    public static readonly FrameStyle Round2 = new("r=.14 s=1.0 x1  (round 2)", 0.14f, 1.0f, 1);

    /// <summary>
    /// Re-stroking the same path compounds alpha. Straight edges are already saturated so they do
    /// not change; only the half-lit corner pixels gain density. Radius and weight are untouched.
    /// </summary>
    public static readonly FrameStyle DoubleStrike = new("r=.14 s=1.0 x2  (double-strike)", 0.14f, 1.0f, 2);

    public static readonly FrameStyle TripleStrike = new("r=.14 s=1.0 x3  (triple-strike)", 0.14f, 1.0f, 3);

    /// <summary>Shorter arcs mean fewer half-lit pixels, at the cost of a boxier page.</summary>
    public static readonly FrameStyle TightRadius = new("r=.08 s=1.0 x1  (tight radius)", 0.08f, 1.0f, 1);

    /// <summary>More mass everywhere, so the corners have more to spread.</summary>
    public static readonly FrameStyle Heavier = new("r=.14 s=1.5 x1  (heavier stroke)", 0.14f, 1.5f, 1);

    public static readonly FrameStyle HeavierTight = new("r=.08 s=1.5 x1  (heavy + tight)", 0.08f, 1.5f, 1);

    /// <summary>No arcs at all — the control that proves the diagnosis.</summary>
    public static readonly FrameStyle Square = new("r=0   s=1.0 x1  (square corners)", 0f, 1.0f, 1);

    public static readonly FrameStyle[] All =
        [Round2, DoubleStrike, TripleStrike, TightRadius, Heavier, HeavierTight, Square];
}

/// <summary>
/// How the digits are horizontally centred. Segoe's "1" is a bare stem with a thin diagonal flag
/// off its top-left and no foot serif. That flag widens the ink bounding box while carrying almost
/// none of the glyph's visual weight, so centring the box pushes the stems — the part the eye
/// actually tracks — to the right. Weeks 1 and 11 are the worst cases and 44 is unaffected.
/// </summary>
internal enum Centring
{
    /// <summary>Geometric: the ink bounding box straddles the centre.</summary>
    BoundingBox,

    /// <summary>Optical: the alpha-weighted centre of mass sits on the centre.</summary>
    OpticalMass,

    /// <summary>Halfway between the two, in case pure optical over-corrects.</summary>
    Blend,
}

internal static class PrototypeGlyph
{
    /// <summary>Your pick from round 1.</summary>
    public const string Face = "Segoe UI Variable Text Semibold";

    public const string FaceRegular = "Segoe UI Variable Text";

    /// <summary>
    /// Also your pick from round 1: smooth AA rather than grid-fit. This must never be left unset
    /// or set to SystemDefault — GDI+ then renders subpixel ClearType, which writes coloured
    /// fringes at full alpha onto a transparent icon, tuned for a background the shell never
    /// supplies. That, plus a ~10 px type size, is what made 05 look the way it did.
    /// </summary>
    public const TextRenderingHint Hint = TextRenderingHint.AntiAlias;

    /// <summary>Your pick from round 3: square corners have no arcs, so no corner-density problem.</summary>
    public static FrameStyle Style { get; set; } = FrameStyle.Square;

    /// <summary>
    /// Your pick from round 4. Pure optical mass over-corrected: shifting each week by a different
    /// subpixel amount changes the phase its stems land on, and a stem that straddles two pixels
    /// renders wider and softer than one sitting on a single column — so some weeks read as a
    /// larger number than others. Halfway keeps the flag correction without that side effect.
    /// </summary>
    public static Centring Centre { get; set; } = Centring.Blend;

    /// <summary>
    /// The type size the taskbar clock uses at this icon size. 02 measured 12 epx where
    /// SM_CXSMICON is 16, and both track DPI together, so the ratio holds across scalings.
    /// </summary>
    public static int ClockSizeFor(int box) => (int)Math.Round(box * 12.0 / 16.0);

    // --- the reference label -------------------------------------------------------------------

    private static readonly Dictionary<string, string> ReferenceCache = [];

    /// <summary>
    /// The widest label the applet can ever show. Everything is fitted to this, never to the week
    /// being displayed — otherwise the glyph resizes as the year goes on.
    /// <para>
    /// It is not "00". Segoe UI Variable's figures are proportional, not tabular: measured at
    /// 23 px, "4" inks 13 px against 11 px for every other digit, so the widest week of 01..53 is
    /// "44" at 26 px against "00"'s 24. Fitting to "00" silently clipped weeks 4, 14, 24, 34 and
    /// 40–49 — a bug that would only have shown up in production, in October.
    /// </para>
    /// </summary>
    private static string ReferenceFor(string face)
    {
        if (ReferenceCache.TryGetValue(face, out string? cached)) return cached;

        using Font probe = new(face, 32f, FontStyle.Regular, GraphicsUnit.Pixel);
        char widest = '0';
        int widestWidth = -1;
        for (char c = '0'; c <= '9'; c++)
        {
            int w = MeasureInk(c.ToString(), probe).Width;
            if (w > widestWidth) (widestWidth, widest) = (w, c);
        }

        string reference = new(widest, 2);
        ReferenceCache[face] = reference;
        return reference;
    }

    public static Bitmap Render(Design design, int box, Color colour, int week, bool padded)
    {
        string number = padded ? week.ToString("00", CultureInfo.InvariantCulture)
                               : week.ToString(CultureInfo.InvariantCulture);

        Bitmap bitmap = new(box, box, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.TextRenderingHint = Hint;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        switch (design)
        {
            case Design.NumberOnly:
                DrawNumber(g, number, box, colour, new RectangleF(0, 0, box, box));
                break;

            case Design.FrameOutline:
                DrawFrame(bitmap, g, number, box, colour, bar: false, rings: false);
                break;

            case Design.FrameBar:
                DrawFrame(bitmap, g, number, box, colour, bar: true, rings: false);
                break;

            case Design.FrameRings:
                DrawFrame(bitmap, g, number, box, colour, bar: true, rings: true);
                break;

            case Design.BarOverNumber:
                DrawBar(bitmap, g, number, box, colour, rings: false);
                break;

            case Design.BarWithRings:
                DrawBar(bitmap, g, number, box, colour, rings: true);
                break;

            case Design.BadgeKnockout:
                DrawBadge(bitmap, g, number, box, colour);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(design));
        }

        return bitmap;
    }

    // --- designs -----------------------------------------------------------------------------

    /// <summary>Prototype scaffolding: the digit ink height the last render achieved.</summary>
    public static int LastNumberInkHeight { get; private set; }

    private static void DrawNumber(Graphics g, string number, int box, Color colour, RectangleF area)
    {
        string reference = ReferenceFor(Face);
        (int px, Rectangle refInk) = Fit(reference, Face, (int)area.Width, (int)area.Height, box);
        LastNumberInkHeight = refInk.Height;

        using Font font = new(Face, px, FontStyle.Regular, GraphicsUnit.Pixel);
        using SolidBrush brush = new(colour);
        Rectangle ink = MeasureInk(number, font);

        // Vertical placement comes from the reference, not the week, so the digits do not shift
        // baseline between weeks. Rounded, because digits have flat tops and feet that stay
        // crisper on the pixel grid.
        float y = MathF.Round(area.Y + ((area.Height - refInk.Height) / 2f)) - refInk.Y;
        float x = area.X + ((area.Width - ink.Width) / 2f) - ink.X;

        // Predicting the offset from a measurement taken at a different origin does not work: the
        // rasteriser's antialiasing spills differently depending on the subpixel phase it lands
        // on, so the ink that actually appears is not the ink that was measured. Draw it, measure
        // where it really went, correct, repeat. Two passes is normally enough.
        //
        // The digits go onto their own layer so "where did the ink go" stays answerable even when
        // a frame has already been drawn underneath.
        using Bitmap layer = new(box, box, PixelFormat.Format32bppArgb);
        float targetCentre = area.X + (area.Width / 2f);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            using (Graphics lg = Graphics.FromImage(layer))
            {
                lg.Clear(Color.Transparent);
                lg.TextRenderingHint = Hint;
                lg.DrawString(number, font, brush, x, y, StringFormat.GenericTypographic);
            }

            Rectangle actual = InkBoundsOf(layer);
            if (actual.IsEmpty) break;

            float boxCentre = actual.X + (actual.Width / 2f);
            float massCentre = OpticalCentreX(layer);
            float actualCentre = Centre switch
            {
                Centring.BoundingBox => boxCentre,
                Centring.OpticalMass => massCentre,
                Centring.Blend => (boxCentre + massCentre) / 2f,
                _ => boxCentre,
            };

            float drift = targetCentre - actualCentre;
            if (MathF.Abs(drift) < 0.15f) break;
            x += drift;
        }

        g.DrawImageUnscaled(layer, 0, 0);
    }

    private static void DrawFrame(Bitmap bitmap, Graphics g, string number, int box, Color colour,
        bool bar, bool rings)
    {
        FrameStyle style = Style;
        float stroke = style.Stroke;
        RectangleF page = new(stroke / 2f, stroke / 2f, box - stroke, box - stroke);
        float radius = box * style.RadiusFactor;

        using SolidBrush brush = new(colour);
        using Pen pen = new(colour, stroke);
        using GraphicsPath path = RoundedRect(page, radius);
        for (int i = 0; i < style.Strikes; i++) g.DrawPath(pen, path);

        int barHeight = 0;
        if (bar)
        {
            barHeight = Math.Max(2, (int)Math.Round(box * 0.17));

            // Whole pixels, from the very edge. Filling from page.Y (a half-pixel, because the
            // outline's stroke is centred there) leaves the bar's bottom edge straddling a pixel
            // row, which shows up as a grey seam under an otherwise solid bar. The bar overlaps
            // the outline's top stroke, but they are the same colour so the overlap is invisible.
            using GraphicsPath barPath = RoundedRectTopOnly(new RectangleF(0, 0, box, barHeight), radius);
            g.FillPath(brush, barPath);
        }

        if (rings && barHeight >= 2)
        {
            KnockOut(g, bitmap, box, mg => DrawSlots(mg, 0, box, 0, barHeight, box));
        }

        // One pixel of air inside the border, no more: every pixel of padding comes off the digits.
        const int bodyPad = 1;
        float top = page.Y + barHeight + bodyPad;
        float bottom = page.Bottom - bodyPad;
        RectangleF body = RectangleF.FromLTRB(page.X + bodyPad, top, page.Right - bodyPad, bottom);

        if (body.Width > 2 && body.Height > 2) DrawNumber(g, number, box, colour, body);
    }

    private static void DrawBar(Bitmap bitmap, Graphics g, string number, int box, Color colour,
        bool rings)
    {
        int barHeight = Math.Max(1, (int)Math.Round(box * 0.13));
        int inset = (int)Math.Round(box * 0.06);
        float width = box - (inset * 2);

        using SolidBrush brush = new(colour);
        g.FillRectangle(brush, inset, 0, width, barHeight);

        if (rings && barHeight >= 2)
        {
            KnockOut(g, bitmap, box, mg => DrawSlots(mg, inset, width, 0, barHeight, box));
        }

        DrawNumber(g, number, box, colour, RectangleF.FromLTRB(0, barHeight + 1, box, box));
    }

    private static void DrawBadge(Bitmap bitmap, Graphics g, string number, int box, Color colour)
    {
        using SolidBrush brush = new(colour);
        using GraphicsPath path = RoundedRect(new RectangleF(0, 0, box, box), box * 0.22f);
        g.FillPath(brush, path);

        int pad = Math.Max(1, (int)Math.Round(box * 0.12));
        RectangleF area = RectangleF.FromLTRB(pad, pad, box - pad, box - pad);
        KnockOut(g, bitmap, box, mg =>
        {
            mg.TextRenderingHint = Hint;
            DrawNumber(mg, number, box, Color.White, area);
        });
    }

    /// <summary>
    /// Two slots cut clean through the binding bar. Notching the bar reads as a calendar's rings
    /// at a size where drawing actual rings above the page would cost more height than the signal
    /// is worth.
    /// </summary>
    private static void DrawSlots(Graphics g, float x, float width, float y, int barHeight, int box)
    {
        // Snapped to whole pixels. A slot on a fractional boundary antialiases into grey mush at
        // 16 px, where the slot is only one pixel wide to begin with.
        int slotWidth = Math.Max(1, (int)Math.Round(box * 0.08));
        foreach (float centre in new[] { x + (width * 0.32f), x + (width * 0.68f) })
        {
            int left = (int)Math.Round(centre - (slotWidth / 2f));
            g.FillRectangle(Brushes.White, left, y - 1, slotWidth, barHeight + 1);
        }
    }

    // --- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// Multiplies the target's alpha by the inverse of what <paramref name="paint"/> draws, cutting
    /// a genuine hole in it. Painting the shape in "the background colour" is not an option: the
    /// background is a taskbar whose colour the applet does not know and must not assume.
    /// </summary>
    private static void KnockOut(Graphics g, Bitmap target, int box, Action<Graphics> paint)
    {
        using Bitmap mask = new(box, box, PixelFormat.Format32bppArgb);
        using (Graphics mg = Graphics.FromImage(mask))
        {
            mg.Clear(Color.Transparent);
            mg.SmoothingMode = SmoothingMode.AntiAlias;
            mg.PixelOffsetMode = PixelOffsetMode.HighQuality;
            paint(mg);
        }

        g.Flush(FlushIntention.Sync);

        Rectangle rect = new(0, 0, target.Width, target.Height);
        BitmapData t = target.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        BitmapData m = mask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* tr = (byte*)t.Scan0 + (y * t.Stride);
                    byte* mr = (byte*)m.Scan0 + (y * m.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int a = tr[(x * 4) + 3];
                        if (a == 0) continue;
                        tr[(x * 4) + 3] = (byte)(a * (255 - mr[(x * 4) + 3]) / 255);
                    }
                }
            }
        }
        finally
        {
            mask.UnlockBits(m);
            target.UnlockBits(t);
        }
    }

    private const int Pad = 16;

    public static Rectangle MeasureInk(string text, Font font)
    {
        int canvas = (int)Math.Ceiling(font.Size * (text.Length + 2) * 1.6) + (Pad * 2);
        using Bitmap probe = new(canvas, canvas, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(probe);
        g.Clear(Color.Transparent);
        g.TextRenderingHint = Hint;
        g.DrawString(text, font, Brushes.White, Pad, Pad, StringFormat.GenericTypographic);

        BitmapData data = probe.LockBits(new Rectangle(0, 0, canvas, canvas), ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        int minX = canvas, minY = canvas, maxX = -1, maxY = -1;
        try
        {
            unsafe
            {
                for (int y = 0; y < canvas; y++)
                {
                    byte* row = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = 0; x < canvas; x++)
                    {
                        if (row[(x * 4) + 3] == 0) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }
        finally
        {
            probe.UnlockBits(data);
        }

        return maxX < 0 ? Rectangle.Empty
            : Rectangle.FromLTRB(minX - Pad, minY - Pad, maxX + 1 - Pad, maxY + 1 - Pad);
    }

    /// <summary>Largest integer type size whose ink fits the given area. Integer sizes only:
    /// fractional sizes at these dimensions land stems between pixels and read as blur.</summary>
    public static (int Px, Rectangle Ink) Fit(string text, string family, int maxWidth, int maxHeight,
        int box)
    {
        for (int px = Math.Max(4, box * 2); px >= 4; px--)
        {
            using Font probe = new(family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            Rectangle ink = MeasureInk(text, probe);
            if (ink.IsEmpty) continue;
            if (ink.Width <= maxWidth && ink.Height <= maxHeight) return (px, ink);
        }

        using Font fallback = new(family, 4, FontStyle.Regular, GraphicsUnit.Pixel);
        return (4, MeasureInk(text, fallback));
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        GraphicsPath path = new();
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>A bar whose top corners are rounded to match the page and whose bottom is square.</summary>
    private static GraphicsPath RoundedRectTopOnly(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height * 2f));
        GraphicsPath path = new();
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        path.CloseFigure();
        return path;
    }

    // --- prototype scaffolding -----------------------------------------------------------------

    /// <summary>
    /// The alpha-weighted horizontal centre of mass — where the glyph's ink actually sits, rather
    /// than where its bounding box does. A thin flag on one side barely moves this; a stem does.
    /// </summary>
    public static float OpticalCentreX(Bitmap bmp)
    {
        double weighted = 0, total = 0;
        Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* row = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int a = row[(x * 4) + 3];
                        if (a == 0) continue;
                        weighted += a * (x + 0.5);
                        total += a;
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return total == 0 ? bmp.Width / 2f : (float)(weighted / total);
    }

    /// <summary>Bounding box of every pixel carrying any alpha at all.</summary>
    public static Rectangle InkBoundsOf(Bitmap bmp)
    {
        int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (bmp.GetPixel(x, y).A == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    public static string DumpFits()
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"reference label = \"{ReferenceFor(Face)}\"  (widest of 01..53)");
        sb.AppendLine();

        foreach (int box in new[] { 16, 24, 32 })
        {
            sb.AppendLine($"--- centring at {box} px: transparent margin each side of the digits ---");
            foreach (int week in new[] { 32, 11, 44, 53, 1, 8 })
            {
                using Bitmap bmp = Render(Design.NumberOnly, box, Color.White, week, padded: true);
                Rectangle ink = InkBoundsOf(bmp);
                int left = ink.X, right = box - ink.Right;
                sb.AppendLine($"  week {week,2}  ink w={ink.Width,2}  left={left,2} right={right,2}  " +
                    $"{(Math.Abs(left - right) <= 0 ? "centred" : $"off by {Math.Abs(left - right)}")}");
            }
            sb.AppendLine();
        }

        foreach (int box in new[] { 16, 24, 32 })
        {
            sb.AppendLine($"--- digit ink height at {box} px (clock type is {ClockSizeFor(box)} px) ---");
            foreach (Design d in Enum.GetValues<Design>())
            {
                using Bitmap _ = Render(d, box, Color.White, 44, padded: true);
                int h = LastNumberInkHeight;
                sb.AppendLine($"  {d,-16} digits {h,2} px  ({h * 100 / box,3}% of the icon)");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
