using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace CalendarWeekTray;

internal readonly record struct GlyphSpec(int Week, int SizePx, Color Ink);

internal readonly record struct GlyphMetrics(
    int TypeSizePx,        // the fitted integer type size
    Rectangle DigitInk,    // ink bounds of the digits, measured on their own layer
    Rectangle Body,        // the rect the digits were fitted into
    Rectangle Page,        // the page outline's rect
    bool Converged);       // did the centring loop settle inside its 4-iteration cap

/// <summary>
/// Draws the calendar-page glyph: square corners, a 1 px outline, a filled binding bar notched
/// with two ring slots, and the unpadded week number in the body. Pure static — no shell, no
/// config, no state (spec §5.1).
/// </summary>
internal static class GlyphRenderer
{
    private const string Face = "Segoe UI Variable Text Semibold";
    private const TextRenderingHint Hint = TextRenderingHint.AntiAlias;

    // internal, not private: ticket 03's test suite measures against these directly rather than
    // keeping a second, driftable copy of the same numbers (§5.3 — measured, not tuned).
    internal const float Stroke = 1.0f;
    internal const float BarFactor = 0.17f;
    internal const int BodyPad = 1;
    internal const float SlotWidthFactor = 0.08f;
    internal const float SlotCentreA = 0.32f;
    internal const float SlotCentreB = 0.68f;
    private const int MeasurePadding = 16;

    // All three caches are keyed on values that are inputs to the render, so none can go stale the
    // way a cached Font would (§5.7). Body width/height, and therefore the fit result and the
    // converged vertical placement, are a pure function of (face, box) once the constants above are
    // fixed. Concurrent because nothing here pins Render to the UI thread, and a test suite is free
    // to call it from several at once.
    private static readonly ConcurrentDictionary<string, string> ReferenceCache = new();
    private static readonly ConcurrentDictionary<(string Face, int Box), (int Px, Rectangle Ink)> FitCache = new();
    private static readonly ConcurrentDictionary<(string Face, int Box), float> YCache = new();

    internal static Bitmap Render(GlyphSpec spec) => Render(spec, out _);

