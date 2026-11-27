// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (tt_var_load_delta_set_index_mapping, ft_var_load_hvvar,
// tt_hvadvance_adjust, ft_var_load_mvar, tt_apply_mvar) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>
/// A delta-set index map (<c>DeltaSetIndexMap</c>, formats 0 and 1), read as FreeType's
/// <c>tt_var_load_delta_set_index_mapping</c> reads it. Checked at load (<see cref="InvalidDataException"/>, which makes
/// <see cref="HvarTable"/> treat its table as absent, as FreeType fails the table on each): a format other than 0 and 1;
/// reserved entry-format bits set; entries past the table; an outer index not
/// below the store's entry count, or an inner index not below that entry's item count (a 32-bit entry of all ones, "no
/// variation", excepted).
/// </summary>
internal sealed class DeltaSetIndexMap
{
    private readonly uint[] m_entries;

    private DeltaSetIndexMap(uint[] entries) => m_entries = entries;

    /// <summary>The map's entries; FreeType treats a map of none as no map.</summary>
    public int Count => m_entries.Length;

    /// <summary>The (outer, inner) pair of an index, the last entry standing in for indices past the map.</summary>
    public (int Outer, int Inner) Get(int index)
    {
        var entry = m_entries[Math.Min(index, m_entries.Length - 1)];
        return ((int)(entry >> 16), (int)(entry & 0xFFFF));
    }

    public static DeltaSetIndexMap Parse(ReadOnlySpan<byte> table, int offset, ItemVariationStore store, string name, ParsedTables parsed)
    {
        var r = new BigEndianReader(table, name);
        if (!r.Contains(offset, 2))
            throw new InvalidDataException($"The {name} delta-set index map at {offset} runs past the table.");

        int format = r.U8(offset);
        int entryFormat = r.U8(offset + 1);
        long count;
        int at;
        if (format == 0)
        {
            count = r.U16(offset + 2);
            at = offset + 4;
        }
        else if (format == 1)
        {
            count = r.U32(offset + 2);
            at = offset + 6;
        }
        else
        {
            throw new InvalidDataException($"The {name} delta-set index map has format {format}; only 0 and 1 exist.");
        }

        if ((entryFormat & 0xC0) != 0)
            throw new InvalidDataException($"The {name} delta-set index map's entry format 0x{entryFormat:X2} sets reserved bits.");

        var entrySize = ((entryFormat & 0x30) >> 4) + 1;
        var innerBits = (entryFormat & 0x0F) + 1;
        if (!r.Contains(at, count * entrySize))
            throw new InvalidDataException($"The {name} delta-set index map's {count} entries run past the table.");

        parsed.Charge(count * entrySize, "the delta-set index map of", count);
        parsed.Reserve(24 + 4 * count, "the delta-set index map of", count);
        var entries = new uint[count];
        for (var i = 0; i < count; i++)
        {
            uint data = 0;
            for (var j = 0; j < entrySize; j++)
                data = data << 8 | r.U8(at + entrySize * i + j);

            if (data == 0xFFFFFFFF)
            {
                entries[i] = 0xFFFFFFFF;
                continue;
            }

            var outer = data >> innerBits;
            var inner = data & ((1u << innerBits) - 1);
            if (outer >= store.DataCount)
                throw new InvalidDataException(
                    $"The {name} delta-set index map's entry {i} names item data {outer}, but the store has {store.DataCount}.");

            if (inner >= store.ItemCount((int)outer))
                throw new InvalidDataException(
                    $"The {name} delta-set index map's entry {i} names item {inner} of data {outer}, which has {store.ItemCount((int)outer)}.");

            entries[i] = outer << 16 | inner;
        }

        return new DeltaSetIndexMap(entries);
    }
}

/// <summary>
/// <c>HVAR</c> or <c>VVAR</c>: the advance-width (or advance-height) deltas of each glyph, as FreeType's
/// <c>tt_hvadvance_adjust</c> applies them. Only the advance map is read: FreeType reads no side-bearing variation. Immutable.
/// </summary>
/// <remarks>
/// A broken table is treated as absent, as FreeType ignores it (Denys, 2026-11-27: "Treat as absent, as FreeType"):
/// <see cref="Parse"/> returns null, the font loads, the advances then come from the varied outline (<c>gvar</c>'s phantom
/// points) exactly as FreeType's do. Broken means: a header past the table; an item variation store offset of 0; the store
/// failing <see cref="ItemVariationStore.ParseFull"/>'s checks; the advance map failing <see cref="DeltaSetIndexMap"/>'s; a
/// major version other than 1. One documented difference: where a store or map runs past the table's end but not past the
/// file's, FreeType reads on into the bytes that follow (its reads are bounded by the file), while this library, which never
/// reads outside a table, treats the table as absent. Accepted as FreeType reads it: a glyph past the advance map (the map's
/// last entry), past the store's items without a map (no delta), an empty advance map (as no map). Not read, not checked: the
/// side-bearing maps.
/// </remarks>
internal sealed class HvarTable
{
    public const uint HvarTag = 0x48564152; // HVAR
    public const uint VvarTag = 0x56564152; // VVAR

