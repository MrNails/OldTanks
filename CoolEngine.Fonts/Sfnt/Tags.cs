namespace CoolEngine.Fonts.Sfnt;

/// <summary>Four-character sfnt table tags as big-endian integers.</summary>
internal static class Tags
{
    public const uint Head = 0x68656164;  // head
    public const uint Hhea = 0x68686561;  // hhea
    public const uint Maxp = 0x6D617870;  // maxp
    public const uint Hmtx = 0x686D7478;  // hmtx
    public const uint Cmap = 0x636D6170;  // cmap
    public const uint Name = 0x6E616D65;  // name
    public const uint Post = 0x706F7374;  // post
    public const uint Os2 = 0x4F532F32;   // OS/2
    public const uint Loca = 0x6C6F6361;  // loca
    public const uint Glyf = 0x676C7966;  // glyf
    public const uint Cff = 0x43464620;   // CFF
    public const uint Cff2 = 0x43464632;  // CFF2
    public const uint Svg = 0x53564720;   // SVG
    public const uint Fvar = 0x66766172;  // fvar
    public const uint Ebdt = 0x45424454;  // EBDT
    public const uint Eblc = 0x45424C43;  // EBLC
    public const uint Cbdt = 0x43424454;  // CBDT
    public const uint Cblc = 0x43424C43;  // CBLC
    public const uint Sbix = 0x73626978;  // sbix
    public const uint Bhed = 0x62686564;  // bhed
    public const uint Hvgl = 0x6876676C;  // hvgl

    /// <summary>The tag of a four-character string, such as <c>"cmap"</c>.</summary>
    public static uint FromString(string tag)
    {
        if (tag.Length != 4)
            throw new ArgumentException($"A tag has four characters, not '{tag}'.", nameof(tag));

        return (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
    }

    /// <summary>The four characters of a tag; bytes outside printable ASCII show as <c>?</c>.</summary>
    public static string ToString(uint tag)
    {
        Span<char> chars = stackalloc char[4];
        for (var i = 0; i < 4; i++)
        {
            var c = (char)((tag >> (24 - 8 * i)) & 0xFF);
            chars[i] = c is >= ' ' and <= '~' ? c : '?';
        }

        return new string(chars);
    }
}
