namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// Reads an sfnt image (a single face or a <c>ttcf</c> collection) into <see cref="FontFace"/>s: the table directory and the
/// tables the library uses, each checked at load.
/// </summary>
/// <remarks>
/// <para>
/// Safety-strict (Denys, 2026-11-26): whatever affects what the library reads, or the bounds it reads within, is checked, and
/// nonconforming data there throws <see cref="InvalidDataException"/> at load, never later. The rules:
/// the offset table, the table directory or a collection header past the end of the data;
/// a collection whose version is not 1.0 or 2.0, with no faces, with a face offset inside its header, or with a face offset
/// repeated;
/// a table record (zero-length ones too) past the end of the data, or one with bytes overlapping the face's directory or the
/// collection header;
/// two records of the file (of one face or of different faces) whose bytes overlap without being the same record (tag, offset
/// and length): faces may share a table, but tables may not overlap (Denys's "Strict overlap rule", 2026-11-26);
/// faces that share a table record but would read it with different inputs (Denys's "Same-inputs rule", 2026-11-26; the
/// inputs are listed at <see cref="TableInputs"/>: the glyph count, the <c>loca</c> format, the number of metrics, and for
/// <c>loca</c> and <c>glyf</c> each other's record);
/// a required table missing (<c>head</c>, <c>hhea</c>, <c>maxp</c>, <c>hmtx</c>, <c>cmap</c>, <c>name</c>, <c>post</c>), or one of
/// <c>loca</c> and <c>glyf</c> without the other (a zero-length <c>glyf</c> beside a <c>loca</c> counts as present, below);
/// an <c>OTTO</c> face without <c>CFF2</c> or <c>CFF </c>; another face without <c>glyf</c> and without a bitmap (<c>EBDT</c>,
/// <c>CBDT</c>, <c>sbix</c>) or <c>SVG </c> table (the outline table follows the sfnt version, as FreeType's driver choice does:
/// <c>OTTO</c> reads <c>CFF2</c>, else <c>CFF </c>; the others read <c>glyf</c> and ignore a CFF table);
/// <c>head</c> shorter than 54 bytes, its majorVersion not 1, its magic number wrong, <c>unitsPerEm</c> outside 16..16384,
/// <c>indexToLocFormat</c> other than 0 or 1;
/// <c>maxp</c> of a version other than 0.5 or 1.0 or too short for it, <c>numGlyphs</c> 0, or version 0.5 with <c>glyf</c>;
/// <c>hhea</c> shorter than 36 bytes, its majorVersion not 1, <c>numberOfHMetrics</c> 0 or above <c>numGlyphs</c>;
/// <c>hmtx</c> shorter than its metrics;
/// <c>name</c> with its header, records or language-tag records past the table;
/// <c>post</c> shorter than 32 bytes, or of a version other than 1.0, 2.0, 2.5, 3.0 and 4.0;
/// <c>loca</c> shorter than <c>numGlyphs + 1</c> offsets, an offset smaller than the one before it or past the end of <c>glyf</c>;
/// a glyph shorter than its 10-byte header; contour ends not ascending; instructions, flags or coordinates past the glyph's data;
/// a flag repeated past the point count; a point flag with the reserved bit 7 set; a component record past the data; a
/// component referencing a glyph out of range or the glyph itself; components forming a cycle; an anchor point out of range;
/// more than 65,535 points in a glyph, simple (a contour ending at point 65,535) or composite (point numbers are 16-bit);
/// a <c>cmap</c> encoding record past the table, or one whose subtable starts inside the table's header and encoding records (as
/// OTS refuses it) or runs (by its declared length) past the table; two subtables checked in full (below) sharing bytes without
/// being the same subtable (one offset), as OTS refuses "excessive overlap" (Denys's "Rules + work budget", 2026-11-26); and,
/// for a subtable FreeType's choice can select (a Unicode encoding) or a format-14 one, checked once per offset: format 0, 6 or 10 data past its subtable; format 4 with an odd or
/// zero <c>segCountX2</c>, a segment starting after its end or not after the previous one (a final pair of 0xFFFF-0xFFFF
/// segments excepted), a last segment not ending at U+FFFF, an <c>idRangeOffset</c> of 0xFFFF in any segment but the final
/// 0xFFFF-0xFFFF one (FreeType drops such a subtable), or (except in that final segment) an <c>idRangeOffset</c> pointing
/// before the glyph id array or its glyph ids past the subtable; format 12 or 13 groups starting after their end or not after
/// the previous group; format 14 selectors 0, past U+10FFFF or not ascending, default ranges or non-default mappings not
/// ascending, past U+10FFFF or past the subtable, or its distinct lists taking more bytes in all than the subtable has (as OTS
/// caps them); the selected Unicode subtable (U+FFFF in the final 0xFFFF-0xFFFF segments excepted) or a format-14 subtable
/// mapping a code point to a glyph id at or past <c>numGlyphs</c>;
/// <c>SVG </c> with its index past the table, a record starting after its end or not after the previous one, a document past
/// the table, two documents (distinct offset or length) sharing bytes (Denys's "Rules + work budget"; records with the same
/// offset and length share one document), a gzip document whose header is not a deflate member's (ID 1F 8B, method 8, no reserved flag bits, optional
/// fields inside the member, a header CRC that matches), that does not inflate, or whose trailer does not match the inflated
/// bytes (CRC-32, then ISIZE).
/// </para>
/// <para>
/// Accepted, by Denys's safety-strict decision of 2026-11-26, as FreeType and OTS accept them (each tested to load and to read
/// as FreeType reads it): table checksums are not verified (nor by FreeType, OTS or DirectWrite); table records out of tag
/// order; of two records with one tag, the first is used, as FreeType uses it (OTS keeps the last); a zero-length record makes
/// its table absent, as FreeType's table lookup treats it (it still takes part in the bounds checks), but a zero-length
/// <c>glyf</c> beside a <c>loca</c> whose offsets are all 0 loads as all-empty glyphs, as FreeType loads it; <c>head</c> flags with
/// the reserved bit 15 set (ignored); <c>name</c> records out of order or repeated (the same platform, encoding, language and
/// name ID; the choice scans them in file order, as FreeType's does), an empty record, one whose string lies outside the
/// storage, or (version 1) one whose language ID names a language tag past the list or ignored, skipped, a language tag that
/// is empty or outside the storage ignored, a <c>name</c> version above 1 (language tags are read only in version 1, as
/// FreeType reads them); <c>cmap</c> encoding records out of order or repeated (the same platform, encoding and subtable
/// language; the last Unicode one and the first format-14 one are chosen, as FreeType chooses them), a <c>cmap</c> version
/// above 0, and a subtable FreeType's choice can never select (not Unicode, not format 14), which gets only its bounds check;
/// composites nested deeper than <c>maxp.maxComponentDepth</c> declares; an <c>OS/2</c> table shorter than its version claims or
/// of a version above 5, read as FreeType reads it (a 78-byte frame and the version's extensions at the table's offset in the
/// file; past the end of the file or at version 0xFFFF, the table is absent); the glyph names of <c>post</c> 2.0 and 2.5 and
/// the character codes of <c>post</c> 4.0, which are never read; an <c>SVG </c> version above 0, read with version 0's layout;
/// the final format-4 segment's <c>idRangeOffset</c>, a final pair of 0xFFFF-0xFFFF segments, and U+FFFF's mapping, which
/// FreeType reads its own way.
/// </para>
/// <para>
/// Ours works where FreeType fails (Denys: "Ours works, documented"; each tested to fail in FreeType and to work here): a
/// <c>glyf</c> face with a <c>CBLC</c>/<c>CBDT</c> or <c>sbix</c> strike, whose outlines FreeType ignores (it then treats the
/// face as bitmap-only), and a face whose only outline is <c>.notdef</c> with an <c>EBLC</c> strike, which FreeType makes not
/// scalable: FreeType refuses a pixel size no strike has, ours renders the outlines; a simple glyph of 4,095 or more contours
/// (FreeType: <c>Invalid_Outline</c>); an outline of more than 32,763 points (FreeType keeps a signed 16-bit count); a row of
/// more cells than FreeType's render pool holds, and an overlapping glyph oversampled past 8,191 pixels wide (FreeType:
/// <c>Raster_Overflow</c>); a (0, 5) format-14 subtable listed last, which FreeType takes for the Unicode map and then maps
/// nothing through, while ours maps through the real Unicode subtable.
/// </para>
/// <para>
/// <see cref="NotSupportedException"/>, for a format or feature out of scope: an sfnt version other than <c>0x00010000</c>,
/// <c>true</c> and <c>OTTO</c> (<c>typ1</c> among them, as in a collection's face); an Apple bitmap-only face (<c>bhed</c>
/// instead of <c>head</c>); a face with Apple <c>hvgl</c> outlines and no <c>glyf</c>; a <c>cmap</c> with no Unicode subtable
/// (only legacy or symbol encodings), or whose selected Unicode subtable has format 2 or 8. And for valid data past one of the
/// library's limits: a file over <see cref="FontLoader.MaxFileSize"/>; a file whose gzip <c>SVG </c> documents (all faces
/// together, each distinct document once) inflate past <see cref="SvgTable.InflateBudget"/>; a file whose parsed tables (all
/// faces together, the collection's face list first) need more than <see cref="ParsedTables.MaxParsedBytesPerFileByte"/>
/// bytes per byte of the file; a file whose load takes more work than <see cref="ParsedTables.MaxWorkPerFileByte"/> units per
/// byte of the file plus <see cref="ParsedTables.WorkAllowance"/> (Denys's "Rules + work budget", 2026-11-26: the backstop);
/// a composite whose whole component tree loads more than 65,535 glyphs, or that nests more than 255 levels deep (the glyph
/// loader recurses once per level).
/// </para>
/// <para>
/// Not checked, as FreeType does not use them: the whole-file <c>checkSumAdjustment</c>, the directory's search fields, table
/// alignment, <c>cmap</c> formats 2 and 8 beyond their length, the structure of <c>cmap</c> subtables that can never be
/// selected (beyond their bounds), the tables this library does not read. The reserved bits of composite glyph flags are not
/// checked, by Denys's decision of 2026-11-26 ("No rule"): they never change what the parser reads. (Background: 51 of 231
/// faces measured in <c>C:\Windows\Fonts</c>, both repository fonts among them, set bit 4; bits 13 to 15 are set nowhere.)
/// </para>
/// <para>
/// The load's work. The rules: each table record is parsed once per file (faces that share it read it with the same inputs
/// and share one parsed copy, <see cref="ParsedTables"/>); each <c>cmap</c> subtable is checked once per offset, however
/// many records point at it; each distinct <c>SVG </c> document is inflated once per file; and distinct tables, distinct
/// <c>cmap</c> subtables checked in full and distinct <c>SVG </c> documents never share bytes (the three overlap rules). The
/// backstop, for any route those rules leave open: every byte or element the load walks (directories, table walks,
/// subtable and list checks, glyph checks, name decoding, gzip input; gzip output is bounded by the SVG inflate budget, not
/// the work budget) is charged to one per-file work budget,
/// <see cref="ParsedTables.MaxWorkPerFileByte"/> units per byte of the file plus <see cref="ParsedTables.WorkAllowance"/>;
/// past it the load throws <see cref="NotSupportedException"/>. So the load's work is linear in the file's length.
/// Stateless: safe on any thread.
/// </para>
/// </remarks>
internal static class SfntReader
{
    private const uint TrueTypeVersion = 0x00010000;
    private const uint AppleTrueTypeTag = 0x74727565; // true
    private const uint OpenTypeCffTag = 0x4F54544F;   // OTTO
    private const uint CollectionTag = 0x74746366;    // ttcf