    internal static Bitmap Render(GlyphSpec spec, out GlyphMetrics metrics)
    {
        int box = spec.SizePx;
        string number = spec.Week.ToString(CultureInfo.InvariantCulture);

        Bitmap bitmap = new(box, box, PixelFormat.Format32bppArgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.TextRenderingHint = Hint;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            RectangleF page = new(Stroke / 2f, Stroke / 2f, box - Stroke, box - Stroke);
            using (Pen pen = new(spec.Ink, Stroke))
            {
                graphics.DrawRectangle(pen, page.X, page.Y, page.Width, page.Height);
            }

            // Whole pixels, from the icon edge. Filling from page.Y (a half pixel, because the
            // outline's stroke is centred there) leaves the bar's bottom edge straddling a pixel
            // row. The bar overlaps the outline's top stroke, but they share a colour, so that is
            // invisible. Math.Max floors this at 2, so the knockout below always has a bar to cut.
            int barHeight = Math.Max(2, (int)Math.Round(box * BarFactor));
            using (SolidBrush brush = new(spec.Ink))
            {
                graphics.FillRectangle(brush, 0, 0, box, barHeight);
            }

            KnockOut(graphics, bitmap, box, maskGraphics => DrawRingSlots(maskGraphics, box, barHeight));

            RectangleF body = RectangleF.FromLTRB(
                page.X + BodyPad,
                page.Y + barHeight + BodyPad,
                page.Right - BodyPad,
                page.Bottom - BodyPad);

            Rectangle digitInk = Rectangle.Empty;
            int typeSizePx = 0;
            bool converged = false;

            // Every pixel of padding comes off the digits; below this there is no room left to fit
            // any of them, so leave the body empty rather than fitting a degenerate font size.
            if (body.Width > 2 && body.Height > 2)
            {
                (digitInk, typeSizePx, converged) = DrawNumber(graphics, number, box, spec.Ink, body);
            }

            metrics = new GlyphMetrics(
                TypeSizePx: typeSizePx,
                DigitInk: digitInk,
                Body: Rectangle.Round(body),
                Page: Rectangle.Round(page),
                Converged: converged);

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    // --- the number ------------------------------------------------------------------------

    private static (Rectangle DigitInk, int TypeSizePx, bool Converged) DrawNumber(
        Graphics graphics, string number, int box, Color colour, RectangleF area)
    {
        string reference = ReferenceFor(Face);
        (int typeSizePx, Rectangle referenceInk) = Fit(reference, Face, (int)area.Width, (int)area.Height, box);

        using Font font = new(Face, typeSizePx, FontStyle.Regular, GraphicsUnit.Pixel);
        using SolidBrush brush = new(colour);
        Rectangle ink = MeasureInk(number, font);

        // Vertical placement comes from a one-time convergence against the reference ink, cached per
        // (face, box) exactly like the fit result, so every week at a given box size shares one
        // baseline and only the reference calculation ever runs the loop.
        float y = YCache.GetOrAdd((Face, box), _ => ComputeReferenceY(reference, font, referenceInk, area, box));
        float x = area.X + ((area.Width - ink.Width) / 2f) - ink.X;

        // The digits go onto their own layer so "where did the ink go" stays answerable
        // underneath a frame that has already been drawn.
        using Bitmap layer = new(box, box, PixelFormat.Format32bppArgb);
        float targetCentre = area.X + (area.Width / 2f);

        Rectangle digitInk = Rectangle.Empty;
        bool converged = false;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            using (Graphics layerGraphics = Graphics.FromImage(layer))
            {
                layerGraphics.Clear(Color.Transparent);
                layerGraphics.TextRenderingHint = Hint;
                layerGraphics.DrawString(number, font, brush, x, y, StringFormat.GenericTypographic);
            }

            digitInk = InkBoundsOf(layer);
            if (digitInk.IsEmpty)
            {
                break;
            }

            // Predicting a draw offset from a measurement taken at a different origin does not
            // work: the rasteriser's antialiasing spills differently depending on the subpixel
            // phase the glyph lands on. Draw it, measure where it really went, correct, repeat.
            float boxCentre = digitInk.X + (digitInk.Width / 2f);
            float massCentre = OpticalCentreX(layer);
            float blendCentre = (boxCentre + massCentre) / 2f;

            float drift = targetCentre - blendCentre;
            if (MathF.Abs(drift) < 0.15f)
            {
                converged = true;
                break;
            }

            x += drift;
        }

        graphics.DrawImageUnscaled(layer, 0, 0);
        return (digitInk, typeSizePx, converged);
    }

    /// <summary>
    /// Converges the reference ink's blended vertical centre onto <paramref name="area"/>'s own
    /// vertical centre — a mirror of the horizontal loop above, but run once against the reference
    /// ink only (never a week's own digits) and cached by the caller, not per render. Same 4-iteration
    /// cap and 0.15 px tolerance as the horizontal loop.
    /// </summary>
    private static float ComputeReferenceY(string reference, Font font, Rectangle referenceInk, RectangleF area, int box)
    {
        float targetCentre = area.Y + (area.Height / 2f);
        float y = MathF.Round(area.Y + ((area.Height - referenceInk.Height) / 2f)) - referenceInk.Y;

        // Drawn at the same horizontal position the real digit draw settles at (area's horizontal
        // centre, mirroring DrawNumber's own initial x guess) rather than area.X: GDI+'s antialiasing
        // couples the horizontal subpixel phase into the vertical ink measurement, so measuring at an
        // arbitrary x would calibrate y against a phase no real render ever lands on.
        float x = area.X + ((area.Width - referenceInk.Width) / 2f) - referenceInk.X;

        using SolidBrush brush = new(Color.White);
        using Bitmap layer = new(box, box, PixelFormat.Format32bppArgb);

        // Unlike the horizontal loop — which is free to just keep whatever "layer" last drew, because
        // it composites that layer directly — this loop hands back a bare y for a draw that happens
        // later, so simply keeping the last attempt's y risks handing back the worse of two states
        // when the reference sits in a GDI+ two-phase dead zone (drift oscillates rather than
        // shrinking). Tracking the best of the (at most 4) attempts tried is not a new tuning knob —
        // the cap and tolerance are unchanged — it only decides which already-computed candidate to
        // report if none of them converge.
        float bestY = y;
        float bestDrift = float.PositiveInfinity;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            using (Graphics layerGraphics = Graphics.FromImage(layer))
            {
                layerGraphics.Clear(Color.Transparent);
                layerGraphics.TextRenderingHint = Hint;
                layerGraphics.DrawString(reference, font, brush, x, y, StringFormat.GenericTypographic);
            }

            Rectangle ink = InkBoundsOf(layer);
            if (ink.IsEmpty)
            {
                break;
            }

            float boxCentre = ink.Y + (ink.Height / 2f);
            float massCentre = OpticalCentreY(layer);
            float blendCentre = (boxCentre + massCentre) / 2f;

            float drift = targetCentre - blendCentre;
            if (MathF.Abs(drift) < MathF.Abs(bestDrift))
            {
                bestY = y;
                bestDrift = drift;
            }

            if (MathF.Abs(drift) < 0.15f)
            {
                break;
            }

            y += drift;
        }

        return bestY;
    }

