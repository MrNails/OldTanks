namespace CoolEngine.Fonts;

/// <summary>The container format of a font file, read from its signature by <see cref="FontLoader.DetectFormat"/>.</summary>
public enum FontFormat
{
    /// <summary>Not a font this library recognises.</summary>
    Unknown,

    /// <summary>An sfnt with TrueType outlines (version <c>0x00010000</c> or <c>true</c>).</summary>
    TrueType,

    /// <summary>An sfnt with CFF or CFF2 outlines (version <c>OTTO</c>).</summary>
    OpenTypeCff,

    /// <summary>A TrueType or OpenType collection (<c>ttcf</c>).</summary>
    Collection,

    /// <summary>WOFF 1.0 (<c>wOFF</c>).</summary>
    Woff,

    /// <summary>WOFF2 (<c>wOF2</c>).</summary>
    Woff2,
}
