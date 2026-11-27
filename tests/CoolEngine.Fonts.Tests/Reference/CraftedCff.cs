using System.Buffers.Binary;
using System.Globalization;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>One DICT entry: an operator (0x100 | b for 12 b) and its operands' encoded bytes.</summary>
internal sealed record DictOp(int Op, byte[] Operands);

/// <summary>A Private DICT and its local subroutines.</summary>
internal sealed class CffPrivate
{
    /// <summary>The entries other than Subrs (which the writer adds when <see cref="Subrs"/> is not null).</summary>
    public List<DictOp> Dict { get; set; } = [];

    public List<byte[]>? Subrs { get; set; }

    public CffPrivate Clone() => new() { Dict = [.. Dict], Subrs = Subrs is null ? null : [.. Subrs] };
}

/// <summary>A Font DICT of an FDArray: its entries other than Private, and its Private DICT.</summary>
internal sealed class CffFontDictModel
{
    public List<DictOp> Dict { get; set; } = [];

    public CffPrivate? Private { get; set; }
}

/// <summary>
/// A CFF or CFF2 table taken apart (independently of the library's parser) so tests can change one part and lay it out again:
/// the INDEXes as element lists, the DICTs as entries whose offsets the writer recomputes, the charset, encoding, FDSelect and
/// variation store as raw bytes. Offsets are written as 5-byte integers, so layout takes one pass.
/// </summary>
internal sealed class CffModel
{
    // DICT operators the writer owns (offsets).
    public const int OpCharset = 15, OpEncoding = 16, OpCharStrings = 17, OpPrivate = 18, OpSubrs = 19, OpVstore = 24;
    public const int OpFdArray = 0x124, OpFdSelect = 0x125;

    public bool IsCff2 { get; set; }

    /// <summary>The CFF header's bytes after the first four (CFF2: none).</summary>
    public byte[] Names { get; set; } = [];

    /// <summary>Top DICT entries other than the offsets the writer owns.</summary>
    public List<DictOp> Top { get; set; } = [];

    public List<byte[]> NameList { get; set; } = [];

    public List<byte[]> Strings { get; set; } = [];

    public List<byte[]> GlobalSubrs { get; set; } = [];

    public List<byte[]> CharStrings { get; set; } = [];

    /// <summary>A charset's raw bytes, or null for a predefined one (<see cref="CharsetOffset"/>).</summary>
    public byte[]? CharsetData { get; set; }

    public int CharsetOffset { get; set; }

    /// <summary>An encoding's raw bytes, or null for the predefined <see cref="EncodingOffset"/> (0 is written as no entry).</summary>
    public byte[]? EncodingData { get; set; }

    public int EncodingOffset { get; set; }

    /// <summary>The Private DICT of a table without an FDArray.</summary>
    public CffPrivate? Private { get; set; }

    /// <summary>The FDArray, or null.</summary>
    public List<CffFontDictModel>? FontDicts { get; set; }

    public byte[]? FdSelectData { get; set; }

    /// <summary>The CFF2 variation store, its 2-byte length field first.</summary>
    public byte[]? VariationStore { get; set; }

    /// <summary>
    /// A minimal valid table of <paramref name="glyphs"/> empty glyphs: CFF with one name, a format-2 charset and no Private
    /// DICT; CFF2 with one empty Font DICT.
    /// </summary>
    public static CffModel Minimal(int glyphs, bool isCff2)
    {
        var model = new CffModel
        {
            IsCff2 = isCff2,
            CharStrings = Enumerable.Range(0, glyphs).Select(_ => isCff2 ? Array.Empty<byte>() : Charstring.Of("endchar")).ToList(),
        };

        if (isCff2)
        {
            model.FontDicts = [new CffFontDictModel()];
        }
        else
        {
            model.NameList = ["Crafted"u8.ToArray()];
            model.CharsetData = new BigEndianWriter().U8(2).U16(1).U16(glyphs - 2).ToArray();
        }

        return model;
    }