    /// <summary>
    /// The widest label the applet can ever show — never "00". Segoe UI Variable's figures are
    /// proportional, not tabular, so the widest week of 1..53 is whichever digit measures widest,
    /// doubled. Probing rather than hard-coding keeps this correct if the installed face changes.
    /// </summary>
    private static string ReferenceFor(string face) => ReferenceCache.GetOrAdd(face, ComputeReference);

    private static string ComputeReference(string face)
    {
        using Font probe = new(face, 32f, FontStyle.Regular, GraphicsUnit.Pixel);
        char widest = '0';
        int widestWidth = -1;
        for (char digit = '0'; digit <= '9'; digit++)
        {
            int width = MeasureInk(digit.ToString(), probe).Width;
            if (width > widestWidth)
            {
                (widestWidth, widest) = (width, digit);
            }
        }

        return new string(widest, 2);
    }

    /// <summary>Largest integer type size whose ink fits the given area. Integer sizes only:
    /// fractional sizes at these dimensions land stems between pixels and read as blur.</summary>
    private static (int Px, Rectangle Ink) Fit(string text, string face, int maxWidth, int maxHeight, int box) =>
        FitCache.GetOrAdd((face, box), _ => ComputeFit(text, face, maxWidth, maxHeight, box));

    private static (int Px, Rectangle Ink) ComputeFit(string text, string face, int maxWidth, int maxHeight, int box)
    {
        for (int px = Math.Max(4, box * 2); px >= 4; px--)
        {
            using Font probe = new(face, px, FontStyle.Regular, GraphicsUnit.Pixel);
            Rectangle ink = MeasureInk(text, probe);
            if (ink.IsEmpty)
            {
                continue;
            }

            if (ink.Width <= maxWidth && ink.Height <= maxHeight)
            {
                return (px, ink);
            }
        }

        using Font fallback = new(face, 4, FontStyle.Regular, GraphicsUnit.Pixel);
        return (4, MeasureInk(text, fallback));
    }

    // --- the binding bar ---------------------------------------------------------------------

    /// <summary>
    /// Two slots cut clean through the binding bar, snapped to whole pixels — a slot on a
    /// fractional boundary antialiases into grey mush at 16 px, where it is one pixel wide.
    /// </summary>
    private static void DrawRingSlots(Graphics graphics, int box, int barHeight)
    {
        int slotWidth = Math.Max(1, (int)Math.Round(box * SlotWidthFactor));
        foreach (float centre in new float[] { box * SlotCentreA, box * SlotCentreB })
        {
            int left = (int)Math.Round(centre - (slotWidth / 2f));
            graphics.FillRectangle(Brushes.White, left, -1, slotWidth, barHeight + 1);
        }
    }

