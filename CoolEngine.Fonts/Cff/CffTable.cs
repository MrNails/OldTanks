// What the load reads follows FreeType 2.13.2 src/cff/cffload.c (cff_font_load, cff_subfont_load, cff_charset_load,
// cff_encoding_load, CFF_Load_FD_Select, cff_index_init, cff_vstore_load), and PrivateDictAt its cff_load_private_dict as
// src/psaux/psfont.c (cf2_font_setup) calls it when a blend vector changes (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Variations;

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// A checked <c>CFF </c> or <c>CFF2</c> table: everything its glyphs' charstrings read (CharStrings, global and local
/// subroutines, the Font DICT of each glyph, the font matrices, the charset seac reads, the variation store CFF2's
/// <c>blend</c> reads), parsed and checked when the font loads. The charstrings themselves run at glyph load
/// (<see cref="Type2Interpreter"/>). Immutable; shared by the faces of a file that point at it.
/// </summary>
/// <remarks>
/// <para>
/// Safety-strict, as <see cref="SfntReader"/>: whatever affects what the glyph loader reads, or the bounds it reads within,
/// is checked here and throws <see cref="InvalidDataException"/> at load. The rules:
/// a CFF header shorter than 4 bytes, its major version not 1, its header size below 4 or its offset size above 4; a CFF2
/// header shorter than 5 bytes, its major version not 2, its header size below 5, its Top DICT past the table;
/// an INDEX (Name, Top DICT, String, global Subrs, CharStrings, FDArray, local Subrs) whose header, offset array or data
/// runs past the table, whose offset size is not 1 to 4, whose first offset is not 1, or whose offsets decrease;
/// a Name INDEX of more than one name (one font per table in OpenType); a Top DICT INDEX with no entry;
/// a DICT holding the reserved operand byte 27, more operands before an operator than FreeType's DICT stack holds (95 in
/// CFF, 512 in CFF2), or ending inside a two-byte operator;
/// an entry present twice in one DICT where OTS refuses the repeat (its <c>cff.cc</c>): CharStrings, FDArray, FDSelect, ROS
/// and vstore in the Top DICT, a second charset entry pointing at a charset of the font's own (not a predefined one), and
/// Subrs in the Private DICT of a table without an FDArray;
/// an entry this library reads (CharStrings, charset, Encoding, Private, FontMatrix, CharstringType, ROS, FDArray, FDSelect,
/// vstore, Subrs, initialRandomSeed, vsindex) with the wrong number or kind of operands in any of its occurrences (FreeType
/// parses every occurrence and fails the face on a malformed one, though the last supplies the value)
/// (one integer; Private two non-negative integers; FontMatrix six numbers; ROS three integers whose registry and ordering are
/// SIDs below 65,000); an offset entry that is negative or points past the table, or a Private DICT that runs past it;
/// a CharstringType other than 2; no CharStrings entry, or a CharStrings INDEX whose count differs from <c>maxp.numGlyphs</c>;
/// a charset of a format other than 0, 1 and 2, past the table, with a range running past SID 65,535, or a predefined charset
/// (ISOAdobe, Expert, Expert Subset) smaller than the glyph count; an encoding of a format other than 0 and 1 (its high bit
/// marks supplements), or whose codes, ranges or supplements run past the table;
/// a CID-keyed table (ROS) or a CFF2 table without an FDArray, or with an empty one; a Font DICT of a CID-keyed table with
/// its own ROS; a CID-keyed table without an FDSelect, or a CFF2 table of several Font DICTs without one; an FDSelect of a
/// format other than 0 and 3 (and 4 in CFF2), past the table, with no ranges, a first range not starting at glyph 0, ranges
/// not ascending, a sentinel below the glyph count, or a Font DICT index not below the FDArray's count;
/// a CFF2 variation store as <see cref="ItemVariationStore"/> lists; a CFF2 Private DICT whose <c>blend</c> names a
/// <c>vsindex</c> the store lacks, counts its values with anything but an integer, blends more values than the stack holds
/// or has fewer operands than its values need, or that sets <c>vsindex</c> after a <c>blend</c>;
/// any entry FreeType 2.13.2's parser knows for the DICT's kind (its <c>cfftoken.h</c> field tables for the CFF Top and
/// CID Font DICTs, the CFF Private DICT, the CFF2 Top, Font and Private DICTs) given fewer operands than its handler pops: at
/// least one for every entry but a delta array, four for FontBBox; FreeType's parser fails there (<c>Stack_Underflow</c>),
/// and with it the face (OT-4 WP6, <see cref="CffDict.CheckOperandCounts"/>; the entries this library reads keep their own,
/// stricter rules above, checked first).
/// </para>
/// <para>
/// Accepted, as FreeType accepts them (each tested to read as FreeType reads it): any other entry this library reads
/// present twice in one DICT (FreeType and OTS accept the repeat; the last one is read, as FreeType reads it, each one's
/// operands checked: Encoding, Private, FontMatrix, CharstringType, initialRandomSeed, a Font DICT's Private and its Private
/// DICT's Subrs, vsindex, and a predefined charset repeated); a 5-byte integer operand (29) with its top bit set, read
/// unsigned, as FreeType's 64-bit <c>cff_parse_integer</c> reads it (an offset of that size then lies past the table);
/// operands after a DICT's last operator, an integer or real cut by the end of the DICT among them (ignored); DICT operators
/// this library does not read, unknown ones (one- and two-byte, the reserved bytes 22 to 26, 31 and 255 among them) included,
/// whatever their operands once FreeType's operand counts are met (ignored, their operands dropped); a Private entry of size or offset 0 (no Private DICT: no local subroutines); charset ranges covering
/// more glyphs than the font has (the extra ones are ignored); an FDSelect sentinel above the glyph count; the FDSelect of a
/// CFF2 table with one Font DICT (not read: every glyph uses Font DICT 0).
/// </para>
/// <para>
/// Ours works where FreeType fails (Denys's "Ours works, documented", each tested to fail in FreeType and to work here): a
/// CFF2 FDSelect of format 4, which FreeType 2.13.2 does not read (<c>CFF_Load_FD_Select</c> fails the face).
/// </para>
/// <para>
/// <see cref="NotSupportedException"/>: a Multiple Master CFF (a MultipleMaster entry; FreeType does not support them
/// either); an FDArray of more than 256 Font DICTs (FreeType's <c>CFF_MAX_CID_FONTS</c>; FreeType then silently ignores the
/// FDArray). And the file's parsed-memory and work budgets of <see cref="ParsedTables"/>.
/// </para>
/// <para>
/// Not checked, as nothing here reads them: the Name and String INDEXes beyond their structure, Top DICT INDEX entries past
/// the first, the operands of DICT entries this library does not read beyond their count (the hinting entries of a Private
/// DICT are read as FreeType reads them, any operand kind, only the first operand of a number entry), the code-to-glyph
/// mapping of an encoding (OpenType maps characters through <c>cmap</c>), the deltas of a CFF2 variation store, and the
/// charstrings, which run at glyph load.
/// </para>
/// <para>
/// The load's work: each INDEX's offsets, each DICT, the charset, encoding and FDSelect bytes and each glyph they fill are
/// charged to the file's work budget before they are walked, and every array sized from the table is reserved against its
/// parsed-memory budget before it is allocated. Private DICTs and their local Subrs INDEXes are parsed once per distinct
/// offset, however many Font DICTs point at them.
/// </para>
/// </remarks>
internal sealed class CffTable
{
    /// <summary>FreeType's limit on Font DICTs (<c>CFF_MAX_CID_FONTS</c>, <c>cfftypes.h</c>).</summary>
    public const int MaxFontDicts = 256;

