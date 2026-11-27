// Follows FreeType 2.13.2 src/cff/cffparse.c (cff_parser_run's storing of the Private DICT fields of src/cff/cfftoken.h:
// cff_parse_num, the delta arrays, do_fixed with a scaling of 3 for BlueScale) and src/cff/cffload.c (cff_load_private_dict:
// the defaults, the even BlueValues count, the BlueShift and BlueFuzz sanitising) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Cff;

namespace CoolEngine.Fonts.Hinting.Cff;

/// <summary>
/// The hinting values of one Private DICT as FreeType's <c>CFF_PrivateRec</c> holds them after <c>cff_load_private_dict</c>:
/// the four blue arrays (delta-decoded, 64-bit <c>FT_Pos</c>, their counts capped at 14, 10, 14 and 10 values, the BlueValues
/// count made even), BlueScale (1000 times the value, 16.16), BlueShift and BlueFuzz (integers, an unlikely value replaced by
/// the default) and LanguageGroup. A Font DICT without a Private DICT has FreeType's zeroed record (<see cref="None"/>), not
/// the defaults. The Adobe engine reads nothing else of the DICT for hinting (StdHW and StdVW only for stem darkening, which is
/// off; StemSnapH, StemSnapV, ForceBold and ExpansionFactor not at all), so those entries are checked, not kept. Immutable.
/// </summary>
internal sealed class CffPrivateHints
{
    /// <summary><c>cfftoken.h</c>'s array sizes of BlueValues, OtherBlues, FamilyBlues and FamilyOtherBlues.</summary>
    public const int MaxBlueValues = 14, MaxOtherBlues = 10, MaxFamilyBlues = 14, MaxFamilyOtherBlues = 10;

    /// <summary>
    /// What one instance keeps, reserved against a file's parsed-memory budget when a Private DICT is read: the four arrays
    /// (48 values of 8 bytes and four array headers of 24) and the object (about 80 bytes), rounded up.
    /// </summary>
    public const long Cost = 576;

    // The Private DICT operators FreeType's parser knows (cfftoken.h).
    private const int OpBlueValues = 6, OpOtherBlues = 7, OpFamilyBlues = 8, OpFamilyOtherBlues = 9;
    private const int OpBlueScale = 0x109, OpBlueShift = 0x10A, OpBlueFuzz = 0x10B, OpLanguageGroup = 0x111;

    // cff_load_private_dict's defaults: BlueShift 7, BlueFuzz 1, BlueScale (FT_Fixed)(0.039625 * 0x10000 * 1000).
    private const long DefaultBlueShift = 7, DefaultBlueFuzz = 1, DefaultBlueScale = 2596864;

    // do_fixed's power_ten_limits[3]: FT_LONG_MAX / 1000 (the reference's FT_Long is 64-bit).
    private const long PowerTenLimit3 = long.MaxValue / 1000;

    private CffPrivateHints(long[] blueValues, int blueValueCount, long[] otherBlues, int otherBlueCount, long[] familyBlues,
        int familyBlueCount, long[] familyOtherBlues, int familyOtherBlueCount, long blueScale, long blueShift, long blueFuzz,
        int languageGroup)
    {
        BlueValues = blueValues;
        BlueValueCount = blueValueCount;
        OtherBlues = otherBlues;
        OtherBlueCount = otherBlueCount;
        FamilyBlues = familyBlues;
        FamilyBlueCount = familyBlueCount;
        FamilyOtherBlues = familyOtherBlues;
        FamilyOtherBlueCount = familyOtherBlueCount;
        BlueScale = blueScale;
        BlueShift = blueShift;
        BlueFuzz = blueFuzz;
        LanguageGroup = languageGroup;
    }

    /// <summary>A Font DICT without a Private DICT: FreeType's zeroed <c>CFF_PrivateRec</c>.</summary>
    public static CffPrivateHints None { get; } = new(new long[MaxBlueValues], 0, new long[MaxOtherBlues], 0, new long[MaxFamilyBlues], 0,
        new long[MaxFamilyOtherBlues], 0, 0, 0, 0, 0);

    /// <summary>
    /// BlueValues (all <see cref="MaxBlueValues"/> slots: a value past the count is one an earlier occurrence of the entry
    /// left, or 0, as in FreeType's array).
    /// </summary>
    public long[] BlueValues { get; }

    /// <summary><c>num_blue_values</c>, made even.</summary>
    public int BlueValueCount { get; }

    public long[] OtherBlues { get; }

    public int OtherBlueCount { get; }

    public long[] FamilyBlues { get; }

    public int FamilyBlueCount { get; }

    public long[] FamilyOtherBlues { get; }

    public int FamilyOtherBlueCount { get; }

    /// <summary><c>blue_scale</c>: BlueScale times 1000, in 16.16.</summary>
    public long BlueScale { get; }

    /// <summary><c>blue_shift</c>, in font units (0..1000).</summary>
    public long BlueShift { get; }

    /// <summary><c>blue_fuzz</c>, in font units (0..1000).</summary>
    public long BlueFuzz { get; }

    /// <summary><c>language_group</c> (FreeType's 32-bit <c>FT_Int</c>).</summary>
    public int LanguageGroup { get; }

