namespace CoolEngine.Fonts.Sfnt;

/// <summary>The <c>hmtx</c> table: each glyph's advance width and left side bearing in font units.</summary>
internal sealed class HmtxTable
{
    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int m_numberOfHMetrics;

    private HmtxTable(ReadOnlyMemory<byte> data, int numberOfHMetrics)
    {
        m_data = data;
        m_numberOfHMetrics = numberOfHMetrics;
    }

    /// <param name="data">The table.</param>
    /// <param name="numberOfHMetrics"><c>hhea.numberOfHMetrics</c>, already checked to be 1..<paramref name="glyphCount"/>.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    public static HmtxTable Parse(ReadOnlyMemory<byte> data, int numberOfHMetrics, int glyphCount)
    {
        var needed = 4L * numberOfHMetrics + 2L * (glyphCount - numberOfHMetrics);
        if (data.Length < needed)
            throw new InvalidDataException(
                $"The 'hmtx' table is {data.Length} bytes long; {numberOfHMetrics} long metrics and {glyphCount - numberOfHMetrics} " +
                $"left side bearings need {needed}.");

        return new HmtxTable(data, numberOfHMetrics);
    }

    /// <summary>The advance width and left side bearing of a glyph (whose index the caller has checked).</summary>
    public (ushort Advance, short LeftSideBearing) Get(int glyphIndex)
    {
        var r = new BigEndianReader(m_data.Span, "hmtx");
        if (glyphIndex < m_numberOfHMetrics)
            return (r.U16(4 * glyphIndex), r.I16(4 * glyphIndex + 2));

        // Past the long records, every glyph shares the last advance and has its own bearing.
        return (r.U16(4 * (m_numberOfHMetrics - 1)), r.I16(4 * m_numberOfHMetrics + 2 * (glyphIndex - m_numberOfHMetrics)));
    }
}
