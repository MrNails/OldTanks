using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>
/// An item variation store (OpenType's <c>ItemVariationStore</c>, here the one a CFF2 table carries): its axis and region
/// counts and the regions each item variation data entry blends. Minimal for CFF2's <c>blend</c> at the default instance
/// (OT-4 WP2); WP5 adds the region coordinates and the deltas.
/// </summary>
/// <remarks>
/// Checked at load (<see cref="InvalidDataException"/>): a format other than 1; the region list or an item variation data
/// entry past the data; a region index not below the region count (FreeType checks this only when a blend uses the entry,
/// <c>cff_blend_build_vector</c>). The deltas of an entry are not read (CFF2 stores none).
/// </remarks>
internal sealed class ItemVariationStore : IBlendRegions
{
    private readonly ushort[][] m_regionIndices;

    private ItemVariationStore(int axisCount, int regionCount, ushort[][] regionIndices)
    {
        AxisCount = axisCount;
        RegionCount = regionCount;
        m_regionIndices = regionIndices;
    }

    /// <summary>A store with no data: any <c>vsindex</c> is out of range.</summary>
    public static ItemVariationStore Empty { get; } = new(0, 0, []);

    public int AxisCount { get; }

    public int RegionCount { get; }

    public int DataCount => m_regionIndices.Length;

    public int RegionIndexCount(int vsindex) => m_regionIndices[vsindex].Length;

    /// <summary>The regions entry <paramref name="vsindex"/> blends, in order.</summary>
    public ReadOnlySpan<ushort> RegionIndices(int vsindex) => m_regionIndices[vsindex];

    /// <summary>Reads and checks the store that starts at <paramref name="offset"/> of <paramref name="data"/>.</summary>
    /// <param name="data">The table holding the store; its offsets are relative to <paramref name="offset"/>.</param>
    /// <param name="offset">Where the store (its format field) starts.</param>
    /// <param name="parsed">The file's load state: walks are charged, arrays reserved.</param>
    public static ItemVariationStore Parse(ReadOnlySpan<byte> data, int offset, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, "CFF2 variation store");
        if (!r.Contains(offset, 8))
            throw new InvalidDataException($"The CFF2 variation store's header at {offset} runs past the table.");

        var format = r.U16(offset);
        if (format != 1)
            throw new InvalidDataException($"The CFF2 variation store has format {format}; only format 1 exists.");

        var regionListOffset = offset + (long)r.U32(offset + 2);
        int dataCount = r.U16(offset + 6);
        if (!r.Contains(offset + 8, 4L * dataCount))
            throw new InvalidDataException($"The CFF2 variation store's {dataCount} data offsets run past the table.");

        if (!r.Contains(regionListOffset, 4))
            throw new InvalidDataException($"The CFF2 variation store's region list at {regionListOffset} runs past the table.");

        int axisCount = r.U16((int)regionListOffset);
        int regionCount = r.U16((int)regionListOffset + 2);
        var regionsLength = 6L * axisCount * regionCount;
        if (!r.Contains(regionListOffset + 4, regionsLength))
            throw new InvalidDataException(
                $"The CFF2 variation store's {regionCount} regions of {axisCount} axes run past the table.");

        parsed.Charge(4L * dataCount, "the CFF2 variation store's data offsets", dataCount);
        parsed.Reserve(64 + 32L * dataCount, "the CFF2 variation store's data", dataCount);
        var regionIndices = new ushort[dataCount][];
        for (var i = 0; i < dataCount; i++)
        {
            var at = offset + (long)r.U32(offset + 8 + 4 * i);
            if (!r.Contains(at, 6))
                throw new InvalidDataException($"The CFF2 variation store's data {i} at {at} runs past the table.");

            // itemCount and the delta counts are not read: CFF2 has no delta sets.
            int count = r.U16((int)at + 4);
            if (!r.Contains(at + 6, 2L * count))
                throw new InvalidDataException($"The CFF2 variation store's data {i} has {count} region indices past the table.");

            parsed.Charge(2L * count, "the CFF2 variation store's region indices of data", i);
            parsed.Reserve(2L * count, "the CFF2 variation store's region indices of data", i);
            var indices = new ushort[count];
            for (var j = 0; j < count; j++)
            {
                indices[j] = r.U16((int)at + 6 + 2 * j);
                if (indices[j] >= regionCount)
                    throw new InvalidDataException(
                        $"The CFF2 variation store's data {i} names region {indices[j]}, but the store has {regionCount} regions.");
            }

            regionIndices[i] = indices;
        }

        return new ItemVariationStore(axisCount, regionCount, regionIndices);
    }
}
