namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>vhea</c> header's count of long <c>vmtx</c> records, read only beside a <c>vmtx</c> table: FreeType 2.13.2 takes a
/// glyph's top side bearing and advance height from them for the vertical phantom points (<c>TT_Get_VMetrics</c>), which a
/// hinted glyph program can measure.
/// </summary>
internal sealed class VheaTable
{
    public const uint Tag = 0x76686561;  // vhea

    private const int MinLength = 36;

    private VheaTable(ushort numberOfLongMetrics) => NumberOfLongMetrics = numberOfLongMetrics;

    /// <summary><c>numOfLongVerMetrics</c>.</summary>
    public ushort NumberOfLongMetrics { get; }

    /// <param name="data">The table.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    public static VheaTable Parse(ReadOnlySpan<byte> data, int glyphCount)
    {
        var r = new BigEndianReader(data, "vhea");
        if (r.Length < MinLength)
            throw new InvalidDataException($"The 'vhea' table is {r.Length} bytes long; it needs {MinLength}.");

        var count = r.U16(34);
        if (count == 0 || count > glyphCount)
            throw new InvalidDataException(
                $"The 'vhea' table's numOfLongVerMetrics is {count}; it must be 1..{glyphCount} (maxp.numGlyphs).");

        return new VheaTable(count);
    }
}