    private const int OpCharset = 15;
    private const int OpEncoding = 16;
    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpSubrs = 19;
    private const int OpVstore = 24;
    private const int OpCharstringType = 0x106;
    private const int OpFontMatrix = 0x107;
    private const int OpInitialRandomSeed = 0x113;
    private const int OpMultipleMaster = 0x118;
    private const int OpRos = 0x11E;
    private const int OpFdArray = 0x124;
    private const int OpFdSelect = 0x125;

    private const int MaxSid = 64999;

    // FreeType's replacement for an initialRandomSeed of 0 (cff_load_private_dict).
    private const int DefaultRandomSeed = 987654321;

    // What a Font DICT keeps: its record (local Subrs reference, vsindex, seed, matrix) and its slot in the FDArray.
    private const int FontDictCost = 96;

    private readonly byte[]? m_fdSelect;

    private CffTable(ReadOnlyMemory<byte> data, bool isCff2, CffIndex charStrings, CffIndex globalSubrs, CffFontDict top,
        CffFontDict[] fontDicts, byte[]? fdSelect, ushort[]? sids, ItemVariationStore variationStore)
    {
        Data = data;
        IsCff2 = isCff2;
        CharStrings = charStrings;
        GlobalSubrs = globalSubrs;
        GlobalBias = Bias(globalSubrs.Count);
        Top = top;
        FontDicts = fontDicts;
        m_fdSelect = fdSelect;
        Sids = sids;
        VariationStore = variationStore;
    }

    /// <summary>The table's bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    public bool IsCff2 { get; }

    public CffIndex CharStrings { get; }

    public CffIndex GlobalSubrs { get; }

    /// <summary>The global subroutine bias (FreeType's <c>cff_compute_bias</c> for Type 2 charstrings).</summary>
    public int GlobalBias { get; }

    /// <summary>The Top DICT: its font matrix, and for a table without an FDArray, its Private DICT and local subroutines.</summary>
    public CffFontDict Top { get; }

    /// <summary>
    /// The FDArray's Font DICTs of a CID-keyed or CFF2 table (FreeType's subfonts); empty otherwise, when every glyph uses
    /// <see cref="Top"/>.
    /// </summary>
    public CffFontDict[] FontDicts { get; }

    /// <summary>The charset's SID (CID in a CID-keyed table) of each glyph; null in CFF2, which has no charset.</summary>
    public ushort[]? Sids { get; }

    /// <summary>The CFF2 variation store; <see cref="ItemVariationStore.Empty"/> when there is none.</summary>
    public ItemVariationStore VariationStore { get; }

    /// <summary>The Font DICT a glyph's charstring runs with: the FDSelect's choice, or the only one.</summary>
    public CffFontDict FontDictOf(int glyphIndex) => FontDicts.Length == 0 ? Top : FontDicts[FontDictIndexOf(glyphIndex)];

    /// <summary>The index in <see cref="FontDicts"/> of the glyph's Font DICT; 0 when the table has none or only one.</summary>
    public int FontDictIndexOf(int glyphIndex) => m_fdSelect is null ? 0 : m_fdSelect[glyphIndex];

