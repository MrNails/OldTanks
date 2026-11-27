namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>A small TrueType bytecode assembler for crafted test programs.</summary>
internal sealed class Bytecode
{
    private readonly List<byte> m_bytes = [];

    public int Length => m_bytes.Count;

    /// <summary>Pushes values: PUSHB when all fit a byte, else PUSHW (NPUSHB/NPUSHW past eight).</summary>
    public Bytecode Push(params int[] values)
    {
        var bytes = values.All(value => value is >= 0 and <= 255);
        if (values.Length <= 8)
        {
            m_bytes.Add((byte)((bytes ? 0xB0 : 0xB8) + values.Length - 1));
        }
        else
        {
            m_bytes.Add(bytes ? (byte)0x40 : (byte)0x41);
            m_bytes.Add((byte)values.Length);
        }

        foreach (var value in values)
        {
            if (!bytes)
                m_bytes.Add((byte)(value >> 8));

            m_bytes.Add((byte)value);
        }

        return this;
    }

    /// <summary>Appends opcodes.</summary>
    public Bytecode Op(params int[] opcodes)
    {
        foreach (var opcode in opcodes)
            m_bytes.Add((byte)opcode);

        return this;
    }

    /// <summary>Appends <paramref name="count"/> copies of an opcode.</summary>
    public Bytecode Repeat(int opcode, int count)
    {
        for (var i = 0; i < count; i++)
            m_bytes.Add((byte)opcode);

        return this;
    }

    public byte[] ToArray() => [.. m_bytes];

    // Opcodes the tests use.
    public const int SvtcaY = 0x00, SvtcaX = 0x01, Srp0 = 0x10, Szp0 = 0x13, Szp2 = 0x15, Szps = 0x16, Sloop = 0x17, Jmpr = 0x1C;
    public const int Pop = 0x21, Clear = 0x22, LoopCall = 0x2A, Call = 0x2B, Fdef = 0x2C, Endf = 0x2D, Mdap1 = 0x2F;
    public const int IupY = 0x30, IupX = 0x31, Shpix = 0x38, Ws = 0x42, Rs = 0x43, Wcvtp = 0x44, Rcvt = 0x45, Add = 0x60;
    public const int Div = 0x62, Jrot = 0x78, InstCtrl = 0x8E, Idef = 0x89, Debug = 0x4F, Mdrp = 0xC0, MdrpRound = 0xC4;
}

/// <summary>Copies of a font with crafted hinting programs and limits.</summary>
internal static class CraftedPrograms
{
    // maxp field offsets (version 1.0).
    public const int MaxTwilightPoints = 16, MaxStorage = 18, MaxFunctionDefs = 20, MaxInstructionDefs = 22;
    public const int MaxStackElements = 24;

    /// <summary>A copy whose <c>fpgm</c>, <c>prep</c> or <c>cvt </c> is replaced; null removes the table.</summary>
    public static byte[] WithPrograms(byte[] font, byte[]? fpgm, byte[]? prep, byte[]? cvt = null, bool keepCvt = true)
    {
        font = fpgm is null ? CraftedFonts.WithoutTable(font, "fpgm") : CraftedFonts.WithTable(font, "fpgm", fpgm);
        font = prep is null ? CraftedFonts.WithoutTable(font, "prep") : CraftedFonts.WithTable(font, "prep", prep);
        if (cvt is not null)
            font = CraftedFonts.WithTable(font, "cvt ", cvt);
        else if (!keepCvt)
            font = CraftedFonts.WithoutTable(font, "cvt ");

        return font;
    }

    /// <summary>A copy with one <c>maxp</c> field set.</summary>
    public static byte[] WithMaxp(byte[] font, int offset, int value) =>
        CraftedFonts.Patch(font, "maxp", maxp => CraftedFonts.WriteU16(maxp, offset, value));

    /// <summary>A copy whose simple glyph <paramref name="glyphIndex"/> has <paramref name="program"/> as its instructions.</summary>
    public static byte[] WithGlyphProgram(byte[] font, int glyphIndex, byte[] program)
    {
        var glyphs = CraftedFonts.ReadGlyphs(font);
        glyphs[glyphIndex] = ReplaceInstructions(glyphs[glyphIndex], program);
        return CraftedFonts.WithGlyphs(font, glyphs);
    }

