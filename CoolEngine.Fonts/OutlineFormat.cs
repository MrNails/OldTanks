namespace CoolEngine.Fonts;

/// <summary>The outline table a face draws its glyphs from.</summary>
public enum OutlineFormat
{
    /// <summary>No outline table: a face with only bitmap or SVG glyphs, whose glyphs are all empty here.</summary>
    None,

    /// <summary>Quadratic outlines in <c>glyf</c> and <c>loca</c>.</summary>
    TrueType,

    /// <summary>Cubic outlines in a <c>CFF </c> table.</summary>
    Cff,

    /// <summary>Cubic outlines in a <c>CFF2</c> table.</summary>
    Cff2,
}
