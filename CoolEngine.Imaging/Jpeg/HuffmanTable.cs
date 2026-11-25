namespace CoolEngine.Imaging.Jpeg;

/// <summary>
/// A Huffman decoding table from a DHT segment, built as ITU-T T.81 Annex C describes: canonical codes assigned in order of length.
/// Codes up to <see cref="LookupBits"/> bits long are resolved with one table lookup, longer ones through the per-length maximum code.
/// </summary>
internal sealed class HuffmanTable
{
    public const int LookupBits = 9;

    private const int MaxCodeLength = 16;
    private const int MaxSymbols = 256;
    private const int MaxDcSymbol = 15;

    private readonly byte[] m_symbols;

    // (length << 8) | symbol for every LookupBits-bit prefix that starts with a code of at most LookupBits bits; 0 otherwise.
    private readonly ushort[] m_lookup = new ushort[1 << LookupBits];

    // The largest code of each length (index 1..16), or -1 when no code has that length.
    private readonly int[] m_maxCode = new int[MaxCodeLength + 1];

    // The index in m_symbols of a code of each length is the code plus this offset.
    private readonly int[] m_valueOffset = new int[MaxCodeLength + 1];

    /// <param name="counts">The 16 counts of codes of length 1 to 16.</param>
    /// <param name="symbols">The symbols in code order; as many as the counts add up to.</param>
    /// <param name="isDc">A DC table, whose symbols are bit counts and may not exceed 15.</param>
    /// <exception cref="InvalidDataException">More than 256 codes, a DC symbol over 15, an all-ones code, or a code that does not fit in its length.</exception>
    public HuffmanTable(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> symbols, bool isDc)
    {
        if (counts.Length != MaxCodeLength)
            throw new ArgumentException($"Expected {MaxCodeLength} code counts, got {counts.Length}.", nameof(counts));

        var total = 0;
        foreach (var count in counts)
            total += count;

        if (total > MaxSymbols)
            throw new InvalidDataException($"Huffman table has {total} codes; at most {MaxSymbols} are allowed.");

        if (total != symbols.Length)
            throw new InvalidDataException($"Huffman table lists {symbols.Length} symbols for {total} codes.");

        if (isDc)
        {
            foreach (var symbol in symbols)
            {
                if (symbol > MaxDcSymbol)
                    throw new InvalidDataException($"DC Huffman symbol {symbol} exceeds {MaxDcSymbol}.");
            }
        }

        m_symbols = symbols.ToArray();

        var code = 0;
        var index = 0;
        for (var length = 1; length <= MaxCodeLength; length++)
        {
            var count = counts[length - 1];
            m_valueOffset[length] = index - code;

            for (var i = 0; i < count; i++)
            {
                // The all-ones code of a length is reserved (T.81 Annex C, libjpeg's JERR_BAD_HUFF_TABLE); past it the code would not fit.
                if (code >= (1 << length) - 1)
                    throw new InvalidDataException($"Huffman code {index} of {length} bits is all ones or does not fit.");

                if (length <= LookupBits)
                {
                    var shift = LookupBits - length;
                    var first = code << shift;
                    var entry = (ushort)(length << 8 | m_symbols[index]);
                    m_lookup.AsSpan(first, 1 << shift).Fill(entry);
                }

                code++;
                index++;
            }

            m_maxCode[length] = count == 0 ? -1 : code - 1;
            code <<= 1;
        }
    }

    /// <summary>Decodes the code at the start of <paramref name="bits"/>, the next 16 bits of the stream, most significant first.</summary>
    /// <returns><c>false</c> when no code of at most 16 bits matches.</returns>
    public bool TryDecode(int bits, out int symbol, out int length)
    {
        var entry = m_lookup[(bits & 0xFFFF) >> (MaxCodeLength - LookupBits)];
        if (entry != 0)
        {
            length = entry >> 8;
            symbol = entry & 0xFF;
            return true;
        }

        for (length = LookupBits + 1; length <= MaxCodeLength; length++)
        {
            var code = (bits & 0xFFFF) >> (MaxCodeLength - length);
            if (code <= m_maxCode[length])
            {
                symbol = m_symbols[code + m_valueOffset[length]];
                return true;
            }
        }

        symbol = 0;
        length = 0;
        return false;
    }
}