    /// <summary>FreeType's <c>cff_compute_bias</c> for Type 2 charstrings.</summary>
    public static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    /// <summary>
    /// A CFF2 Private DICT's entries at normalized coordinates (OT-4 WP5), as FreeType's <c>cff_load_private_dict</c> parses it
    /// again when a size's blend vector changes: each <c>blend</c> result weighed at <paramref name="coords"/> (a 16.16 value
    /// kept in 32 bits, <see cref="DictOperandKind.Blended"/>). Only the stem-hint values change (for OT-4 WP6): the local
    /// subroutines and <c>vsindex</c> stay those read at load, as FreeType keeps them. Empty for a Font DICT without a Private DICT.
    /// </summary>
    /// <param name="fontDictIndex">The Font DICT (an index of <see cref="FontDicts"/>).</param>
    /// <param name="coords">The normalized coordinates, one per axis of the variation store.</param>
    public List<DictEntry> PrivateDictAt(int fontDictIndex, ReadOnlySpan<long> coords)
    {
        var fontDict = FontDicts.Length == 0 ? Top : FontDicts[fontDictIndex];
        if (!IsCff2 || fontDict.PrivateSize == 0)
            return [];

        // The DICT was checked at load with the same blends; its work is bounded by its size times the store's axes (each blend
        // builds a vector of its regions over every axis).
        var bytes = Data.Span.Slice(fontDict.PrivateOffset, fontDict.PrivateSize);
        return CffDict.Parse(bytes, true, $"Private DICT at {fontDict.PrivateOffset}", new ParsedTables(bytes.Length, unlimited: true),
            VariationStore, coords);
    }

    /// <summary>
    /// The work <see cref="PrivateHintsAt"/> is charged for a Font DICT (OT-4 WP6): the Private DICT's size times the variation
    /// store's regions plus one, a bound on its tokens and the blend weights its blends compute; 0 without a Private DICT.
    /// </summary>
    public long PrivateReparseCost(int fontDictIndex)
    {
        var fontDict = FontDicts.Length == 0 ? Top : FontDicts[fontDictIndex];
        return (long)fontDict.PrivateSize * (VariationStore.RegionCount + 1);
    }

    /// <summary>
    /// A CFF2 Font DICT's hinting values at normalized coordinates (OT-4 WP6): <see cref="PrivateDictAt"/>'s entries as
    /// FreeType's <c>cff_load_private_dict</c> stores them; FreeType's zeroed values for a Font DICT without a Private DICT.
    /// </summary>
    /// <param name="fontDictIndex">The Font DICT (an index of <see cref="FontDicts"/>).</param>
    /// <param name="coords">The normalized coordinates, one per axis of the variation store.</param>
    public Hinting.Cff.CffPrivateHints PrivateHintsAt(int fontDictIndex, ReadOnlySpan<long> coords)
    {
        var fontDict = FontDicts.Length == 0 ? Top : FontDicts[fontDictIndex];
        if (!IsCff2 || fontDict.PrivateSize == 0)
            return fontDict.Hints;

        var bytes = Data.Span.Slice(fontDict.PrivateOffset, fontDict.PrivateSize);
        return Hinting.Cff.CffPrivateHints.Parse(bytes, PrivateDictAt(fontDictIndex, coords), true, "CFF2",
            $"Private DICT at {fontDict.PrivateOffset}");
    }