    private const int OffsetTableLength = 12;
    private const int TableRecordLength = 16;
    private const int CollectionHeaderLength = 12;

    // What a face's own objects take (the face, its table holder, its outline source, its names) and each directory record (the
    // record array and the tag dictionary), accounted against the file's parsed-memory budget.
    private const int FaceCost = 2048;
    private const int TableRecordCost = 64;

    // A collection face's entries in the face list (16 bytes) and the face-offset set (about 24 bytes), rounded up.
    private const long CollectionFaceCost = 48;

    /// <summary>Reads every face of an sfnt or collection image.</summary>
    /// <param name="data">The image; the faces keep it, so the caller must not change it afterwards.</param>
    public static IReadOnlyList<FontFace> ReadFaces(byte[] data)
    {
        if (data.LongLength > FontLoader.MaxFileSize)
            throw new NotSupportedException($"The font is {data.LongLength} bytes long, more than the {FontLoader.MaxFileSize}-byte limit.");

        var r = new BigEndianReader(data, "font");
        if (r.Length < 4)
            throw new InvalidDataException("The font data is shorter than its 4-byte signature.");

        return ReadFaces(data, new ParsedTables(data.LongLength));
    }

    /// <summary>
    /// The parsed-table bytes a load of this image accounts, without the limit: the measurement behind
    /// <see cref="ParsedTables.MaxParsedBytesPerFileByte"/>.
    /// </summary>
    internal static long MeasureParsedBytes(byte[] data)
    {
        var parsed = new ParsedTables(data.LongLength, unlimited: true);
        ReadFaces(data, parsed);
        return parsed.ParsedBytes;
    }