    /// <summary>Reads a table.</summary>
    public static CffModel Read(byte[] table, bool isCff2)
    {
        var model = new CffModel { IsCff2 = isCff2 };
        List<DictOp> top;
        if (!isCff2)
        {
            var (names, end) = ReadIndex(table, table[2], false);
            var (tops, end2) = ReadIndex(table, end, false);
            var (strings, end3) = ReadIndex(table, end2, false);
            var (subrs, _) = ReadIndex(table, end3, false);
            model.NameList = names;
            model.Strings = strings;
            model.GlobalSubrs = subrs;
            top = ParseDict(tops[0]);
        }
        else
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(3));
            top = ParseDict(table.AsSpan(table[2], length).ToArray());
            model.GlobalSubrs = ReadIndex(table, table[2] + length, true).Elements;
        }

        foreach (var op in top)
        {
            switch (op.Op)
            {
                case OpCharStrings:
                    model.CharStrings = ReadIndex(table, Integer(op, 0), isCff2).Elements;
                    break;

                case OpCharset:
                    model.CharsetOffset = Integer(op, 0);
                    break;

                case OpEncoding:
                    model.EncodingOffset = Integer(op, 0);
                    break;

                case OpPrivate:
                    model.Private = ReadPrivate(table, Integer(op, 0), Integer(op, 1), isCff2);
                    break;

                case OpFdArray:
                    model.FontDicts = [];
                    foreach (var fd in ReadIndex(table, Integer(op, 0), isCff2).Elements)
                    {
                        var entries = ParseDict(fd);
                        var privateOp = entries.SingleOrDefault(entry => entry.Op == OpPrivate);
                        model.FontDicts.Add(new CffFontDictModel
                        {
                            Dict = entries.Where(entry => entry.Op != OpPrivate).ToList(),
                            Private = privateOp is null ? null : ReadPrivate(table, Integer(privateOp, 0), Integer(privateOp, 1), isCff2),
                        });
                    }

                    break;

                case OpVstore:
                {
                    var at = Integer(op, 0);
                    var length = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(at));
                    model.VariationStore = table.AsSpan(at, 2 + length).ToArray();
                    break;
                }

                default:
                    if (op.Op != OpFdSelect)
                        model.Top.Add(op);

                    break;
            }
        }

        var glyphs = model.CharStrings.Count;
        if (model.CharsetOffset > 2)
        {
            model.CharsetData = table.AsSpan(model.CharsetOffset, CharsetLength(table, model.CharsetOffset, glyphs)).ToArray();
            model.CharsetOffset = 0;
        }

        if (model.EncodingOffset > 1)
        {
            var at = model.EncodingOffset;
            var format = table[at];
            var length = 2 + ((format & 0x7F) == 0 ? table[at + 1] : 2 * table[at + 1]);
            if ((format & 0x80) != 0)
                length += 1 + 3 * table[at + length];

            model.EncodingData = table.AsSpan(at, length).ToArray();
            model.EncodingOffset = 0;
        }

        if (top.SingleOrDefault(op => op.Op == OpFdSelect) is { } fdSelect)
        {
            var at = Integer(fdSelect, 0);
            var length = table[at] switch
            {
                0 => 1 + glyphs,
                3 => 1 + 2 + 3 * BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(at + 1)) + 2,
                _ => 1 + 4 + 6 * (int)BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(at + 1)) + 4,
            };
            model.FdSelectData = table.AsSpan(at, length).ToArray();
        }

        return model;
    }

    /// <summary>The model's CFF table from the font's <c>CFF </c> table (or <c>CFF2</c>).</summary>
    public static CffModel FromFont(byte[] font, bool isCff2) => Read(CraftedFonts.Table(font, isCff2 ? "CFF2" : "CFF "), isCff2);

    /// <summary>A copy of the font with this model as its CFF (or CFF2) table.</summary>
    public byte[] InFont(byte[] font) => CraftedFonts.WithTable(font, IsCff2 ? "CFF2" : "CFF ", Write());

    /// <summary>Lays the table out: header, the INDEXes and DICTs, then charset, encoding, FDSelect, CharStrings, FDArray, the Private DICTs with their Subrs, the variation store.</summary>
    public byte[] Write()
    {
        // Pass 1: sizes. Every offset operand takes 5 bytes, so DICT sizes do not depend on the offsets.
        var topSize = DictSize(Top) + OffsetOpsSize();
        var privates = new List<CffPrivate>();
        if (Private is not null)
            privates.Add(Private);

        foreach (var fd in FontDicts ?? [])
        {
            if (fd.Private is not null && !privates.Contains(fd.Private))
                privates.Add(fd.Private);
        }

        var w = new BigEndianWriter();
        var headerSize = IsCff2 ? 5 : 4;
        var topIndexSize = IsCff2 ? topSize : IndexSize([new byte[topSize]], false);
        var position = headerSize;
        if (!IsCff2)
            position += IndexSize(NameList, false);

        position += topIndexSize;
        if (!IsCff2)
            position += IndexSize(Strings, false);

        position += IndexSize(GlobalSubrs, IsCff2);
        var charsetAt = CharsetData is null ? CharsetOffset : position;
        position += CharsetData?.Length ?? 0;
        var encodingAt = EncodingData is null ? EncodingOffset : position;
        position += EncodingData?.Length ?? 0;
        var fdSelectAt = FdSelectData is null ? 0 : position;
        position += FdSelectData?.Length ?? 0;
        var charStringsAt = position;
        position += IndexSize(CharStrings, IsCff2);
        var fdArrayAt = position;
        if (FontDicts is not null)
            position += IndexSize(FontDicts.Select(fd => new byte[DictSize(fd.Dict) + (fd.Private is null ? 0 : 11)]).ToList(), IsCff2);

        var privateAt = new Dictionary<CffPrivate, (int At, int Size)>();
        foreach (var p in privates)
        {
            var size = DictSize(p.Dict) + (p.Subrs is null ? 0 : 6);
            privateAt[p] = (position, size);
            position += size;
            if (p.Subrs is not null)
                position += IndexSize(p.Subrs, IsCff2);
        }

        var vstoreAt = VariationStore is null ? 0 : position;

        // Pass 2: bytes.
        // Entries of operator -1 are raw bytes laid after every entry (a DICT's tail).
        var top = Top.Where(entry => entry.Op >= 0).ToList();
        if (!IsCff2)
        {
            if (CharsetData is not null || CharsetOffset != 0)
                top.Add(new DictOp(OpCharset, Int(charsetAt)));

            if (EncodingData is not null || EncodingOffset != 0)
                top.Add(new DictOp(OpEncoding, Int(encodingAt)));
        }

        top.Add(new DictOp(OpCharStrings, Int(charStringsAt)));
        if (Private is not null)
            top.Add(new DictOp(OpPrivate, [.. Int(privateAt[Private].Size), .. Int(privateAt[Private].At)]));

        if (FontDicts is not null)
            top.Add(new DictOp(OpFdArray, Int(fdArrayAt)));

        if (FdSelectData is not null)
            top.Add(new DictOp(OpFdSelect, Int(fdSelectAt)));

        if (VariationStore is not null)
            top.Add(new DictOp(OpVstore, Int(vstoreAt)));

        top.AddRange(Top.Where(entry => entry.Op < 0));
        var topBytes = EncodeDict(top);
        if (!IsCff2)
        {
            w.U8(1).U8(0).U8(4).U8(4);
            WriteIndex(w, NameList, false);
            WriteIndex(w, [topBytes], false);
            WriteIndex(w, Strings, false);
        }
        else
        {
            w.U8(2).U8(0).U8(5).U16(topBytes.Length);
            w.Bytes(topBytes);
        }

        WriteIndex(w, GlobalSubrs, IsCff2);
        if (CharsetData is not null)
            w.Bytes(CharsetData);

        if (EncodingData is not null)
            w.Bytes(EncodingData);

        if (FdSelectData is not null)
            w.Bytes(FdSelectData);

        WriteIndex(w, CharStrings, IsCff2);
        if (FontDicts is not null)
        {
            var dicts = FontDicts.Select(fd =>
            {
                var entries = new List<DictOp>(fd.Dict);
                if (fd.Private is not null)
                    entries.Add(new DictOp(OpPrivate, [.. Int(privateAt[fd.Private].Size), .. Int(privateAt[fd.Private].At)]));

                return EncodeDict(entries);
            }).ToList();
            WriteIndex(w, dicts, IsCff2);
        }

        foreach (var p in privates)
        {
            var entries = new List<DictOp>(p.Dict);
            if (p.Subrs is not null)
                entries.Add(new DictOp(OpSubrs, Int(privateAt[p].Size)));

            w.Bytes(EncodeDict(entries));
            if (p.Subrs is not null)
                WriteIndex(w, p.Subrs, IsCff2);
        }

        if (VariationStore is not null)
            w.Bytes(VariationStore);

        return w.ToArray();
    }

    // ---- DICT encoding.

    /// <summary>A 5-byte integer operand (byte 29).</summary>
    public static byte[] Int(int value) => new BigEndianWriter().U8(29).U32((uint)value).ToArray();

    /// <summary>An integer operand in its shortest encoding.</summary>
    public static byte[] Number(int value) => value switch
    {
        >= -107 and <= 107 => [(byte)(value + 139)],
        >= 108 and <= 1131 => [(byte)((value - 108) / 256 + 247), (byte)((value - 108) % 256)],
        >= -1131 and <= -108 => [(byte)((-value - 108) / 256 + 251), (byte)((-value - 108) % 256)],
        >= short.MinValue and <= short.MaxValue => [28, (byte)(value >> 8), (byte)value],
        _ => Int(value),
    };

    /// <summary>A real operand (byte 30) of the decimal text, such as "0.001" or "-1.5E-3".</summary>
    public static byte[] Real(string text)
    {
        var nibbles = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsDigit(c))
                nibbles.Add(c - '0');
            else if (c == '.')
                nibbles.Add(0xA);
            else if (c == '-')
                nibbles.Add(0xE);
            else if (c is 'E' or 'e')
            {
                if (i + 1 < text.Length && text[i + 1] == '-')
                {
                    nibbles.Add(0xC);
                    i++;
                }
                else
                {
                    nibbles.Add(0xB);
                }
            }
        }

        nibbles.Add(0xF);
        if (nibbles.Count % 2 == 1)
            nibbles.Add(0xF);

        var bytes = new List<byte> { 30 };
        for (var i = 0; i < nibbles.Count; i += 2)
            bytes.Add((byte)(nibbles[i] << 4 | nibbles[i + 1]));

        return [.. bytes];
    }

    /// <summary>Operands of the given values, each in its shortest integer encoding.</summary>
    public static byte[] Numbers(params int[] values) => values.SelectMany(Number).ToArray();

    public static byte[] EncodeDict(IEnumerable<DictOp> entries)
    {
        var w = new BigEndianWriter();
        foreach (var entry in entries)
        {
            w.Bytes(entry.Operands);
            if (entry.Op < 0)
                continue;

            if (entry.Op >= 0x100)
                w.U8(12).U8(entry.Op & 0xFF);
            else
                w.U8(entry.Op);
        }

        return w.ToArray();
    }

    // ---- INDEX layout.

    public static int IndexSize(IReadOnlyList<byte[]> elements, bool isCff2) =>
        elements.Count == 0 ? (isCff2 ? 4 : 2) : (isCff2 ? 4 : 2) + 1 + 4 * (elements.Count + 1) + elements.Sum(e => e.Length);

    public static void WriteIndex(BigEndianWriter w, IReadOnlyList<byte[]> elements, bool isCff2)
    {
        if (isCff2)
            w.U32((uint)elements.Count);
        else
            w.U16(elements.Count);

        if (elements.Count == 0)
            return;

        w.U8(4);
        var offset = 1u;
        w.U32(offset);
        foreach (var element in elements)
        {
            offset += (uint)element.Length;
            w.U32(offset);
        }

        foreach (var element in elements)
            w.Bytes(element);
    }

    public static (List<byte[]> Elements, int End) ReadIndex(byte[] table, int at, bool isCff2)
    {
        var count = isCff2 ? (int)BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(at)) : BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(at));
        var header = isCff2 ? 4 : 2;
        if (count == 0)
            return ([], at + header);

        var offSize = table[at + header];
        var offsets = new int[count + 1];
        for (var i = 0; i <= count; i++)
        {
            var p = at + header + 1 + i * offSize;
            var value = 0;
            for (var b = 0; b < offSize; b++)
                value = value << 8 | table[p + b];

            offsets[i] = value;
        }

        var dataBase = at + header + 1 + (count + 1) * offSize - 1;
        var elements = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
            elements.Add(table.AsSpan(dataBase + offsets[i], offsets[i + 1] - offsets[i]).ToArray());

        return (elements, dataBase + offsets[count]);
    }

    /// <summary>Splits a DICT into entries, each with the raw bytes of its operands.</summary>
    public static List<DictOp> ParseDict(byte[] dict)
    {
        var entries = new List<DictOp>();
        var start = 0;
        var p = 0;
        while (p < dict.Length)
        {
            int v = dict[p];
            if (v >= 28 && v != 31 && v != 255)
            {
                if (v == 30)
                {
                    p++;
                    while (dict[p] >> 4 != 0xF && (dict[p] & 0xF) != 0xF)
                        p++;

                    p++;
                }
                else
                {
                    p += v switch
                    {
                        28 => 3,
                        29 => 5,
                        > 246 => 2,
                        _ => 1,
                    };
                }

                continue;
            }

            var operands = dict.AsSpan(start, p - start).ToArray();
            var op = v == 12 ? 0x100 | dict[++p] : v;
            p++;
            entries.Add(new DictOp(op, operands));
            start = p;
        }

        return entries;
    }

    /// <summary>The integers of an entry's operands (reals are not expected where this is used).</summary>
    public static int Integer(DictOp op, int index)
    {
        var values = new List<int>();
        var p = 0;
        var b = op.Operands;
        while (p < b.Length)
        {
            int v = b[p];
            switch (v)
            {
                case 28:
                    values.Add((short)(b[p + 1] << 8 | b[p + 2]));
                    p += 3;
                    break;
                case 29:
                    values.Add(BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p + 1)));
                    p += 5;
                    break;
                case <= 246:
                    values.Add(v - 139);
                    p += 1;
                    break;
                case <= 250:
                    values.Add((v - 247) * 256 + b[p + 1] + 108);
                    p += 2;
                    break;
                default:
                    values.Add(-(v - 251) * 256 - b[p + 1] - 108);
                    p += 2;
                    break;
            }
        }

        return values[index];
    }

    private static CffPrivate ReadPrivate(byte[] table, int size, int offset, bool isCff2)
    {
        var entries = ParseDict(table.AsSpan(offset, size).ToArray());
        var subrs = entries.SingleOrDefault(entry => entry.Op == OpSubrs);
        return new CffPrivate
        {
            Dict = entries.Where(entry => entry.Op != OpSubrs).ToList(),
            Subrs = subrs is null ? null : ReadIndex(table, offset + Integer(subrs, 0), isCff2).Elements,
        };
    }

    private static int CharsetLength(byte[] table, int at, int glyphs)
    {
        var format = table[at];
        if (format == 0)
            return 1 + 2 * (glyphs - 1);

        var p = at + 1;
        var covered = 1;
        while (covered < glyphs)
        {
            var left = format == 1 ? table[p + 2] : BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(p + 2));
            covered += left + 1;
            p += format == 1 ? 3 : 4;
        }

        return p - at;
    }

    private int OffsetOpsSize()
    {
        var size = 0;
        if (!IsCff2)
        {
            if (CharsetData is not null || CharsetOffset != 0)
                size += 6;

            if (EncodingData is not null || EncodingOffset != 0)
                size += 6;
        }

        size += 6; // CharStrings
        if (Private is not null)
            size += 11;

        if (FontDicts is not null)
            size += 7;

        if (FdSelectData is not null)
            size += 7;

        if (VariationStore is not null)
            size += 6;

        return size;
    }

    private static int DictSize(IEnumerable<DictOp> entries) => entries.Sum(e => e.Operands.Length + (e.Op < 0 ? 0 : e.Op >= 0x100 ? 2 : 1));
}

