namespace CoolEngine.Fonts;

/// <summary>Whether a <see cref="ScaledFont"/> grid-fits its glyphs.</summary>
public enum HintingMode
{
    /// <summary>Scaled outlines as they are, as FreeType's <c>FT_LOAD_NO_HINTING</c>.</summary>
    None,

    /// <summary>
    /// The font's own hints: the TrueType instructions, or the CFF and CFF2 stem hints as FreeType's Adobe engine applies them
    /// (blue zones and stems in y, no stem darkening; past 2,000 pixels per em a CFF glyph is loaded unhinted with its
    /// metrics still grid-fitted, as FreeType retries it).
    /// </summary>
    Native,
}
