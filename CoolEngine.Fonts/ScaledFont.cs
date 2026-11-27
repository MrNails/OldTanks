// LineMetrics follows FreeType 2.13.2 src/base/ftobjs.c (FT_Request_Metrics, ft_recompute_scaled_metrics) (FreeType Project
// License; credit in docs).

using CoolEngine.Fonts.Rasterization;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts;

/// <summary>
/// A face at one pixel size with one hinting mode: outlines, metrics and coverage bitmaps in 26.6 fixed-point pixels, equal
/// to FreeType 2.13.2's for the same size and load flags. NOT thread-safe (it holds hinting state): use one per thread.
/// </summary>
/// <remarks>
/// Each of <see cref="GetOutline"/>, <see cref="GetMetrics"/> and <see cref="Render"/> loads the glyph again, and with
/// <see cref="HintingMode.Native"/> runs its glyph programs again. The size's twilight zone persists from one load to the
/// next, as in FreeType, and so does a WCVTF write to the size's cvt (made when no WCVTP or DELTAC of the same load has made
/// a working copy first); for a font whose glyph programs write to either, two calls for one glyph can differ, as two
/// <c>FT_Load_Glyph</c> calls can.
/// </remarks>
public sealed class ScaledFont
{
    /// <summary>The most pixels one glyph bitmap may have, 16384 × 16384; <see cref="Render"/> throws past it.</summary>
    public const long MaxGlyphPixelCount = 16384L * 16384;

    private readonly IGlyphLoader m_loader;

    internal ScaledFont(FontFace face, ScaledFontOptions options, FontScale scale, IGlyphLoader loader)
    {
        Face = face;
        Options = options;
        Scale = scale;
        m_loader = loader;

        // FreeType's ft_recompute_scaled_metrics with GRID_FIT_METRICS: the size metrics are the same with and without hinting.
        LineMetrics = new LineMetrics(
            (int)FixedMath.PixCeil(FixedMath.MulFix(face.Ascender, scale.YScale)),
            (int)FixedMath.PixFloor(FixedMath.MulFix(face.Descender, scale.YScale)),
            (int)FixedMath.PixRound(FixedMath.MulFix(face.Height, scale.YScale)),
            (int)FixedMath.PixRound(FixedMath.MulFix(face.MaxAdvanceWidth, scale.XScale)));
    }

    /// <summary>The face this font sizes.</summary>
    public FontFace Face { get; }

    /// <summary>The options it was made with.</summary>
    public ScaledFontOptions Options { get; }

    /// <summary>Ascender, descender, line height and largest advance, as FreeType's <c>FT_Size_Metrics</c>.</summary>
    public LineMetrics LineMetrics { get; }

    internal FontScale Scale { get; }

    /// <summary>The glyph's metrics, as FreeType's <c>FT_Glyph_Metrics</c> after <c>FT_Load_Glyph</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or not below the face's glyph count.</exception>
    /// <exception cref="NotSupportedException">The face's outlines come in a later OT-4 package (CFF: WP2).</exception>
    public GlyphMetrics GetMetrics(int glyphIndex) => Load(glyphIndex).Metrics;

    /// <summary>The glyph's outline, as the <c>FT_Outline</c> after <c>FT_Load_Glyph</c>; <see cref="GlyphOutline.Empty"/> for an empty glyph.</summary>
    /// <inheritdoc cref="GetMetrics"/>
    public GlyphOutline GetOutline(int glyphIndex) => Load(glyphIndex).Outline;

    /// <summary>
    /// The glyph's coverage bitmap, as <c>FT_Render_Glyph</c> with <c>FT_RENDER_MODE_NORMAL</c>: the outline's control box rounded
    /// outward to whole pixels, non-zero winding; 0 × 0 for an empty glyph.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or not below the face's glyph count.</exception>
    /// <exception cref="NotSupportedException">
    /// The face's outlines come in a later OT-4 package (CFF: WP2), or the glyph passes a rendering limit: a box past ±32,767
    /// pixels, more than <see cref="MaxGlyphPixelCount"/> pixels, or more rasteriser work than the library allows.
    /// </exception>
    public GlyphBitmap Render(int glyphIndex)
    {
        var glyph = Load(glyphIndex);
        return GlyphRenderer.Render(glyph.Outline, glyph.Overlap);
    }

    /// <summary>The rasteriser work <see cref="Render"/> would do for the glyph, counted without rendering (for measurements).</summary>
    internal long MeasureRenderWork(int glyphIndex)
    {
        var glyph = Load(glyphIndex);
        return GlyphRenderer.MeasureWork(glyph.Outline, glyph.Overlap);
    }

    private LoadedGlyph Load(int glyphIndex)
    {
        Face.CheckGlyphIndex(glyphIndex);
        return m_loader.Load(glyphIndex);
    }
}