/// <summary>A Type 2 charstring assembler: integers become numbers in their shortest form, strings operators.</summary>
internal static class Charstring
{
    private static readonly Dictionary<string, int[]> s_operators = new()
    {
        ["hstem"] = [1], ["vstem"] = [3], ["vmoveto"] = [4], ["rlineto"] = [5], ["hlineto"] = [6], ["vlineto"] = [7],
        ["rrcurveto"] = [8], ["callsubr"] = [10], ["return"] = [11], ["endchar"] = [14], ["vsindex"] = [15], ["blend"] = [16],
        ["hstemhm"] = [18], ["hintmask"] = [19], ["cntrmask"] = [20], ["rmoveto"] = [21], ["hmoveto"] = [22],
        ["vstemhm"] = [23], ["rcurveline"] = [24], ["rlinecurve"] = [25], ["vvcurveto"] = [26], ["hhcurveto"] = [27],
        ["callgsubr"] = [29], ["vhcurveto"] = [30], ["hvcurveto"] = [31],
        ["dotsection"] = [12, 0], ["and"] = [12, 3], ["or"] = [12, 4], ["not"] = [12, 5], ["abs"] = [12, 9], ["add"] = [12, 10],
        ["sub"] = [12, 11], ["div"] = [12, 12], ["neg"] = [12, 14], ["eq"] = [12, 15], ["drop"] = [12, 18], ["put"] = [12, 20],
        ["get"] = [12, 21], ["ifelse"] = [12, 22], ["random"] = [12, 23], ["mul"] = [12, 24], ["sqrt"] = [12, 26],
        ["dup"] = [12, 27], ["exch"] = [12, 28], ["index"] = [12, 29], ["roll"] = [12, 30], ["hflex"] = [12, 34],
        ["flex"] = [12, 35], ["hflex1"] = [12, 36], ["flex1"] = [12, 37],
    };