    /// <summary>A simple glyph's bytes with its instructions replaced.</summary>
    public static byte[] ReplaceInstructions(byte[] glyph, byte[] program)
    {
        var contours = (short)CraftedFonts.U16(glyph, 0);
        if (contours <= 0)
            throw new ArgumentException("Not a simple glyph.", nameof(glyph));

        var lengthOffset = 10 + 2 * contours;
        var oldLength = CraftedFonts.U16(glyph, lengthOffset);
        var rest = glyph.AsSpan(lengthOffset + 2 + oldLength);
        var result = new byte[lengthOffset + 2 + program.Length + rest.Length];
        glyph.AsSpan(0, lengthOffset).CopyTo(result);
        CraftedFonts.WriteU16(result, lengthOffset, program.Length);
        program.CopyTo(result, lengthOffset + 2);
        rest.CopyTo(result.AsSpan(lengthOffset + 2 + program.Length));
        return result;
    }

    /// <summary>The number of points of a simple glyph, without phantom points.</summary>
    public static int PointCount(byte[] font, int glyphIndex)
    {
        var glyph = CraftedFonts.ReadGlyphs(font)[glyphIndex];
        var contours = (short)CraftedFonts.U16(glyph, 0);
        return CraftedFonts.U16(glyph, 10 + 2 * (contours - 1)) + 1;
    }

    /// <summary>
    /// A valid program of exactly <paramref name="length"/> bytes whose table checksum is <paramref name="checksum"/>: an
    /// NPUSHB of eight bytes, of which four are free to adjust the sum, a CLEAR, then SVTCA[y] padding.
    /// </summary>
    public static byte[] ProgramWithChecksum(int length, uint checksum)
    {
        var program = new byte[length];
        program[0] = 0x40;
        program[1] = 8;
        program[10] = 0x22;
        var sum = CraftedFonts.Checksum(program);
        var word = checksum - sum;
        program[4] = (byte)(word >> 24);
        program[5] = (byte)(word >> 16);
        program[6] = (byte)(word >> 8);
        program[7] = (byte)word;
        return program;
    }

    /// <summary>
    /// A <c>ttcf</c> collection of faces given as tables, where faces holding the same table (the same array) share one record
    /// (as in FontLoaderTests).
    /// </summary>
    public static byte[] SharedCollection(IReadOnlyList<IReadOnlyList<CraftedTable>> faces)
    {
        var header = 12 + 4 * faces.Count;
        var directories = faces.Select(face => 12 + 16 * face.Count).ToArray();
        var output = new MemoryStream();
        output.Write(new byte[header + directories.Sum()]);
        var offsets = new Dictionary<byte[], uint>(ReferenceEqualityComparer.Instance);
        foreach (var table in faces.SelectMany(face => face))
        {
            if (offsets.ContainsKey(table.Data))
                continue;

            offsets[table.Data] = (uint)output.Length;
            output.Write(table.Data);
            output.Write(new byte[(4 - table.Data.Length % 4) % 4]);
        }

        var result = output.ToArray();
        System.Text.Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
        CraftedFonts.WriteU16(result, 4, 1);
        CraftedFonts.WriteU32(result, 8, (uint)faces.Count);
        var at = header;
        for (var i = 0; i < faces.Count; i++)
        {
            CraftedFonts.WriteU32(result, 12 + 4 * i, (uint)at);
            CraftedFonts.WriteU32(result, at, CraftedFonts.TrueTypeVersion);
            CraftedFonts.WriteU16(result, at + 4, faces[i].Count);
            var sorted = faces[i].OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
            for (var t = 0; t < sorted.Count; t++)
            {
                var record = at + 12 + 16 * t;
                System.Text.Encoding.ASCII.GetBytes(sorted[t].Tag).CopyTo(result, record);
                CraftedFonts.WriteU32(result, record + 8, offsets[sorted[t].Data]);
                CraftedFonts.WriteU32(result, record + 12, (uint)sorted[t].Data.Length);
            }

            at += directories[i];
        }

        return result;
    }
}