    /// <summary>The load work a load of this image charges, without the limit: the measurement behind
    /// <see cref="ParsedTables.MaxWorkPerFileByte"/>.</summary>
    internal static long MeasureWork(byte[] data)
    {
        var parsed = new ParsedTables(data.LongLength, unlimited: true);
        ReadFaces(data, parsed);
        return parsed.Work;
    }

    /// <summary>Reads every face with a work limit of <paramref name="workLimit"/> units instead of the file's (for tests).</summary>
    internal static IReadOnlyList<FontFace> ReadFaces(byte[] data, long workLimit) =>
        ReadFaces(data, new ParsedTables(data.LongLength, workLimit: workLimit));

    /// <summary>
    /// The tables a load of this image parses: each distinct table (record and inputs) once, however many faces share it (for
    /// tests).
    /// </summary>
    internal static int CountParses(byte[] data)
    {
        var parsed = new ParsedTables(data.LongLength);
        ReadFaces(data, parsed);
        return parsed.TablesParsed;
    }

    /// <summary>
    /// Reads every face's directory first, checks that no two different records of the file overlap, then parses each face.
    /// The guarantee: each table record is parsed once per file, faces that share it reading it with the same inputs (else
    /// <see cref="InvalidDataException"/>); each <c>cmap</c> subtable is checked once per offset and each distinct
    /// <c>SVG </c> document inflated once; distinct tables, distinct fully checked <c>cmap</c> subtables and distinct
    /// <c>SVG </c> documents never share bytes (the three overlap rules). As the backstop for routes those rules miss, all
    /// the load's work is charged to the file's work budget (<see cref="ParsedTables.Charge"/>), so it stays linear in the
    /// file's length; past it the load throws <see cref="NotSupportedException"/>.
    /// </summary>
    private static IReadOnlyList<FontFace> ReadFaces(byte[] data, ParsedTables parsed)
    {
        var directories = ReadDirectories(data, parsed);
        CheckOverlaps(directories, parsed);

        var faces = new FontFace[directories.Count];
        for (var i = 0; i < faces.Length; i++)
        {
            var (version, records) = directories[i];
            parsed.Charge(TableRecordLength * (long)records.Length, "the table lookup of face", i);
            faces[i] = new FontFace(ReadTables(new TableDirectory(data, version, records), i, parsed));
        }

        return faces;
    }