    /// <summary>
    /// Assembles the items: an <see cref="int"/> (16-bit) is a number in its shortest form, a
    /// <see cref="double"/> a 16.16 number (255), a <see cref="string"/> an operator (or "op:N" for a raw byte), a
    /// <see cref="byte"/> array raw bytes.
    /// </summary>
    public static byte[] Of(params object[] items)
    {
        var bytes = new List<byte>();
        foreach (var item in items)
        {
            switch (item)
            {
                case int value when value is >= short.MinValue and <= short.MaxValue:
                    bytes.AddRange(CffModel.Number(value));
                    break;

                case double value:
                {
                    var fixedValue = (int)Math.Round(value * 65536);
                    bytes.Add(255);
                    bytes.AddRange(BitConverter.IsLittleEndian
                        ? BitConverter.GetBytes(fixedValue).Reverse()
                        : BitConverter.GetBytes(fixedValue));
                    break;
                }

                case string name when name.StartsWith("op:", StringComparison.Ordinal):
                    bytes.Add(byte.Parse(name[3..], CultureInfo.InvariantCulture));
                    break;

                case string name:
                    bytes.AddRange(s_operators[name].Select(b => (byte)b));
                    break;

                case byte[] raw:
                    bytes.AddRange(raw);
                    break;

                default:
                    throw new ArgumentException($"Cannot assemble {item}.");
            }
        }

        return [.. bytes];
    }
}
