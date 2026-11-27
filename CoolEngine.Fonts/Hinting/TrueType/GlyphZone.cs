namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The points of one glyph handed to <see cref="ITrueTypeHinter.HintGlyph"/>, as FreeType's glyph zone: the last four are the
/// phantom points (left and right side bearing, top and bottom). The hinter moves <see cref="X"/> and <see cref="Y"/> in place
/// and may flip the on-curve bit of <see cref="Tags"/> (FLIPPT, FLIPRGON, FLIPRGOFF).
/// </summary>
internal sealed class GlyphZone
{
    public GlyphZone(long[] x, long[] y, long[]? unscaledX, long[]? unscaledY, byte[] tags, int[] contourEnds, int pointCount,
        int contourCount, int firstPoint = 0)
    {
        X = x;
        Y = y;
        UnscaledX = unscaledX;
        UnscaledY = unscaledY;
        Tags = tags;
        ContourEnds = contourEnds;
        PointCount = pointCount;
        ContourCount = contourCount;
        FirstPoint = firstPoint;
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

    /// <summary>
    /// Per point: bit 0 set for a point on the curve. The hinter adds its touch flags (bits 3 and 4) here, as FreeType keeps
    /// them in the outline's tags.
    /// </summary>
    public byte[] Tags { get; }

    /// <summary>Each contour's last point, relative to the zone's first point.</summary>
    public int[] ContourEnds { get; }

    /// <summary>The number of points in the zone, the four phantom points included.</summary>
    public int PointCount { get; }

    public int ContourCount { get; }

    /// <summary>
    /// The zone's first point in the whole outline: non-zero only for a composite nested in another composite (FreeType's
    /// <c>first_point</c>), which SHZ does not subtract.
    /// </summary>
    public int FirstPoint { get; }
}
