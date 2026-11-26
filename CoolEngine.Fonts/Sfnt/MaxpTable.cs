namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>maxp</c> table: the glyph count, and in version 1.0 (TrueType outlines) the limits the glyph loader and the
/// TrueType interpreter work within.
/// </summary>
internal sealed class MaxpTable
{
    public const uint Version05 = 0x00005000;
    public const uint Version10 = 0x00010000;

    private const int Version05Length = 6;
    private const int Version10Length = 32;

    private MaxpTable(uint version, ushort[] fields)
    {
        Version = version;
        NumGlyphs = fields[0];
        if (version != Version10)
            return;

        MaxPoints = fields[1];
        MaxContours = fields[2];
        MaxCompositePoints = fields[3];
        MaxCompositeContours = fields[4];
        MaxZones = fields[5];
        MaxTwilightPoints = fields[6];
        MaxStorage = fields[7];
        MaxFunctionDefs = fields[8];
        MaxInstructionDefs = fields[9];
        MaxStackElements = fields[10];
        MaxSizeOfInstructions = fields[11];
        MaxComponentElements = fields[12];
        MaxComponentDepth = fields[13];
    }

    public uint Version { get; }

    public ushort NumGlyphs { get; }

    public ushort MaxPoints { get; }

    public ushort MaxContours { get; }

    public ushort MaxCompositePoints { get; }

    public ushort MaxCompositeContours { get; }

    public ushort MaxZones { get; }

    public ushort MaxTwilightPoints { get; }

    public ushort MaxStorage { get; }

    public ushort MaxFunctionDefs { get; }

    public ushort MaxInstructionDefs { get; }

    public ushort MaxStackElements { get; }

    public ushort MaxSizeOfInstructions { get; }

    public ushort MaxComponentElements { get; }

    public ushort MaxComponentDepth { get; }

    public static MaxpTable Parse(ReadOnlySpan<byte> data)
    {
        var r = new BigEndianReader(data, "maxp");
        var version = r.U32(0);
        var length = version switch
        {
            Version05 => Version05Length,
            Version10 => Version10Length,
            _ => throw new InvalidDataException($"The 'maxp' table's version is 0x{version:X8}, not 0.5 or 1.0."),
        };

        if (r.Length < length)
            throw new InvalidDataException($"The 'maxp' table is {r.Length} bytes long; version 0x{version:X8} needs {length}.");

        var fields = new ushort[(length - 4) / 2];
        for (var i = 0; i < fields.Length; i++)
            fields[i] = r.U16(4 + 2 * i);

        if (fields[0] == 0)
            throw new InvalidDataException("The 'maxp' table's numGlyphs is 0; glyph 0 (.notdef) must exist.");

        return new MaxpTable(version, fields);
    }
}
