// SelectUnicode follows FreeType 2.13.2 src/base/ftobjs.c (find_unicode_charmap) and src/sfnt/sfobjs.c (sfnt_find_encoding),
// and the lookups give the results of src/sfnt/ttcmap.c (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>cmap</c> table: the Unicode subtable FreeType 2.13.2 selects (<c>find_unicode_charmap</c>) and the format-14 variation
/// selector subtable. Every subtable's declared range must lie inside the table; a subtable FreeType's choice can select (a
/// Unicode encoding) or a format-14 one, of a format this library reads, is checked in full at load, once per offset however
/// many records point at it; formats 2 and 8 are not read. Every glyph id the selected subtable or a format-14 subtable maps to
/// must be below the glyph count.
/// </summary>
internal sealed class CmapTable
{
    private const int PlatformUnicode = 0;
    private const int PlatformIso = 2;
    private const int PlatformWindows = 3;
    private const int UnicodeFull = 4;               // platform 0, Unicode full repertoire (UCS-4)
    private const int UnicodeVariationSequences = 5; // platform 0, format 14
    private const int WindowsUnicodeBmp = 1;
    private const int WindowsUnicodeFull = 10;
    private const int MaxCodePoint = 0x10FFFF;

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int m_format;
    private readonly int m_offset;
    private readonly int m_length;
    private readonly int m_variationOffset;
    private readonly int m_variationLength;
    private readonly int m_glyphCount;

    private CmapTable(ReadOnlyMemory<byte> data, Subtable unicode, Subtable? variation, int glyphCount)
    {
        m_data = data;
        m_format = unicode.Format;
        m_offset = unicode.Offset;
        m_length = unicode.Length;
        m_variationOffset = variation?.Offset ?? -1;
        m_variationLength = variation?.Length ?? 0;
        m_glyphCount = glyphCount;
    }

    /// <param name="data">The table.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    /// <param name="parsed">The file's load state, against whose parsed-memory budget the subtable list is accounted.</param>
    public static CmapTable Parse(ReadOnlyMemory<byte> data, int glyphCount, ParsedTables parsed)
    {
        // The version is not checked: a version above 0 is accepted (Denys's safety-strict decision), as FreeType ignores it.
        var r = new BigEndianReader(data.Span, "cmap");
        r.Check(0, 4);

        var count = r.U16(2);
        var recordsEnd = 4 + 8 * count;
        r.Check(4, 8L * count);

        // The subtable lists and the set of checked offsets (an entry with its share of the set's arrays at 48 bytes).
        parsed.Reserve(64 + 64L * count + 48L * count, "the 'cmap' subtables");
        parsed.Charge(8L * count, "the 'cmap' encoding records");
        var subtables = new List<Subtable>(count);
        var checkedInFull = new List<Subtable>();
        var offsets = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var at = 4 + 8 * i;
            var platform = r.U16(at);
            var encoding = r.U16(at + 2);
            var offset = r.U32(at + 4);
            if (offset < recordsEnd)
                throw new InvalidDataException(
                    $"The 'cmap' subtable ({platform}, {encoding}) starts at {offset}, inside the table's header and encoding " +
                    $"records ({recordsEnd} bytes).");

            if (offset > r.Length - 2L)
                throw new InvalidDataException(
                    $"The 'cmap' subtable ({platform}, {encoding}) starts at {offset}, past the {r.Length}-byte table.");

            var format = r.U16((int)offset);

            // Records in any order, and the same (platform, encoding, language) more than once, are accepted (Denys's
            // safety-strict decision; FreeType and OTS accept both); the choice scans the records in file order as FreeType
            // does (the last Unicode one, the first format-14 one).
            var length = SubtableLength(r, (int)offset, format, platform, encoding);
            if (length < 0)
                continue; // a format FreeType does not know either

            // Every subtable's declared range lies inside the table (SubtableLength). A subtable FreeType's choice can
            // select (a Unicode encoding) or a format-14 one is checked in full, once per offset however many records point
            // at it; any other is never read, so it gets only the bounds check (as FreeType drops a broken one and OTS never
            // parses it). Formats 2 and 8 are never read either.
            var subtable = new Subtable(platform, encoding, format, (int)offset, length);
            if (format is not (2 or 8) && (format == 14 || IsUnicode(platform, encoding)) && offsets.Add(subtable.Offset))
                checkedInFull.Add(subtable);

            subtables.Add(subtable);
        }

