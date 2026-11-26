// Follows FreeType 2.13.2 src/base/ftobjs.c (ft_glyphslot_preset_bitmap) and src/smooth/ftsmooth.c (ft_smooth_render,
// ft_smooth_raster_overlap, ft_smooth_overlap_spans) (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Rasterization;

/// <summary>
/// Renders a glyph outline as FreeType 2.13.2's <c>FT_Render_Glyph</c> with <c>FT_RENDER_MODE_NORMAL</c> does
/// (<c>ft_glyphslot_preset_bitmap</c>, <c>ft_smooth_render</c>): the bitmap is the outline's control box rounded outward to
/// whole pixels, and a glyph flagged as overlapping is rendered four times finer in each direction and averaged.
/// </summary>
/// <remarks>
/// Limits on valid data (<see cref="NotSupportedException"/>): a box past ±32,767 pixels (FreeType refuses it too), more than
/// <see cref="ScaledFont.MaxGlyphPixelCount"/> pixels, and more rasteriser work than <see cref="WorkBudget"/>. Where FreeType
/// gives up with <c>Raster_Overflow</c>, this one still renders: a pixel row with more cells than FreeType's render pool holds,
/// and an overlapping glyph more than 8,191 pixels wide (FreeType's oversampled spans are 16-bit; this rasteriser has none).
/// </remarks>
internal static class GlyphRenderer
{
    /// <summary>
    /// The most work units (see <see cref="CoverageRasterizer"/>: grid cells cleared and swept, segments, cell steps and cubic
    /// bisections) one glyph may take. Measured on 2026-11-25 over all 1,767,308 glyphs of the 218 TrueType faces in
    /// <c>C:\Windows\Fonts</c> and the test fonts, unhinted: at 9 px the most was 3,494 units, at 72 px 143,450, at 4096 px
    /// 490,200,950 (CascadiaCode.ttf and CascadiaMono.ttf, glyph 1,387: a 6,896 × 4,080 overlapping glyph, oversampled,
    /// 2.2 s to render), with no glyph refused. The budget is twice that, rounded up.
    /// </summary>
    internal const long WorkBudget = 1_000_000_000;

    /// <summary>FreeType's <c>SCALE</c> in <c>ftsmooth.c</c>: the oversampling factor for overlapping outlines.</summary>
    private const int OverlapScale = 4;

    /// <summary>The bitmap box limit of <c>ft_glyphslot_preset_bitmap</c> (16-bit spans).</summary>
    private const int MaxBox = 0x7FFF;

    [ThreadStatic]
    private static CoverageRasterizer? s_rasterizer;

    /// <param name="outline">The outline in 26.6, origin at the glyph origin.</param>
    /// <param name="overlap">Render oversampled, as FreeType does for <c>FT_OUTLINE_OVERLAP</c>.</param>
    /// <exception cref="NotSupportedException">
    /// The bitmap would reach past ±32,767 pixels or hold more than <see cref="ScaledFont.MaxGlyphPixelCount"/> pixels, or the
    /// rasteriser's work passes <see cref="WorkBudget"/>.
    /// </exception>
    public static GlyphBitmap Render(GlyphOutline outline, bool overlap) => Run(outline, overlap, WorkBudget, countOnly: false).Bitmap;

    /// <summary>
    /// The work <see cref="Render"/> would do for this outline, counted without allocating or filling a grid and without a
    /// budget; for the measurement behind <see cref="WorkBudget"/>.
    /// </summary>
    internal static long MeasureWork(GlyphOutline outline, bool overlap) => Run(outline, overlap, long.MaxValue, countOnly: true).Work;

