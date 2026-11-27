// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (tt_var_load_item_variation_store, tt_var_get_item_delta) and
// src/cff/cffload.c (cff_vstore_load, cff_blend_build_vector) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>
/// An item variation store (OpenType's <c>ItemVariationStore</c>): its regions and its item variation data, as two readers
/// read it. The CFF2 table's store (<see cref="Parse"/>, FreeType's <c>cff_vstore_load</c>) is read for its regions only: CFF2
/// has no delta sets; <c>blend</c> weighs each region by <see cref="BuildBlendVector"/>. The store of <c>HVAR</c>,
/// <c>VVAR</c> and <c>MVAR</c> (<see cref="ParseFull"/>, FreeType's <c>tt_var_load_item_variation_store</c>) has its delta
/// sets, blended by <see cref="GetItemDelta"/>. Immutable.
/// </summary>
/// <remarks>
/// <para>
/// Both readers check at load (<see cref="InvalidDataException"/>): a format other than 1; the region list or an item variation
/// data entry past the data; a region index not below the region count (FreeType checks this for CFF2 only when a blend uses
/// the entry, <c>cff_blend_build_vector</c>).
/// </para>
/// <para>
/// <see cref="ParseFull"/> checks besides, as FreeType's loader fails the table on each (FreeType then ignores the whole
/// <c>HVAR</c>, <c>VVAR</c> or <c>MVAR</c>, and so does this library: their parsers treat the table as absent): no item
/// variation data; an axis count other than
/// <c>fvar</c>'s; 32,768 regions or more; more word deltas than regions in an entry, or more regions than the store has; delta
/// sets past the data.
/// </para>
/// </remarks>
internal sealed class ItemVariationStore : IBlendRegions
{
    private readonly ushort[][] m_regionIndices;

    // Per region, per axis: start, peak and end in 16.16.
    private readonly long[] m_regions;

    // ParseFull only: per entry, its item count, word delta count, long-words flag and where its delta sets start.
    private readonly ItemData[] m_items;
    private readonly ReadOnlyMemory<byte> m_data;

    private ItemVariationStore(int axisCount, int regionCount, ushort[][] regionIndices, long[] regions, ItemData[] items,
        ReadOnlyMemory<byte> data)
    {
        AxisCount = axisCount;
        RegionCount = regionCount;
        m_regionIndices = regionIndices;
        m_regions = regions;
        m_items = items;
        m_data = data;
    }

    /// <summary>A store with no data: any <c>vsindex</c> is out of range.</summary>
    public static ItemVariationStore Empty { get; } = new(0, 0, [], [], [], ReadOnlyMemory<byte>.Empty);

    public int AxisCount { get; }

    public int RegionCount { get; }

    public int DataCount => m_regionIndices.Length;

    public int RegionIndexCount(int vsindex) => m_regionIndices[vsindex].Length;

    /// <summary>The regions entry <paramref name="vsindex"/> blends, in order.</summary>
    public ReadOnlySpan<ushort> RegionIndices(int vsindex) => m_regionIndices[vsindex];

    /// <summary>The item count of entry <paramref name="outer"/> (a store read by <see cref="ParseFull"/>).</summary>
    public int ItemCount(int outer) => m_items[outer].ItemCount;

    /// <summary>Reads and checks the CFF2 table's store that starts at <paramref name="offset"/> of <paramref name="data"/>.</summary>
    /// <param name="data">The table holding the store; its offsets are relative to <paramref name="offset"/>.</param>
    /// <param name="offset">Where the store (its format field) starts.</param>
    /// <param name="parsed">The file's load state: walks are charged, arrays reserved.</param>
    public static ItemVariationStore Parse(ReadOnlySpan<byte> data, int offset, ParsedTables parsed) =>
        Read(data, ReadOnlyMemory<byte>.Empty, offset, "CFF2 variation store", -1, full: false, parsed);

    /// <summary>Reads and checks the store of <c>HVAR</c>, <c>VVAR</c> or <c>MVAR</c>, delta sets included.</summary>
    /// <param name="table">The table holding the store.</param>
    /// <param name="offset">Where the store starts in the table.</param>
    /// <param name="name">The table, for messages (<c>'HVAR'</c>).</param>
    /// <param name="axisCount"><c>fvar</c>'s axis count, which the store's must equal.</param>
    /// <param name="parsed">The file's load state.</param>
    public static ItemVariationStore ParseFull(ReadOnlyMemory<byte> table, int offset, string name, int axisCount, ParsedTables parsed) =>
        Read(table.Span, table, offset, $"{name} item variation store", axisCount, full: true, parsed);

