using System.Buffers.Binary;
using System.Text;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>One table of a crafted font: its tag and bytes.</summary>
public sealed record CraftedTable(string Tag, byte[] Data);

/// <summary>
/// Builds copies of test fonts with one change each: tables replaced, removed, added or patched, a glyph replaced. Every copy
/// gets correct table checksums and a correct <c>head.checkSumAdjustment</c> unless the caller asks otherwise.
/// </summary>
internal static class CraftedFonts
{
    public const uint TrueTypeVersion = 0x00010000;
    public const uint OpenTypeCffVersion = 0x4F54544F; // OTTO

    /// <summary>A fresh copy of the repository's Arial, the base of most crafted fonts.</summary>
    public static byte[] Arial() => FontCache.Arial();

    /// <summary>The sfnt version of a single-face font.</summary>
    public static uint Version(byte[] font) => BinaryPrimitives.ReadUInt32BigEndian(font);

    /// <summary>The tables of a single-face font in directory order.</summary>
    public static List<CraftedTable> ReadTables(byte[] font, int faceOffset = 0)
    {
        var count = U16(font, faceOffset + 4);
        var tables = new List<CraftedTable>(count);
        for (var i = 0; i < count; i++)
        {
            var at = faceOffset + 12 + 16 * i;
            var tag = Encoding.ASCII.GetString(font, at, 4);
            var offset = (int)U32(font, at + 8);
            var length = (int)U32(font, at + 12);
            tables.Add(new CraftedTable(tag, font.AsSpan(offset, length).ToArray()));
        }

        return tables;
    }

    /// <summary>A copy of the table's bytes.</summary>
    public static byte[] Table(byte[] font, string tag) => ReadTables(font).Single(table => table.Tag == tag).Data;

    /// <summary>Lays tables out as a single-face sfnt: directory sorted by tag (unless <paramref name="sort"/> is false), each
    /// table 4-byte aligned and zero-padded, checksums and <c>checkSumAdjustment</c> computed unless <paramref name="checksums"/> is false.</summary>
    public static byte[] Build(uint version, IEnumerable<CraftedTable> tables, bool sort = true, bool checksums = true)
    {
        var list = sort ? tables.OrderBy(table => table.Tag, StringComparer.Ordinal).ToList() : tables.ToList();
        var directoryLength = 12 + 16 * list.Count;
        var length = directoryLength + list.Sum(table => Pad(table.Data.Length));
        var font = new byte[length];

        WriteU32(font, 0, version);
        WriteU16(font, 4, list.Count);
        var entrySelector = list.Count == 0 ? 0 : (int)Math.Floor(Math.Log2(list.Count));
        WriteU16(font, 6, 16 << entrySelector);
        WriteU16(font, 8, entrySelector);
        WriteU16(font, 10, list.Count * 16 - (16 << entrySelector));

        var offset = directoryLength;
        var headOffset = -1;
        for (var i = 0; i < list.Count; i++)
        {
            var table = list[i];
            var data = (byte[])table.Data.Clone();
            if (table.Tag == "head" && data.Length >= 12)
            {
                WriteU32(data, 8, 0);
                headOffset = offset;
            }

            data.CopyTo(font, offset);
            var record = 12 + 16 * i;
            Encoding.ASCII.GetBytes(table.Tag).CopyTo(font, record);
            WriteU32(font, record + 4, checksums ? Checksum(data) : Checksum(data) ^ 0x1234);
            WriteU32(font, record + 8, (uint)offset);
            WriteU32(font, record + 12, (uint)data.Length);
            offset += Pad(data.Length);
        }

        if (headOffset >= 0)
            WriteU32(font, headOffset + 8, 0xB1B0AFBA - Checksum(font));

        return font;
    }

    /// <summary>A copy with the table replaced, or added when the font has none.</summary>
    public static byte[] WithTable(byte[] font, string tag, byte[] data)
    {
        var tables = ReadTables(font).Where(table => table.Tag != tag).ToList();
        tables.Add(new CraftedTable(tag, data));
        return Build(Version(font), tables);
    }