    /// <summary>Parses and checks the table.</summary>
    /// <param name="data">The <c>CFF </c> or <c>CFF2</c> table.</param>
    /// <param name="isCff2">The table is <c>CFF2</c>.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>, which the CharStrings count must equal.</param>
    /// <param name="parsed">The file's load state: its budgets.</param>
    /// <exception cref="InvalidDataException">The table breaks a rule listed in the remarks.</exception>
    /// <exception cref="NotSupportedException">The table is a Multiple Master CFF, has more than 256 Font DICTs, or passes a budget.</exception>
    public static CffTable Parse(ReadOnlyMemory<byte> data, bool isCff2, int glyphCount, ParsedTables parsed)
    {
        var span = data.Span;
        ReadOnlySpan<byte> topDict;
        CffIndex globalSubrs;

        if (!isCff2)
        {
            if (span.Length < 4)
                throw new InvalidDataException($"The CFF table is {span.Length} bytes long, shorter than its 4-byte header.");

            if (span[0] != 1)
                throw new InvalidDataException($"The CFF table's major version is {span[0]}, not 1.");

            if (span[2] < 4)
                throw new InvalidDataException($"The CFF table's header size is {span[2]}, less than 4.");

            if (span[3] > 4)
                throw new InvalidDataException($"The CFF table's header offset size is {span[3]}, more than 4.");

            var names = CffIndex.Read(span, span[2], false, "Name INDEX", parsed);
            if (names.Count > 1)
                throw new InvalidDataException($"The CFF table's Name INDEX holds {names.Count} fonts; an OpenType CFF table holds one.");

            var topDicts = CffIndex.Read(span, names.End, false, "Top DICT INDEX", parsed);
            if (topDicts.Count == 0)
                throw new InvalidDataException("The CFF table's Top DICT INDEX is empty.");

            var strings = CffIndex.Read(span, topDicts.End, false, "String INDEX", parsed);
            globalSubrs = CffIndex.Read(span, strings.End, false, "global Subrs INDEX", parsed);
            topDict = topDicts.Get(span, 0);
        }
        else
        {
            if (span.Length < 5)
                throw new InvalidDataException($"The CFF2 table is {span.Length} bytes long, shorter than its 5-byte header.");

            if (span[0] != 2)
                throw new InvalidDataException($"The CFF2 table's major version is {span[0]}, not 2.");

            if (span[2] < 5)
                throw new InvalidDataException($"The CFF2 table's header size is {span[2]}, less than 5.");

            var topLength = span[3] << 8 | span[4];
            if (span[2] + topLength > span.Length)
                throw new InvalidDataException(
                    $"The CFF2 table's Top DICT ({topLength} bytes at {span[2]}) runs past the table's {span.Length} bytes.");

            topDict = span.Slice(span[2], topLength);
            globalSubrs = CffIndex.Read(span, span[2] + topLength, true, "global Subrs INDEX", parsed);
        }

        var name = isCff2 ? "CFF2" : "CFF";
        var top = new DictValues(topDict, CffDict.Parse(topDict, isCff2, "Top DICT", parsed, null), "Top DICT", name);

        // CFF2's Top DICT knows neither entry (FreeType ignores them there).
        if (!isCff2 && top.Has(OpMultipleMaster))
            throw new NotSupportedException("The CFF table is a Multiple Master font, which is not supported.");

        // FreeType keeps CharstringType in a 32-bit field.
        if (!isCff2 && top.Find(OpCharstringType) is { } type && (int)top.Integer(type, "CharstringType") != 2)
            throw new InvalidDataException(
                $"The CFF table's CharstringType is {(int)top.Integer(type, "CharstringType")}; OpenType CFF charstrings are Type 2.");

        var charStringsOffset = top.Offset(OpCharStrings, "CharStrings", span.Length, once: true) ??
                                throw new InvalidDataException($"The {name} table's Top DICT has no CharStrings entry.");
        if (charStringsOffset == 0)
            throw new InvalidDataException($"The {name} table's Top DICT has a CharStrings offset of 0.");

        var charStrings = CffIndex.Read(span, charStringsOffset, isCff2, "CharStrings INDEX", parsed);
        if (charStrings.Count != glyphCount)
            throw new InvalidDataException(
                $"The {name} table's CharStrings INDEX holds {charStrings.Count} glyphs, but 'maxp' counts {glyphCount}.");

        var isCid = false;
        if (!isCff2 && top.FindOnce(OpRos) is { } ros)
        {
            top.Ros(ros);
            isCid = true;
        }

        var topFont = top.FontMatrixOf();
        var variationStore = ItemVariationStore.Empty;
        if (isCff2 && top.Offset(OpVstore, "vstore", span.Length, once: true) is { } vstore && vstore != 0)
        {
            if (vstore + 2L > span.Length)
                throw new InvalidDataException($"The CFF2 table's vstore entry ({vstore}) leaves no room for the store's length field.");

            variationStore = ItemVariationStore.Parse(span, vstore + 2, parsed);
        }

        var privates = new Dictionary<(int Offset, int Size), CffFontDict>();
        var subrs = new Dictionary<int, CffIndex>();
        ushort[]? sids = null;
        CffFontDict[] fontDicts = [];
        byte[]? fdSelect = null;
        var topFontDict = topFont;

        if (!isCff2)
        {
            sids = ReadCharset(span, top.Charset(OpCharset, span.Length), glyphCount, parsed);
            if (!isCid)
            {
                CheckEncoding(span, top.Offset(OpEncoding, "Encoding", span.Length) ?? 0, parsed);
                topFontDict = ReadPrivate(span, top, topFont, isCff2, null, privates, subrs, parsed, topLevel: true);
            }
        }

        if (isCid || isCff2)
        {
            var fdArrayOffset = top.Offset(OpFdArray, "FDArray", span.Length, once: true);
            if (fdArrayOffset is null or 0)
                throw new InvalidDataException(isCff2
                    ? "The CFF2 table's Top DICT has no FDArray entry."
                    : "The CFF table is CID-keyed (it has an ROS entry) but has no FDArray entry.");

            var fdArray = CffIndex.Read(span, fdArrayOffset.Value, isCff2, "FDArray INDEX", parsed);
            if (fdArray.Count == 0)
                throw new InvalidDataException($"The {name} table's FDArray is empty.");

            if (fdArray.Count > MaxFontDicts)
                throw new NotSupportedException(
                    $"The {name} table has {fdArray.Count} Font DICTs, more than the {MaxFontDicts} this library (as FreeType) supports.");

            parsed.Reserve((long)FontDictCost * fdArray.Count, "the Font DICTs of the FDArray of", fdArray.Count);
            fontDicts = new CffFontDict[fdArray.Count];
            for (var i = 0; i < fontDicts.Length; i++)
            {
                var what = $"Font DICT {i}";
                var fdBytes = fdArray.Get(span, i);
                var fd = new DictValues(fdBytes, CffDict.Parse(fdBytes, isCff2, what, parsed, null), what, name);
                if (!isCff2 && fd.Has(OpRos))
                    throw new InvalidDataException($"The CFF table's {what} has an ROS entry; only the Top DICT may.");

                fontDicts[i] = ReadPrivate(span, fd, fd.FontMatrixOf(), isCff2, variationStore, privates, subrs, parsed, topLevel: false);

                // FreeType reads a CID-keyed CFF table's Font DICTs with its Top DICT fields.
                fd.CheckOperandCounts(isCff2 ? CffDictKind.Cff2Font : CffDictKind.CffTop);
            }

            if (!isCff2 || fontDicts.Length > 1)
            {
                var fdSelectOffset = top.Offset(OpFdSelect, "FDSelect", span.Length, once: true);
                if (fdSelectOffset is null or 0)
                    throw new InvalidDataException(isCff2
                        ? $"The CFF2 table has {fontDicts.Length} Font DICTs but no FDSelect."
                        : "The CFF table is CID-keyed but has no FDSelect.");

                fdSelect = ReadFdSelect(span, fdSelectOffset.Value, glyphCount, fontDicts.Length, isCff2, parsed);
            }
        }

        // The entries this library does not read still need the operands FreeType's handlers pop (after the checks of the
        // entries it reads, whose own rules come first).
        top.CheckOperandCounts(isCff2 ? CffDictKind.Cff2Top : CffDictKind.CffTop);

        return new CffTable(data, isCff2, charStrings, globalSubrs, topFontDict, fontDicts, fdSelect, sids, variationStore);
    }

