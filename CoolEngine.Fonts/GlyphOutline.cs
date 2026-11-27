namespace CoolEngine.Fonts;

/// <summary>
/// A scaled glyph outline in 26.6 fixed-point pixels, as the <c>FT_Outline</c> FreeType leaves in the glyph slot after
/// <c>FT_Load_Glyph</c>: points in contour order, y up, the glyph origin at (0, 0).
/// </summary>
public sealed class GlyphOutline
{
    internal GlyphOutline(OutlinePoint[] points, int[] contourEndPoints)
    {
        Points = points;
        ContourEndPoints = contourEndPoints;
    }

    /// <summary>The outline of an empty glyph: no points and no contours.</summary>
    public static GlyphOutline Empty { get; } = new([], []);

    /// <summary>Every point of every contour.</summary>
    public ReadOnlyMemory<OutlinePoint> Points { get; }

    /// <summary>The index in <see cref="Points"/> of each contour's last point, ascending.</summary>
    public ReadOnlyMemory<int> ContourEndPoints { get; }
}