    private readonly ItemVariationStore m_store;
    private readonly DeltaSetIndexMap? m_advanceMap;

    private HvarTable(ItemVariationStore store, DeltaSetIndexMap? advanceMap)
    {
        m_store = store;
        m_advanceMap = advanceMap;
    }

    /// <summary>The table, or null when FreeType ignores it (broken, or a major version other than 1).</summary>
    /// <exception cref="NotSupportedException">The file's parsed-memory or work budget is passed.</exception>
    public static HvarTable? Parse(ReadOnlyMemory<byte> data, uint tag, int axisCount, ParsedTables parsed)
    {
        try
        {
            return Read(data, tag, axisCount, parsed);
        }
        catch (InvalidDataException)
        {
            // ft_var_load_hvvar fails: FreeType ignores the table (its advances are not adjusted).
            return null;
        }
    }

    private static HvarTable? Read(ReadOnlyMemory<byte> data, uint tag, int axisCount, ParsedTables parsed)
    {
        var name = $"'{Tags.ToString(tag)}'";
        var r = new BigEndianReader(data.Span, Tags.ToString(tag));
        if (!r.Contains(0, 12))
            throw new InvalidDataException($"The {name} table is {r.Length} bytes long, shorter than its header.");

        if (r.U16(0) != 1)
            return null;

        var storeOffset = r.U32(4);
        var mapOffset = r.U32(8);
        if (storeOffset == 0 || storeOffset > int.MaxValue)
            throw new InvalidDataException($"The {name} table's item variation store offset is {storeOffset}.");

        var store = ItemVariationStore.ParseFull(data, (int)storeOffset, name, axisCount, parsed);
        DeltaSetIndexMap? map = null;
        if (mapOffset != 0)
        {
            if (mapOffset > int.MaxValue)
                throw new InvalidDataException($"The {name} table's advance map at {mapOffset} runs past the table.");

            map = DeltaSetIndexMap.Parse(data.Span, (int)mapOffset, store, name, parsed);
            if (map.Count == 0)
                map = null;
        }

        return new HvarTable(store, map);
    }

    /// <summary><c>tt_hvadvance_adjust</c>: the advance delta of a glyph at the normalized coordinates.</summary>
    public int AdvanceDelta(int glyphIndex, ReadOnlySpan<long> coords, IWorkCounter? work)
    {
        var (outer, inner) = m_advanceMap is null ? (0, glyphIndex) : m_advanceMap.Get(glyphIndex);
        return m_store.GetItemDelta(outer, inner, coords, work);
    }
}

/// <summary>
/// <c>MVAR</c>: the deltas of the font-wide metrics FreeType's <c>tt_apply_mvar</c> varies and this library reads (the
/// typographic ascender, descender and line gap of <c>OS/2</c>: the face's line metrics and the vertical phantom points), as
/// FreeType applies them. Immutable.
/// </summary>
/// <remarks>
/// A broken table is treated as absent, as FreeType ignores it (Denys, 2026-11-27: "Treat as absent, as FreeType"):
/// <see cref="Parse"/> returns null, the font loads, its line metrics unvaried. Broken means: a header past the table; records
/// past the table; records but no item variation store; the store failing <see cref="ItemVariationStore.ParseFull"/>'s checks;
/// a record naming an item the store lacks ((0xFFFF, 0xFFFF), "no variation", excepted); a major version other than 1. As for
/// <see cref="HvarTable"/>, a read past the table's end but not the file's is FreeType's and not ours (documented). Records
/// are stepped by the declared value record size (the spec and OTS), each read for its first 8 bytes; FreeType steps by 8
/// whatever the size says, so it reads only 8-byte records right (Denys's "Ours works, documented": a record size above 8
/// works here; FreeType misreads it), and a size below 8 is read with FreeType's stride of 8. Accepted as FreeType reads it:
/// a table without records (nothing to vary); tags FreeType does not know, and the tags it varies that this library does not
/// read (ignored); repeated or unsorted tags, the last record of a tag with a delta other than 0 winning, as in FreeType's
/// <c>tt_apply_mvar</c> (Denys, 2026-11-27: "Follow FreeType", an exception to the rule that rejects duplicates where OTS
/// rejects them).
/// </remarks>
internal sealed class MvarTable
{
    public const uint Tag = 0x4D564152; // MVAR