    private static ItemVariationStore Read(ReadOnlySpan<byte> data, ReadOnlyMemory<byte> memory, int offset, string what,
        int fvarAxes, bool full, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, what);
        if (!r.Contains(offset, 8))
            throw new InvalidDataException($"The {what}'s header at {offset} runs past the table.");

        var format = r.U16(offset);
        if (format != 1)
            throw new InvalidDataException($"The {what} has format {format}; only format 1 exists.");

        var regionListOffset = offset + (long)r.U32(offset + 2);
        int dataCount = r.U16(offset + 6);
        if (!r.Contains(offset + 8, 4L * dataCount))
            throw new InvalidDataException($"The {what}'s {dataCount} data offsets run past the table.");

        if (full && dataCount == 0)
            throw new InvalidDataException($"The {what} has no item variation data (FreeType refuses it).");

        if (!r.Contains(regionListOffset, 4))
            throw new InvalidDataException($"The {what}'s region list at {regionListOffset} runs past the table.");

        int axisCount = r.U16((int)regionListOffset);
        int regionCount = r.U16((int)regionListOffset + 2);
        var regionsLength = 6L * axisCount * regionCount;
        if (!r.Contains(regionListOffset + 4, regionsLength))
            throw new InvalidDataException($"The {what}'s {regionCount} regions of {axisCount} axes run past the table.");

        if (full && axisCount != fvarAxes)
            throw new InvalidDataException($"The {what} has {axisCount} axes, but 'fvar' has {fvarAxes}.");

        if (full && regionCount >= 32768)
            throw new InvalidDataException($"The {what} has {regionCount} regions; OpenType allows fewer than 32,768.");

        parsed.Charge(regionsLength, "the regions of the", -1);
        parsed.Reserve(64 + 8L * 3 * axisCount * regionCount, "the regions of the", -1);
        var regions = new long[3 * axisCount * regionCount];
        for (var i = 0; i < regions.Length; i++)
            regions[i] = r.I16((int)regionListOffset + 4 + 2 * i) * 4L;

        parsed.Charge(4L * dataCount, "the data offsets of the", dataCount);
        parsed.Reserve(64 + 64L * dataCount, "the data of the", dataCount);
        var regionIndices = new ushort[dataCount][];
        var items = full ? new ItemData[dataCount] : [];
        for (var i = 0; i < dataCount; i++)
        {
            var at = offset + (long)r.U32(offset + 8 + 4 * i);
            if (!r.Contains(at, 6))
                throw new InvalidDataException($"The {what}'s data {i} at {at} runs past the table.");

            int itemCount = r.U16((int)at);
            int wordCount = r.U16((int)at + 2);
            int count = r.U16((int)at + 4);
            if (!r.Contains(at + 6, 2L * count))
                throw new InvalidDataException($"The {what}'s data {i} has {count} region indices past the table.");

            var longWords = (wordCount & 0x8000) != 0;
            wordCount &= 0x7FFF;
            if (full && wordCount > count)
                throw new InvalidDataException($"The {what}'s data {i} has {wordCount} word deltas but {count} regions.");

            if (full && count > regionCount)
                throw new InvalidDataException($"The {what}'s data {i} blends {count} regions, but the store has {regionCount}.");

            parsed.Charge(2L * count, "the region indices of data", i);
            parsed.Reserve(2L * count, "the region indices of data", i);
            var indices = new ushort[count];
            for (var j = 0; j < count; j++)
            {
                indices[j] = r.U16((int)at + 6 + 2 * j);
                if (indices[j] >= regionCount)
                    throw new InvalidDataException(
                        $"The {what}'s data {i} names region {indices[j]}, but the store has {regionCount} regions.");
            }

            regionIndices[i] = indices;
            if (!full)
                continue;

            // The delta sets: per item, the word deltas (16 or 32 bits) then the rest (8 or 16 bits).
            var rowSize = (long)(wordCount + count) * (longWords ? 2 : 1);
            var deltasAt = at + 6 + 2L * count;
            if (!r.Contains(deltasAt, rowSize * itemCount))
                throw new InvalidDataException($"The {what}'s data {i} has {itemCount} delta sets past the table.");

            items[i] = new ItemData(itemCount, wordCount, longWords, (int)deltasAt, (int)rowSize);
        }