        CheckOverlaps(checkedInFull, parsed);
        foreach (var subtable in checkedInFull)
            Validate(r.Sub(subtable.Offset, subtable.Length, "cmap"), subtable, glyphCount, parsed);

        var unicode = SelectUnicode(subtables);
        if (unicode is null)
            throw new NotSupportedException("The font has no Unicode 'cmap' subtable; legacy and symbol encodings are not supported.");

        if (unicode.Value.Format is 2 or 8)
            throw new NotSupportedException(
                $"The font's Unicode 'cmap' subtable ({unicode.Value.Platform}, {unicode.Value.Encoding}) has format " +
                $"{unicode.Value.Format}, which is not supported.");

        CheckGlyphIds(r.Sub(unicode.Value.Offset, unicode.Value.Length, "cmap"), unicode.Value, glyphCount, parsed);

        Subtable? variation = null;
        foreach (var subtable in subtables)
        {
            if (subtable is { Platform: PlatformUnicode, Encoding: UnicodeVariationSequences, Format: 14 })
            {
                variation = subtable;
                break;
            }
        }

        return new CmapTable(data, unicode.Value, variation, glyphCount);
    }

    /// <summary>The glyph for a code point; 0 when unmapped, outside 0..0x10FFFF, or mapped past the glyph count.</summary>
    public int Lookup(int codePoint)
    {
        if ((uint)codePoint > MaxCodePoint)
            return 0;

        var r = new BigEndianReader(m_data.Span.Slice(m_offset, m_length), "cmap");
        var glyph = m_format switch
        {
            0 => codePoint < 256 ? r.U8(6 + codePoint) : 0,
            4 => codePoint == 0xFFFF ? LookupFfff(r) : LookupFormat4(r, codePoint),
            6 => LookupTrimmed(r, (uint)codePoint, r.U16(6), r.U16(8), 10),
            10 => LookupTrimmed(r, (uint)codePoint, r.U32(12), r.U32(16), 20),
            12 => LookupGroups(r, (uint)codePoint, false),
            13 => LookupGroups(r, (uint)codePoint, true),
            _ => 0,
        };

        return (uint)glyph < (uint)m_glyphCount ? glyph : 0;
    }

    /// <summary>
    /// The glyph for a variation sequence, as FreeType's <c>FT_Face_GetCharVariantIndex</c>: the base mapping when the sequence
    /// is a default one, its own glyph when it is a non-default one, 0 otherwise or without a format-14 subtable.
    /// </summary>
    public int LookupVariant(int codePoint, int selector)
    {
        if (m_variationOffset < 0 || (uint)codePoint > MaxCodePoint || (uint)selector > MaxCodePoint)
            return 0;

        var r = new BigEndianReader(m_data.Span.Slice(m_variationOffset, m_variationLength), "cmap");
        var count = (int)r.U32(6);

        // Binary search of the selector records (11 bytes each, ascending).
        int low = 0, high = count;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            var at = 10 + 11 * mid;
            var value = r.U24(at);
            if (selector < value)
            {
                high = mid;
            }
            else if (selector > value)
            {
                low = mid + 1;
            }
            else
            {
                var defaultOffset = (int)r.U32(at + 3);
                var nonDefaultOffset = (int)r.U32(at + 7);
                if (defaultOffset != 0 && InDefaultRanges(r, defaultOffset, (uint)codePoint))
                    return Lookup(codePoint);

                if (nonDefaultOffset != 0)
                {
                    var glyph = LookupNonDefault(r, nonDefaultOffset, (uint)codePoint);
                    return glyph < m_glyphCount ? glyph : 0;
                }

                return 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// FreeType's choice: the last full-repertoire subtable ((3, 10) or (0, 4)), else the last Unicode one, scanning backwards.
    /// Platform 0 (any encoding), platform 2 (any encoding) and Windows (3, 1) and (3, 10) are Unicode. A format-14 subtable
    /// maps nothing by itself and is not chosen.
    /// </summary>
    private static Subtable? SelectUnicode(List<Subtable> subtables)
    {
        for (var i = subtables.Count - 1; i >= 0; i--)
        {
            var s = subtables[i];
            if (s.Format != 14 && ((s.Platform == PlatformWindows && s.Encoding == WindowsUnicodeFull) ||
                                   (s.Platform == PlatformUnicode && s.Encoding == UnicodeFull)))
                return s;
        }

        for (var i = subtables.Count - 1; i >= 0; i--)
        {
            var s = subtables[i];
            if (s.Format != 14 && IsUnicode(s.Platform, s.Encoding))
                return s;
        }

        return null;
    }

    private static bool IsUnicode(int platform, int encoding) =>
        platform is PlatformUnicode or PlatformIso ||
        (platform == PlatformWindows && encoding is WindowsUnicodeBmp or WindowsUnicodeFull);

    /// <summary>
    /// Subtables checked in full may share bytes only as the same subtable (one offset, so one declared length), as OTS refuses
    /// "excessive overlap" (Denys's "Rules + work budget", 2026-11-26): otherwise subtables nested at different offsets would
    /// each walk the same bytes. Empty subtables overlap nothing.
    /// </summary>
    private static void CheckOverlaps(List<Subtable> subtables, ParsedTables parsed)
    {
        parsed.Charge(16L * subtables.Count, "the 'cmap' subtable overlap check");
        var sorted = subtables.ToArray();
        Array.Sort(sorted, (a, b) => a.Offset.CompareTo(b.Offset));
        long end = 0;
        Subtable? previous = null;
        foreach (var subtable in sorted)
        {
            if (subtable.Length == 0)
                continue;

            if (subtable.Offset < end)
                throw new InvalidDataException(
                    $"The 'cmap' subtable ({subtable.Platform}, {subtable.Encoding}) format {subtable.Format} ({subtable.Length} " +
                    $"bytes at {subtable.Offset}) overlaps the ({previous!.Value.Platform}, {previous.Value.Encoding}) format " +
                    $"{previous.Value.Format} subtable ({previous.Value.Length} bytes at {previous.Value.Offset}); subtables may " +
                    "share bytes only as the same subtable.");

            if (subtable.Offset + (long)subtable.Length > end)
            {
                end = subtable.Offset + (long)subtable.Length;
                previous = subtable;
            }
        }
    }

    /// <summary>The subtable's declared length, checked to fit the table; -1 for an unknown format, which is skipped.</summary>
    private static int SubtableLength(BigEndianReader r, int offset, int format, int platform, int encoding)
    {
        long length = format switch
        {
            0 or 2 or 4 or 6 => r.U16(offset + 2),
            8 or 10 or 12 or 13 => r.U32(offset + 4),
            14 => r.U32(offset + 2),
            _ => -1,
        };

        if (length < 0)
            return -1;

        if (!r.Contains(offset, length))
            throw new InvalidDataException(
                $"The 'cmap' subtable ({platform}, {encoding}) format {format} declares {length} bytes at {offset}, past the " +
                $"{r.Length}-byte table.");

        return (int)length;
    }

    private static void Validate(BigEndianReader s, Subtable subtable, int glyphCount, ParsedTables parsed)
    {
        var name = $"The 'cmap' subtable ({subtable.Platform}, {subtable.Encoding}) format {subtable.Format}";

        // Each check walks at most the subtable's declared bytes; format 14 charges its records and lists as it meets them.
        if (subtable.Format != 14)
            parsed.Charge(subtable.Length, "checking the 'cmap' subtable at offset", subtable.Offset);

        switch (subtable.Format)
        {
            case 0:
                s.Check(6, 256);
                break;

            case 4:
                ValidateFormat4(s, name);
                break;

            case 6:
                s.Check(10, 2L * s.U16(8));
                break;

            case 10:
                s.Check(20, 2L * s.U32(16));
                break;

            case 12:
            case 13:
                ValidateGroups(s, name);
                break;

            case 14:
                ValidateFormat14(s, name, glyphCount, parsed);
                break;

            // Formats 2 and 8 are not read; their length has been checked.
        }
    }

    private static void ValidateFormat4(BigEndianReader s, string name)
    {
        var segCountX2 = s.U16(6);
        if (segCountX2 == 0 || (segCountX2 & 1) != 0)
            throw new InvalidDataException($"{name} has segCountX2 {segCountX2}; it must be even and not 0.");

        var segments = segCountX2 / 2;
        s.Check(14, 8L * segments + 2);

        var ends = 14;
        var starts = ends + segCountX2 + 2;
        var offsets = starts + 2 * segCountX2;
        var previousStart = -1;
        var previousEnd = -1;

        for (var i = 0; i < segments; i++)
        {
            int start = s.U16(starts + 2 * i);
            int end = s.U16(ends + 2 * i);
            if (start > end)
                throw new InvalidDataException($"{name}: segment {i} starts at U+{start:X4} after its end U+{end:X4}.");

            // A final pair of 0xFFFF-0xFFFF segments is accepted, as FreeType maps it and OTS accepts it (Germania.ttf).
            var finalPair = i == segments - 1 && start == 0xFFFF && previousStart == 0xFFFF;
            if (start <= previousEnd && !finalPair)
                throw new InvalidDataException(
                    $"{name}: segment {i} (U+{start:X4}..U+{end:X4}) is not after the previous segment's end U+{previousEnd:X4}.");

            // The final 0xFFFF-0xFFFF segment "need not contain any valid mappings" (the spec): its idRangeOffset is not
            // checked, as FreeType and OTS accept any, and U+FFFF maps as FreeType maps it (LookupFfff). Any other segment's
            // idRangeOffset 0xFFFF makes FreeType drop the subtable, so it is rejected.
            var rangeOffset = s.U16(offsets + 2 * i);
            var terminator = i == segments - 1 && start == 0xFFFF;
            if (rangeOffset == 0xFFFF && !terminator)
                throw new InvalidDataException(
                    $"{name}: segment {i} (U+{start:X4}..U+{end:X4}) has idRangeOffset 0xFFFF, which only the final " +
                    "0xFFFF-0xFFFF segment may have.");

            previousStart = start;
            if (rangeOffset != 0 && !terminator)
            {
                long glyphIds = offsets + 2 * i + rangeOffset;
                if (glyphIds < offsets + segCountX2)
                    throw new InvalidDataException(
                        $"{name}: segment {i}'s idRangeOffset {rangeOffset} points before the glyph id array.");

                if (!s.Contains(glyphIds, 2L * (end - start + 1)))
                    throw new InvalidDataException(
                        $"{name}: segment {i}'s glyph ids (idRangeOffset {rangeOffset}) run past the subtable's {s.Length} bytes.");
            }

            previousEnd = end;
        }

        if (previousEnd != 0xFFFF)
            throw new InvalidDataException($"{name}: the last segment ends at U+{previousEnd:X4}, not U+FFFF.");
    }

    private static void ValidateGroups(BigEndianReader s, string name)
    {
        var groups = s.U32(12);
        s.Check(16, 12L * groups);

        long previousEnd = -1;
        for (var i = 0; i < groups; i++)
        {
            var at = 16 + 12 * i;
            long start = s.U32(at);
            long end = s.U32(at + 4);
            if (start > end)
                throw new InvalidDataException($"{name}: group {i} starts at 0x{start:X} after its end 0x{end:X}.");

            if (start <= previousEnd)
                throw new InvalidDataException(
                    $"{name}: group {i} (0x{start:X}..0x{end:X}) is not after the previous group's end 0x{previousEnd:X}.");

            previousEnd = end;
        }
    }

    /// <summary>
    /// Format 14: selectors ascending, none 0 or past U+10FFFF; default ranges and non-default mappings ascending and inside
    /// Unicode; non-default glyph ids below the glyph count. A range or mapping list shared by several selectors is checked once.
    /// </summary>
    private static void ValidateFormat14(BigEndianReader s, string name, int glyphCount, ParsedTables parsed)
    {
        var count = s.U32(6);
        s.Check(10, 11L * count);
        parsed.Charge(10 + 11L * count, "the format-14 selector records");

        long previous = -1;
        // A list may serve as default ranges for one selector and as non-default mappings for another: each kind is checked
        // once per offset.
        // Each set holds at most one entry per selector record; an entry with its share of the set's growing arrays is accounted
        // at 48 bytes, before the sets are made.
        parsed.Reserve(2 * (64 + 48L * count), "the format-14 list sets");
        var checkedDefaults = new HashSet<uint>();
        var checkedNonDefaults = new HashSet<uint>();

        // The distinct lists (each kind of each offset once) may not take more bytes in all than the subtable has, as OTS caps
        // them: lists at overlapping offsets would otherwise walk the same bytes again and again.
        long listBytes = 0;
        for (var i = 0; i < count; i++)
        {
            var at = 10 + 11 * i;
            var selector = s.U24(at);
            if (selector == 0 || selector > MaxCodePoint)
                throw new InvalidDataException($"{name}: variation selector U+{selector:X} is 0 or past U+10FFFF.");

            if (selector <= previous)
                throw new InvalidDataException($"{name}: variation selector U+{selector:X} is not after U+{previous:X}.");

            previous = selector;

            var defaultOffset = s.U32(at + 3);
            if (defaultOffset != 0 && checkedDefaults.Add(defaultOffset))
            {
                var ranges = s.U32((int)Math.Min(defaultOffset, int.MaxValue));
                s.Check(defaultOffset + 4L, 4L * ranges);
                listBytes += 4 + 4L * ranges;
                CheckListBytes(s, name, listBytes);
                parsed.Charge(4 + 4L * ranges, "the format-14 default ranges of selector record", i);
                long previousEnd = -1;
                for (var j = 0; j < ranges; j++)
                {
                    var range = (int)defaultOffset + 4 + 4 * j;
                    long start = s.U24(range);
                    if (start <= previousEnd)
                        throw new InvalidDataException(
                            $"{name}: a default range of selector U+{selector:X} starts at U+{start:X}, not after U+{previousEnd:X}.");

                    previousEnd = start + s.U8(range + 3);
                    if (previousEnd > MaxCodePoint)
                        throw new InvalidDataException(
                            $"{name}: a default range of selector U+{selector:X} ends at U+{previousEnd:X}, past U+10FFFF.");
                }
            }

            var nonDefaultOffset = s.U32(at + 7);
            if (nonDefaultOffset != 0 && checkedNonDefaults.Add(nonDefaultOffset))
            {
                var mappings = s.U32((int)Math.Min(nonDefaultOffset, int.MaxValue));
                s.Check(nonDefaultOffset + 4L, 5L * mappings);
                listBytes += 4 + 5L * mappings;
                CheckListBytes(s, name, listBytes);
                parsed.Charge(4 + 5L * mappings, "the format-14 non-default mappings of selector record", i);
                long previousCode = -1;
                for (var j = 0; j < mappings; j++)
                {
                    long code = s.U24((int)nonDefaultOffset + 4 + 5 * j);
                    if (code <= previousCode)
                        throw new InvalidDataException(
                            $"{name}: a non-default mapping of selector U+{selector:X} is for U+{code:X}, not after U+{previousCode:X}.");

                    if (code > MaxCodePoint)
                        throw new InvalidDataException(
                            $"{name}: a non-default mapping of selector U+{selector:X} is for U+{code:X}, past U+10FFFF.");

                    var glyph = s.U16((int)nonDefaultOffset + 4 + 5 * j + 3);
                    if (glyph >= glyphCount)
                        throw GlyphOutOfRange(name, code, glyph, glyphCount);

                    previousCode = code;
                }
            }
        }
    }

    private static void CheckListBytes(BigEndianReader s, string name, long listBytes)
    {
        if (listBytes > s.Length)
            throw new InvalidDataException(
                $"{name}: its default-range and non-default-mapping lists take {listBytes} bytes or more in all, more than the " +
                $"subtable's {s.Length} bytes.");
    }

    /// <summary>The selected subtable maps no code point to a glyph id at or past the glyph count.</summary>
    private static void CheckGlyphIds(BigEndianReader s, Subtable subtable, int glyphCount, ParsedTables parsed)
    {
        var name = $"The 'cmap' subtable ({subtable.Platform}, {subtable.Encoding}) format {subtable.Format}";

        // Format 4 visits every code point of its segments (charged per segment); the others walk at most their bytes.
        if (subtable.Format != 4)
            parsed.Charge(subtable.Length, "checking the glyph ids of the selected 'cmap' subtable");
        switch (subtable.Format)
        {
            case 0:
                for (var code = 0; code < 256; code++)
                {
                    if (s.U8(6 + code) >= glyphCount)
                        throw GlyphOutOfRange(name, code, s.U8(6 + code), glyphCount);
                }

                break;

            case 4:
            {
                var segCountX2 = s.U16(6);
                var ends = 14;
                var starts = ends + segCountX2 + 2;
                var deltas = starts + segCountX2;
                var offsets = deltas + segCountX2;
                for (var i = 0; i < segCountX2 / 2; i++)
                {
                    int start = s.U16(starts + 2 * i);
                    int end = s.U16(ends + 2 * i);
                    var delta = s.I16(deltas + 2 * i);
                    var rangeOffset = s.U16(offsets + 2 * i);

                    // The terminator (or the final pair of them) is exempt: U+FFFF maps as FreeType maps it, past the glyph
                    // count to 0. Only the final segments can start at 0xFFFF.
                    if (start == 0xFFFF)
                        continue;

                    parsed.Charge(end - start + 1, "checking the glyph ids of the selected 'cmap' subtable, segment", i);
                    for (var code = start; code <= end; code++)
                    {
                        int glyph;
                        if (rangeOffset == 0)
                        {
                            glyph = (code + delta) & 0xFFFF;
                        }
                        else
                        {
                            glyph = s.U16(offsets + 2 * i + rangeOffset + 2 * (code - start));
                            if (glyph != 0)
                                glyph = (glyph + delta) & 0xFFFF;
                        }

                        if (glyph >= glyphCount)
                            throw GlyphOutOfRange(name, code, glyph, glyphCount);
                    }
                }

                break;
            }

            case 6:
            case 10:
            {
                var (first, count, array) = subtable.Format == 6 ? (s.U16(6), (uint)s.U16(8), 10) : (s.U32(12), s.U32(16), 20);
                for (var i = 0; i < count; i++)
                {
                    var glyph = s.U16(array + 2 * i);
                    if (glyph >= glyphCount)
                        throw GlyphOutOfRange(name, first + i, glyph, glyphCount);
                }

                break;
            }

            case 12:
            case 13:
            {
                var groups = s.U32(12);
                for (var i = 0; i < groups; i++)
                {
                    var at = 16 + 12 * i;
                    long start = s.U32(at);
                    long end = s.U32(at + 4);
                    long first = s.U32(at + 8);

                    // Format 12 maps a run from the first id; format 13 maps the whole group to it.
                    var last = subtable.Format == 12 ? first + (end - start) : first;
                    if (last >= glyphCount)
                        throw GlyphOutOfRange(name, start, last, glyphCount);
                }

                break;
            }
        }
    }

    private static InvalidDataException GlyphOutOfRange(string name, long code, long glyph, int glyphCount) =>
        new($"{name} maps U+{code:X4} into glyph ids up to {glyph}; the font has {glyphCount} glyphs.");

    /// <summary>
    /// U+FFFF in a format-4 subtable, as FreeType 2.13.2's <c>tt_cmap4_char_map_binary</c> maps it: with a final pair of
    /// 0xFFFF-0xFFFF segments (which FreeType's validator flags as overlapping), its search settles on the first of the pair,
    /// read the usual way; with one terminator, <see cref="LookupTerminator"/>; otherwise the usual search.
    /// </summary>
    private int LookupFfff(BigEndianReader r)
    {
        var segCountX2 = r.U16(6);
        var starts = 14 + segCountX2 + 2;
        var last = segCountX2 / 2 - 1;
        if (r.U16(starts + 2 * last) != 0xFFFF)
            return LookupFormat4(r, 0xFFFF);

        return last > 0 && r.U16(starts + 2 * (last - 1)) == 0xFFFF
            ? LookupSegment(r, last - 1, 0xFFFF)
            : LookupTerminator(r);
    }

    /// <summary>
    /// U+FFFF through the terminator, as FreeType 2.13.2's <c>tt_cmap4_char_map_binary</c> maps it: an idRangeOffset that would
    /// read past the <c>cmap</c> table becomes delta 1 and offset 0; an offset of 0xFFFF means no glyph; otherwise the usual
    /// rules, with the glyph id read anywhere inside the <c>cmap</c> table.
    /// </summary>
    private int LookupTerminator(BigEndianReader r)
    {
        var segCountX2 = r.U16(6);
        var last = segCountX2 - 2;
        var deltaAt = 14 + segCountX2 + 2 + segCountX2 + last;
        var offsetAt = deltaAt + segCountX2;
        int delta = r.I16(deltaAt);
        int rangeOffset = r.U16(offsetAt);

        // Positions in the whole cmap table.
        long absoluteOffsetField = m_offset + offsetAt;
        if (rangeOffset != 0 && absoluteOffsetField + rangeOffset + 2 > m_data.Length)
        {
            delta = 1;
            rangeOffset = 0;
        }

        if (rangeOffset == 0xFFFF)
            return 0;

        if (rangeOffset == 0)
            return (0xFFFF + delta) & 0xFFFF;

        var table = new BigEndianReader(m_data.Span, "cmap");
        int glyph = table.U16((int)(absoluteOffsetField + rangeOffset));
        return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
    }

    private static int LookupFormat4(BigEndianReader r, int codePoint)
    {
        if (codePoint > 0xFFFF)
            return 0;

        var segCountX2 = r.U16(6);
        var segments = segCountX2 / 2;
        var ends = 14;
        var starts = ends + segCountX2 + 2;
        var deltas = starts + segCountX2;
        var offsets = deltas + segCountX2;

        int low = 0, high = segments;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            int end = r.U16(ends + 2 * mid);
            if (codePoint > end)
            {
                low = mid + 1;
                continue;
            }

            int start = r.U16(starts + 2 * mid);
            if (codePoint < start)
            {
                high = mid;
                continue;
            }

            return LookupSegment(r, mid, codePoint);
        }

        return 0;
    }

    /// <summary>A code point inside format-4 segment <paramref name="segment"/>, mapped the usual way.</summary>
    private static int LookupSegment(BigEndianReader r, int segment, int codePoint)
    {
        var segCountX2 = r.U16(6);
        var starts = 14 + segCountX2 + 2;
        var deltas = starts + segCountX2;
        var offsets = deltas + segCountX2;

        int start = r.U16(starts + 2 * segment);
        var delta = r.I16(deltas + 2 * segment);
        var rangeOffset = r.U16(offsets + 2 * segment);
        if (rangeOffset == 0)
            return (codePoint + delta) & 0xFFFF;

        int glyph = r.U16(offsets + 2 * segment + rangeOffset + 2 * (codePoint - start));
        return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
    }

    /// <summary>Formats 6 and 10: one run of 16-bit glyph ids from a first code.</summary>
    private static int LookupTrimmed(BigEndianReader r, uint codePoint, uint first, uint count, int arrayOffset)
    {
        var index = codePoint - first;
        return index < count ? r.U16(arrayOffset + 2 * (int)index) : 0;
    }

    /// <summary>Formats 12 (a run of glyphs per group) and 13 (one glyph per group).</summary>
    private static int LookupGroups(BigEndianReader r, uint codePoint, bool manyToOne)
    {
        int low = 0, high = (int)r.U32(12);
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            var at = 16 + 12 * mid;
            var start = r.U32(at);
            if (codePoint < start)
            {
                high = mid;
                continue;
            }

            if (codePoint > r.U32(at + 4))
            {
                low = mid + 1;
                continue;
            }

            // FreeType computes the glyph in 32 bits and then compares it with the glyph count.
            var glyph = manyToOne ? r.U32(at + 8) : unchecked(r.U32(at + 8) + (codePoint - start));
            return glyph > int.MaxValue ? -1 : (int)glyph;
        }

        return 0;
    }

    private static bool InDefaultRanges(BigEndianReader r, int offset, uint codePoint)
    {
        int low = 0, high = (int)r.U32(offset);
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            var at = offset + 4 + 4 * mid;
            var start = r.U24(at);
            if (codePoint < start)
                high = mid;
            else if (codePoint > start + r.U8(at + 3))
                low = mid + 1;
            else
                return true;
        }

        return false;
    }

    private static int LookupNonDefault(BigEndianReader r, int offset, uint codePoint)
    {
        int low = 0, high = (int)r.U32(offset);
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            var at = offset + 4 + 5 * mid;
            var code = r.U24(at);
            if (codePoint < code)
                high = mid;
            else if (codePoint > code)
                low = mid + 1;
            else
                return r.U16(at + 3);
        }

        return 0;
    }

    private readonly record struct Subtable(int Platform, int Encoding, int Format, int Offset, int Length);
}