    public const uint Hasc = 0x68617363; // hasc
    public const uint Hdsc = 0x68647363; // hdsc
    public const uint Hlgp = 0x686C6770; // hlgp

    private const int RecordSize = 8;

    private readonly ItemVariationStore? m_store;
    private readonly (uint Tag, int Outer, int Inner)[] m_records;

    private MvarTable(ItemVariationStore? store, (uint, int, int)[] records)
    {
        m_store = store;
        m_records = records;
    }

    /// <summary>The table, or null when FreeType ignores it (broken, or a major version other than 1).</summary>
    /// <exception cref="NotSupportedException">The file's parsed-memory or work budget is passed.</exception>
    public static MvarTable? Parse(ReadOnlyMemory<byte> data, int axisCount, ParsedTables parsed)
    {
        try
        {
            return Read(data, axisCount, parsed);
        }
        catch (InvalidDataException)
        {
            // ft_var_load_mvar fails: FreeType ignores the table (no metric varies).
            return null;
        }
    }

    private static MvarTable? Read(ReadOnlyMemory<byte> data, int axisCount, ParsedTables parsed)
    {
        var r = new BigEndianReader(data.Span, "MVAR");
        if (!r.Contains(0, 12))
            throw new InvalidDataException($"The 'MVAR' table is {r.Length} bytes long, shorter than its 12-byte header.");

        if (r.U16(0) != 1)
            return null;

        // The declared record size, or FreeType's 8 when it is smaller.
        var stride = Math.Max((int)r.U16(6), RecordSize);
        int count = r.U16(8);
        int storeOffset = r.U16(10);
        if (count == 0)
            return new MvarTable(null, []);

        if (!r.Contains(12, (long)stride * count))
            throw new InvalidDataException($"The 'MVAR' table's {count} value records run past the table.");

        if (storeOffset == 0)
            throw new InvalidDataException($"The 'MVAR' table has {count} value records but no item variation store.");

        var store = ItemVariationStore.ParseFull(data, storeOffset, "'MVAR'", axisCount, parsed);
        parsed.Charge(RecordSize * count, "the 'MVAR' value records", count);
        parsed.Reserve(24 + 16L * count, "the 'MVAR' value records", count);
        var records = new (uint, int, int)[count];
        for (var i = 0; i < count; i++)
        {
            var at = 12 + stride * i;
            int outer = r.U16(at + 4), inner = r.U16(at + 6);
            if (!(outer == 0xFFFF && inner == 0xFFFF) && (outer >= store.DataCount || inner >= store.ItemCount(outer)))
                throw new InvalidDataException(
                    $"The 'MVAR' table's value record {i} names item ({outer}, {inner}), which its store lacks.");

            records[i] = (r.U32(at), outer, inner);
        }

        return new MvarTable(store, records);
    }

    /// <summary>
    /// The deltas <c>tt_apply_mvar</c> applies to the face's ascender, descender and line gap (the <c>hasc</c>, <c>hdsc</c> and
    /// <c>hlgp</c> values): of several records with one tag, the last with a delta other than 0, as in FreeType.
    /// </summary>
    public (short Ascender, short Descender, short LineGap) LineDeltas(ReadOnlySpan<long> coords, IWorkCounter work)
    {
        short ascender = 0, descender = 0, lineGap = 0;
        if (m_store is null)
            return (0, 0, 0);

        work.Charge(m_records.Length);
        foreach (var (tag, outer, inner) in m_records)
        {
            if (tag is not (Hasc or Hdsc or Hlgp))
                continue;

            // FreeType tests the full delta, then keeps its low 16 bits.
            var delta = m_store.GetItemDelta(outer, inner, coords, work);
            if (delta == 0)
                continue;

            if (tag == Hasc)
                ascender = (short)delta;
            else if (tag == Hdsc)
                descender = (short)delta;
            else
                lineGap = (short)delta;
        }

        return (ascender, descender, lineGap);
    }
}
