namespace CoolEngine.Fonts.Sfnt;

/// <summary>The <c>vmtx</c> table: each glyph's advance height and top side bearing in font units.</summary>
internal sealed class VmtxTable
{
    public const uint Tag = 0x766D7478;  // vmtx

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int m_numberOfLongMetrics;

    private VmtxTable(ReadOnlyMemory<byte> data, int numberOfLongMetrics)
    {
        m_data = data;
        m_numberOfLongMetrics = numberOfLongMetrics;
    }

    /// <param name="data">The table.</param>
    /// <param name="numberOfLongMetrics"><c>vhea.numOfLongVerMetrics</c>, already checked to be 1..<paramref name="glyphCount"/>.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    public static VmtxTable Parse(ReadOnlyMemory<byte> data, int numberOfLongMetrics, int glyphCount)
    {
        var needed = 4L * numberOfLongMetrics + 2L * (glyphCount - numberOfLongMetrics);
        if (data.Length < needed)
            throw new InvalidDataException(
                $"The 'vmtx' table is {data.Length} bytes long; {numberOfLongMetrics} long metrics and " +
                $"{glyphCount - numberOfLongMetrics} top side bearings need {needed}.");

        return new VmtxTable(data, numberOfLongMetrics);
    }

    /// <summary>The advance height and top side bearing of a glyph (whose index the caller has checked).</summary>
    public (ushort Advance, short TopSideBearing) Get(int glyphIndex)
    {
        var r = new BigEndianReader(m_data.Span, "vmtx");
        if (glyphIndex < m_numberOfLongMetrics)
            return (r.U16(4 * glyphIndex), r.I16(4 * glyphIndex + 2));

        // Past the long records, every glyph shares the last advance and has its own bearing.
        return (r.U16(4 * (m_numberOfLongMetrics - 1)),
            r.I16(4 * m_numberOfLongMetrics + 2 * (glyphIndex - m_numberOfLongMetrics)));
    }
}
