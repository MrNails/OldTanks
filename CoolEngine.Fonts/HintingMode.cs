namespace CoolEngine.Fonts;

/// <summary>Whether a <see cref="ScaledFont"/> grid-fits its glyphs.</summary>
public enum HintingMode
{
    /// <summary>Scaled outlines as they are, as FreeType's <c>FT_LOAD_NO_HINTING</c>.</summary>
    None,

    /// <summary>The font's own hints: the TrueType instructions or the CFF stem hints.</summary>
    Native,
}
