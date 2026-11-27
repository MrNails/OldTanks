using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// A checked CFF or CFF2 INDEX: its element count and where each element lies in the table. Every element lies inside the
/// table: the offsets start at 1, never decrease, and the last one ends inside the table.
/// </summary>
internal sealed class CffIndex
{
    private const int MaxOffsetSize = 4;

    // Positions in the table: element i is m_starts[i] .. m_starts[i + 1].
    private readonly int[] m_starts;

    private CffIndex(int[] starts, int end)
    {
        m_starts = starts;
        End = end;
    }

    /// <summary>An INDEX with no elements (a Private DICT without Subrs).</summary>
    public static CffIndex Empty { get; } = new([], 0);

    /// <summary>The number of elements.</summary>
    public int Count => m_starts.Length == 0 ? 0 : m_starts.Length - 1;

    /// <summary>The table position just past the INDEX.</summary>
    public int End { get; }

    /// <summary>Where element <paramref name="index"/> starts in the table.</summary>
    public int Start(int index) => m_starts[index];

    /// <summary>The element's length in bytes.</summary>
    public int Length(int index) => m_starts[index + 1] - m_starts[index];

    /// <summary>The element's bytes.</summary>
    public ReadOnlySpan<byte> Get(ReadOnlySpan<byte> table, int index) => table.Slice(m_starts[index], Length(index));

    /// <summary>
    /// Reads and checks the INDEX at <paramref name="offset"/>: a 16-bit count (32-bit in CFF2); when it is not 0, the offset
    /// size (1..4) and count + 1 offsets, the first 1, none smaller than the one before, the last ending inside the table.
    /// </summary>
    /// <param name="table">The CFF or CFF2 table.</param>
    /// <param name="offset">Where the INDEX starts in the table.</param>
    /// <param name="isCff2">Read a 32-bit count.</param>
    /// <param name="what">The INDEX, for messages ("the CharStrings INDEX").</param>
    /// <param name="parsed">The file's load state: the offsets are charged before they are walked, their array reserved.</param>
    public static CffIndex Read(ReadOnlySpan<byte> table, long offset, bool isCff2, string what, ParsedTables parsed)
    {
        var headerLength = isCff2 ? 4 : 2;
        var t = isCff2 ? "CFF2" : "CFF";
        if (offset < 0 || offset + headerLength > table.Length)
            throw new InvalidDataException($"The {t} table's {what} at {offset} runs past the table's {table.Length} bytes.");

        var r = new BigEndianReader(table, "CFF ");
        var at = (int)offset;
        long count = isCff2 ? r.U32(at) : r.U16(at);
        if (count == 0)
            return new CffIndex([], at + headerLength);

        if (at + headerLength + 1 > table.Length)
            throw new InvalidDataException($"The {t} table's {what} at {offset} has no offset size byte inside the table's {table.Length} bytes.");

        int offSize = r.U8(at + headerLength);
        if (offSize is < 1 or > MaxOffsetSize)
            throw new InvalidDataException($"The {t} table's {what} has offset size {offSize}; it must be 1 to 4.");

        var offsetsAt = at + headerLength + 1L;
        var offsetsLength = (count + 1) * offSize;
        if (offsetsAt + offsetsLength > table.Length)
            throw new InvalidDataException(
                $"The {t} table's {what} of {count} elements has its offsets past the table's {table.Length} bytes.");

        parsed.Charge(offsetsLength, "a CFF INDEX of", count);
        parsed.Reserve(32 + 4 * (count + 1), "a CFF INDEX of", count);

        // Element data starts after the offsets; an offset of 1 is its first byte.
        var dataBase = offsetsAt + offsetsLength - 1;
        var starts = new int[count + 1];
        long previous = 0;
        for (var i = 0; i <= count; i++)
        {
            var p = (int)(offsetsAt + i * offSize);
            long value = offSize switch
            {
                1 => r.U8(p),
                2 => r.U16(p),
                3 => r.U24(p),
                _ => r.U32(p),
            };

            if (i == 0 && value != 1)
                throw new InvalidDataException($"The {t} table's {what} has first offset {value}; it must be 1.");

            if (value < previous)
                throw new InvalidDataException(
                    $"The {t} table's {what} has offset {value} for element {i}, smaller than the one before ({previous}).");

            if (dataBase + value > table.Length)
                throw new InvalidDataException(
                    $"The {t} table's {what} has element {i - 1} ending at {dataBase + value}, past the table's {table.Length} bytes.");

            previous = value;
            starts[i] = (int)(dataBase + value);
        }

        return new CffIndex(starts, starts[^1]);
    }
}
