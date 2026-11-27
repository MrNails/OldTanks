// The tokenizer follows FreeType 2.13.2 src/cff/cffparse.c (cff_parser_run, cff_parse_blend) and src/cff/cffload.c
// (cff_blend_doBlend); the operand-count rule cff_parser_run's Stack_Underflow over the field tables of src/cff/cfftoken.h
// and the callbacks' counts (cff_parse_font_bbox, cff_parse_font_matrix, cff_parse_private_dict, cff_parse_cid_ros)
// (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Cff;

/// <summary>How a DICT operand is encoded.</summary>
internal enum DictOperandKind : byte
{
    /// <summary>An integer: bytes 28, 29 or 32..254.</summary>
    Integer,

    /// <summary>A real: byte 30 and its nibbles.</summary>
    Real,

    /// <summary>The 16.16 result of a CFF2 <c>blend</c> in a Private DICT.</summary>
    Blended,
}

/// <param name="Kind">The encoding.</param>
/// <param name="Position">Where the operand's first byte lies in its DICT (not used for <see cref="DictOperandKind.Blended"/>).</param>
/// <param name="Fixed">The 16.16 value of a blended operand.</param>
internal readonly record struct DictOperand(DictOperandKind Kind, int Position, int Fixed);

/// <param name="Operator">The operator: 0..255, or 0x100 | b for the two-byte operator 12 b.</param>
/// <param name="Operands">Its operands, first to last.</param>
internal readonly record struct DictEntry(int Operator, DictOperand[] Operands);

/// <summary>The field table FreeType 2.13.2's DICT parser reads a DICT with (<c>cfftoken.h</c>).</summary>
internal enum CffDictKind : byte
{
    /// <summary>A CFF Top DICT, and a CID-keyed CFF table's Font DICT (<c>CFF_CODE_TOPDICT</c>).</summary>
    CffTop,

    /// <summary>A CFF Private DICT (<c>CFF_CODE_PRIVATE</c>).</summary>
    CffPrivate,

    /// <summary>A CFF2 Top DICT (<c>CFF2_CODE_TOPDICT</c>).</summary>
    Cff2Top,

    /// <summary>A CFF2 Font DICT (<c>CFF2_CODE_FONTDICT</c>).</summary>
    Cff2Font,

    /// <summary>A CFF2 Private DICT (<c>CFF2_CODE_PRIVATE</c>).</summary>
    Cff2Private,
}

/// <summary>The variation data a CFF2 Private DICT's <c>blend</c> reads: the region count of each <c>vsindex</c>.</summary>
internal interface IBlendRegions
{
    /// <summary>The number of item variation data entries (valid <c>vsindex</c> values are below it).</summary>
    int DataCount { get; }

    /// <summary>The regions entry <paramref name="vsindex"/> blends.</summary>
    int RegionIndexCount(int vsindex);

    /// <summary>
    /// FreeType's <c>cff_blend_build_vector</c> at normalized coordinates (OT-4 WP5): 1 for the default master, then each
    /// region's weight, 16.16.
    /// </summary>
    void BuildBlendVector(int vsindex, ReadOnlySpan<long> coords, Span<int> weights);
}

/// <summary>
/// The CFF and CFF2 DICT tokenizer: operands and operators as FreeType's <c>cff_parser_run</c> tokenizes them, with the CFF2
/// <c>blend</c> of a Private DICT applied at the default instance (every delta weighted 0, as FreeType's blend vector is
/// without design coordinates) or, given normalized coordinates, at those (FreeType's <c>cff_load_private_dict</c> when a
/// size's vector changes, OT-4 WP5).
/// </summary>
internal static class CffDict
{
    /// <summary>FreeType's operand stack for CFF DICTs (<c>CFF_MAX_STACK_DEPTH</c>, <c>cffparse.h</c>).</summary>
    public const int CffStackSize = 96;

    /// <summary>FreeType's operand stack for CFF2 DICTs (<c>CFF2_MAX_STACK</c> and <c>CFF2_DEFAULT_STACK</c>, <c>cffparse.h</c>).</summary>
    public const int Cff2StackSize = 513;