    /// <summary>
    /// The Font DICT with its Private DICT and local subroutines (FreeType's <c>cff_subfont_load</c>); a Private DICT (and a
    /// Subrs INDEX) shared by several Font DICTs is parsed once.
    /// </summary>
    /// <param name="topLevel">The Top DICT's own Private DICT (a table without an FDArray), whose repeated Subrs OTS refuses.</param>
    private static CffFontDict ReadPrivate(ReadOnlySpan<byte> span, DictValues dict, CffFontDict fontMatrix, bool isCff2,
        IBlendRegions? blend, Dictionary<(int, int), CffFontDict> privates, Dictionary<int, CffIndex> subrs, ParsedTables parsed,
        bool topLevel)
    {
        // Without a Private DICT (FreeType: no Private entry, or one of size or offset 0): no local subroutines, and FreeType's
        // random state (with its "random-seed" property 0) stays 0.
        if (dict.Find(OpPrivate) is not { } operands)
            return fontMatrix;

        var (privateSize, privateOffset) = dict.Private(operands);
        if (privateSize == 0 || privateOffset == 0)
            return fontMatrix;

        if (privateOffset + privateSize > span.Length)
            throw new InvalidDataException(
                $"The {dict.Table} table's Private DICT of {dict.What} ({privateSize} bytes at {privateOffset}) runs past the table's {span.Length} bytes.");

        var size = (int)privateSize;
        var offset = (int)privateOffset;

        if (!privates.TryGetValue((offset, size), out var shared))
        {
            var what = $"Private DICT at {offset}";
            var bytes = span.Slice(offset, size);
            var values = new DictValues(bytes, CffDict.Parse(bytes, isCff2, what, parsed, isCff2 ? blend ?? ItemVariationStore.Empty : null),
                what, dict.Table);

            var localSubrs = CffIndex.Empty;
            if ((topLevel ? values.FindOnce(OpSubrs) : values.Find(OpSubrs)) is { } subrsOperands)
            {
                var relative = values.Integer(subrsOperands, "Subrs");
                if (relative < 0 || offset + relative > span.Length)
                    throw new InvalidDataException(
                        $"The {dict.Table} table's {what} has a Subrs offset of {relative}, outside the table.");

                if (relative != 0)
                {
                    var at = (int)(offset + relative);
                    if (subrs.TryGetValue(at, out var cached))
                    {
                        localSubrs = cached;
                    }
                    else
                    {
                        localSubrs = CffIndex.Read(span, at, isCff2, "local Subrs INDEX", parsed);
                        subrs.Add(at, localSubrs);
                    }
                }
            }

            var vsindex = 0u;
            if (isCff2 && values.Find(CffDict.OpVsindex) is { } vsindexOperands)
                vsindex = (uint)values.Integer(vsindexOperands, "vsindex");

            var seed = 0L;
            if (!isCff2)
            {
                seed = values.Find(OpInitialRandomSeed) is { } seedOperands ? values.Integer(seedOperands, "initialRandomSeed") : 0;

                // FreeType's sanitising (cff_load_private_dict): a positive seed.
                seed = seed < 0 ? -seed : seed == 0 ? DefaultRandomSeed : seed;
            }

            // FreeType's state is the seed's low 32 bits (FT_UInt32). The hinting values (OT-4 WP6) as cff_load_private_dict
            // leaves them, their arrays reserved first.
            parsed.Reserve(Hinting.Cff.CffPrivateHints.Cost, "the hinting values of the CFF Private DICT at", offset);
            shared = new CffFontDict(localSubrs, vsindex, (uint)seed)
            {
                PrivateOffset = offset,
                PrivateSize = size,
                Hints = values.Hints(isCff2),
            };
            privates.Add((offset, size), shared);
        }

        return shared with
        {
            HasFontMatrix = fontMatrix.HasFontMatrix,
            Matrix = fontMatrix.Matrix,
            OffsetX = fontMatrix.OffsetX,
            OffsetY = fontMatrix.OffsetY,
            UnitsPerEm = fontMatrix.UnitsPerEm,
        };
    }