    /// <summary>
    /// Reads a Private DICT's hinting entries as FreeType's <c>cff_parser_run</c> stores them in turn (a later occurrence of
    /// an entry overwrites an earlier one; a delta array overwrites only as many slots as it has values), then applies
    /// <c>cff_load_private_dict</c>'s corrections.
    /// </summary>
    /// <param name="dict">The DICT's bytes, which the operand positions index.</param>
    /// <param name="entries">The DICT's entries, as <see cref="CffDict.Parse"/> tokenized them.</param>
    /// <param name="isCff2">A CFF2 Private DICT, read with FreeType's CFF2 field table.</param>
    /// <param name="table">"CFF" or "CFF2", for messages.</param>
    /// <param name="what">The DICT, for messages.</param>
    /// <exception cref="InvalidDataException">
    /// An entry FreeType's Private DICT parser knows has fewer operands than it pops (<see cref="CffDict.CheckOperandCounts"/>):
    /// FreeType's parser fails there (<c>Stack_Underflow</c>), and with it the face.
    /// </exception>
    public static CffPrivateHints Parse(ReadOnlySpan<byte> dict, IReadOnlyList<DictEntry> entries, bool isCff2, string table, string what)
    {
        CffDict.CheckOperandCounts(entries, isCff2 ? CffDictKind.Cff2Private : CffDictKind.CffPrivate, table, what);

        var blueValues = new long[MaxBlueValues];
        var otherBlues = new long[MaxOtherBlues];
        var familyBlues = new long[MaxFamilyBlues];
        var familyOtherBlues = new long[MaxFamilyOtherBlues];
        int blueValueCount = 0, otherBlueCount = 0, familyBlueCount = 0, familyOtherBlueCount = 0;
        long blueScale = DefaultBlueScale, blueShift = DefaultBlueShift, blueFuzz = DefaultBlueFuzz;
        var languageGroup = 0;

        foreach (var entry in entries)
        {
            var operands = entry.Operands;
            switch (entry.Operator)
            {
                case OpBlueValues:
                    blueValueCount = ReadDelta(dict, operands, blueValues);
                    break;

                case OpOtherBlues:
                    otherBlueCount = ReadDelta(dict, operands, otherBlues);
                    break;

                case OpFamilyBlues:
                    familyBlueCount = ReadDelta(dict, operands, familyBlues);
                    break;

                case OpFamilyOtherBlues:
                    familyOtherBlueCount = ReadDelta(dict, operands, familyOtherBlues);
                    break;

                // StemSnapH, StemSnapV, StdHW, StdVW, ForceBold and ExpansionFactor are not read (checked above only).
                // The number entries have at least one operand (checked above); FreeType reads only the first.
                case OpBlueScale:
                    blueScale = FixedThousand(dict, operands[0]);
                    break;

                case OpBlueShift:
                    blueShift = Number(dict, operands[0]);
                    break;

                case OpBlueFuzz:
                    blueFuzz = Number(dict, operands[0]);
                    break;

                case OpLanguageGroup:
                    languageGroup = (int)Number(dict, operands[0]);
                    break;
            }
        }

        // cff_load_private_dict: "ensure that `num_blue_values' is even", and the ad-hoc limits on BlueShift and BlueFuzz.
        blueValueCount &= ~1;
        if (blueShift is > 1000 or < 0)
            blueShift = DefaultBlueShift;

        if (blueFuzz is > 1000 or < 0)
            blueFuzz = DefaultBlueFuzz;

        return new CffPrivateHints(blueValues, blueValueCount, otherBlues, otherBlueCount, familyBlues, familyBlueCount, familyOtherBlues,
            familyOtherBlueCount, blueScale, blueShift, blueFuzz, languageGroup);
    }

    /// <summary>A delta array: up to the array's size of values, each the running sum (64-bit, wrapping); returns the count.</summary>
    private static int ReadDelta(ReadOnlySpan<byte> dict, DictOperand[] operands, long[] values)
    {
        var count = Math.Min(operands.Length, values.Length);
        var value = 0L;
        for (var i = 0; i < count; i++)
        {
            value = unchecked(value + Number(dict, operands[i]));
            values[i] = value;
        }

        return count;
    }

    /// <summary>
    /// <c>cff_parse_num</c>: an integer as it is; a real truncated (its 16.16 value shifted right by 16); a CFF2 blend result
    /// rounded from its top 24 bits and kept in 16 bits.
    /// </summary>
    private static long Number(ReadOnlySpan<byte> dict, in DictOperand operand) => operand.Kind switch
    {
        DictOperandKind.Real => CffNumbers.ParseReal(dict, operand.Position, 0, false, out _) >> 16,
        DictOperandKind.Blended => (short)((((uint)operand.Fixed >> 8) + 0x80u) >> 8),
        _ => CffNumbers.ParseInteger(dict, operand.Position),
    };

    /// <summary><c>cff_parse_fixed_scaled(..., 3)</c> (<c>do_fixed</c>): 1000 times the value, in 16.16, with FreeType's overflow values.</summary>
    private static long FixedThousand(ReadOnlySpan<byte> dict, in DictOperand operand)
    {
        switch (operand.Kind)
        {
            case DictOperandKind.Real:
                return CffNumbers.ParseReal(dict, operand.Position, 3, false, out _);

            case DictOperandKind.Blended:
            {
                // The 255 operand's four bytes as an FT_UInt32, held in a 64-bit FT_Fixed.
                long value = (uint)operand.Fixed;
                if (CffFixed.Abs(value) > PowerTenLimit3)
                    return value > 0 ? 0x7FFFFFFFL : -0x7FFFFFFFL;

                return value * 1000;
            }

            default:
            {
                var value = CffNumbers.ParseInteger(dict, operand.Position);
                if (unchecked(CffFixed.Abs(value) << 16) > PowerTenLimit3)
                    return value > 0 ? 0x7FFFFFFFL : -0x7FFFFFFFL;

                value *= 1000;
                if (value > 0x7FFF)
                    return 0x7FFFFFFFL;

                if (value < -0x7FFF)
                    return -0x7FFFFFFFL;

                return (long)((ulong)value << 16);
            }
        }
    }
}