    public const int OpVsindex = 22;
    public const int OpBlend = 23;

    // What the parse allocates per DICT byte at worst, accounted against the file's parsed-memory budget though it is
    // transient (the DICT's entries live only while its table is parsed): an operand takes 12 bytes in the growing operand
    // list and 12 in its entry's array, an operator one entry (16 bytes in the growing entry list, its array header); estimated
    // (not measured) at about 36 bytes per byte for the worst DICTs in OT-4 WP2's review.
    private const int BytesPerDictByte = 36;

    /// <summary>
    /// Tokenizes a DICT. Operands after the last operator (complete or cut by the end of the DICT, a real included) are
    /// ignored, as FreeType ignores them.
    /// </summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <param name="isCff2">The table is CFF2 (its stack is 513 operands, not 96).</param>
    /// <param name="what">The DICT, for messages ("the Top DICT", "the Private DICT of Font DICT 3").</param>
    /// <param name="parsed">The file's load state: the DICT's bytes are charged, its entries reserved.</param>
    /// <param name="blend">For a CFF2 Private DICT, the variation data its <c>vsindex</c> and <c>blend</c> read; else null.</param>
    /// <param name="coords">The normalized coordinates (16.16) <c>blend</c> weighs its deltas at; empty for the default instance.</param>
    /// <exception cref="InvalidDataException">The DICT breaks a rule (listed on <see cref="CffTable"/>).</exception>
    public static List<DictEntry> Parse(ReadOnlySpan<byte> dict, bool isCff2, string what, ParsedTables parsed, IBlendRegions? blend,
        ReadOnlySpan<long> coords = default)
    {
        parsed.Charge(dict.Length, "a CFF DICT of", dict.Length);
        parsed.Reserve(64 + (long)BytesPerDictByte * dict.Length, "a CFF DICT of", dict.Length);

        var stackSize = isCff2 ? Cff2StackSize : CffStackSize;
        var t = isCff2 ? "CFF2" : "CFF";
        var entries = new List<DictEntry>();
        var stack = new List<DictOperand>();
        var vsindex = 0u;
        var usedBlend = false;

        var p = 0;
        while (p < dict.Length)
        {
            int v = dict[p];
            if (v >= 27 && v != 31 && v != 255)
            {
                if (v == 27)
                    throw new InvalidDataException($"The {t} table's {what} holds the reserved operand byte 27 at {p}.");

                if (stack.Count >= stackSize)
                    throw new InvalidDataException(
                        $"The {t} table's {what} has more than {stackSize - 1} operands before an operator (FreeType's DICT stack).");

                var start = p;
                DictOperandKind kind;
                if (v == 30)
                {
                    kind = DictOperandKind.Real;
                    p++;
                    for (;;)
                    {
                        if (p >= dict.Length)
                            return entries; // A real cut by the end of the DICT: trailing, ignored.

                        if (dict[p] >> 4 == 0xF || (dict[p] & 0xF) == 0xF)
                            break;

                        p++;
                    }
                }
                else
                {
                    kind = DictOperandKind.Integer;
                    var extra = v switch
                    {
                        28 => 2,
                        29 => 4,
                        > 246 => 1,
                        _ => 0,
                    };

                    if (p + extra >= dict.Length)
                        return entries; // An integer cut by the end of the DICT: trailing, ignored.

                    p += extra;
                }

                stack.Add(new DictOperand(kind, start, 0));
                p++;
                continue;
            }

            if (stack.Count >= stackSize)
                throw new InvalidDataException(
                    $"The {t} table's {what} has more than {stackSize - 1} operands before an operator (FreeType's DICT stack).");

            var code = v;
            if (v == 12)
            {
                if (p + 1 >= dict.Length)
                    throw new InvalidDataException($"The {t} table's {what} ends inside the two-byte operator at {p}.");

                p++;
                code = 0x100 | dict[p];
            }

            p++;

            if (blend is not null && code == OpVsindex)
            {
                if (usedBlend)
                    throw new InvalidDataException($"The CFF2 table's {what} sets vsindex after a blend.");

                vsindex = (uint)RequireInteger(dict, stack, t, what, "vsindex");
            }
            else if (blend is not null && code == OpBlend)
            {
                Blend(dict, stack, vsindex, blend, what, coords);
                usedBlend = true;
                continue; // The blended values stay on the stack.
            }

            entries.Add(new DictEntry(code, stack.ToArray()));
            stack.Clear();
        }

        return entries;
    }

