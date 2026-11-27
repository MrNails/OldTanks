// Follows FreeType 2.13.2 src/truetype/ttpload.c (tt_face_load_hdmx, tt_face_get_device_metrics) (FreeType Project License;
// credit in docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The <c>hdmx</c> table as FreeType 2.13.2 reads it: optional, and silently ignored (never an error) when it is shorter than
/// 8 bytes, has no records or more than 255, or a record size other than <c>(numGlyphs + 2 + 3) &amp; ~3</c> (a size with
/// its upper 16 bits all set is masked first, as for the HANNOM fonts); records past the table are dropped, the version is not
/// checked, and a pixel size is found by binary search over the records sorted by size. One difference from FreeType
/// (mentor, 2026-11-27, by the duplicates rule of OT-4, following OTS where it rejects): a table with two records of one pixel
/// size is ignored, as OTS drops it ("DeviceRecord's are not sorted"), so hinted advances are computed as without an
/// <c>hdmx</c>; FreeType uses it and picks one of the two records through its unstable <c>qsort</c>. Records out of order but
/// distinct are accepted and sorted, as FreeType sorts them.
/// </summary>
internal sealed class HdmxTable
{
    public const uint Tag = 0x68646D78;  // hdmx

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int[] m_records;

    private HdmxTable(ReadOnlyMemory<byte> data, int[] records)
    {
        m_data = data;
        m_records = records;
    }

    /// <summary>The table; one without records when FreeType would not use it, or when two records have one pixel size.</summary>
    public static HdmxTable Read(ReadOnlyMemory<byte> data, int glyphCount)
    {
        var table = data.Span;
        var unused = new HdmxTable(data, []);
        if (table.Length < 8)
            return unused;

        var recordCount = table[2] << 8 | table[3];
        var recordSize = (uint)(table[4] << 24 | table[5] << 16 | table[6] << 8 | table[7]);
        if (recordSize >= 0xFFFF0000U)
            recordSize &= 0xFFFFU;

        if (recordCount is > 255 or 0)
            return unused;

        if (recordSize != ((uint)(glyphCount + 2 + 3) & ~3U))
            return unused;

        var records = new List<int>(recordCount);
        var offset = 8L;
        for (var i = 0; i < recordCount; i++)
        {
            if (offset + recordSize > table.Length)
                break;

            records.Add((int)offset);
            offset += recordSize;
        }

        // Sorted by pixel size (the first byte of a record) for the binary search.
        var sorted = records.OrderBy(record => data.Span[record]).ToArray();
        for (var i = 1; i < sorted.Length; i++)
        {
            if (table[sorted[i]] == table[sorted[i - 1]])
                return unused;
        }

        return new HdmxTable(data, sorted);
    }

    /// <summary>
    /// The record for <paramref name="ppem"/> as the offset of its widths (FreeType's <c>size->widthp</c>); -1 when there is none.
    /// </summary>
    public int FindWidths(int ppem)
    {
        var data = m_data.Span;
        int min = 0, max = m_records.Length;
        while (min < max)
        {
            var mid = (min + max) >> 1;
            var size = data[m_records[mid]];
            if (size > ppem)
                max = mid;
            else if (size < ppem)
                min = mid + 1;
            else
                return m_records[mid] + 2;
        }

        return -1;
    }

    /// <summary>The advance width in pixels of a glyph in the record <see cref="FindWidths"/> found.</summary>
    public int GetWidth(int widths, int glyphIndex) => m_data.Span[widths + glyphIndex];
}
