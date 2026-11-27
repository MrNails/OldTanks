using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>SVG </c> table's document index. Every document is checked at load; gzip-compressed ones (starting 1F 8B) are
/// inflated once and kept, so <see cref="GetDocument"/> returns plain SVG bytes.
/// </summary>
/// <remarks>
/// <para>
/// A gzip document is one gzip member: its header is read here (ID 1F 8B, method 8, no reserved flag bits, then the optional
/// extra field, name, comment and header CRC, which must match), the deflate data is inflated through
/// <see cref="DeflateStream"/>, and the last eight bytes are the trailer: the CRC-32 of the inflated bytes, then their length
/// (ISIZE). Both must match; a truncated or altered member fails one of them. Bytes between the end of the deflate stream and
/// the trailer are not detected (<see cref="DeflateStream"/> does not report where the stream ended).
/// </para>
/// <para>
/// All gzip documents of the file (every face's tables; each distinct position in the file once, shared by every table that
/// points at it) inflate within one budget of <see cref="InflateBudget"/> bytes, kept in <see cref="ParsedTables"/>. A table's
/// new documents are inflated twice: a first pass only counts the bytes and their CRC-32, checks the trailer and the budget,
/// which <see cref="NotSupportedException"/> enforces before anything is allocated; a second pass inflates into arrays of the
/// exact sizes.
/// </para>
/// </remarks>
internal sealed class SvgTable
{
    /// <summary>The most bytes all of a file's gzip documents may inflate to, together: <see cref="FontLoader.MaxFileSize"/>.</summary>
    public const long InflateBudget = FontLoader.MaxFileSize;

    private const int HeaderLength = 10;
    private const int RecordLength = 12;
    private const int TrailerLength = 8;
    private const int BufferLength = 81920;

    // gzip header flags.
    private const byte FlagHeaderCrc = 0x02;
    private const byte FlagExtra = 0x04;
    private const byte FlagName = 0x08;
    private const byte FlagComment = 0x10;
    private const byte FlagsReserved = 0xE0;

    // What the parse allocates per record (the record, its range and its entries in the size and document maps), accounted
    // against the file's parsed-memory budget.
    private const int RecordCost = 128;

    private readonly Range[] m_ranges;

    private SvgTable(Range[] ranges) => m_ranges = ranges;