    /// <summary>A copy without the table.</summary>
    public static byte[] WithoutTable(byte[] font, params string[] tags) =>
        Build(Version(font), ReadTables(font).Where(table => !tags.Contains(table.Tag)));

    /// <summary>A copy with the table changed by <paramref name="patch"/> (which receives a copy of its bytes).</summary>
    public static byte[] Patch(byte[] font, string tag, Action<byte[]> patch)
    {
        var data = Table(font, tag);
        patch(data);
        return WithTable(font, tag, data);
    }

    /// <summary>A copy whose table is cut to <paramref name="length"/> bytes.</summary>
    public static byte[] Truncate(byte[] font, string tag, int length) => WithTable(font, tag, Table(font, tag)[..length]);

    /// <summary>The glyphs of a TrueType font, one byte array each.</summary>
    public static List<byte[]> ReadGlyphs(byte[] font)
    {
        var head = Table(font, "head");
        var maxp = Table(font, "maxp");
        var loca = Table(font, "loca");
        var glyf = Table(font, "glyf");
        var longOffsets = U16(head, 50) == 1;
        var count = U16(maxp, 4);
        var glyphs = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
        {
            var start = longOffsets ? (int)U32(loca, 4 * i) : U16(loca, 2 * i) * 2;
            var end = longOffsets ? (int)U32(loca, 4 * i + 4) : U16(loca, 2 * i + 2) * 2;
            glyphs.Add(glyf[start..end]);
        }

        return glyphs;
    }

    /// <summary>A copy with new glyph data (as many glyphs as <c>maxp.numGlyphs</c>), 32-bit <c>loca</c> offsets.</summary>
    public static byte[] WithGlyphs(byte[] font, IReadOnlyList<byte[]> glyphs)
    {
        var glyf = new MemoryStream();
        var loca = new byte[4 * (glyphs.Count + 1)];
        for (var i = 0; i < glyphs.Count; i++)
        {
            WriteU32(loca, 4 * i, (uint)glyf.Length);
            glyf.Write(glyphs[i]);
        }

        WriteU32(loca, 4 * glyphs.Count, (uint)glyf.Length);

        var head = Table(font, "head");
        WriteU16(head, 50, 1);

        var tables = ReadTables(font).Where(table => table.Tag is not ("glyf" or "loca" or "head")).ToList();
        tables.Add(new CraftedTable("glyf", glyf.ToArray()));
        tables.Add(new CraftedTable("loca", loca));
        tables.Add(new CraftedTable("head", head));
        return Build(Version(font), tables);
    }

    /// <summary>
    /// A copy with glyphs added after the last one: <c>maxp.numGlyphs</c> raised, each new glyph given a left side bearing of 0 in
    /// <c>hmtx</c> (so it shares the last advance), <c>maxp.maxComponentDepth</c> raised to <paramref name="componentDepth"/>.
    /// </summary>
    /// <returns>The font and the index of the first new glyph.</returns>
    public static (byte[] Font, int FirstGlyph) AppendGlyphs(byte[] font, int componentDepth, params byte[][] glyphs) =>
        AppendGlyphs(font, componentDepth, 0, glyphs);

