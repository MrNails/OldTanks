namespace CoolEngine.Fonts;

/// <summary>A glyph's horizontal metrics in 26.6 fixed-point pixels, as FreeType's <c>FT_Glyph_Metrics</c> (the <c>hori*</c> fields).</summary>
/// <param name="Width">The width of the outline's control box.</param>
/// <param name="Height">The height of the outline's control box.</param>
/// <param name="BearingX">From the origin to the left of the control box.</param>
/// <param name="BearingY">From the baseline to the top of the control box.</param>
/// <param name="Advance">The horizontal advance.</param>
public readonly record struct GlyphMetrics(int Width, int Height, int BearingX, int BearingY, int Advance);