    /// <summary>
    /// Multiplies the target's alpha by the inverse of what <paramref name="paint"/> draws,
    /// cutting a genuine hole in it. Painting the shape in "the background colour" is not an
    /// option: the background is a taskbar whose colour the applet does not know.
    /// </summary>
    private static void KnockOut(Graphics graphics, Bitmap target, int box, Action<Graphics> paint)
    {
        using Bitmap mask = new(box, box, PixelFormat.Format32bppArgb);
        using (Graphics maskGraphics = Graphics.FromImage(mask))
        {
            maskGraphics.Clear(Color.Transparent);
            maskGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            maskGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            paint(maskGraphics);
        }

        graphics.Flush(FlushIntention.Sync);

        Rectangle rect = new(0, 0, target.Width, target.Height);
        BitmapData targetData = target.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        BitmapData maskData = mask.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* targetRow = (byte*)targetData.Scan0 + (y * targetData.Stride);
                    byte* maskRow = (byte*)maskData.Scan0 + (y * maskData.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int alpha = targetRow[(x * 4) + 3];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        targetRow[(x * 4) + 3] = (byte)(alpha * (255 - maskRow[(x * 4) + 3]) / 255);
                    }
                }
            }
        }
        finally
        {
            mask.UnlockBits(maskData);
            target.UnlockBits(targetData);
        }
    }

    // --- measurement ------------------------------------------------------------------------

    private static Rectangle MeasureInk(string text, Font font)
    {
        int canvas = (int)Math.Ceiling(font.Size * (text.Length + 2) * 1.6) + (MeasurePadding * 2);
        using Bitmap probe = new(canvas, canvas, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(probe);
        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = Hint;
        graphics.DrawString(text, font, Brushes.White, MeasurePadding, MeasurePadding, StringFormat.GenericTypographic);

        Rectangle ink = InkBoundsOf(probe);
        return ink.IsEmpty
            ? Rectangle.Empty
            : new Rectangle(ink.X - MeasurePadding, ink.Y - MeasurePadding, ink.Width, ink.Height);
    }

    /// <summary>Bounding box of every pixel on the layer carrying any alpha at all.</summary>
    private static Rectangle InkBoundsOf(Bitmap bitmap)
    {
        Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* row = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        if (row[(x * 4) + 3] == 0)
                        {
                            continue;
                        }

                        if (x < minX)
                        {
                            minX = x;
                        }

                        if (x > maxX)
                        {
                            maxX = x;
                        }

                        if (y < minY)
                        {
                            minY = y;
                        }

                        if (y > maxY)
                        {
                            maxY = y;
                        }
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>
    /// The alpha-weighted horizontal centre of mass — where the glyph's ink actually sits, rather
    /// than where its bounding box does. A thin flag on one side barely moves this; a stem does.
    /// internal, not private: ticket 03's centring test calls this directly rather than keeping a
    /// separately-typed copy that would validate its own logic instead of the shipped code path.
    /// </summary>
    internal static float OpticalCentreX(Bitmap bitmap)
    {
        double weighted = 0;
        double total = 0;
        Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* row = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int alpha = row[(x * 4) + 3];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        weighted += alpha * (x + 0.5);
                        total += alpha;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return total == 0 ? bitmap.Width / 2f : (float)(weighted / total);
    }

    /// <summary>
    /// The alpha-weighted vertical centre of mass — the vertical mirror of <see cref="OpticalCentreX"/>.
    /// internal, not private: the vertical centring test calls this directly for the same reason
    /// <see cref="OpticalCentreX"/> is internal (§5.4).
    /// </summary>
    internal static float OpticalCentreY(Bitmap bitmap)
    {
        double weighted = 0;
        double total = 0;
        Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    byte* row = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int alpha = row[(x * 4) + 3];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        weighted += alpha * (y + 0.5);
                        total += alpha;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return total == 0 ? bitmap.Height / 2f : (float)(weighted / total);
    }
}
