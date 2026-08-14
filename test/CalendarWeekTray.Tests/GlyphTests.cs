using Xunit;

namespace CalendarWeekTray.Tests;

/// <summary>
/// Measured properties over <see cref="GlyphRenderer"/>, swept across all 53 ISO weeks and the five
/// sizes <c>SM_CXSMICON</c> reports at the standard scalings. No golden images (spec §11.3): a
/// checked-in reference bitmap breaks on a Windows font update with no bug present, and reports only
/// that something changed, never what. Every assertion here reads a number back out of a render that
/// actually happened.
/// </summary>
public class GlyphTests
{
    private static readonly int[] Sizes = [16, 20, 24, 28, 32];
    private static readonly int[] Weeks = Enumerable.Range(1, 53).ToArray();

    /// <summary>
    /// The exact (week, size) pairs ticket 02 measured landing outside the blend loop's 0.15 px
    /// convergence band and accepted as a property of the decided algorithm, not a defect — see
    /// <see cref="TheCentringLoopConvergesOutsideTheKnownDeadZone"/>.
    /// </summary>
    private static readonly HashSet<(int Week, int Size)> KnownDeadZone =
    [
        (1, 16), (1, 24), (13, 20), (17, 32), (18, 32), (2, 24), (2, 28), (23, 24), (26, 24),
        (26, 28), (3, 16), (30, 20), (30, 32), (32, 20), (32, 24), (33, 20), (34, 24), (35, 24),
        (37, 24), (38, 20), (39, 16), (39, 20), (39, 32), (4, 16), (4, 20), (4, 24), (4, 28),
        (40, 16), (40, 24), (40, 28), (41, 24), (42, 24), (44, 32), (46, 24), (48, 28), (49, 16),
        (49, 28), (51, 28), (52, 24), (52, 28), (53, 24), (6, 24), (6, 28), (7, 24), (8, 28), (9, 24),
    ];

    public static TheoryData<int, int> AllWeekSizeCombos()
    {
        TheoryData<int, int> data = [];
        foreach (int size in Sizes)
        {
            foreach (int week in Weeks)
            {
                data.Add(week, size);
            }
        }

        return data;
    }

    // --- 1 & 2: air preserved, and centring ------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllWeekSizeCombos))]
    public void DigitInkStaysInsideThePageAndIsCentred(int week, int size)
    {
        using Bitmap bitmap = GlyphRenderer.Render(new GlyphSpec(week, size, Color.White), out GlyphMetrics metrics);

        Assert.False(metrics.DigitInk.IsEmpty);

        // 1. Air preserved: one assertion covering both the `06` "00" overflow and the `12` BodyPad
        // reclaim, per spec §11.2 item 1.
        int barHeight = ExpectedBarHeight(size);
        int leftAir = metrics.DigitInk.Left - metrics.Page.Left;
        int rightAir = metrics.Page.Right - metrics.DigitInk.Right;
        int bottomAir = metrics.Page.Bottom - metrics.DigitInk.Bottom;
        int barAir = metrics.DigitInk.Top - barHeight;

        Assert.True(leftAir >= 1, $"week {week} @ {size}px: left air {leftAir} < 1");
        Assert.True(rightAir >= 1, $"week {week} @ {size}px: right air {rightAir} < 1");
        Assert.True(bottomAir >= 1, $"week {week} @ {size}px: bottom air {bottomAir} < 1");
        Assert.True(barAir >= 1, $"week {week} @ {size}px: air below bar {barAir} < 1");

        // 2. Centring: the loop targets the blend of the ink's bounding-box centre and its
        // alpha-weighted mass centre (spec §5.4), never the box alone — centring the box is the very
        // thing §5.4 says is wrong for this typeface. DigitInk's own left/right margins are therefore
        // free to be asymmetric by design (an unbalanced glyph like "1" pulls the box to one side on
        // purpose); what must be symmetric is the point the loop actually converges on. DigitInk is
        // pure ink with no frame pixels in it (the air assertions above guarantee at least 1 px
        // clearance), so cropping to it and handing that to the renderer's own
        // <see cref="GlyphRenderer.OpticalCentreX"/> reproduces exactly what the loop measured,
        // without a second, separately-typed copy of that algorithm to fall out of sync.
        using Bitmap inkOnly = bitmap.Clone(metrics.DigitInk, bitmap.PixelFormat);
        double boxCentre = metrics.DigitInk.Left + (metrics.DigitInk.Width / 2.0);
        double massCentre = metrics.DigitInk.Left + GlyphRenderer.OpticalCentreX(inkOnly);
        double blendCentre = (boxCentre + massCentre) / 2.0;

        double bodyLeft = (GlyphRenderer.Stroke / 2.0) + GlyphRenderer.BodyPad;
        double bodyRight = size - bodyLeft;
        double leftGap = blendCentre - bodyLeft;
        double rightGap = bodyRight - blendCentre;

        Assert.True(
            Math.Abs(leftGap - rightGap) <= 1,
            $"week {week} @ {size}px: left gap {leftGap:F2}, right gap {rightGap:F2}");
    }