    /// <summary>FreeType's <c>cff_charset_load</c>: the SID of each glyph, checked.</summary>
    private static ushort[] ReadCharset(ReadOnlySpan<byte> span, int offset, int glyphCount, ParsedTables parsed)
    {
        parsed.Reserve(32 + 2L * glyphCount, "the CFF charset of", glyphCount);
        parsed.Charge(glyphCount, "the CFF charset of", glyphCount);
        var sids = new ushort[glyphCount];

        if (offset <= 2)
        {
            var predefined = offset switch
            {
                0 => ReadOnlySpan<ushort>.Empty,
                1 => CffPredefined.ExpertCharset,
                _ => CffPredefined.ExpertSubsetCharset,
            };
            var (count, label) = offset switch
            {
                0 => (CffPredefined.IsoAdobeCount, "ISOAdobe"),
                1 => (predefined.Length, "Expert"),
                _ => (predefined.Length, "Expert Subset"),
            };

            if (glyphCount > count)
                throw new InvalidDataException(
                    $"The CFF table's predefined {label} charset has {count} glyphs, fewer than the font's {glyphCount}.");

            for (var i = 0; i < glyphCount; i++)
                sids[i] = offset == 0 ? (ushort)i : predefined[i];

            return sids;
        }

        var r = new BigEndianReader(span, "CFF ");
        var format = r.U8(offset);
        var at = offset + 1;
        switch (format)
        {
            case 0:
                if (!r.Contains(at, 2L * (glyphCount - 1)))
                    throw new InvalidDataException($"The CFF table's format-0 charset at {offset} runs past the table.");

                parsed.Charge(2L * glyphCount, "the CFF charset of", glyphCount);
                for (var j = 1; j < glyphCount; j++)
                    sids[j] = r.U16(at + 2 * (j - 1));

                break;

            case 1:
            case 2:
            {
                var rangeLength = format == 1 ? 3 : 4;
                var j = 1;
                while (j < glyphCount)
                {
                    if (!r.Contains(at, rangeLength))
                        throw new InvalidDataException($"The CFF table's format-{format} charset at {offset} runs past the table.");

                    parsed.Charge(rangeLength, "the CFF charset range at", at);
                    int sid = r.U16(at);
                    int left = format == 1 ? r.U8(at + 2) : r.U16(at + 2);
                    at += rangeLength;
                    if (sid + left > 0xFFFF)
                        throw new InvalidDataException(
                            $"The CFF table's charset has a range of {left + 1} SIDs from {sid}, past SID 65,535.");

                    for (var i = 0; j < glyphCount && i <= left; i++, j++)
                        sids[j] = (ushort)(sid + i);
                }

                break;
            }

            default:
                throw new InvalidDataException($"The CFF table's charset has format {format}; only 0, 1 and 2 exist.");
        }

        return sids;
    }

    /// <summary>FreeType's <c>cff_encoding_load</c>, structure only: the encoding's codes are never read (OpenType maps through <c>cmap</c>).</summary>
    private static void CheckEncoding(ReadOnlySpan<byte> span, int offset, ParsedTables parsed)
    {
        if (offset <= 1)
            return;

        var r = new BigEndianReader(span, "CFF ");
        if (!r.Contains(offset, 2))
            throw new InvalidDataException($"The CFF table's encoding at {offset} runs past the table.");

        var format = r.U8(offset);
        int count = r.U8(offset + 1);
        long length = (format & 0x7F) switch
        {
            0 => count,
            1 => 2L * count,
            _ => throw new InvalidDataException($"The CFF table's encoding has format {format & 0x7F}; only 0 and 1 exist."),
        };

        var at = offset + 2L;
        if (!r.Contains(at, length))
            throw new InvalidDataException($"The CFF table's encoding at {offset} runs past the table.");

        at += length;
        if ((format & 0x80) != 0)
        {
            // Supplements: a count, then a code and a SID each.
            if (!r.Contains(at, 1) || !r.Contains(at + 1, 3L * r.U8((int)at)))
                throw new InvalidDataException($"The CFF table's encoding at {offset} has its supplements past the table.");

            at += 1 + 3L * r.U8((int)at);
        }

        parsed.Charge(at - offset, "the CFF encoding at", offset);
    }

    /// <summary>FreeType's <c>CFF_Load_FD_Select</c>, checked, expanded to one Font DICT index per glyph.</summary>
    private static byte[] ReadFdSelect(ReadOnlySpan<byte> span, int offset, int glyphCount, int fontDictCount, bool isCff2,
        ParsedTables parsed)
    {
        var r = new BigEndianReader(span, "CFF ");
        var t = isCff2 ? "CFF2" : "CFF";
        if (!r.Contains(offset, 1))
            throw new InvalidDataException($"The {t} table's FDSelect at {offset} runs past the table.");

        parsed.Reserve(32 + glyphCount, "the CFF FDSelect of", glyphCount);
        parsed.Charge(glyphCount, "the CFF FDSelect of", glyphCount);
        var fds = new byte[glyphCount];
        var format = r.U8(offset);
        var at = offset + 1;

        if (format == 0)
        {
            if (!r.Contains(at, glyphCount))
                throw new InvalidDataException($"The {t} table's format-0 FDSelect at {offset} runs past the table.");

            for (var i = 0; i < glyphCount; i++)
                fds[i] = (byte)CheckFd(t, r.U8(at + i), fontDictCount, i);

            return fds;
        }

        if (format != 3 && !(format == 4 && isCff2))
            throw new InvalidDataException(
                $"The {t} table's FDSelect has format {format}; only 0 and 3 exist{(isCff2 ? ", and 4 in CFF2" : "")}.");

        var wide = format == 4;
        var countLength = wide ? 4 : 2;
        var rangeLength = wide ? 6 : 3;
        if (!r.Contains(at, countLength))
            throw new InvalidDataException($"The {t} table's format-{format} FDSelect at {offset} has its range count past the table.");

        long rangeCount = wide ? r.U32(at) : r.U16(at);
        at += countLength;
        if (rangeCount == 0)
            throw new InvalidDataException($"The {t} table's format-{format} FDSelect has no ranges.");

        if (!r.Contains(at, rangeCount * rangeLength + countLength))
            throw new InvalidDataException($"The {t} table's format-{format} FDSelect of {rangeCount} ranges runs past the table.");

        parsed.Charge(rangeCount * rangeLength, "the CFF FDSelect ranges of", rangeCount);
        long previous = -1;
        for (long i = 0; i <= rangeCount; i++)
        {
            var p = (int)(at + i * rangeLength);
            long first = wide ? r.U32(p) : r.U16(p);
            if (i == 0 && first != 0)
                throw new InvalidDataException($"The {t} table's FDSelect starts its first range at glyph {first}, not 0.");

            if (i < rangeCount && first <= previous)
                throw new InvalidDataException($"The {t} table's FDSelect range {i} starts at glyph {first}, not after the range before.");

            if (i == rangeCount)
            {
                // The sentinel: the end of the last range.
                if (first < glyphCount)
                    throw new InvalidDataException(
                        $"The {t} table's FDSelect sentinel is {first}, below the glyph count {glyphCount}.");

                if (first <= previous)
                    throw new InvalidDataException($"The {t} table's FDSelect sentinel {first} is not after its last range.");

                break;
            }

            var fd = CheckFd(t, wide ? r.U16(p + 4) : r.U8(p + 2), fontDictCount, -1);
            var end = (int)Math.Min(wide ? r.U32(p + rangeLength) : r.U16(p + rangeLength), glyphCount);
            for (var g = (int)Math.Min(first, glyphCount); g < end; g++)
                fds[g] = (byte)fd;

            previous = first;
        }

        return fds;
    }