        return new ItemVariationStore(axisCount, regionCount, regionIndices, regions, items, memory);
    }

    /// <summary>
    /// <c>tt_var_get_item_delta</c>: the blended delta of item (<paramref name="outer"/>, <paramref name="inner"/>) at the
    /// normalized coordinates, <c>FT_MulAddFix</c> of the region scalars and the deltas; 0 for (0xFFFF, 0xFFFF) or an index out
    /// of range, as FreeType returns.
    /// </summary>
    public int GetItemDelta(int outer, int inner, ReadOnlySpan<long> coords, IWorkCounter? work = null)
    {
        if (outer == 0xFFFF && inner == 0xFFFF)
            return 0;

        if ((uint)outer >= (uint)m_items.Length || (uint)inner >= (uint)m_items[outer].ItemCount)
            return 0;

        var item = m_items[outer];
        var indices = m_regionIndices[outer];
        work?.Charge(indices.Length * (1L + AxisCount));
        var row = m_data.Span.Slice(item.DeltasAt + item.RowSize * inner, item.RowSize);
        long sum = 0;
        var at = 0;
        for (var master = 0; master < indices.Length; master++)
        {
            long delta;
            if (master < item.WordCount)
            {
                delta = item.LongWords ? Read32(row, at) : (short)(row[at] << 8 | row[at + 1]);
                at += item.LongWords ? 4 : 2;
            }
            else
            {
                delta = item.LongWords ? (short)(row[at] << 8 | row[at + 1]) : (sbyte)row[at];
                at += item.LongWords ? 2 : 1;
            }

            sum += RegionScalar(indices[master], coords) * delta;
        }

        // FT_MulAddFix.
        return (int)((sum + 0x8000) >> 16);
    }

    /// <summary>
    /// <c>cff_blend_build_vector</c> with a normalized vector: the weight of the default master (1) and of each region entry
    /// <paramref name="vsindex"/> blends, 16.16 in 32 bits as FreeType keeps them.
    /// </summary>
    public void BuildBlendVector(int vsindex, ReadOnlySpan<long> coords, Span<int> weights)
    {
        var indices = m_regionIndices[vsindex];
        weights[0] = CffFixed.One;
        for (var master = 1; master <= indices.Length; master++)
        {
            var region = indices[master - 1] * AxisCount * 3;
            long weight = 0x10000;
            for (var j = 0; j < coords.Length; j++)
            {
                long start = m_regions[region + 3 * j], peak = m_regions[region + 3 * j + 1], end = m_regions[region + 3 * j + 2];
                var coord = coords[j];
                long axisScalar;
                if (start > peak || peak > end)
                    axisScalar = 0x10000;
                else if (start < 0 && end > 0 && peak != 0)
                    axisScalar = 0x10000;
                else if (peak == 0)
                    axisScalar = 0x10000;
                else if (coord < start || coord > end)
                    axisScalar = 0;
                else if (coord == peak)
                    axisScalar = 0x10000;
                else if (coord < peak)
                    axisScalar = FixedMath.DivFix(coord - start, peak - start);
                else
                    axisScalar = FixedMath.DivFix(end - coord, end - peak);

                weight = FixedMath.MulFix(weight, axisScalar);
            }

            // BV is an FT_Int32 array.
            weights[master] = (int)weight;
        }
    }

    /// <summary>The scalar of one region at the coordinates, as <c>tt_var_get_item_delta</c> computes it.</summary>
    private long RegionScalar(int region, ReadOnlySpan<long> coords)
    {
        long scalar = 0x10000;
        var at = region * AxisCount * 3;
        for (var j = 0; j < AxisCount; j++)
        {
            long start = m_regions[at + 3 * j], peak = m_regions[at + 3 * j + 1], end = m_regions[at + 3 * j + 2];
            var coord = j < coords.Length ? coords[j] : 0;
            if (start > peak || peak > end)
                continue;

            if (start < 0 && end > 0 && peak != 0)
                continue;

            if (peak == 0 || coord == peak)
                continue;

            if (coord <= start || coord >= end)
                return 0;

            scalar = coord < peak
                ? FixedMath.MulDiv(scalar, coord - start, peak - start)
                : FixedMath.MulDiv(scalar, end - coord, end - peak);
        }

        return scalar;
    }

    private static int Read32(ReadOnlySpan<byte> row, int at) => row[at] << 24 | row[at + 1] << 16 | row[at + 2] << 8 | row[at + 3];

    /// <param name="ItemCount">The delta sets.</param>
    /// <param name="WordCount">The deltas of each set stored in the larger size.</param>
    /// <param name="LongWords">The larger size is 32 bits (else 16) and the smaller 16 (else 8).</param>
    /// <param name="DeltasAt">Where the first set starts in the table.</param>
    /// <param name="RowSize">The bytes of one set.</param>
    private readonly record struct ItemData(int ItemCount, int WordCount, bool LongWords, int DeltasAt, int RowSize);
}
