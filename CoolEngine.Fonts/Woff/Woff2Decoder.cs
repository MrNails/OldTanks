// Follows FreeType 2.13.2 src/sfnt/sfwoff2.c (woff2_open_font, reconstruct_font, reconstruct_glyf, reconstruct_hmtx)
// (FreeType Project License; credit in docs).

using System.Buffers.Binary;
using System.Text;

namespace CoolEngine.Fonts.Woff;

/// <summary>
/// Decodes WOFF2 (W3C Recommendation, https://www.w3.org/TR/WOFF2/) into the sfnt or collection image it wraps, which
/// <see cref="FontLoader"/> then parses like any other font: the header and table directory, the collection directory, the one
/// Brotli stream, the <c>glyf</c>/<c>loca</c> transform (version 0) and the <c>hmtx</c> transform (version 1).
/// </summary>
/// <remarks>
/// <para>
/// Layout. The reconstruction follows FreeType 2.13.2's <c>src/sfnt/sfwoff2.c</c> (<c>woff2_open_font</c>,
/// <c>reconstruct_font</c>, <c>reconstruct_glyf</c>, <c>reconstruct_hmtx</c>). The sfnt directory is sorted by tag. For a font
/// that is not a collection the table data is laid out as FreeType lays it out: in tag order, with a transformed <c>loca</c>
/// directly after its <c>glyf</c> (FreeType's <c>reconstruct_glyf</c> places it there); each table is padded with zeros to
/// four bytes, every table checksum is computed, and <c>head.checkSumAdjustment</c> is computed over the whole image. For the
/// four Source Sans 3 files this is DirectWrite's image byte for byte. A collection is written as a <c>ttcf</c> 1.0 header (a
/// 2.0 header's DSIG fields do not exist in the output), the faces' directories one after the other, then each table once, in
/// the order of the WOFF2 directory: tables are shared between faces, where FreeType builds one sfnt per face, so no layout of
/// FreeType's applies. Each face's <c>checkSumAdjustment</c> is the one its face alone would have (the first face's, for a
/// <c>head</c> shared by faces). A transformed <c>hmtx</c> shared by faces is rebuilt from the first face that lists it.
/// Decoders differ byte for byte in the rebuilt tables; what the library reads is the same.
/// </para>
/// <para>
/// Strict rules (safety-strict, Denys, 2026-11-26, and his "Reject, like the spec" for the W3C "MUST" cases that OTS and
/// DirectWrite reject): whatever affects what the library reads or the bounds it reads within throws
/// <see cref="InvalidDataException"/>, one test each in <c>Woff2DecoderTests</c>:
/// header: the data shorter than the 48-byte header; a signature other than <c>wOF2</c>; a declared length different from the
/// file's; no tables; a table directory with more entries than the file has bytes for;
/// directory: a <c>UIntBase128</c> with a leading 0x80, of more than five bytes or above 2<sup>32</sup> - 1; a table directory
/// or collection directory running past the end of the file; a transform version the format does not define for the table
/// (<c>glyf</c> and <c>loca</c>: 0 or 3; <c>hmtx</c>: 0 or 1; every other table: 0); a transformed <c>glyf</c> beside an
/// untransformed <c>loca</c> or the reverse; a transformed <c>loca</c> whose transform length is not 0; a tag twice in one font;
/// collection: a version other than 0x00010000 and 0x00020000, no fonts, a font with no tables, more fonts, or a font with more
/// tables, than the file has bytes for, a table index past the directory, a font with <c>glyf</c> but no <c>loca</c> or the
/// reverse, a collection whose <c>loca</c> does not directly follow its <c>glyf</c> in the directory (so a <c>glyf</c> shared
/// by fonts has one <c>loca</c>);
/// compressed data and blocks: the compressed data past the end of the file; a block (metadata, private) with a length but no
/// offset, an offset not at the four-byte aligned end of what precedes it, or past the end of the file; padding bytes that are
/// not zero; bytes after the last block beyond its padding, and any byte after the private block; Brotli data that does not
/// decode, ends early, or decodes to a length other than the sum of the table lengths (the transform length for a transformed
/// table, the original length for the others);
/// <c>glyf</c>: a transformed table shorter than its 36-byte header or with a stream past its end; an <c>indexFormat</c> other
/// than 0 or 1, or one that differs from <c>head.indexToLocFormat</c>; an <c>nContourStream</c> shorter than the glyph count; a
/// glyph with fewer than -1 contours; an empty glyph with an explicit bounding box; a composite glyph without one; a contour with
/// no points, a glyph with more than 65,535 points, or more points than <c>flagStream</c> has flags; a stream read past its end
/// (<c>nPointsStream</c>, <c>glyphStream</c>, <c>compositeStream</c>, <c>bboxStream</c>, <c>instructionStream</c>); a
/// coordinate delta or coordinate outside the 16-bit range; short-format offsets that do not fit (<c>glyf</c> over 131,070
/// bytes with <c>indexFormat</c> 0); a transformed <c>loca</c> whose original length is not (glyphs + 1) × the offset size;
/// <c>hmtx</c>: a transformed table without a <c>glyf</c> and <c>hhea</c> in its font, with an <c>hhea</c> shorter than 36
/// bytes, or empty; flags with reserved bits set or with both bit 0 and bit 1 clear; fewer than one or more than
/// <c>numGlyphs</c> metrics; the table shorter than its flags say; over a plain (untransformed) <c>glyf</c>: no <c>head</c> or
/// <c>maxp</c>, a <c>loca</c> shorter than the glyph count needs, a glyph that starts past the end of <c>glyf</c>.
/// </para>
/// <para>
/// Library limits (<see cref="NotSupportedException"/>, raised before the memory is allocated): the table lengths in the
/// directory add up past <see cref="FontLoader.MaxFileSize"/>; the tables the decoder rebuilds (<c>loca</c> with the glyph
/// boxes, <c>glyf</c>, every <c>hmtx</c>) together would be longer than it; or the image would be.
/// </para>
/// <para>
/// Accepted, because they change nothing the library reads: a non-zero header <c>reserved</c>, a <c>totalSfntSize</c> that is
/// wrong (the W3C text forbids rejecting it; DirectWrite refuses it), the version numbers, a table whose <c>origLength</c>
/// differs from its rebuilt length (<c>glyf</c>, <c>loca</c>, <c>hmtx</c>), a <c>glyf</c> header <c>reserved</c> field or option
/// bits other than bit 0, the order of the directory (including a <c>loca</c> before its <c>glyf</c> in a font that is not a
/// collection), unused bytes at the end of a <c>glyf</c> stream, an <c>hmtx</c> stream longer than its flags say, the content of
/// the metadata and private blocks (they are bounds-checked and skipped), bytes after the Brotli stream within its compressed
/// size, a <c>head</c> shorter than 52 bytes beside a transformed <c>glyf</c> (the index format is not compared; the sfnt reader
/// refuses such a <c>head</c>). <c>Woff2DecoderTests</c> asserts each, and what DirectWrite does with it.
/// </para>
/// <para>
/// Memory and work. The declared lengths decide only what the output may reach, never an allocation: the Brotli buffer starts
/// small, doubles as the decoder fills it (at most 1.5 times the stream while it is copied) and never passes the declared sum.
/// The rebuilt tables are charged to one running total before each is allocated (the limit is
/// <see cref="FontLoader.MaxFileSize"/>; the <c>glyf</c> writer gets what is left, not a fresh limit), each in proportion to
/// the bytes it is built from or to its own size (a transformed <c>hmtx</c> costs 2 × (metrics + glyphs) bytes of the total, however few bytes it takes in the
/// file), and the glyph boxes of a plain <c>glyf</c> are read once however many <c>hmtx</c> tables use it. A table's checksum is
/// computed once, when it is written. So the work is linear in the image plus the directory. The image is checked against the
/// limit before it is allocated. Peak memory is the Brotli output (the stream, kept until the image is written), plus the rebuilt
/// tables (the <c>glyf</c> writer's buffer is trimmed to its length when more than a quarter is spare), plus the image: at most
/// twice the decoded size plus the transformed tables' size and a quarter of the rebuilt <c>glyf</c>, and a fixed 700 KiB of
/// scratch for all the <c>glyf</c> tables together. Stateless: safe on any thread.
/// </para>
/// </remarks>
internal static class Woff2Decoder
{
    private const uint Signature = 0x774F4632;     // wOF2
    private const uint CollectionTag = 0x74746366; // ttcf
    private const int HeaderLength = 48;
    private const int OffsetTableLength = 12;
    private const int RecordLength = 16;
    private const int GlyfHeaderLength = 36;