    private static int CheckFd(string t, int fd, int fontDictCount, int glyph)
    {
        if (fd >= fontDictCount)
            throw new InvalidDataException(glyph >= 0
                ? $"The {t} table's FDSelect gives glyph {glyph} Font DICT {fd}, but the FDArray has {fontDictCount}."
                : $"The {t} table's FDSelect names Font DICT {fd}, but the FDArray has {fontDictCount}.");

        return fd;
    }

    /// <summary>A parsed DICT's entries, read with the strict rules: an entry this library reads appears at most once.</summary>
    private readonly ref struct DictValues
    {
        private readonly ReadOnlySpan<byte> m_dict;
        private readonly List<DictEntry> m_entries;

        public DictValues(ReadOnlySpan<byte> dict, List<DictEntry> entries, string what, string table)
        {
            m_dict = dict;
            m_entries = entries;
            What = what;
            Table = table;
        }

        public string What { get; }

        public string Table { get; }

        public bool Has(int op)
        {
            foreach (var entry in m_entries)
            {
                if (entry.Operator == op)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The operands of the entry's last occurrence (as FreeType, which reads them all in turn), or null; every occurrence's
        /// operands are checked (<see cref="CheckOperands"/>), as FreeType fails the face on a malformed one.
        /// </summary>
        public DictOperand[]? Find(int op)
        {
            DictOperand[]? found = null;
            foreach (var entry in m_entries)
            {
                if (entry.Operator != op)
                    continue;

                CheckOperands(op, entry.Operands);
                found = entry.Operands;
            }

            return found;
        }

        /// <summary>
        /// The operands of an entry OTS refuses to see twice, or null; <see cref="InvalidDataException"/> when it appears twice.
        /// </summary>
        public DictOperand[]? FindOnce(int op)
        {
            DictOperand[]? found = null;
            foreach (var entry in m_entries)
            {
                if (entry.Operator != op)
                    continue;

                if (found is not null)
                    throw new InvalidDataException($"The {Table} table's {What} has the entry {Name(op)} twice, which OTS refuses.");

                CheckOperands(op, entry.Operands);
                found = entry.Operands;
            }

            return found;
        }

        /// <summary>
        /// The operand rule of an entry this library reads: Private two non-negative integers, FontMatrix six numbers, ROS
        /// three integers (its SIDs checked by <see cref="Ros"/>), every other entry one integer.
        /// </summary>
        private void CheckOperands(int op, DictOperand[] operands)
        {
            switch (op)
            {
                case OpPrivate:
                    Private(operands);
                    break;

                case OpFontMatrix:
                    if (operands.Length != 6 || operands.Any(operand => operand.Kind == DictOperandKind.Blended))
                        throw new InvalidDataException(
                            $"The {Table} table's {What} gives FontMatrix {operands.Length} operands; it takes six numbers.");

                    break;

                case OpRos:
                    break;

                default:
                    Integer(operands, Name(op));
                    break;
            }
        }

        public long Integer(DictOperand[] operands, string name) => CffDict.RequireInteger(m_dict, operands, Table, What, name);

        /// <summary><see cref="CffDict.CheckOperandCounts"/> on the DICT's entries.</summary>
        public void CheckOperandCounts(CffDictKind kind) => CffDict.CheckOperandCounts(m_entries, kind, Table, What);

        /// <summary>
        /// The DICT's hinting entries as FreeType stores them (a Private DICT; OT-4 WP6), after FreeType's operand-count rule
        /// for the Private DICT.
        /// </summary>
        public Hinting.Cff.CffPrivateHints Hints(bool isCff2) => Hinting.Cff.CffPrivateHints.Parse(m_dict, m_entries, isCff2, Table, What);

        /// <summary>
        /// The charset entry's offset (0, the ISOAdobe charset, when absent): the last entry, as FreeType reads it; two entries
        /// pointing at charsets of the font's own are <see cref="InvalidDataException"/>, as OTS refuses them (a predefined
        /// charset, 0 to 2, may repeat).
        /// </summary>
        public int Charset(int op, int tableLength)
        {
            var custom = 0;
            DictOperand[]? last = null;
            foreach (var entry in m_entries)
            {
                if (entry.Operator != op)
                    continue;

                last = entry.Operands;
                if (Integer(entry.Operands, "charset") > 2 && ++custom > 1)
                    throw new InvalidDataException($"The {Table} table's {What} has two charset entries of its own, which OTS refuses.");
            }

            if (last is null)
                return 0;

            return CheckOffset(Integer(last, "charset"), "charset", tableLength);
        }

        /// <summary>
        /// An offset entry's value (its last occurrence, or with <paramref name="once"/> its only one): null when absent;
        /// <see cref="InvalidDataException"/> when negative or past the table.
        /// </summary>
        public int? Offset(int op, string name, int tableLength, bool once = false)
        {
            if ((once ? FindOnce(op) : Find(op)) is not { } operands)
                return null;

            return CheckOffset(Integer(operands, name), name, tableLength);
        }

        private int CheckOffset(long value, string name, int tableLength)
        {
            if (value < 0 || value > tableLength)
                throw new InvalidDataException($"The {Table} table's {What} has a {name} offset of {value}, outside the table.");

            return (int)value;
        }

        public (long Size, long Offset) Private(DictOperand[] operands)
        {
            if (operands.Length != 2 || operands[0].Kind != DictOperandKind.Integer || operands[1].Kind != DictOperandKind.Integer)
                throw new InvalidDataException(
                    $"The {Table} table's {What} gives Private {operands.Length} operands; it takes two integers (size and offset).");

            var size = CffNumbers.ParseInteger(m_dict, operands[0].Position);
            var offset = CffNumbers.ParseInteger(m_dict, operands[1].Position);
            if (size < 0 || offset < 0)
                throw new InvalidDataException(
                    $"The {Table} table's {What} gives its Private DICT size {size} at offset {offset}; neither may be negative.");

            return (size, offset);
        }

        public void Ros(DictOperand[] operands)
        {
            if (operands.Length != 3 || operands.Any(operand => operand.Kind != DictOperandKind.Integer))
                throw new InvalidDataException($"The {Table} table's {What} gives ROS {operands.Length} operands; it takes three integers.");

            var registry = CffNumbers.ParseInteger(m_dict, operands[0].Position);
            var ordering = CffNumbers.ParseInteger(m_dict, operands[1].Position);
            if (registry is < 0 or > MaxSid || ordering is < 0 or > MaxSid)
                throw new InvalidDataException(
                    $"The CFF table's {What} gives ROS registry {registry} and ordering {ordering}; both must be SIDs below 65,000.");
        }

        /// <summary>The DICT's FontMatrix as FreeType's <c>cff_parse_font_matrix</c> reads it (none: identity, not marked present).</summary>
        public CffFontDict FontMatrixOf()
        {
            // Find checks every occurrence's operands.
            if (Find(OpFontMatrix) is not { } operands)
                return new CffFontDict(CffIndex.Empty, 0, 0);

            var (matrix, offsetX, offsetY, upm) = CffNumbers.ParseFontMatrix(m_dict, operands);
            return new CffFontDict(CffIndex.Empty, 0, 0)
            {
                HasFontMatrix = true,
                Matrix = matrix,
                OffsetX = offsetX,
                OffsetY = offsetY,
                UnitsPerEm = upm,
            };
        }

        private static string Name(int op) => op switch
        {
            OpCharset => "charset",
            OpEncoding => "Encoding",
            OpCharStrings => "CharStrings",
            OpPrivate => "Private",
            OpSubrs => "Subrs",
            OpVstore => "vstore",
            CffDict.OpVsindex => "vsindex",
            OpCharstringType => "CharstringType",
            OpFontMatrix => "FontMatrix",
            OpInitialRandomSeed => "initialRandomSeed",
            OpRos => "ROS",
            OpFdArray => "FDArray",
            OpFdSelect => "FDSelect",
            _ => op >= 0x100 ? $"12 {op & 0xFF}" : $"{op}",
        };
    }
}

/// <summary>
/// One font dictionary as the glyph loader needs it: the Top DICT of a table without an FDArray, or one Font DICT of the
/// FDArray. The font matrix is as <c>cff_parse_font_matrix</c> left it (FreeType normalises it per face, in
/// <see cref="CffOutlineSource"/>).
/// </summary>
/// <param name="LocalSubrs">The Private DICT's local subroutines; empty when it has none.</param>
/// <param name="Vsindex">The Private DICT's <c>vsindex</c> (CFF2), the default for its charstrings.</param>
/// <param name="RandomSeed">
/// The Private DICT's <c>initialRandomSeed</c>, made positive as FreeType does (CFF): the <c>random</c> operator's first state;
/// 0 for a Font DICT without a Private DICT, as FreeType's state is then.
/// </param>
internal sealed record CffFontDict(CffIndex LocalSubrs, uint Vsindex, uint RandomSeed)
{
    /// <summary>The local subroutine bias.</summary>
    public int LocalBias => CffTable.Bias(LocalSubrs.Count);

    /// <summary>The DICT has a FontMatrix entry.</summary>
    public bool HasFontMatrix { get; init; }

    /// <summary>The parsed FontMatrix (identity without one).</summary>
    public CffMatrix Matrix { get; init; } = CffMatrix.Identity;

    /// <summary>The matrix's translation in 16.16 font units, before FreeType's normalisation.</summary>
    public long OffsetX { get; init; }

    /// <summary>The matrix's translation in 16.16 font units, before FreeType's normalisation.</summary>
    public long OffsetY { get; init; }

    /// <summary>The units per em the FontMatrix implies (0 without one).</summary>
    public long UnitsPerEm { get; init; }

    /// <summary>Where the Private DICT lies in the table (0 with <see cref="PrivateSize"/> 0: none).</summary>
    public int PrivateOffset { get; init; }

    /// <summary>The Private DICT's length; 0 for none.</summary>
    public int PrivateSize { get; init; }

    /// <summary>
    /// The Private DICT's hinting values (OT-4 WP6), read at load; FreeType's zeroed values without a Private DICT. A CFF2
    /// table's at variation coordinates come from <see cref="CffTable.PrivateDictAt"/>.
    /// </summary>
    public Hinting.Cff.CffPrivateHints Hints { get; init; } = Hinting.Cff.CffPrivateHints.None;
}