    private static List<(uint Version, TableRecord[] Records)> ReadDirectories(byte[] data, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, "font");
        if (r.U32(0) != CollectionTag)
            return [ReadDirectory(data, 0, 0, 0, parsed)];

        r.Check(0, CollectionHeaderLength);
        var major = r.U16(4);
        var minor = r.U16(6);
        if (major is not (1 or 2) || minor != 0)
            throw new InvalidDataException($"The collection header's version is {major}.{minor}, not 1.0 or 2.0.");

        var count = r.U32(8);
        if (count == 0)
            throw new InvalidDataException("The collection has no faces.");

        // Version 2.0 adds the DSIG tag, length and offset after the face offsets.
        var headerLength = CollectionHeaderLength + 4L * count + (major == 2 ? 12 : 0);
        if (!r.Contains(0, headerLength))
            throw new InvalidDataException($"The collection header of {count} faces ({headerLength} bytes) runs past the data.");

        // The face list and the offset set are accounted before they are sized from the header's count.
        parsed.Reserve(64 + CollectionFaceCost * count, "the collection's face list");
        parsed.Charge(4L * count, "the collection's face offsets");
        var directories = new List<(uint, TableRecord[])>((int)count);
        var offsets = new HashSet<uint>((int)count);
        for (var i = 0; i < count; i++)
        {
            var offset = r.U32(CollectionHeaderLength + 4 * i);
            if (offset < headerLength)
                throw new InvalidDataException($"Face {i} of the collection starts at {offset}, inside the collection header.");

            if (offset > int.MaxValue)
                throw new InvalidDataException($"Face {i} of the collection starts at {offset}, past the data.");

            if (!offsets.Add(offset))
                throw new InvalidDataException($"Face {i} of the collection starts at {offset}, as an earlier face does.");

            directories.Add(ReadDirectory(data, i, (int)offset, (int)headerLength, parsed));
        }