    /// <summary>
    /// Renders an outline already in bitmap coordinates (bottom-left at 0, 0) into a <paramref name="width"/> ×
    /// <paramref name="height"/> bitmap, row 0 at the top, as FreeType's <c>FT_Outline_Get_Bitmap</c> into an 8-bit bitmap does.
    /// </summary>
    public static byte[] Rasterize(GlyphOutline outline, int width, int height)
    {
        var points = outline.Points.Span;
        var pixels = new byte[width * height];
        if (points.Length == 0 || width == 0 || height == 0)
            return pixels;

        var (xs, ys, kinds) = Translate(points, 0, 0, 1);
        var rasterizer = s_rasterizer ??= new CoverageRasterizer();
        rasterizer.Begin(WorkBudget, countOnly: false);
        var band = CoverageRasterizer.BandHeight(width);
        for (var start = 0; start < height; start += band)
        {
            rasterizer.Reset(width, height, start, Math.Min(start + band, height));
            rasterizer.AddOutline(xs, ys, kinds, outline.ContourEndPoints.Span);
            rasterizer.Sweep(pixels);
        }

        return pixels;
    }

    private static (GlyphBitmap Bitmap, long Work) Run(GlyphOutline outline, bool overlap, long budget, bool countOnly)
    {
        var points = outline.Points.Span;
        if (points.Length == 0)
            return (new GlyphBitmap(0, 0, 0, 0, []), 0);

        long xMin = points[0].X, xMax = xMin, yMin = points[0].Y, yMax = yMin;
        foreach (var point in points)
        {
            xMin = Math.Min(xMin, point.X);
            xMax = Math.Max(xMax, point.X);
            yMin = Math.Min(yMin, point.Y);
            yMax = Math.Max(yMax, point.Y);
        }

        // Floor the minimum, ceil the maximum.
        var left = xMin >> 6;
        var bottom = yMin >> 6;
        var right = (xMax >> 6) + (((xMax & 63) + 63) >> 6);
        var top = (yMax >> 6) + (((yMax & 63) + 63) >> 6);

        if (left < -MaxBox - 1 || right > MaxBox || bottom < -MaxBox - 1 || top > MaxBox)
            throw new NotSupportedException($"The glyph's bitmap ({left}, {bottom})..({right}, {top}) is larger than 16-bit coordinates allow.");

        var width = right - left;
        var height = top - bottom;
        if (width == 0 || height == 0)
            return (new GlyphBitmap((int)width, (int)height, (int)left, (int)top, []), 0);

        if (width * height > ScaledFont.MaxGlyphPixelCount)
            throw new NotSupportedException(
                $"The glyph's {width} x {height} bitmap has more than {ScaledFont.MaxGlyphPixelCount} pixels, this library's limit.");

        var pixels = countOnly ? [] : new byte[width * height];
        var scale = overlap ? OverlapScale : 1;
        var (xs, ys, kinds) = Translate(points, left, bottom, scale);

        var rasterizer = s_rasterizer ??= new CoverageRasterizer();
        rasterizer.Begin(budget, countOnly);
        var gridWidth = (int)width * scale;
        var gridHeight = (int)height * scale;
        var band = CoverageRasterizer.BandHeight(gridWidth);
        for (var start = 0; start < gridHeight; start += band)
        {
            rasterizer.Reset(gridWidth, gridHeight, start, Math.Min(start + band, gridHeight));
            rasterizer.AddOutline(xs, ys, kinds, outline.ContourEndPoints.Span);
            if (countOnly)
                continue;

            if (overlap)
                rasterizer.SweepOversampled(pixels, (int)width, (int)height, OverlapScale);
            else
                rasterizer.Sweep(pixels);
        }

        return (new GlyphBitmap((int)width, (int)height, (int)left, (int)top, pixels), rasterizer.Work);
    }

    /// <summary>Translates into the bitmap (its bottom-left corner at 0, 0), then inflates when oversampling.</summary>
    private static (long[] X, long[] Y, OutlinePointKind[] Kinds) Translate(ReadOnlySpan<OutlinePoint> points, long left,
        long bottom, int scale)
    {
        var xs = new long[points.Length];
        var ys = new long[points.Length];
        var kinds = new OutlinePointKind[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            xs[i] = (points[i].X - left * 64) * scale;
            ys[i] = (points[i].Y - bottom * 64) * scale;
            kinds[i] = points[i].Kind;
        }

        return (xs, ys, kinds);
    }
}