    /// <param name="data">The table.</param>
    /// <param name="tableOffset">Where the table starts in the file: documents are shared by their position in the file.</param>
    /// <param name="parsed">The file's load state: its inflated documents, its inflate budget and its parsed-memory budget.</param>
    public static SvgTable Parse(ReadOnlyMemory<byte> data, long tableOffset, ParsedTables parsed)
    {
        // A version above 0 is accepted (Denys's safety-strict decision) and read with version 0's layout.
        var r = new BigEndianReader(data.Span, "SVG ");
        r.Check(0, HeaderLength);
        var listOffset = r.U32(2);
        var count = r.U16((int)Math.Min(listOffset, int.MaxValue));
        r.Check((long)listOffset + 2, (long)RecordLength * count);

        parsed.Reserve(48 + (long)RecordCost * count, "the 'SVG ' index");
        parsed.Charge((long)RecordLength * count, "the 'SVG ' index");
        var records = new (int Start, int End, long Offset, long Length)[count];
        long previousEnd = -1;

        for (var i = 0; i < count; i++)
        {
            var at = (int)listOffset + 2 + RecordLength * i;
            int start = r.U16(at);
            int end = r.U16(at + 2);
            if (start > end)
                throw new InvalidDataException($"The 'SVG ' table's record {i} starts at glyph {start} after its end {end}.");

            if (start <= previousEnd)
                throw new InvalidDataException(
                    $"The 'SVG ' table's record {i} (glyphs {start}..{end}) is not after the previous record's end {previousEnd}.");

            previousEnd = end;

            // In 64 bits: a 32-bit sum could wrap past the table's end back into it.
            long documentOffset = (long)listOffset + r.U32(at + 4);
            long documentLength = r.U32(at + 8);
            if (!r.Contains(documentOffset, documentLength))
                throw new InvalidDataException(
                    $"The 'SVG ' table's document for glyphs {start}..{end} ({documentLength} bytes at {documentOffset}) lies " +
                    $"outside its {r.Length} bytes.");

            records[i] = (start, end, documentOffset, documentLength);
        }

        CheckOverlaps(records, parsed);

        // First pass: check every gzip document the file has not met yet, against what is left of the file's budget.
        var sizes = new Dictionary<(long, long), int>();
        foreach (var (start, end, offset, length) in records)
        {
            var position = (tableOffset + offset, length);
            var document = data.Slice((int)offset, (int)length);
            if (!IsGzip(document.Span) || parsed.SvgDocuments.ContainsKey(position) || sizes.ContainsKey(position))
                continue;

            var size = Measure(document, parsed, start, end);
            sizes.Add(position, size);
            parsed.SvgBudget -= size;
        }

        // Second pass: inflate each into an array of the size the first pass found, sharing documents the file already has.
        var ranges = new Range[count];
        for (var i = 0; i < count; i++)
        {
            var (start, end, offset, length) = records[i];
            var position = (tableOffset + offset, length);
            if (!parsed.SvgDocuments.TryGetValue(position, out var document))
            {
                document = data.Slice((int)offset, (int)length);
                if (sizes.TryGetValue(position, out var size))
                    document = Inflate(document, size);

                parsed.SvgDocuments.Add(position, document);
            }

            ranges[i] = new Range(start, end, document);
        }

        return new SvgTable(ranges);
    }