        return directories;
    }

    /// <param name="data">The image.</param>
    /// <param name="faceIndex">The face's index in its file.</param>
    /// <param name="offset">Where its offset table starts.</param>
    /// <param name="collectionHeaderLength">The length of the collection header the tables may not overlap; 0 for a single face.</param>
    /// <param name="parsed">The file's load state, against whose parsed-memory budget the directory is accounted.</param>
    private static (uint Version, TableRecord[] Records) ReadDirectory(byte[] data, int faceIndex, int offset,
        int collectionHeaderLength, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, "font");
        if (!r.Contains(offset, OffsetTableLength))
            throw new InvalidDataException($"Face {faceIndex}'s offset table at {offset} runs past the data.");

        var version = r.U32(offset);
        if (version is not (TrueTypeVersion or AppleTrueTypeTag or OpenTypeCffTag))
            throw new NotSupportedException(
                $"Face {faceIndex} has sfnt version 0x{version:X8} ('{Tags.ToString(version)}'), not 0x00010000, 'true' or 'OTTO'.");

        var tableCount = r.U16(offset + 4);
        var directoryEnd = offset + OffsetTableLength + (long)TableRecordLength * tableCount;
        if (directoryEnd > r.Length)
            throw new InvalidDataException($"Face {faceIndex}'s directory of {tableCount} tables runs past the data.");

        parsed.Reserve(FaceCost + TableRecordCost * tableCount, "the directory of face", faceIndex);
        parsed.Charge(OffsetTableLength + (long)TableRecordLength * tableCount, "the directory of face", faceIndex);

        var records = new TableRecord[tableCount];
        for (var i = 0; i < tableCount; i++)
        {
            var at = offset + OffsetTableLength + TableRecordLength * i;
            var tag = r.U32(at);
            var tableOffset = r.U32(at + 8);
            var tableLength = r.U32(at + 12);
            var name = Tags.ToString(tag);

            if ((long)tableOffset + tableLength > r.Length)
                throw new InvalidDataException(
                    $"Face {faceIndex}'s '{name}' table ({tableLength} bytes at {tableOffset}) runs past the {r.Length}-byte data.");

            if (tableLength > 0 && tableOffset < collectionHeaderLength)
                throw new InvalidDataException(
                    $"Face {faceIndex}'s '{name}' table ({tableLength} bytes at {tableOffset}) overlaps the collection header.");

            if (tableLength > 0 && tableOffset < directoryEnd && tableOffset + tableLength > offset)
                throw new InvalidDataException(
                    $"Face {faceIndex}'s '{name}' table ({tableLength} bytes at {tableOffset}) overlaps its table directory.");

            // Records out of tag order are accepted, and of two records with one tag the first is used, as FreeType does
            // (TableDirectory keeps the first).
            records[i] = new TableRecord(tag, (int)tableOffset, (int)tableLength);
        }

        return (version, records);
    }

    /// <summary>
    /// Two records of the file whose bytes overlap must be the same record (tag, offset and length): faces may share a table,
    /// but no table may overlap another, within a face or across faces. Empty tables overlap nothing.
    /// </summary>
    private static void CheckOverlaps(List<(uint Version, TableRecord[] Records)> directories, ParsedTables parsed)
    {
        var total = directories.Sum(directory => (long)directory.Records.Length);
        parsed.Reserve(64 + 64 * total, "the overlap check");
        parsed.Charge(TableRecordLength * total, "the overlap check");
        var ranges = new HashSet<(uint Tag, int Offset, int Length)>();
        foreach (var (_, records) in directories)
        {
            foreach (var record in records)
            {
                if (record.Length > 0)
                    ranges.Add((record.Tag, record.Offset, record.Length));
            }
        }

        var sorted = ranges.ToArray();
        Array.Sort(sorted, (a, b) => a.Offset != b.Offset ? a.Offset.CompareTo(b.Offset) : a.Length.CompareTo(b.Length));
        long end = 0;
        var previous = default((uint Tag, int Offset, int Length));
        foreach (var range in sorted)
        {
            if (range.Offset < end)
                throw new InvalidDataException(
                    $"The '{Tags.ToString(range.Tag)}' table ({range.Length} bytes at {range.Offset}) overlaps the " +
                    $"'{Tags.ToString(previous.Tag)}' table ({previous.Length} bytes at {previous.Offset}); two records may share " +
                    "bytes only as the same record (tag, offset and length).");

            if (range.Offset + (long)range.Length > end)
            {
                end = range.Offset + (long)range.Length;
                previous = range;
            }
        }
    }

    /// <summary>
    /// Parses the face's tables. A table record an earlier face of the file parsed is shared, not parsed again; the face must
    /// read it with the same inputs (<see cref="TableInputs"/>), else <see cref="InvalidDataException"/>. So each table record
    /// is parsed once per file, whatever the number of faces.
    /// </summary>
    private static FaceData ReadTables(TableDirectory tables, int faceIndex, ParsedTables parsed)
    {
        if (!tables.Contains(Tags.Head) && tables.Contains(Tags.Bhed))
            throw new NotSupportedException("The font is an Apple bitmap-only font ('bhed' instead of 'head'), which is not supported.");

        var headRecord = tables.GetRequiredRecord(Tags.Head);
        var maxpRecord = tables.GetRequiredRecord(Tags.Maxp);
        var hheaRecord = tables.GetRequiredRecord(Tags.Hhea);
        var hmtxRecord = tables.GetRequiredRecord(Tags.Hmtx);
        var cmapRecord = tables.GetRequiredRecord(Tags.Cmap);
        var nameRecord = tables.GetRequiredRecord(Tags.Name);
        var postRecord = tables.GetRequiredRecord(Tags.Post);

        // Each record is parsed once per file: faces that share it must read it with the same inputs (TableInputs).
        var none = TableInputs.None;
        var head = parsed.Get(headRecord, none, faceIndex, () => HeadTable.Parse(tables.Get(Tags.Head).Span));
        var maxp = parsed.Get(maxpRecord, none, faceIndex, () => MaxpTable.Parse(tables.Get(Tags.Maxp).Span));
        var glyphCount = maxp.NumGlyphs;
        var hhea = parsed.Get(hheaRecord, new TableInputs(GlyphCount: glyphCount), faceIndex,
            () => HheaTable.Parse(tables.Get(Tags.Hhea).Span, glyphCount));
        var hmtx = parsed.Get(hmtxRecord, new TableInputs(GlyphCount: glyphCount, NumberOfHMetrics: hhea.NumberOfHMetrics), faceIndex,
            () => HmtxTable.Parse(tables.Get(Tags.Hmtx), hhea.NumberOfHMetrics, glyphCount));
        var name = parsed.Get(nameRecord, none, faceIndex, () => NameTable.Parse(tables.Get(Tags.Name), parsed));
        var post = parsed.Get(postRecord, none, faceIndex, () => PostTable.Parse(tables.Get(Tags.Post).Span));

        // OS/2 is read from the file at the table's offset, as FreeType reads it; a table FreeType treats as absent is null
        // (a zero-length record is absent already).
        var os2 = tables.TryGetRecord(Tags.Os2, out var os2Record)
            ? parsed.Get(os2Record, none, faceIndex, () => Os2Table.Parse(tables.Data, os2Record.Offset))
            : null;
        if (os2 is { IsPresent: false })
            os2 = null;
        var cmap = parsed.Get(cmapRecord, new TableInputs(GlyphCount: glyphCount), faceIndex,
            () => CmapTable.Parse(tables.Get(Tags.Cmap), glyphCount, parsed));
        var svg = tables.TryGetRecord(Tags.Svg, out var svgRecord)
            ? parsed.Get(svgRecord, none, faceIndex, () => SvgTable.Parse(tables.Get(Tags.Svg), svgRecord.Offset, parsed))
            : null;

        // A zero-length glyf is absent, but beside a loca FreeType loads the face with all-empty glyphs (its offsets must then
        // all be 0: any other lies past the empty glyf), so it counts as present here.
        var hasLoca = tables.Contains(Tags.Loca);
        var emptyGlyf = hasLoca ? tables.FirstRecord(Tags.Glyf) : null;
        var hasGlyf = tables.Contains(Tags.Glyf) || emptyGlyf is { Length: 0 };
        if (hasLoca != hasGlyf)
            throw new InvalidDataException(hasGlyf
                ? "The font has a 'glyf' table without a 'loca' table."
                : "The font has a 'loca' table without a 'glyf' table.");

        // FreeType chooses its driver by sfnt version: an 'OTTO' face reads CFF2, else CFF; any other reads glyf (ignoring a
        // CFF table) or is bitmap-only.
        OutlineFormat format;
        GlyfTable? glyf = null;
        if (tables.SfntVersion == OpenTypeCffTag)
        {
            if (tables.Contains(Tags.Cff2))
                format = OutlineFormat.Cff2;
            else if (tables.Contains(Tags.Cff))
                format = OutlineFormat.Cff;
            else
                throw new InvalidDataException("The font is an 'OTTO' face without a 'CFF2' or 'CFF ' table.");
        }
        else if (hasGlyf)
        {
            if (maxp.Version != MaxpTable.Version10)
                throw new InvalidDataException("The font has TrueType outlines but a version 0.5 'maxp' table; it needs version 1.0.");

            // loca and glyf are parsed together: each reads the other's record, the loca format and the glyph count.
            var locaRecord = tables.GetRequiredRecord(Tags.Loca);
            var glyfRecord = tables.TryGetRecord(Tags.Glyf, out var record) ? record : emptyGlyf!.Value;
            parsed.Register(locaRecord,
                new TableInputs(GlyphCount: glyphCount, IndexToLocFormat: head.IndexToLocFormat, Companion: glyfRecord), faceIndex);
            glyf = parsed.Get(glyfRecord,
                new TableInputs(GlyphCount: glyphCount, IndexToLocFormat: head.IndexToLocFormat, Companion: locaRecord), faceIndex,
                () => GlyfTable.Parse(tables.Get(Tags.Loca), tables.Get(Tags.Glyf), head.IndexToLocFormat, glyphCount, parsed));
            format = OutlineFormat.TrueType;
        }
        else if (tables.Contains(Tags.Hvgl))
        {
            throw new NotSupportedException("The font has Apple 'hvgl' outlines, which are not supported.");
        }
        else if (tables.Contains(Tags.Ebdt) || tables.Contains(Tags.Cbdt) || tables.Contains(Tags.Sbix) || svg is not null)
        {
            format = OutlineFormat.None;
        }
        else
        {
            throw new InvalidDataException("The font has no outline table ('glyf') and no bitmap or 'SVG ' table.");
        }

        return new FaceData
        {
            FaceIndex = faceIndex,
            Tables = tables,
            Head = head,
            Hhea = hhea,
            Maxp = maxp,
            Hmtx = hmtx,
            Os2 = os2,
            Name = name,
            Post = post,
            Cmap = cmap,
            Svg = svg,
            Glyf = glyf,
            OutlineFormat = format,
        };
    }
}