    /// <summary>
    /// FreeType's operand-count rule (<c>cff_parser_run</c>): every entry its parser knows for the DICT kind
    /// (<c>cfftoken.h</c>) must have the operands its handler pops, or the parser fails (<c>Stack_Underflow</c>) and with it the
    /// face: at least one for every entry but a delta array, which may be empty; four for FontBBox, six for FontMatrix, two
    /// for Private and three for ROS (their callbacks). Entries FreeType's parser does not know (in CFF2, the Private DICT's
    /// lenIV, ForceBold, ForceBoldThreshold, initialRandomSeed, defaultWidthX and nominalWidthX among them) take any operands.
    /// MultipleMaster is left to the face's <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="dict">The DICT's entries.</param>
    /// <param name="kind">Which of FreeType's field tables reads the DICT.</param>
    /// <param name="table">"CFF" or "CFF2", for messages.</param>
    /// <param name="what">The DICT, for messages.</param>
    /// <exception cref="InvalidDataException">An entry has fewer operands than FreeType's handler pops.</exception>
    public static void CheckOperandCounts(IReadOnlyList<DictEntry> dict, CffDictKind kind, string table, string what)
    {
        foreach (var entry in dict)
        {
            var needed = NeededOperands(kind, entry.Operator);
            if (entry.Operands.Length < needed)
                throw new InvalidDataException(
                    $"The {table} table's {what} gives the entry {OperatorName(entry.Operator)} {entry.Operands.Length} operands; " +
                    $"FreeType's parser needs at least {needed} (it fails the face).");
        }
    }

    /// <summary>The operands FreeType's handler of an entry pops (0: not known to the parser for the kind, or a delta array).</summary>
    private static int NeededOperands(CffDictKind kind, int op) => kind switch
    {
        // CFF_CODE_TOPDICT, which also reads a CID-keyed CFF table's Font DICTs. XUID (14), BaseFontName, BaseFontBlend,
        // BlendAxisTypes and Chameleon are compiled out (#if 0) in FreeType 2.13.2. MultipleMaster's callback needs five
        // operands (a Top DICT with the entry is NotSupportedException anyway, checked first).
        CffDictKind.CffTop => op switch
        {
            0 or 1 or 2 or 3 or 4 or 13 or 15 or 16 or 17 => 1,
            0x100 or 0x101 or 0x102 or 0x103 or 0x104 or 0x105 or 0x106 or 0x108 or 0x114 or 0x115 => 1,
            >= 0x11F and <= 0x126 => 1,
            5 => 4,
            0x107 => 6,
            18 => 2,
            0x11E => 3,
            0x118 => 5,
            _ => 0,
        },

        // CFF_CODE_PRIVATE: BlueValues, OtherBlues, FamilyBlues, FamilyOtherBlues, StemSnapH and StemSnapV are delta arrays.
        CffDictKind.CffPrivate => op switch
        {
            10 or 11 or 19 or 20 or 21 => 1,
            0x109 or 0x10A or 0x10B or 0x10E or 0x10F or 0x110 or 0x111 or 0x112 or 0x113 => 1,
            _ => 0,
        },

        // CFF2_CODE_TOPDICT.
        CffDictKind.Cff2Top => op switch
        {
            17 or 24 or 25 or 0x124 or 0x125 => 1,
            0x107 => 6,
            _ => 0,
        },

        // CFF2_CODE_FONTDICT.
        CffDictKind.Cff2Font => op switch
        {
            18 => 2,
            0x107 => 6,
            _ => 0,
        },

        // CFF2_CODE_PRIVATE (blend, 23, is checked by the tokenizer).
        CffDictKind.Cff2Private => op switch
        {
            10 or 11 or 19 or OpVsindex => 1,
            0x109 or 0x10A or 0x10B or 0x111 or 0x112 => 1,
            _ => 0,
        },

        _ => 0,
    };

