namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// What a table's parse depends on besides its own bytes. Every face that shares a table record must read it with the same
/// inputs (Denys's "Same-inputs rule", 2026-11-26), else <see cref="InvalidDataException"/>:
/// <c>hhea</c> and <c>cmap</c> read <c>maxp.numGlyphs</c>; <c>hmtx</c> reads <c>hhea.numberOfHMetrics</c> and
/// <c>maxp.numGlyphs</c>; <c>loca</c> and <c>glyf</c>, parsed together, read <c>head.indexToLocFormat</c>, <c>maxp.numGlyphs</c>
/// and each other's record; <c>vhea</c> reads <c>maxp.numGlyphs</c>; <c>vmtx</c> reads <c>vhea.numOfLongVerMetrics</c> and
/// <c>maxp.numGlyphs</c>. The variation tables (OT-4 WP5) read <c>fvar</c>'s axis count; <c>gvar</c> also <c>maxp.numGlyphs</c> and
/// the <c>glyf</c> record (its glyphs' point counts); <c>cvar</c> also the cvt's entries and the <c>gvar</c> record (its shared
/// tuples). <c>head</c>, <c>maxp</c>, <c>name</c>, <c>post</c>, <c>OS/2</c>, <c>SVG </c> and <c>fvar</c> read nothing else (the
/// public lists built from <c>fvar</c> and a face's <c>name</c> are kept per pair: <see cref="ParsedTables.GetPair{T}"/>).
/// </summary>
internal readonly record struct TableInputs(
    int? GlyphCount = null,
    int? IndexToLocFormat = null,
    int? NumberOfHMetrics = null,
    TableRecord? Companion = null,
    int? NumberOfVMetrics = null,
    int? AxisCount = null,
    int? CvtEntries = null)
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

        if (AxisCount is { } axes)
            parts.Add($"{axes} variation axes");

        if (CvtEntries is { } cvt)
            parts.Add($"{cvt} cvt entries");

        if (Companion is { } companion)
            parts.Add($"with the '{Tags.ToString(companion.Tag)}' table of {companion.Length} bytes at {companion.Offset}");

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}
