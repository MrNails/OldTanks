// The tokenizer follows FreeType 2.13.2 src/cff/cffparse.c (cff_parser_run, cff_parse_blend) and src/cff/cffload.c
// (cff_blend_doBlend) (FreeType Project License; credit in docs).

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

/// <summary>The variation data a CFF2 Private DICT's <c>blend</c> reads: the region count of each <c>vsindex</c>.</summary>
internal interface IBlendRegions
{
    /// <summary>The number of item variation data entries (valid <c>vsindex</c> values are below it).</summary>
    int DataCount { get; }

    /// <summary>The regions entry <paramref name="vsindex"/> blends.</summary>
    int RegionIndexCount(int vsindex);
}

/// <summary>
/// The CFF and CFF2 DICT tokenizer: operands and operators as FreeType's <c>cff_parser_run</c> tokenizes them, with the CFF2
/// <c>blend</c> of a Private DICT applied at the default instance (every delta weighted 0, as FreeType's blend vector is
/// without design coordinates).
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
    /// <exception cref="InvalidDataException">The DICT breaks a rule (listed on <see cref="CffTable"/>).</exception>
    public static List<DictEntry> Parse(ReadOnlySpan<byte> dict, bool isCff2, string what, ParsedTables parsed, IBlendRegions? blend)
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
                Blend(dict, stack, vsindex, blend, what);
                usedBlend = true;
                continue; // The blended values stay on the stack.
            }

            entries.Add(new DictEntry(code, stack.ToArray()));
            stack.Clear();
        }

        return entries;
    }

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
    /// FreeType's <c>cff_parse_blend</c> and <c>cff_blend_doBlend</c> at the default instance: the last operand counts the
    /// blended values; each value's deltas (one per region of the current <c>vsindex</c>) are weighted 0 and removed.
    /// </summary>
    private static void Blend(ReadOnlySpan<byte> dict, List<DictOperand> stack, uint vsindex, IBlendRegions blend, string what)
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
        for (var i = 0; i < (int)numBlends; i++)
        {
            // At the default instance every delta weighs 0, so the blended value is the default one (in 16.16).
            var sum = CffNumbers.ParseFixed(dict, stack[first + i]);
            // FreeType stores the 16.16 sum back as a 5-byte operand: 32 bits.
            stack[first + i] = new DictOperand(DictOperandKind.Blended, stack[first + i].Position, (int)sum);
        }

        stack.RemoveRange(first + (int)numBlends, stack.Count - first - (int)numBlends);
    }
}