    private static string OperatorName(int op) => op >= 0x100 ? $"12 {op & 0xFF}" : $"{op}";

    /// <summary>The value of an operator's single integer operand; <see cref="InvalidDataException"/> for any other operand list.</summary>
    public static long RequireInteger(ReadOnlySpan<byte> dict, IReadOnlyList<DictOperand> operands, string table, string what,
        string name)
    {
        if (operands.Count != 1)
            throw new InvalidDataException(
                $"The {table} table's {what} gives {name} {operands.Count} operands; it takes one integer.");

        if (operands[0].Kind != DictOperandKind.Integer)
            throw new InvalidDataException($"The {table} table's {what} gives {name} a {Describe(operands[0].Kind)} operand; it takes an integer.");

        return CffNumbers.ParseInteger(dict, operands[0].Position);
    }

    public static string Describe(DictOperandKind kind) => kind switch
    {
        DictOperandKind.Real => "real",
        DictOperandKind.Blended => "blended",
        _ => "integer",
    };

    /// <summary>
    /// FreeType's <c>cff_parse_blend</c> and <c>cff_blend_doBlend</c>: the last operand counts the blended values; each value's
    /// deltas (one per region of the current <c>vsindex</c>) are weighted by the blend vector (all 0 at the default instance:
    /// <paramref name="coords"/> empty), summed with <c>FT_MulFix</c> in 64 bits, kept in 32 bits, and removed.
    /// </summary>
    private static void Blend(ReadOnlySpan<byte> dict, List<DictOperand> stack, uint vsindex, IBlendRegions blend, string what,
        ReadOnlySpan<long> coords)
    {
        if (vsindex >= (uint)blend.DataCount)
            throw new InvalidDataException(
                $"The CFF2 table's {what} blends with vsindex {vsindex}, but the variation store has {blend.DataCount} entries.");

        if (stack.Count == 0)
            throw new InvalidDataException($"The CFF2 table's {what} has a blend without operands.");

        var last = stack[^1];
        if (last.Kind != DictOperandKind.Integer)
            throw new InvalidDataException($"The CFF2 table's {what} has a blend whose count is a {Describe(last.Kind)} operand.");

        var numBlends = (uint)CffNumbers.ParseInteger(dict, last.Position);
        if (numBlends > Cff2StackSize)
            throw new InvalidDataException($"The CFF2 table's {what} blends {numBlends} values, more than its {Cff2StackSize}-operand stack.");

        var count = stack.Count - 1;
        var lenBv = blend.RegionIndexCount((int)vsindex) + 1L;
        var numOperands = numBlends * lenBv;
        if (numOperands > count)
            throw new InvalidDataException(
                $"The CFF2 table's {what} blends {numBlends} values of {lenBv} operands each, but has only {count} operands.");

        var first = count - (int)numOperands;
        Span<int> weights = coords.IsEmpty ? [] : new int[(int)lenBv];
        if (!coords.IsEmpty)
            blend.BuildBlendVector((int)vsindex, coords, weights);

        var delta = first + (int)numBlends;
        for (var i = 0; i < (int)numBlends; i++)
        {
            // At the default instance every delta weighs 0, so the blended value is the default one (in 16.16).
            var sum = CffNumbers.ParseFixed(dict, stack[first + i]);
            for (var j = 1; j < weights.Length; j++)
                sum += FixedMath.MulFix(CffNumbers.ParseFixed(dict, stack[delta++]), weights[j]);

            // FreeType stores the 16.16 sum back as a 5-byte operand: 32 bits.
            stack[first + i] = new DictOperand(DictOperandKind.Blended, stack[first + i].Position, (int)sum);
        }

        stack.RemoveRange(first + (int)numBlends, stack.Count - first - (int)numBlends);
    }
}
