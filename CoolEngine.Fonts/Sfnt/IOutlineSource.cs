namespace CoolEngine.Fonts.Sfnt;

/// <summary>A size as FreeType's <c>FT_Set_Pixel_Sizes(face, 0, PixelSize)</c> sets it: 16.16 scales from font units to 26.6.</summary>
/// <param name="PixelSize">Pixels per em.</param>
/// <param name="XScale"><c>FT_DivFix(PixelSize * 64, unitsPerEm)</c>.</param>
/// <param name="YScale">The same as <paramref name="XScale"/>: sizes are square.</param>
internal readonly record struct FontScale(int PixelSize, long XScale, long YScale)
{
    public static FontScale FromPixelSize(int pixelSize, int unitsPerEm)
    {
        var scale = FixedMath.DivFix(pixelSize * 64L, unitsPerEm);
        return new FontScale(pixelSize, scale, scale);
    }
}

/// <summary>A glyph as one outline source loads it: the outline (origin at 0, 0), its metrics and the overlap flag.</summary>
/// <param name="Outline">The scaled (and, when hinting, grid-fitted) outline in 26.6.</param>
/// <param name="Metrics">The metrics as FreeType reports them for the same load.</param>
/// <param name="Overlap">
/// The glyph sets an overlap flag (<c>OVERLAP_SIMPLE</c> or <c>OVERLAP_COMPOUND</c>), so FreeType renders it oversampled
/// (<c>FT_OUTLINE_OVERLAP</c>).
/// </param>
internal readonly record struct LoadedGlyph(GlyphOutline Outline, GlyphMetrics Metrics, bool Overlap);

/// <summary>
/// The seam between a face and its outline table: <c>glyf</c> (<see cref="GlyfOutlineSource"/>), <c>CFF </c> and <c>CFF2</c>
/// (<see cref="Cff.CffOutlineSource"/>), or none (<see cref="EmptyOutlineSource"/>). A source is immutable and belongs to
/// one face; it checks its table at load (in its constructor) and is safe on any thread.
/// </summary>
internal interface IOutlineSource
{
    /// <summary>
    /// Makes the loader one <see cref="ScaledFont"/> uses. It may hold hinting state, so it is not thread-safe.
    /// </summary>
    /// <param name="options">The size, hinting mode and interpreter version (already checked).</param>
    /// <param name="scale">The scales <paramref name="options"/> gives this face.</param>
    /// <param name="variation">
    /// The variation coordinates of a variable face, when <see cref="ScaledFontOptions.Variation"/> gives any (FreeType's face
    /// after <c>FT_Set_Var_Design_Coordinates</c>); null for none, as FreeType's face without a blend.
    /// </param>
    IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale, Variations.VariationInstance? variation);
}

/// <summary>Loads glyphs at one size; one per <see cref="ScaledFont"/>, not thread-safe.</summary>
internal interface IGlyphLoader
{
    /// <param name="glyphIndex">A glyph index the caller has checked against the glyph count.</param>
    LoadedGlyph Load(int glyphIndex);
}
