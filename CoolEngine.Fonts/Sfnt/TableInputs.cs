namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// What a table's parse depends on besides its own bytes. Every face that shares a table record must read it with the same
/// inputs (Denys's "Same-inputs rule", 2026-11-26), else <see cref="InvalidDataException"/>:
/// <c>hhea</c> and <c>cmap</c> read <c>maxp.numGlyphs</c>; <c>hmtx</c> reads <c>hhea.numberOfHMetrics</c> and
/// <c>maxp.numGlyphs</c>; <c>loca</c> and <c>glyf</c>, parsed together, read <c>head.indexToLocFormat</c>, <c>maxp.numGlyphs</c>
/// and each other's record; <c>vhea</c> reads <c>maxp.numGlyphs</c>; <c>vmtx</c> reads <c>vhea.numOfLongVerMetrics</c> and
/// <c>maxp.numGlyphs</c>. <c>head</c>, <c>maxp</c>, <c>name</c>, <c>post</c>, <c>OS/2</c> and <c>SVG </c> read nothing else.
/// </summary>
internal readonly record struct TableInputs(
    int? GlyphCount = null,
    int? IndexToLocFormat = null,
    int? NumberOfHMetrics = null,
    TableRecord? Companion = null,
    int? NumberOfVMetrics = null)
{
    /// <summary>A table whose parse reads nothing but its own bytes.</summary>
    public static TableInputs None => default;

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (GlyphCount is { } glyphCount)
            parts.Add($"numGlyphs {glyphCount}");

        if (IndexToLocFormat is { } format)
            parts.Add($"indexToLocFormat {format}");

        if (NumberOfHMetrics is { } metrics)
            parts.Add($"numberOfHMetrics {metrics}");

        if (NumberOfVMetrics is { } verticalMetrics)
            parts.Add($"numOfLongVerMetrics {verticalMetrics}");

        if (Companion is { } companion)
            parts.Add($"with the '{Tags.ToString(companion.Tag)}' table of {companion.Length} bytes at {companion.Offset}");

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}
