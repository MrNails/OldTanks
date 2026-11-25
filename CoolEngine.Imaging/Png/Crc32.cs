namespace CoolEngine.Imaging.Png;

/// <summary>The CRC-32 of PNG chunks (ISO 3309 / ITU-T V.42: reflected polynomial <c>0xEDB88320</c>, initial and final XOR <c>0xFFFFFFFF</c>).</summary>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320;

    private static readonly uint[] s_table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var table = s_table;
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
            crc = table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var bit = 0; bit < 8; bit++)
                c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;

            table[n] = c;
        }

        return table;
    }
}
