namespace CoolEngine.Fonts.Sfnt;

/// <summary>The <c>hhea</c> table: the horizontal line metrics and the number of long <c>hmtx</c> records.</summary>
internal sealed class HheaTable
{
    private const int MinLength = 36;

    private HheaTable(short ascender, short descender, short lineGap, ushort advanceWidthMax, ushort numberOfHMetrics)
    {
        Ascender = ascender;
        Descender = descender;
        LineGap = lineGap;
        AdvanceWidthMax = advanceWidthMax;
        NumberOfHMetrics = numberOfHMetrics;
    }

    public short Ascender { get; }

    public short Descender { get; }

    public short LineGap { get; }

    public ushort AdvanceWidthMax { get; }

    public ushort NumberOfHMetrics { get; }

    /// <param name="data">The table.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    public static HheaTable Parse(ReadOnlySpan<byte> data, int glyphCount)
    {
        var r = new BigEndianReader(data, "hhea");
        if (r.Length < MinLength)
            throw new InvalidDataException($"The 'hhea' table is {r.Length} bytes long; it needs {MinLength}.");

        if (r.U16(0) != 1)
            throw new InvalidDataException($"The 'hhea' table's majorVersion is {r.U16(0)}, not 1.");

        var numberOfHMetrics = r.U16(34);
        if (numberOfHMetrics == 0 || numberOfHMetrics > glyphCount)
            throw new InvalidDataException(
                $"The 'hhea' table's numberOfHMetrics is {numberOfHMetrics}; it must be 1..{glyphCount} (maxp.numGlyphs).");

        return new HheaTable(r.I16(4), r.I16(6), r.I16(8), r.U16(10), numberOfHMetrics);
    }
}
