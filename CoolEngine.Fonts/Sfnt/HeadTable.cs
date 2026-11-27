namespace CoolEngine.Fonts.Sfnt;

/// <summary>The <c>head</c> table: units per em, flags, the font bounding box and the <c>loca</c> format.</summary>
internal sealed class HeadTable
{
    public const int MinLength = 54;
    public const int ChecksumAdjustmentOffset = 8;

    private const uint Magic = 0x5F0F3CF5;
    private const int MinUnitsPerEm = 16;
    private const int MaxUnitsPerEm = 16384;

    private HeadTable(ushort flags, ushort unitsPerEm, short xMin, short yMin, short xMax, short yMax, ushort macStyle,
        short indexToLocFormat)
    {
        Flags = flags;
        UnitsPerEm = unitsPerEm;
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
        MacStyle = macStyle;
        IndexToLocFormat = indexToLocFormat;
    }

    public ushort Flags { get; }

    public ushort UnitsPerEm { get; }

    public short XMin { get; }

    public short YMin { get; }

    public short XMax { get; }

    public short YMax { get; }

    public ushort MacStyle { get; }

    /// <summary>0 for 16-bit <c>loca</c> offsets (halved), 1 for 32-bit ones.</summary>
    public short IndexToLocFormat { get; }

    public static HeadTable Parse(ReadOnlySpan<byte> data)
    {
        var r = new BigEndianReader(data, "head");
        if (r.Length < MinLength)
            throw new InvalidDataException($"The 'head' table is {r.Length} bytes long; it needs {MinLength}.");

        if (r.U16(0) != 1)
            throw new InvalidDataException($"The 'head' table's majorVersion is {r.U16(0)}, not 1.");

        // The reserved flags bit 15 is ignored, as FreeType ignores it (Denys's "Accept it", 2026-11-26).

        if (r.U32(12) != Magic)
            throw new InvalidDataException($"The 'head' table's magic number is 0x{r.U32(12):X8}, not 0x{Magic:X8}.");

        var unitsPerEm = r.U16(18);
        if (unitsPerEm is < MinUnitsPerEm or > MaxUnitsPerEm)
            throw new InvalidDataException($"The 'head' table's unitsPerEm is {unitsPerEm}, outside {MinUnitsPerEm}..{MaxUnitsPerEm}.");

        var indexToLocFormat = r.I16(50);
        if (indexToLocFormat is not (0 or 1))
            throw new InvalidDataException($"The 'head' table's indexToLocFormat is {indexToLocFormat}, not 0 or 1.");

        return new HeadTable(r.U16(16), unitsPerEm, r.I16(36), r.I16(38), r.I16(40), r.I16(42), r.U16(44), indexToLocFormat);
    }
}