    /// <summary>The glyph's SVG document without gzip; empty when the index has none for it.</summary>
    public ReadOnlyMemory<byte> GetDocument(int glyphIndex)
    {
        int low = 0, high = m_ranges.Length;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            var range = m_ranges[mid];
            if (glyphIndex < range.Start)
                high = mid;
            else if (glyphIndex > range.End)
                low = mid + 1;
            else
                return range.Document;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Documents (distinct offset or length) may share bytes only as the same document, as OTS refuses overlapping documents
    /// (Denys's "Rules + work budget", 2026-11-26): otherwise records over one gzip member with different lengths would each
    /// inflate it. Records with the same offset and length share one document; empty documents overlap nothing.
    /// </summary>
    private static void CheckOverlaps((int Start, int End, long Offset, long Length)[] records, ParsedTables parsed)
    {
        parsed.Reserve(64 + 48L * records.Length, "the 'SVG ' document overlap check");
        parsed.Charge(16L * records.Length, "the 'SVG ' document overlap check");
        var documents = records.Where(record => record.Length > 0).Select(record => (record.Offset, record.Length)).Distinct()
            .OrderBy(document => document.Offset).ToArray();
        long end = 0;
        foreach (var (offset, length) in documents)
        {
            if (offset < end)
                throw new InvalidDataException(
                    $"The 'SVG ' document of {length} bytes at {offset} overlaps another; documents may share bytes only as the " +
                    "same document (offset and length).");

            end = Math.Max(end, offset + length);
        }
    }

    private static bool IsGzip(ReadOnlySpan<byte> document) => document.Length >= 2 && document[0] == 0x1F && document[1] == 0x8B;

    /// <summary>
    /// Inflates without keeping the bytes: their count, checked against the trailer's CRC-32 and ISIZE and against the
    /// remaining budget.
    /// </summary>
    private static int Measure(ReadOnlyMemory<byte> document, ParsedTables parsed, int start, int end)
    {
        var name = $"The 'SVG ' document for glyphs {start}..{end}";
        var budget = parsed.SvgBudget;
        parsed.Charge(document.Length, "checking the 'SVG ' document for glyphs from", start);
        var deflate = DeflateData(document, name);
        var trailer = document.Span[^TrailerLength..];
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
        var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer[4..]);

        var buffer = new byte[BufferLength];
        var crc = Crc32.Initial;
        long total = 0;
        try
        {
            using var inflater = new DeflateStream(Open(deflate), CompressionMode.Decompress);
            int read;
            while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > budget)
                    throw new NotSupportedException(
                        $"{name} inflates past what is left of the file's {InflateBudget}-byte budget for SVG documents.");

                crc = Crc32.Update(crc, buffer.AsSpan(0, read));
            }
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"{name} is not valid deflate data.", exception);
        }

        if (Crc32.Finish(crc) != expectedCrc)
            throw new InvalidDataException($"{name} fails its gzip CRC-32 check: its trailer records 0x{expectedCrc:X8}.");

        if ((uint)total != expectedSize)
            throw new InvalidDataException(
                $"{name} inflates to {total} bytes, but its gzip ISIZE records {expectedSize}.");

        return (int)total;
    }

    private static byte[] Inflate(ReadOnlyMemory<byte> document, int size)
    {
        var output = new byte[size];
        using var inflater = new DeflateStream(Open(DeflateData(document, "")), CompressionMode.Decompress);
        inflater.ReadExactly(output);
        return output;
    }

    /// <summary>The gzip member's deflate data: after its header, before its 8-byte trailer.</summary>
    private static ReadOnlyMemory<byte> DeflateData(ReadOnlyMemory<byte> document, string name)
    {
        var span = document.Span;
        if (span.Length < HeaderLength + TrailerLength)
            throw new InvalidDataException($"{name} is too short to be gzip data.");

        // ID1 ID2 (checked by the caller), CM, FLG, MTIME (4), XFL, OS.
        if (span[2] != 8)
            throw new InvalidDataException($"{name} has gzip compression method {span[2]}, not 8 (deflate).");

        var flags = span[3];
        if ((flags & FlagsReserved) != 0)
            throw new InvalidDataException($"{name} sets reserved gzip header flags (0x{flags:X2}).");

        var limit = span.Length - TrailerLength;
        var offset = HeaderLength;
        if ((flags & FlagExtra) != 0)
        {
            if (offset + 2 > limit)
                throw new InvalidDataException($"{name}'s gzip header runs into its trailer.");

            offset += 2 + BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
        }

        if ((flags & FlagName) != 0)
            offset = SkipZeroTerminated(span, offset, limit, name);

        if ((flags & FlagComment) != 0)
            offset = SkipZeroTerminated(span, offset, limit, name);

        if ((flags & FlagHeaderCrc) != 0)
        {
            // FHCRC: the low 16 bits of the CRC-32 of the header bytes before it.
            if (offset + 2 > limit)
                throw new InvalidDataException($"{name}'s gzip header runs into its trailer.");

            var expected = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            var actual = (ushort)Crc32.Finish(Crc32.Update(Crc32.Initial, span[..offset]));
            if (actual != expected)
                throw new InvalidDataException(
                    $"{name} fails its gzip header CRC check: 0x{actual:X4}, recorded 0x{expected:X4}.");

            offset += 2;
        }

        if (offset > limit)
            throw new InvalidDataException($"{name}'s gzip header runs into its trailer.");

        return document[offset..limit];
    }

    private static int SkipZeroTerminated(ReadOnlySpan<byte> span, int offset, int limit, string name)
    {
        var end = offset < limit ? span[offset..limit].IndexOf((byte)0) : -1;
        if (end < 0)
            throw new InvalidDataException($"{name}'s gzip header runs into its trailer.");

        return offset + end + 1;
    }

    private static MemoryStream Open(ReadOnlyMemory<byte> data) =>
        MemoryMarshal.TryGetArray(data, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(data.ToArray(), writable: false);

    private readonly record struct Range(int Start, int End, ReadOnlyMemory<byte> Document);
}
