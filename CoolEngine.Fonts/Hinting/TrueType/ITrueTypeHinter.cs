namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The TrueType bytecode hinter one <see cref="ScaledFont"/> uses with <see cref="HintingMode.Native"/> (OT-4 WP4). The glyph
/// loader (<see cref="Sfnt.GlyfOutlineSource"/>) calls it where FreeType 2.13.2's <c>ttgload.c</c> calls
/// <c>TT_Hint_Glyph</c>: for every simple glyph after scaling, and for a composite whose last component has
/// <c>WE_HAVE_INSTRUCTIONS</c> with a non-empty program after its components are placed. Holds the size's state (the
/// <c>cvt</c>, the <c>prep</c> results), so it is not thread-safe.
/// </summary>
internal interface ITrueTypeHinter
{
    /// <summary>The interpreter mode; the loader rounds a composite's x offset under <c>ROUND_XY_TO_GRID</c> only in v35.</summary>
    TrueTypeInterpreterVersion Version { get; }

    /// <summary>
    /// Hints one glyph in place (FreeType's <c>TT_Hint_Glyph</c>): rounds the phantom points, runs
    /// <paramref name="instructions"/> on <paramref name="zone"/>.
    /// </summary>
    /// <param name="zone">The glyph's points with the four phantom points last.</param>
    /// <param name="instructions">The glyph program; may be empty for a simple glyph.</param>
    /// <param name="isComposite">The zone holds a composite's already-hinted components.</param>
    /// <returns>
    /// Whether the loader takes the zone's phantom points as the glyph's new ones (false in v40 backward-compatibility mode,
    /// where FreeType keeps the unhinted ones).
    /// </returns>
    /// <exception cref="InvalidDataException">The program fails or hits an execution limit.</exception>
    bool HintGlyph(GlyphZone zone, ReadOnlySpan<byte> instructions, bool isComposite);

    /// <summary>
    /// The metrics FreeType reports for a hinted load (the <c>hdmx</c> advance where it applies, then
    /// <c>ft_glyphslot_grid_fit_metrics</c>), from those computed from the hinted outline and phantom points.
    /// </summary>
    GlyphMetrics FitMetrics(int glyphIndex, GlyphMetrics metrics);
}