    // --- 3: convergence -------------------------------------------------------------------------

    /// <summary>
    /// The blend loop is capped at 4 iterations by design (spec §5.4) and explicitly "tak[es] the
    /// last result if it has not converged" — non-convergence is an accepted outcome, not a defect;
    /// <see cref="GlyphMetrics.Converged"/> exists to surface it (ticket 02's answer). Ticket 02
    /// measured 46/265 week×size combinations landing there: at those specific (digit, size) pairs,
    /// GDI+'s antialiasing only offers two achievable subpixel phases near the target, straddling it
    /// just outside the 0.15 px band on both sides, so no `x` the loop could try lands inside it —
    /// confirmed by raising the cap to 40, which still left 29/265 stuck. Asserting `Converged` for
    /// every combination would therefore fault a limitation of the decided algorithm that no
    /// iteration budget fixes. What every OTHER combination must still do is converge — this checks
    /// the full 265 against the exact, named <see cref="KnownDeadZone"/> so a regression that shifts
    /// failures onto a new, unexamined pair is caught even if the total count does not change.
    /// </summary>
    [Fact]
    public void TheCentringLoopConvergesOutsideTheKnownDeadZone()
    {
        List<string> unexpected = [];
        List<(int Week, int Size)> stillDead = [];

        foreach (int size in Sizes)
        {
            foreach (int week in Weeks)
            {
                using Bitmap _ = GlyphRenderer.Render(new GlyphSpec(week, size, Color.White), out GlyphMetrics metrics);
                if (metrics.Converged)
                {
                    continue;
                }

                if (KnownDeadZone.Contains((week, size)))
                {
                    stillDead.Add((week, size));
                }
                else
                {
                    unexpected.Add($"week {week} @ {size}px");
                }
            }
        }

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} combination(s) failed to converge outside the known dead zone: " +
            string.Join(", ", unexpected));
    }

    // --- 4: the reference really is the widest -------------------------------------------------

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(28)]
    [InlineData(32)]
    public void NoWeekExceedsTheWidestReferenceAtTheSameFittedSize(int size)
    {
        // The fit is always computed against the internal "widest digit doubled" reference, never
        // against the week being displayed (spec §5.3), so every doubled-digit reference shares the
        // same fitted type size as every real week at this box. Rendering all ten stands in for
        // asking the renderer which digit it decided was widest, without reaching into its private
        // fit cache.
        int widestReferenceInk = 0;
        for (int digit = 0; digit <= 9; digit++)
        {
            int candidateWeek = (digit * 10) + digit;
            using Bitmap _ = GlyphRenderer.Render(new GlyphSpec(candidateWeek, size, Color.White), out GlyphMetrics candidateMetrics);
            widestReferenceInk = Math.Max(widestReferenceInk, candidateMetrics.DigitInk.Width);
        }

        foreach (int week in Weeks)
        {
            using Bitmap _ = GlyphRenderer.Render(new GlyphSpec(week, size, Color.White), out GlyphMetrics metrics);
            Assert.True(
                metrics.DigitInk.Width <= widestReferenceInk,
                $"week {week} @ {size}px: digit ink {metrics.DigitInk.Width}px exceeds the widest reference's {widestReferenceInk}px");
        }
    }

    // --- 5: bar geometry ------------------------------------------------------------------------

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(28)]
    [InlineData(32)]
    public void TheBindingBarIsExactAndTheSlotsAreClean(int size)
    {
        using Bitmap bitmap = GlyphRenderer.Render(new GlyphSpec(1, size, Color.White));

        int expectedBarHeight = ExpectedBarHeight(size);
        int measuredBarHeight = MeasureBarHeight(bitmap, size);
        Assert.Equal(expectedBarHeight, measuredBarHeight);

        int barMidY = expectedBarHeight / 2;
        int slotWidth = Math.Max(1, (int)Math.Round(size * GlyphRenderer.SlotWidthFactor));
        foreach (float slotCentreFactor in new[] { GlyphRenderer.SlotCentreA, GlyphRenderer.SlotCentreB })
        {
            float centre = size * slotCentreFactor;
            int slotX = (int)Math.Round(centre - (slotWidth / 2f));
            byte alpha = bitmap.GetPixel(slotX, barMidY).A;
            Assert.True(alpha == 0, $"{size}px: slot at x={slotX} has alpha {alpha}, expected exactly 0");
        }
    }

    // --- shared helpers -------------------------------------------------------------------------

    private static int ExpectedBarHeight(int box) => Math.Max(2, (int)Math.Round(box * GlyphRenderer.BarFactor));

    /// <summary>
    /// Scans a column clear of both ring slots (the box midpoint, always between 0.32 and 0.68 of
    /// its width) from the top down, counting contiguous opaque rows. The gap the digit body's
    /// padding leaves below the bar means this stops exactly at the bar's real bottom edge.
    /// </summary>
    private static int MeasureBarHeight(Bitmap bitmap, int box)
    {
        int column = box / 2;
        int y = 0;
        while (y < box && bitmap.GetPixel(column, y).A != 0)
        {
            y++;
        }

        return y;
    }
}
