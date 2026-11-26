namespace CoolEngine.Fonts.Sfnt;

/// <summary>CRC-32 (ISO 3309, the polynomial 0xEDB88320 reflected), as gzip's trailer records it.</summary>
internal static class Crc32
{
    public const uint Initial = 0xFFFFFFFF;

    private static readonly uint[] s_table = CreateTable();

    public static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
            crc = s_table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc;
    }

    public static uint Finish(uint crc) => crc ^ 0xFFFFFFFF;

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;

            table[n] = c;
        }

        return table;
    }
}
