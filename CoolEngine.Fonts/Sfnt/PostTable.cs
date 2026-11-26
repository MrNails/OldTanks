namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>post</c> table. Only its 32-byte header is read and <c>isFixedPitch</c> kept; glyph names are not used.
/// </summary>
internal sealed class PostTable
{
    private const int HeaderLength = 32;
    private const uint Version1 = 0x00010000;
    private const uint Version2 = 0x00020000;
    private const uint Version25 = 0x00025000;
    private const uint Version3 = 0x00030000;
    private const uint Version4 = 0x00040000;

    private PostTable(bool isFixedPitch) => IsFixedPitch = isFixedPitch;

    public bool IsFixedPitch { get; }

    public static PostTable Parse(ReadOnlySpan<byte> data)
    {
        var r = new BigEndianReader(data, "post");
        if (r.Length < HeaderLength)
            throw new InvalidDataException($"The 'post' table is {r.Length} bytes long; it needs {HeaderLength}.");

        // Only the header is read. The glyph names of versions 2.0 and 2.5, and Apple's version 4.0 character codes, are never
        // returned, so they are not checked (Denys's safety-strict decision of 2026-11-26), as FreeType reads them only on request.
        var version = r.U32(0);
        if (version is not (Version1 or Version2 or Version25 or Version3 or Version4))
            throw new InvalidDataException($"The 'post' table's version is 0x{version:X8}, not 1.0, 2.0, 2.5, 3.0 or 4.0.");

        return new PostTable(r.U32(12) != 0);
    }
}
