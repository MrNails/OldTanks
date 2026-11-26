namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The points of one glyph handed to <see cref="ITrueTypeHinter.HintGlyph"/>, as FreeType's glyph zone: the last four are the
/// phantom points (left and right side bearing, top and bottom). The hinter moves <see cref="X"/> and <see cref="Y"/> in place.
/// </summary>
internal sealed class GlyphZone
{
    public GlyphZone(long[] x, long[] y, long[]? unscaledX, long[]? unscaledY, byte[] tags, int[] contourEnds, int pointCount,
        int contourCount)
    {
        X = x;
        Y = y;
        UnscaledX = unscaledX;
        UnscaledY = unscaledY;
        Tags = tags;
        ContourEnds = contourEnds;
        PointCount = pointCount;
        ContourCount = contourCount;
    }

    /// <summary>Current x in 26.6, phantom points included; the hinter moves them.</summary>
    public long[] X { get; }

    /// <summary>Current y in 26.6, phantom points included; the hinter moves them.</summary>
    public long[] Y { get; }

    /// <summary>
    /// The points in font units, phantom points included, for a simple glyph (FreeType's <c>orus</c>); null for a composite,
    /// whose program refers to the hinted components (FreeType copies the current points into <c>orus</c> then).
    /// </summary>
    public long[]? UnscaledX { get; }

    /// <inheritdoc cref="UnscaledX"/>
    public long[]? UnscaledY { get; }

    /// <summary>Per point: bit 0 set for a point on the curve. The hinter keeps its touch flags itself.</summary>
    public byte[] Tags { get; }

    /// <summary>Each contour's last point, relative to the zone's first point.</summary>
    public int[] ContourEnds { get; }

    /// <summary>The number of points in the zone, the four phantom points included.</summary>
    public int PointCount { get; }

    public int ContourCount { get; }
}