    /// <summary>
    /// As <see cref="AppendGlyphs(byte[], int, byte[][])"/>, with every new glyph's left side bearing set to
    /// <paramref name="leftSideBearing"/> (equal to a glyph's x minimum, it keeps the outline where the glyph draws it).
    /// </summary>
    public static (byte[] Font, int FirstGlyph) AppendGlyphs(byte[] font, int componentDepth, short leftSideBearing,
        params byte[][] glyphs)
    {
        var all = ReadGlyphs(font);
        var first = all.Count;
        all.AddRange(glyphs);

        var maxp = Table(font, "maxp");
        WriteU16(maxp, 4, all.Count);
        WriteU16(maxp, 30, Math.Max(U16(maxp, 30), componentDepth));

        var longMetrics = U16(Table(font, "hhea"), 34);
        var used = 4 * longMetrics + 2 * (first - longMetrics);
        var hmtx = Table(font, "hmtx")[..used].Concat(new byte[2 * glyphs.Length]).ToArray();
        for (var i = 0; i < glyphs.Length; i++)
            WriteU16(hmtx, used + 2 * i, leftSideBearing);
        var tables = ReadTables(WithGlyphs(font, all)).Where(table => table.Tag is not ("maxp" or "hmtx")).ToList();
        tables.Add(new CraftedTable("maxp", maxp));
        tables.Add(new CraftedTable("hmtx", hmtx));

        return (Build(Version(font), tables), first);
    }

    /// <summary>A copy with one glyph's data replaced.</summary>
    public static byte[] WithGlyph(byte[] font, int glyphIndex, byte[] glyph)
    {
        var glyphs = ReadGlyphs(font);
        glyphs[glyphIndex] = glyph;
        return WithGlyphs(font, glyphs);
    }

    /// <summary>The index of the first glyph whose <c>numberOfContours</c> passes <paramref name="predicate"/>.</summary>
    public static int FindGlyph(byte[] font, Func<int, bool> predicate)
    {
        var glyphs = ReadGlyphs(font);
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (glyphs[i].Length >= 10 && predicate((short)U16(glyphs[i], 0)))
                return i;
        }

        throw new InvalidOperationException("No glyph matches.");
    }

    /// <summary>Wraps single-face fonts into a <c>ttcf</c> collection (version 1.0), sharing nothing.</summary>
    public static byte[] Collection(params byte[][] faces)
    {
        var header = 12 + 4 * faces.Length;
        var output = new MemoryStream();
        output.Write(new byte[header]);
        var offsets = new uint[faces.Length];
        for (var i = 0; i < faces.Length; i++)
        {
            offsets[i] = (uint)output.Length;
            var face = (byte[])faces[i].Clone();

            // Move every table offset by where the face lands in the collection.
            for (var t = 0; t < U16(face, 4); t++)
            {
                var record = 12 + 16 * t + 8;
                WriteU32(face, record, U32(face, record) + offsets[i]);
            }

            output.Write(face);
        }

        var result = output.ToArray();
        Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
        WriteU16(result, 4, 1);
        WriteU16(result, 6, 0);
        WriteU32(result, 8, (uint)faces.Length);
        for (var i = 0; i < faces.Length; i++)
            WriteU32(result, 12 + 4 * i, offsets[i]);

        return result;
    }

    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var j = 0; j < 4; j++)
                word = word << 8 | (i + j < data.Length ? data[i + j] : 0u);

            sum += word;
        }

        return sum;
    }

    public static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

    public static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));

    public static void WriteU16(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), (ushort)value);

    public static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);

    private static int Pad(int length) => (length + 3) & ~3;
}

/// <summary>Writes big-endian structures for crafted tables.</summary>
internal sealed class BigEndianWriter
{
    private readonly MemoryStream m_stream = new();

    public int Position => (int)m_stream.Length;

    public BigEndianWriter U8(int value)
    {
        m_stream.WriteByte((byte)value);
        return this;
    }

    public BigEndianWriter U16(int value)
    {
        m_stream.WriteByte((byte)(value >> 8));
        m_stream.WriteByte((byte)value);
        return this;
    }

    public BigEndianWriter U24(int value)
    {
        m_stream.WriteByte((byte)(value >> 16));
        return U16(value);
    }

    public BigEndianWriter U32(uint value)
    {
        U16((int)(value >> 16));
        return U16((int)value);
    }

    public BigEndianWriter Bytes(ReadOnlySpan<byte> bytes)
    {
        m_stream.Write(bytes);
        return this;
    }

    public byte[] ToArray() => m_stream.ToArray();
}