    private const uint GlyfTag = 0x676C7966; // glyf
    private const uint LocaTag = 0x6C6F6361; // loca
    private const uint HmtxTag = 0x686D7478; // hmtx
    private const uint HheaTag = 0x68686561; // hhea
    private const uint HeadTag = 0x68656164; // head
    private const uint MaxpTag = 0x6D617870; // maxp

    // The known-tag table of the format: the directory's flags byte indexes it (63 means a four-byte tag follows).
    private static readonly string[] s_knownTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG",
        "EBDT", "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea", "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC",
        "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar", "bdat", "bloc", "bsln", "cvar", "fdsc",
        "feat", "fmtx", "fvar", "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop", "trak", "Zapf", "Silf", "Glat",
        "Gloc", "Feat", "Sill",
    ];

    private sealed class Entry
    {
        public uint Tag;
        public bool Transformed;
        public long OrigLength;
        public long TransformLength;   // the length in the decompressed stream
        public long SourceOffset;      // its offset in the decompressed stream
        public ReadOnlyMemory<byte> Data; // what goes into the sfnt (the stream's bytes, or the rebuilt table)
        public bool Rebuilt;
    }

    private sealed record Font(uint Flavor, int[] Tables);

    /// <param name="data">The whole WOFF2 file, signature included.</param>
    /// <returns>A complete sfnt or collection image, which <see cref="FontLoader"/> then parses.</returns>
    /// <exception cref="InvalidDataException">The data breaks a strict rule (listed on this class).</exception>
    /// <exception cref="NotSupportedException">The decoded font would pass <see cref="FontLoader.MaxFileSize"/>.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> data) => Decode(data, FontLoader.MaxFileSize, null);

    /// <summary>What the decoder did, for tests that assert its work.</summary>
    internal sealed class Statistics
    {
        /// <summary>Bytes summed for table checksums.</summary>
        public long ChecksummedBytes { get; set; }

        /// <summary>Glyph boxes read from a plain <c>glyf</c> for a transformed <c>hmtx</c>.</summary>
        public long GlyphBoxReads { get; set; }
    }

    /// <summary><see cref="Decode(ReadOnlySpan{byte})"/> with the output limit as a parameter (tests pass a small one).</summary>
    internal static byte[] Decode(ReadOnlySpan<byte> data, long maxOutput, Statistics? statistics)
    {
        if (data.Length < HeaderLength)
            throw new InvalidDataException($"The WOFF2 data is {data.Length} bytes long, shorter than its {HeaderLength}-byte header.");

        if (BinaryPrimitives.ReadUInt32BigEndian(data) != Signature)
            throw new InvalidDataException("The WOFF2 signature is not 'wOF2'.");

        var flavor = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var length = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(data[12..]);
        var compressedSize = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        var metaOffset = BinaryPrimitives.ReadUInt32BigEndian(data[28..]);
        var metaLength = BinaryPrimitives.ReadUInt32BigEndian(data[32..]);
        var metaOrigLength = BinaryPrimitives.ReadUInt32BigEndian(data[36..]);
        var privOffset = BinaryPrimitives.ReadUInt32BigEndian(data[40..]);
        var privLength = BinaryPrimitives.ReadUInt32BigEndian(data[44..]);

        if (length != data.Length)
            throw new InvalidDataException($"The WOFF2 header gives the length {length}, but the file is {data.Length} bytes long.");

        if (tableCount == 0)
            throw new InvalidDataException("The WOFF2 font has no tables.");

        // Every directory entry takes at least two bytes: no table list is allocated for more entries than the file can hold.
        if ((long)tableCount * 2 > data.Length - HeaderLength)
            throw new InvalidDataException($"The WOFF2 table directory ({tableCount} entries) runs past the end of the file.");

        var collection = flavor == CollectionTag;
        var reader = new Reader(data, "table directory") { Position = HeaderLength };
        var entries = ReadDirectory(ref reader, tableCount, out var streamLength);
        var fonts = collection ? ReadCollection(ref reader, entries) : [new Font(flavor, Enumerable.Range(0, entries.Length).ToArray())];
        CheckTables(entries, fonts, collection);

        var compressedStart = (long)reader.Position;
        var compressedEnd = compressedStart + compressedSize;
        CheckBlocks(data, compressedEnd, metaOffset, metaLength, metaOrigLength, privOffset, privLength);

        if (streamLength > maxOutput)
            throw new NotSupportedException($"The WOFF2 font's tables add up to {streamLength} bytes, more than the {maxOutput}-byte limit.");

        var stream = BrotliInflate(data.Slice((int)compressedStart, (int)compressedSize), streamLength);
        foreach (var entry in entries)
        {
            entry.Data = stream.AsMemory((int)entry.SourceOffset, (int)entry.TransformLength);
            if (!entry.Transformed)
                entry.Rebuilt = true;
        }

        Reconstruct(entries, fonts, new Budget(maxOutput), statistics);

        return Assemble(entries, fonts, collection, maxOutput, statistics);
    }

    // ---- Directory ----

    private static Entry[] ReadDirectory(ref Reader reader, int tableCount, out long streamLength)
    {
        var entries = new Entry[tableCount];
        long sourceOffset = 0;
        for (var i = 0; i < tableCount; i++)
        {
            var flags = reader.U8();
            var tagIndex = flags & 0x3F;
            var version = flags >> 6;
            var tag = tagIndex == 63 ? reader.U32() : TagOf(s_knownTags[tagIndex]);

            var transformed = tag switch
            {
                GlyfTag or LocaTag => version switch
                {
                    0 => true,
                    3 => false,
                    _ => throw UnknownTransform(tag, version),
                },
                HmtxTag => version switch
                {
                    0 => false,
                    1 => true,
                    _ => throw UnknownTransform(tag, version),
                },
                _ => version == 0 ? false : throw UnknownTransform(tag, version),
            };

            var origLength = reader.Base128();
            var transformLength = transformed ? reader.Base128() : origLength;
            if (tag == LocaTag && transformed && transformLength != 0)
                throw new InvalidDataException($"The WOFF2 'loca' table is transformed but its transform length is {transformLength}, not 0.");

            entries[i] = new Entry { Tag = tag, Transformed = transformed, OrigLength = origLength, TransformLength = transformLength, SourceOffset = sourceOffset };
            sourceOffset += transformLength;
        }

        streamLength = sourceOffset;
        return entries;
    }

    private static InvalidDataException UnknownTransform(uint tag, int version) =>
        new($"The WOFF2 table '{TagText(tag)}' has the transform version {version}, which the format does not define for it.");

    private static Font[] ReadCollection(ref Reader reader, Entry[] entries)
    {
        reader.Name = "collection directory";
        var version = reader.U32();
        if (version != 0x00010000 && version != 0x00020000)
            throw new InvalidDataException($"The WOFF2 collection version is 0x{version:X8}, not 0x00010000 or 0x00020000.");

        var fontCount = reader.U255();
        if (fontCount == 0)
            throw new InvalidDataException("The WOFF2 collection has no fonts.");

        // A font takes at least six bytes of directory: its table count, its flavor and one table index.
        if ((long)fontCount * 6 > reader.Remaining)
            throw new InvalidDataException($"The WOFF2 collection directory ({fontCount} fonts) runs past the end of the file.");

        var fonts = new Font[fontCount];
        for (var i = 0; i < fontCount; i++)
        {
            var count = reader.U255();
            var flavor = reader.U32();
            if (count == 0)
                throw new InvalidDataException($"Font {i} of the WOFF2 collection has no tables.");

            if (count > reader.Remaining)
                throw new InvalidDataException($"The table list of font {i} of the WOFF2 collection runs past the end of the file.");

            var tables = new int[count];
            for (var t = 0; t < count; t++)
            {
                var index = reader.U255();
                if (index >= entries.Length)
                    throw new InvalidDataException($"Font {i} of the WOFF2 collection lists table {index}, but the directory has {entries.Length}.");

                tables[t] = index;
            }

            fonts[i] = new Font(flavor, tables);
        }

        return fonts;
    }

    /// <summary>Tags unique per font, <c>glyf</c> and <c>loca</c> together and (in a collection) adjacent.</summary>
    private static void CheckTables(Entry[] entries, Font[] fonts, bool collection)
    {
        for (var f = 0; f < fonts.Length; f++)
        {
            var tags = new HashSet<uint>();
            int glyf = -1, loca = -1;
            foreach (var index in fonts[f].Tables)
            {
                var entry = entries[index];
                if (!tags.Add(entry.Tag))
                    throw new InvalidDataException($"Font {f} of the WOFF2 file lists the table '{TagText(entry.Tag)}' twice.");

                if (entry.Tag == GlyfTag)
                    glyf = index;
                else if (entry.Tag == LocaTag)
                    loca = index;
            }

            if ((glyf < 0) != (loca < 0))
                throw new InvalidDataException($"Font {f} of the WOFF2 file has '{(glyf < 0 ? "loca" : "glyf")}' without '{(glyf < 0 ? "glyf" : "loca")}'.");

            if (glyf < 0)
                continue;

            if (entries[glyf].Transformed != entries[loca].Transformed)
                throw new InvalidDataException($"Font {f} of the WOFF2 file transforms only one of 'glyf' and 'loca'.");

            if (collection && loca != glyf + 1)
                throw new InvalidDataException($"In font {f} of the WOFF2 collection, 'loca' (table {loca}) does not directly follow 'glyf' (table {glyf}).");
        }
    }

    private static void CheckBlocks(ReadOnlySpan<byte> data, long compressedEnd, uint metaOffset, uint metaLength,
        uint metaOrigLength, uint privOffset, uint privLength)
    {
        var fileLength = data.Length;
        if (compressedEnd > fileLength)
            throw new InvalidDataException("The WOFF2 compressed font data runs past the end of the file.");

        var end = compressedEnd;
        if (metaOffset == 0)
        {
            if (metaLength != 0 || metaOrigLength != 0)
                throw new InvalidDataException("The WOFF2 metadata block has a length but no offset.");
        }
        else
        {
            if (metaLength != 0 && metaOrigLength == 0)
                throw new InvalidDataException("The WOFF2 metadata block is compressed but has no original length.");

            if (metaOffset != Pad4(end))
                throw new InvalidDataException($"The WOFF2 metadata block starts at {metaOffset}, not at {Pad4(end)}, the four-byte aligned end of the data before it.");

            if (metaOffset + (long)metaLength > fileLength)
                throw new InvalidDataException("The WOFF2 metadata block runs past the end of the file.");

            RequireZero(data, end, metaOffset, "before the WOFF2 metadata block");
            end = metaOffset + (long)metaLength;
        }

        if (privOffset == 0)
        {
            if (privLength != 0)
                throw new InvalidDataException("The WOFF2 private data block has a length but no offset.");
        }
        else
        {
            if (privOffset != Pad4(end))
                throw new InvalidDataException($"The WOFF2 private data block starts at {privOffset}, not at {Pad4(end)}, the four-byte aligned end of the data before it.");

            if (privOffset + (long)privLength > fileLength)
                throw new InvalidDataException("The WOFF2 private data block runs past the end of the file.");

            RequireZero(data, end, privOffset, "before the WOFF2 private data block");
            end = privOffset + (long)privLength;
            if (end != fileLength)
                throw new InvalidDataException($"The WOFF2 file has {fileLength - end} bytes after its private data block.");
        }

        if (Pad4(end) != Pad4(fileLength))
            throw new InvalidDataException($"The WOFF2 file is {fileLength} bytes long, but its blocks end at {end}.");

        RequireZero(data, end, fileLength, "at the end of the WOFF2 file");
    }

    /// <summary>The bytes from <paramref name="from"/> to <paramref name="to"/> are padding and must be zero.</summary>
    private static void RequireZero(ReadOnlySpan<byte> data, long from, long to, string where)
    {
        for (var i = from; i < to; i++)
        {
            if (data[(int)i] != 0)
                throw new InvalidDataException($"The padding byte at {i} {where} is not zero.");
        }
    }

    // ---- Brotli ----

    /// <summary>
    /// Decodes the one Brotli stream into exactly <paramref name="expected"/> bytes. The buffer grows with the output (never
    /// past <paramref name="expected"/>, which the caller has checked against the library's limit), so a stream that claims or
    /// produces far more costs only what it has produced.
    /// </summary>
    internal static byte[] BrotliInflate(ReadOnlySpan<byte> source, long expected)
    {
        var buffer = new byte[Math.Min(expected, Math.Max(4096L, source.Length * 4L))];
        var written = 0;
        var consumed = 0;
        using var decoder = new System.IO.Compression.BrotliDecoder();
        var probe = new byte[1];
        while (true)
        {
            var status = decoder.Decompress(source[consumed..], buffer.AsSpan(written), out var read, out var wrote);
            consumed += read;
            written += wrote;
            switch (status)
            {
                case System.Buffers.OperationStatus.Done:
                    if (written != expected)
                        throw new InvalidDataException($"The WOFF2 Brotli data decodes to {written} bytes, but the table lengths add up to {expected}.");

                    return buffer;

                case System.Buffers.OperationStatus.DestinationTooSmall:
                    if (written < expected)
                    {
                        Array.Resize(ref buffer, (int)Math.Min(expected, buffer.LongLength * 2));
                        break;
                    }

                    // The buffer is full: the stream may be complete (its end marker not read yet) or have more to say.
                    var last = decoder.Decompress(source[consumed..], probe, out _, out var extra);
                    if (last != System.Buffers.OperationStatus.Done || extra != 0)
                        throw new InvalidDataException($"The WOFF2 Brotli data decodes to more than the {expected} bytes the table lengths add up to.");

                    return buffer;

                case System.Buffers.OperationStatus.NeedMoreData:
                    throw new InvalidDataException($"The WOFF2 Brotli data ends after {written} of {expected} bytes.");

                default:
                    throw new InvalidDataException("The WOFF2 Brotli data is damaged.");
            }
        }
    }

    // ---- Rebuilding the tables ----

    /// <summary>
    /// One running total of what the decoder rebuilds: each table is charged before it is allocated, and a charge past the
    /// limit is <see cref="NotSupportedException"/>.
    /// </summary>
    private sealed class Budget
    {
        private readonly long m_limit;

        public Budget(long limit)
        {
            m_limit = limit;
            Remaining = limit;
        }

        public long Remaining { get; private set; }

        public void Take(long bytes, string what)
        {
            if (bytes > Remaining)
                throw new NotSupportedException($"The rebuilt WOFF2 tables ({what}) would be more than the {m_limit}-byte limit.");

            Remaining -= bytes;
        }
    }

    /// <summary>The working arrays of the <c>glyf</c> transform, allocated once for all of a file's <c>glyf</c> tables.</summary>
    private sealed class GlyfScratch
    {
        public ushort[] EndPoints { get; } = new ushort[short.MaxValue + 1];

        public int[] Dx { get; } = new int[ushort.MaxValue + 1];

        public int[] Dy { get; } = new int[ushort.MaxValue + 1];

        public bool[] OnCurve { get; } = new bool[ushort.MaxValue + 1];

        public byte[] Flags { get; } = new byte[ushort.MaxValue + 1];
    }

    private static void Reconstruct(Entry[] entries, Font[] fonts, Budget budget, Statistics? statistics)
    {
        // glyf and loca first (the hmtx transform needs the glyph boxes), once per distinct glyf.
        var glyphs = new Dictionary<int, GlyfResult>();
        GlyfScratch? scratch = null;
        foreach (var font in fonts)
        {
            var glyf = FindTable(entries, font, GlyfTag);
            if (glyf < 0 || !entries[glyf].Transformed || glyphs.ContainsKey(glyf))
                continue;

            var loca = FindTable(entries, font, LocaTag);
            var indexToLocFormat = HeadIndexToLocFormat(entries, font);
            scratch ??= new GlyfScratch();
            var result = ReconstructGlyf(entries[glyf].Data, indexToLocFormat, budget, scratch);
            if (entries[loca].OrigLength != result.Loca.Length)
                throw new InvalidDataException($"The WOFF2 'loca' table's original length is {entries[loca].OrigLength}, but {result.GlyphCount} glyphs need {result.Loca.Length}.");

            entries[glyf].Data = result.Glyf;
            entries[glyf].Rebuilt = true;
            entries[loca].Data = result.Loca;
            entries[loca].Rebuilt = true;
            glyphs[glyf] = result;
        }

        // The glyph boxes of plain glyf tables, read once each however many hmtx tables use them.
        var plainBoxes = new Dictionary<int, (int GlyphCount, short[] XMin)>();
        foreach (var font in fonts)
        {
            var hmtx = FindTable(entries, font, HmtxTag);
            if (hmtx < 0 || !entries[hmtx].Transformed || entries[hmtx].Rebuilt)
                continue;

            entries[hmtx].Data = ReconstructHmtx(entries, font, hmtx, glyphs, plainBoxes, budget, statistics);
            entries[hmtx].Rebuilt = true;
        }
    }

    private static int FindTable(Entry[] entries, Font font, uint tag)
    {
        foreach (var index in font.Tables)
        {
            if (entries[index].Tag == tag)
                return index;
        }

        return -1;
    }

    /// <summary><c>head.indexToLocFormat</c> of the font, or -1 when there is no <c>head</c> long enough to hold it.</summary>
    private static int HeadIndexToLocFormat(Entry[] entries, Font font)
    {
        var head = FindTable(entries, font, HeadTag);
        if (head < 0 || entries[head].Data.Length < 52)
            return -1;

        return BinaryPrimitives.ReadInt16BigEndian(entries[head].Data.Span[50..]);
    }

    private sealed record GlyfResult(ReadOnlyMemory<byte> Glyf, byte[] Loca, int GlyphCount, short[] XMin);

    private static GlyfResult ReconstructGlyf(ReadOnlyMemory<byte> sourceMemory, int headIndexToLocFormat, Budget budget, GlyfScratch scratch)
    {
        var source = sourceMemory.Span;
        if (source.Length < GlyfHeaderLength)
            throw new InvalidDataException($"The transformed WOFF2 'glyf' table is {source.Length} bytes long, shorter than its {GlyfHeaderLength}-byte header.");

        var optionFlags = BinaryPrimitives.ReadUInt16BigEndian(source[2..]);
        var glyphCount = BinaryPrimitives.ReadUInt16BigEndian(source[4..]);
        var indexFormat = BinaryPrimitives.ReadUInt16BigEndian(source[6..]);
        if (indexFormat > 1)
            throw new InvalidDataException($"The transformed WOFF2 'glyf' table has the index format {indexFormat}, not 0 or 1.");

        if (headIndexToLocFormat >= 0 && headIndexToLocFormat != indexFormat)
            throw new InvalidDataException($"The transformed WOFF2 'glyf' table has the index format {indexFormat}, but 'head' says {headIndexToLocFormat}.");

        // The seven streams follow the header in order; the optional overlap bitmap follows them.
        var streams = new ReadOnlyMemory<byte>[7];
        long at = GlyfHeaderLength;
        var names = new[] { "nContourStream", "nPointsStream", "flagStream", "glyphStream", "compositeStream", "bboxStream", "instructionStream" };
        for (var i = 0; i < 7; i++)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(source[(8 + 4 * i)..]);
            if (size > source.Length - at)
                throw new InvalidDataException($"The WOFF2 'glyf' {names[i]} ({size} bytes) runs past the end of the table.");

            at += size;
        }

        at = GlyfHeaderLength;
        for (var i = 0; i < 7; i++)
        {
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(source[(8 + 4 * i)..]);
            streams[i] = sourceMemory.Slice((int)at, size);
            at += size;
        }

        ReadOnlySpan<byte> overlap = default;
        if ((optionFlags & 1) != 0)
        {
            var overlapLength = (glyphCount + 7) / 8;
            if (overlapLength > source.Length - at)
                throw new InvalidDataException("The WOFF2 'glyf' overlap bitmap runs past the end of the table.");

            overlap = source.Slice((int)at, overlapLength);
        }

        var contourStream = new Reader(streams[0].Span, "'glyf' nContourStream");
        var pointsStream = new Reader(streams[1].Span, "'glyf' nPointsStream");
        var flagStream = streams[2].Span;
        var flagPosition = 0;
        var glyphStream = new Reader(streams[3].Span, "'glyf' glyphStream");
        var compositeStream = streams[4].Span;
        var bboxLength = streams[5].Length;
        var bitmapLength = 4 * ((glyphCount + 31) / 32);
        if (bboxLength < bitmapLength)
            throw new InvalidDataException($"The WOFF2 'glyf' bboxStream ({bboxLength} bytes) is shorter than its {bitmapLength}-byte bitmap.");

        var bboxBitmap = streams[5].Span[..bitmapLength];
        var bboxStream = new Reader(streams[5].Span[bitmapLength..], "'glyf' bboxStream");
        var instructions = new Reader(streams[6].Span, "'glyf' instructionStream");

        if (contourStream.Remaining < 2L * glyphCount)
            throw new InvalidDataException($"The WOFF2 'glyf' nContourStream has {contourStream.Remaining} bytes, fewer than the {2L * glyphCount} its {glyphCount} glyphs need.");

        // What the table needs besides the glyphs: loca, and the glyph boxes kept for an hmtx transform. Charged before they exist.
        var locaLength = (glyphCount + 1L) * (indexFormat == 0 ? 2 : 4);
        budget.Take(locaLength + 2L * glyphCount, "loca and glyph boxes");
        var loca = new byte[locaLength];
        var xMin = new short[glyphCount];
        var sink = new ByteSink(budget.Remaining);
        var endPoints = scratch.EndPoints;
        var dx = scratch.Dx;
        var dy = scratch.Dy;
        var onCurve = scratch.OnCurve;

        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            var start = sink.Length;
            WriteLocaOffset(loca, indexFormat, glyph, start);
            var hasBox = (bboxBitmap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
            var contours = contourStream.S16();

            if (contours == 0)
            {
                if (hasBox)
                    throw new InvalidDataException($"Glyph {glyph} of the WOFF2 'glyf' table is empty but has an explicit bounding box.");

                continue;
            }

            if (contours == -1)
            {
                if (!hasBox)
                    throw new InvalidDataException($"Composite glyph {glyph} of the WOFF2 'glyf' table has no explicit bounding box.");

                var box = bboxStream.Bytes(8);
                var (components, hasInstructions) = ScanComposite(compositeStream, glyph);
                compositeStream = compositeStream[components.Length..];
                sink.U16(0xFFFF);
                sink.Bytes(box);
                sink.Bytes(components);
                if (hasInstructions)
                {
                    var instructionLength = glyphStream.U255();
                    sink.U16((ushort)instructionLength);
                    sink.Bytes(instructions.Bytes(instructionLength));
                }

                xMin[glyph] = BinaryPrimitives.ReadInt16BigEndian(box);
                sink.Pad4(start);
                continue;
            }

            if (contours < 0)
                throw new InvalidDataException($"Glyph {glyph} of the WOFF2 'glyf' table has {contours} contours; only -1 marks a composite.");

            // A simple glyph: the points per contour, then one flag byte and 1 to 4 coordinate bytes per point.
            var total = 0;
            for (var c = 0; c < contours; c++)
            {
                var points = pointsStream.U255();
                if (points == 0)
                    throw new InvalidDataException($"Contour {c} of glyph {glyph} of the WOFF2 'glyf' table has no points.");

                total += points;
                if (total > ushort.MaxValue)
                    throw new InvalidDataException($"Glyph {glyph} of the WOFF2 'glyf' table has more than {ushort.MaxValue} points.");

                endPoints[c] = (ushort)(total - 1);
            }

            if (total > flagStream.Length - flagPosition)
                throw new InvalidDataException($"Glyph {glyph} of the WOFF2 'glyf' table has {total} points, but the flagStream has {flagStream.Length - flagPosition} flags left.");

            int minX = 0, minY = 0, maxX = 0, maxY = 0, x = 0, y = 0;
            for (var p = 0; p < total; p++)
            {
                var flag = flagStream[flagPosition++];
                onCurve[p] = (flag & 0x80) == 0;
                glyphStream.Triplet(flag & 0x7F, out dx[p], out dy[p]);
                x += dx[p];
                y += dy[p];
                if (dx[p] is < short.MinValue or > short.MaxValue || dy[p] is < short.MinValue or > short.MaxValue
                    || x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue)
                    throw new InvalidDataException($"Point {p} of glyph {glyph} of the WOFF2 'glyf' table is outside the 16-bit range.");

                if (p == 0)
                {
                    minX = maxX = x;
                    minY = maxY = y;
                }
                else
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            var instructionCount = glyphStream.U255();
            var code = instructions.Bytes(instructionCount);

            sink.U16((ushort)contours);
            if (hasBox)
            {
                var box = bboxStream.Bytes(8);
                sink.Bytes(box);
                xMin[glyph] = BinaryPrimitives.ReadInt16BigEndian(box);
            }
            else
            {
                sink.U16((ushort)(short)minX);
                sink.U16((ushort)(short)minY);
                sink.U16((ushort)(short)maxX);
                sink.U16((ushort)(short)maxY);
                xMin[glyph] = (short)minX;
            }

            for (var c = 0; c < contours; c++)
                sink.U16(endPoints[c]);

            sink.U16((ushort)instructionCount);
            sink.Bytes(code);
            var overlapBit = overlap.Length > 0 && (overlap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
            WritePoints(sink, total, scratch, overlapBit);
            sink.Pad4(start);
        }

        WriteLocaOffset(loca, indexFormat, glyphCount, sink.Length);
        budget.Take(sink.Length, "glyf");
        return new GlyfResult(sink.Memory, loca, glyphCount, xMin);
    }

    private static void WriteLocaOffset(byte[] loca, int indexFormat, int index, long offset)
    {
        if (indexFormat == 0)
        {
            if (offset > 2L * ushort.MaxValue)
                throw new InvalidDataException($"The WOFF2 'glyf' table is {offset} bytes long at glyph {index}, too long for the short 'loca' format.");

            BinaryPrimitives.WriteUInt16BigEndian(loca.AsSpan(2 * index), (ushort)(offset / 2));
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4 * index), (uint)offset);
        }
    }

    private const int ComponentArgsAreWords = 0x0001;
    private const int ComponentHaveScale = 0x0008;
    private const int ComponentMoreComponents = 0x0020;
    private const int ComponentHaveXyScale = 0x0040;
    private const int ComponentHaveTwoByTwo = 0x0080;
    private const int ComponentHaveInstructions = 0x0100;

    /// <summary>Takes the component records of one composite glyph off the composite stream.</summary>
    private static (byte[] Components, bool HasInstructions) ScanComposite(ReadOnlySpan<byte> stream, int glyph)
    {
        var at = 0;
        var hasInstructions = false;
        int flags;
        do
        {
            if (at + 2 > stream.Length)
                throw new InvalidDataException($"The WOFF2 'glyf' compositeStream ends inside the components of glyph {glyph}.");

            flags = BinaryPrimitives.ReadUInt16BigEndian(stream[at..]);
            hasInstructions |= (flags & ComponentHaveInstructions) != 0;
            var size = 2 + 2 + ((flags & ComponentArgsAreWords) != 0 ? 4 : 2);
            if ((flags & ComponentHaveScale) != 0)
                size += 2;
            else if ((flags & ComponentHaveXyScale) != 0)
                size += 4;
            else if ((flags & ComponentHaveTwoByTwo) != 0)
                size += 8;

            if (at + size > stream.Length)
                throw new InvalidDataException($"The WOFF2 'glyf' compositeStream ends inside a component of glyph {glyph}.");

            at += size;
        }
        while ((flags & ComponentMoreComponents) != 0);

        return (stream[..at].ToArray(), hasInstructions);
    }

    /// <summary>Writes a simple glyph's flags (run-length compressed) and coordinates as <c>glyf</c> stores them.</summary>
    private static void WritePoints(ByteSink sink, int count, GlyfScratch scratch, bool overlap)
    {
        var onCurve = scratch.OnCurve;
        var dx = scratch.Dx;
        var dy = scratch.Dy;
        var flags = scratch.Flags;
        for (var p = 0; p < count; p++)
        {
            var flag = onCurve[p] ? 0x01 : 0;
            flag |= CoordinateFlags(dx[p], 0x02, 0x10);
            flag |= CoordinateFlags(dy[p], 0x04, 0x20);
            flags[p] = (byte)flag;
        }

        if (overlap)
            flags[0] |= 0x40;

        for (var p = 0; p < count;)
        {
            var run = 1;
            while (p + run < count && flags[p + run] == flags[p] && run < 256)
                run++;

            if (run > 1)
            {
                sink.U8((byte)(flags[p] | 0x08));
                sink.U8((byte)(run - 1));
            }
            else
            {
                sink.U8(flags[p]);
            }

            p += run;
        }

        for (var p = 0; p < count; p++)
            WriteCoordinate(sink, dx[p]);

        for (var p = 0; p < count; p++)
            WriteCoordinate(sink, dy[p]);

        static int CoordinateFlags(int delta, int shortFlag, int sameFlag) =>
            delta == 0 ? sameFlag : Math.Abs(delta) < 256 ? shortFlag | (delta > 0 ? sameFlag : 0) : 0;

        static void WriteCoordinate(ByteSink sink, int delta)
        {
            if (delta == 0)
                return;

            if (Math.Abs(delta) < 256)
                sink.U8((byte)Math.Abs(delta));
            else
                sink.U16((ushort)(short)delta);
        }
    }

    private static byte[] ReconstructHmtx(Entry[] entries, Font font, int hmtx, Dictionary<int, GlyfResult> glyphs,
        Dictionary<int, (int GlyphCount, short[] XMin)> plainBoxes, Budget budget, Statistics? statistics)
    {
        var source = entries[hmtx].Data.Span;
        var glyf = FindTable(entries, font, GlyfTag);
        var hhea = FindTable(entries, font, HheaTag);
        if (glyf < 0 || hhea < 0)
            throw new InvalidDataException($"The transformed WOFF2 'hmtx' table needs 'glyf' and 'hhea' in its font, which has no '{(glyf < 0 ? "glyf" : "hhea")}'.");

        int glyphCount;
        short[] xMin;
        if (glyphs.TryGetValue(glyf, out var result))
        {
            glyphCount = result.GlyphCount;
            xMin = result.XMin;
        }
        else
        {
            if (!plainBoxes.TryGetValue(glyf, out var boxes))
            {
                boxes = XMinOfPlainGlyf(entries, font, glyf, budget, statistics);
                plainBoxes[glyf] = boxes;
            }

            (glyphCount, xMin) = boxes;
        }

        if (entries[hhea].Data.Length < 36)
            throw new InvalidDataException("The WOFF2 'hhea' table is too short to hold numberOfHMetrics, which the transformed 'hmtx' table needs.");

        var metricCount = BinaryPrimitives.ReadUInt16BigEndian(entries[hhea].Data.Span[34..]);
        if (source.Length < 1)
            throw new InvalidDataException("The transformed WOFF2 'hmtx' table is empty.");

        var flags = source[0];
        if ((flags & 0xFC) != 0)
            throw new InvalidDataException($"The transformed WOFF2 'hmtx' table has reserved flag bits set (0x{flags:X2}).");

        var hasLsb = (flags & 0x01) == 0;
        var hasLeftSideBearing = (flags & 0x02) == 0;
        if (hasLsb && hasLeftSideBearing)
            throw new InvalidDataException("The transformed WOFF2 'hmtx' table has flags 0, but that is the untransformed form.");

        if (metricCount < 1 || metricCount > glyphCount)
            throw new InvalidDataException($"The WOFF2 'hhea' table has {metricCount} metrics, but the font has {glyphCount} glyphs.");

        var needed = 1L + 2L * metricCount + (hasLsb ? 2L * metricCount : 0) + (hasLeftSideBearing ? 2L * (glyphCount - metricCount) : 0);
        if (needed > source.Length)
            throw new InvalidDataException($"The transformed WOFF2 'hmtx' table is {source.Length} bytes long, but its flags and {metricCount} metrics need {needed}.");

        budget.Take(2L * metricCount + 2L * glyphCount, "hmtx");
        var output = new byte[2L * metricCount + 2L * glyphCount];
        var advances = source[1..];
        var proportional = advances[(2 * metricCount)..];
        var rest = proportional[(hasLsb ? 2 * metricCount : 0)..];
        for (var i = 0; i < glyphCount; i++)
        {
            int bearing;
            if (i < metricCount)
            {
                bearing = hasLsb ? BinaryPrimitives.ReadInt16BigEndian(proportional[(2 * i)..]) : xMin[i];
                BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4 * i), BinaryPrimitives.ReadUInt16BigEndian(advances[(2 * i)..]));
                BinaryPrimitives.WriteInt16BigEndian(output.AsSpan(4 * i + 2), (short)bearing);
            }
            else
            {
                var j = i - metricCount;
                bearing = hasLeftSideBearing ? BinaryPrimitives.ReadInt16BigEndian(rest[(2 * j)..]) : xMin[i];
                BinaryPrimitives.WriteInt16BigEndian(output.AsSpan(4 * metricCount + 2 * j), (short)bearing);
            }
        }

        return output;
    }

    /// <summary>The glyph count (<c>maxp</c>) and every glyph's <c>xMin</c> of a plain <c>glyf</c> with its <c>loca</c>.</summary>
    private static (int GlyphCount, short[] XMin) XMinOfPlainGlyf(Entry[] entries, Font font, int glyf, Budget budget, Statistics? statistics)
    {
        var loca = FindTable(entries, font, LocaTag);
        var head = FindTable(entries, font, HeadTag);
        var maxp = FindTable(entries, font, MaxpTag);
        if (head < 0 || maxp < 0 || entries[head].Data.Length < 52 || entries[maxp].Data.Length < 6)
            throw new InvalidDataException("The transformed WOFF2 'hmtx' table needs 'head' and 'maxp' to read an untransformed 'glyf'.");

        var glyphCount = BinaryPrimitives.ReadUInt16BigEndian(entries[maxp].Data.Span[4..]);
        var longOffsets = BinaryPrimitives.ReadInt16BigEndian(entries[head].Data.Span[50..]) != 0;
        var locaData = entries[loca].Data.Span;
        var glyfData = entries[glyf].Data.Span;
        var entryLength = longOffsets ? 4 : 2;
        if (locaData.Length < (glyphCount + 1L) * entryLength)
            throw new InvalidDataException("The WOFF2 'loca' table is too short for the glyph count, which the transformed 'hmtx' table needs.");

        budget.Take(2L * glyphCount, "glyph boxes");
        if (statistics is not null)
            statistics.GlyphBoxReads += glyphCount;

        var xMin = new short[glyphCount];
        for (var i = 0; i < glyphCount; i++)
        {
            var start = LocaOffset(locaData, longOffsets, i);
            var end = LocaOffset(locaData, longOffsets, i + 1);
            if (end <= start)
                continue;

            if (start + 4 > glyfData.Length)
                throw new InvalidDataException($"Glyph {i} of the WOFF2 'glyf' table is past the table's end, which the transformed 'hmtx' table needs.");

            xMin[i] = BinaryPrimitives.ReadInt16BigEndian(glyfData[(int)(start + 2)..]);
        }

        return (glyphCount, xMin);
    }

    private static long LocaOffset(ReadOnlySpan<byte> loca, bool longOffsets, int i) =>
        longOffsets ? BinaryPrimitives.ReadUInt32BigEndian(loca[(4 * i)..]) : 2L * BinaryPrimitives.ReadUInt16BigEndian(loca[(2 * i)..]);

    // ---- The sfnt image ----

    private static byte[] Assemble(Entry[] entries, Font[] fonts, bool collection, long maxOutput, Statistics? statistics)
    {
        var headerLength = collection ? 12L + 4L * fonts.Length : 0;
        var lengths = entries.Select(entry => entry.Data.Length).ToArray();
        var offsets = new long[entries.Length];
        Array.Fill(offsets, -1);

        // The faces' directories, then the tables (once each) in the layout order.
        var sorted = fonts.Select(font => font.Tables.OrderBy(index => entries[index].Tag).ToArray()).ToArray();
        var position = headerLength + sorted.Sum(tables => OffsetTableLength + (long)RecordLength * tables.Length);
        if (position > maxOutput)
            throw new NotSupportedException($"The decoded WOFF2 font would be more than the {maxOutput}-byte limit.");

        var fontOffsets = new long[fonts.Length];
        var directory = headerLength;
        for (var f = 0; f < fonts.Length; f++)
        {
            fontOffsets[f] = directory;
            directory += OffsetTableLength + (long)RecordLength * sorted[f].Length;
        }

        var order = LayoutOrder(entries, sorted, collection);
        foreach (var index in order)
        {
            offsets[index] = position;
            position += Pad4(lengths[index]);
            if (position > maxOutput)
                throw new NotSupportedException($"The decoded WOFF2 font would be more than the {maxOutput}-byte limit.");
        }

        var image = new byte[position];
        if (collection)
        {
            BinaryPrimitives.WriteUInt32BigEndian(image, CollectionTag);
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(4), 0x00010000);
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(8), (uint)fonts.Length);
            for (var f = 0; f < fonts.Length; f++)
                BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(12 + 4 * f), (uint)fontOffsets[f]);
        }

        // Each table is copied and checksummed once, here; the rebuilt copy is released as soon as it is in the image.
        var checksums = new uint[entries.Length];
        var heads = new Dictionary<int, long>();
        foreach (var index in order)
        {
            var entry = entries[index];
            var destination = image.AsSpan((int)offsets[index], lengths[index]);
            entry.Data.Span.CopyTo(destination);
            entry.Data = default;
            if (entry.Tag == HeadTag && destination.Length >= 12)
            {
                destination[8..12].Clear();
                heads.Add(index, offsets[index] + 8);
            }

            checksums[index] = Checksum(destination);
            if (statistics is not null)
                statistics.ChecksummedBytes += destination.Length;
        }

        // checkSumAdjustment is the font's own sum (its offset table, records and tables) taken from 0xB1B0AFBA; a head table
        // shared by fonts takes the first font's.
        var adjustments = new Dictionary<int, uint>();
        for (var f = 0; f < fonts.Length; f++)
        {
            var tables = sorted[f];
            var at = (int)fontOffsets[f];
            var entrySelector = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)tables.Length);
            var searchRange = (1 << entrySelector) * 16;
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(at), fonts[f].Flavor);
            BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(at + 4), (ushort)tables.Length);
            BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(at + 6), (ushort)searchRange);
            BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(at + 8), (ushort)entrySelector);
            BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(at + 10), (ushort)(tables.Length * 16 - searchRange));

            uint fontSum = 0;
            var headIndex = -1;
            for (var t = 0; t < tables.Length; t++)
            {
                var index = tables[t];
                var record = image.AsSpan(at + OffsetTableLength + RecordLength * t);
                BinaryPrimitives.WriteUInt32BigEndian(record, entries[index].Tag);
                BinaryPrimitives.WriteUInt32BigEndian(record[4..], checksums[index]);
                BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offsets[index]);
                BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)lengths[index]);
                fontSum += checksums[index];
                if (heads.ContainsKey(index))
                    headIndex = index;
            }

            if (headIndex >= 0 && !adjustments.ContainsKey(headIndex))
            {
                fontSum += Checksum(image.AsSpan(at, OffsetTableLength + RecordLength * tables.Length));
                adjustments.Add(headIndex, 0xB1B0AFBAu - fontSum);
            }
        }

        foreach (var (index, adjustment) in adjustments)
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan((int)heads[index]), adjustment);

        return image;
    }

    /// <summary>
    /// The order the table data is laid out in. A font that is not a collection: FreeType's, tag order with a transformed
    /// <c>loca</c> directly after its <c>glyf</c>. A collection: the directory's order, each table once.
    /// </summary>
    private static List<int> LayoutOrder(Entry[] entries, int[][] sorted, bool collection)
    {
        if (!collection)
        {
            var order = new List<int>(sorted[0]);
            var glyf = order.FindIndex(index => entries[index].Tag == GlyfTag && entries[index].Transformed);
            if (glyf >= 0)
            {
                var loca = order.Single(index => entries[index].Tag == LocaTag);
                order.Remove(loca);
                order.Insert(order.FindIndex(index => entries[index].Tag == GlyfTag) + 1, loca);
            }

            return order;
        }

        var used = new bool[entries.Length];
        foreach (var tables in sorted)
        {
            foreach (var index in tables)
                used[index] = true;
        }

        return Enumerable.Range(0, entries.Length).Where(index => used[index]).ToList();
    }

    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        var i = 0;
        for (; i + 4 <= data.Length; i += 4)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data[i..]);

        if (i < data.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            data[i..].CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }

        return sum;
    }

    private static long Pad4(long value) => (value + 3) & ~3L;

    private static uint TagOf(string tag) => BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(tag));

    private static string TagText(uint tag)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, tag);
        return Encoding.Latin1.GetString(bytes);
    }

    /// <summary>A growing output buffer that refuses to pass its limit (what is left of the running total).</summary>
    private sealed class ByteSink(long limit)
    {
        private byte[] m_buffer = new byte[256];

        public int Length { get; private set; }

        /// <summary>The bytes written; the buffer is trimmed to them first when more than a quarter of it is spare.</summary>
        public ReadOnlyMemory<byte> Memory
        {
            get
            {
                if (m_buffer.Length - Length > Length / 4)
                    Array.Resize(ref m_buffer, Length);

                return m_buffer.AsMemory(0, Length);
            }
        }

        public void U8(byte value)
        {
            Ensure(1);
            m_buffer[Length++] = value;
        }

        public void U16(ushort value)
        {
            Ensure(2);
            BinaryPrimitives.WriteUInt16BigEndian(m_buffer.AsSpan(Length), value);
            Length += 2;
        }

        public void Bytes(ReadOnlySpan<byte> value)
        {
            Ensure(value.Length);
            value.CopyTo(m_buffer.AsSpan(Length));
            Length += value.Length;
        }

        /// <summary>Pads with zeros so the glyph that began at <paramref name="start"/> ends on a four-byte boundary.</summary>
        public void Pad4(long start)
        {
            var padding = (int)((4 - (Length - start) % 4) % 4);
            Ensure(padding);
            Array.Clear(m_buffer, Length, padding);
            Length += padding;
        }

        private void Ensure(int more)
        {
            if ((long)Length + more > limit)
                throw new NotSupportedException($"The rebuilt WOFF2 'glyf' table would be more than the {limit}-byte limit left to it.");

            if (Length + more > m_buffer.Length)
                Array.Resize(ref m_buffer, (int)Math.Min(limit, Math.Max((long)m_buffer.Length * 2, (long)Length + more)));
        }
    }

    /// <summary>A bounds-checked big-endian reader over one stream; reading past its end is an <see cref="InvalidDataException"/>.</summary>
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> m_data;

        public Reader(ReadOnlySpan<byte> data, string name)
        {
            m_data = data;
            Name = name;
        }

        public string Name { get; set; }

        public int Position { get; set; }

        public readonly int Remaining => m_data.Length - Position;

        public byte U8()
        {
            Need(1);
            return m_data[Position++];
        }

        public ushort U16()
        {
            Need(2);
            var value = BinaryPrimitives.ReadUInt16BigEndian(m_data[Position..]);
            Position += 2;
            return value;
        }

        public short S16() => (short)U16();

        public uint U32()
        {
            Need(4);
            var value = BinaryPrimitives.ReadUInt32BigEndian(m_data[Position..]);
            Position += 4;
            return value;
        }

        public ReadOnlySpan<byte> Bytes(int count)
        {
            Need(count);
            var value = m_data.Slice(Position, count);
            Position += count;
            return value;
        }

        /// <summary><c>UIntBase128</c>: at most five bytes, no leading 0x80, no value above 2^32 - 1.</summary>
        public uint Base128()
        {
            uint accumulator = 0;
            for (var i = 0; i < 5; i++)
            {
                var b = U8();
                if (i == 0 && b == 0x80)
                    throw new InvalidDataException($"A UIntBase128 in the WOFF2 {Name} starts with a zero byte (0x80).");

                if ((accumulator & 0xFE000000) != 0)
                    throw new InvalidDataException($"A UIntBase128 in the WOFF2 {Name} is above 2^32 - 1.");

                accumulator = (accumulator << 7) | (uint)(b & 0x7F);
                if ((b & 0x80) == 0)
                    return accumulator;
            }

            throw new InvalidDataException($"A UIntBase128 in the WOFF2 {Name} is longer than five bytes.");
        }

        /// <summary><c>255UInt16</c>.</summary>
        public int U255()
        {
            var b = U8();
            return b switch
            {
                253 => U16(),
                254 => U8() + 506,
                255 => U8() + 253,
                _ => b,
            };
        }

        /// <summary>One point's coordinate deltas (the triplet encoding of the format's section 5.2).</summary>
        public void Triplet(int flag, out int dx, out int dy)
        {
            var count = flag < 84 ? 1 : flag < 120 ? 2 : flag < 124 ? 3 : 4;
            Need(count);
            int b0 = m_data[Position], b1 = count > 1 ? m_data[Position + 1] : 0, b2 = count > 2 ? m_data[Position + 2] : 0;
            var b3 = count > 3 ? m_data[Position + 3] : 0;
            Position += count;

            if (flag < 10)
            {
                dx = 0;
                dy = Signed(flag, ((flag & 14) << 7) + b0);
            }
            else if (flag < 20)
            {
                dx = Signed(flag, (((flag - 10) & 14) << 7) + b0);
                dy = 0;
            }
            else if (flag < 84)
            {
                var b = flag - 20;
                dx = Signed(flag, 1 + (b & 0x30) + (b0 >> 4));
                dy = Signed(flag >> 1, 1 + ((b & 0x0C) << 2) + (b0 & 0x0F));
            }
            else if (flag < 120)
            {
                var b = flag - 84;
                dx = Signed(flag, 1 + ((b / 12) << 8) + b0);
                dy = Signed(flag >> 1, 1 + (((b % 12) >> 2) << 8) + b1);
            }
            else if (flag < 124)
            {
                dx = Signed(flag, (b0 << 4) + (b1 >> 4));
                dy = Signed(flag >> 1, ((b1 & 0x0F) << 8) + b2);
            }
            else
            {
                dx = Signed(flag, (b0 << 8) + b1);
                dy = Signed(flag >> 1, (b2 << 8) + b3);
            }

            static int Signed(int flag, int value) => (flag & 1) != 0 ? value : -value;
        }

        private readonly void Need(int count)
        {
            if (count < 0 || count > m_data.Length - Position)
                throw new InvalidDataException($"The WOFF2 {Name} is read past its end.");
        }
    }
}
