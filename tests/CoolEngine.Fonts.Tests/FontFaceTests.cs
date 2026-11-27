using CoolEngine.Fonts.Tests.Reference;
using static CoolEngine.Fonts.Tests.Reference.CraftedFonts;

namespace CoolEngine.Fonts.Tests;

[TestFixture]
public sealed class FontFaceTests
{
    private const int MaxCodePoint = 0x10FFFF;
    private const int MaxReported = 10;

    private FreeTypeReference m_freeType = null!;
    private DirectWriteReference m_directWrite = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        m_freeType = new FreeTypeReference();
        m_directWrite = new DirectWriteReference();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        m_freeType.Dispose();
        m_directWrite.Dispose();
    }

    private static IEnumerable<TestCaseData> AllFaces() =>
        TestFonts.AllFaces().Select(face => new TestCaseData(face).SetArgDisplayNames(face.Name));

    /// <summary>The faces without an <c>fvar</c> table.</summary>
    private static IEnumerable<TestCaseData> NonVariableFaces() =>
        TestFonts.AllFaces()
            .Where(face => !FontCache.Face(face).IsVariable)
            .Select(face => new TestCaseData(face).SetArgDisplayNames(face.Name));

    [TestCaseSource(nameof(AllFaces))]
    public void GetGlyphIndex_EveryCodePoint_MatchesFreeType(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_freeType.OpenFace(FontCache.Bytes(testFace.Path), testFace.FaceIndex);

        var mismatches = new List<string>();
        var count = 0;
        for (var codePoint = 0; codePoint <= MaxCodePoint; codePoint++)
        {
            var ours = face.GetGlyphIndex(codePoint);
            var expected = reference.GetCharIndex(codePoint);
            if (ours == expected)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"U+{codePoint:X4}: {ours}, FreeType {expected}");
        }

        Assert.That(count, Is.Zero, $"{count} code points differ: {string.Join("; ", mismatches)}");
    }

    [TestCaseSource(nameof(AllFaces))]
    public void GetGlyphIndex_EveryCodePoint_MatchesDirectWrite(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_directWrite.OpenFace(testFace.Path, testFace.FaceIndex);
        var codePoints = Enumerable.Range(0, 0x10000).Select(codePoint => (uint)codePoint).ToArray();

        var expected = reference.GetGlyphIndices(codePoints);

        var mismatches = new List<string>();
        var count = 0;
        foreach (var codePoint in codePoints)
        {
            var ours = face.GetGlyphIndex((int)codePoint);
            if (ours == expected[codePoint])
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"U+{codePoint:X4}: {ours}, DirectWrite {expected[codePoint]}");
        }

        Assert.That(count, Is.Zero, $"{count} code points differ: {string.Join("; ", mismatches)}");
    }

    [Test]
    public void GetGlyphIndexWithSelector_FontWithFormat14_MatchesFreeType()
    {
        var (name, data, faceIndex) = FontWithFormat14();
        var face = FontLoader.Load(data).Faces[faceIndex];
        using var reference = m_freeType.OpenFace(data, faceIndex);

        var pairs = Format14Pairs(data, faceIndex).ToList();
        Assert.That(pairs, Is.Not.Empty, $"{name} maps variation sequences");

        // Every mapped pair, each with an unmapped neighbour and an unknown selector.
        var mismatches = new List<string>();
        var count = 0;
        foreach (var (codePoint, selector) in pairs.SelectMany(p => new[] { p, (p.CodePoint + 1, p.Selector), (p.CodePoint, 0xFE00 + 15) }))
        {
            var ours = face.GetGlyphIndex(codePoint, selector);
            var expected = reference.GetCharVariantIndex(codePoint, selector);
            if (ours == expected)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"U+{codePoint:X4} U+{selector:X4}: {ours}, FreeType {expected}");
        }

        Assert.That(count, Is.Zero, $"{name}: {count} sequences differ: {string.Join("; ", mismatches)}");
    }

    [TestCaseSource(nameof(AllFaces))]
    public void IsVariable_EachFace_MatchesFreeTypeMultipleMasters(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_freeType.OpenFace(FontCache.Bytes(testFace.Path), testFace.FaceIndex);

        Assert.That(face.IsVariable, Is.EqualTo(reference.HasMultipleMasters));
    }

    /// <summary>
    /// FreeType takes a (0, 5) format-14 subtable listed last for the Unicode map, and then maps nothing; ours maps through the
    /// real Unicode subtable (Denys: "Ours works, documented").
    /// </summary>
    [Test]
    public void GetGlyphIndex_Format14ListedLast_MapsThroughUnicodeSubtable()
    {
        var cmap = CraftedTables.Cmap((0, 3, CraftedTables.SimpleFormat4()), (0, 5, CraftedTables.Format14((0xFE0E, [], [('A', 37)]))));
        var data = WithTable(Arial(), "cmap", cmap);
        var face = FontLoader.Load(data).Faces[0];
        using var reference = m_freeType.OpenFace(data, 0);

        Assert.Multiple(() =>
        {
            Assert.That(reference.GetCharIndex('A'), Is.Zero, "FreeType maps nothing through the format-14 subtable");
            Assert.That(face.GetGlyphIndex('A'), Is.EqualTo(36), "ours maps 'A' through the (0, 3) format 4 subtable");
            Assert.That(face.GetGlyphIndex('A', 0xFE0E), Is.EqualTo(37), "and the variation sequence through format 14");
        });
    }

    [TestCaseSource(nameof(AllFaces))]
    public void Properties_EveryFace_MatchFreeType(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_freeType.OpenFace(FontCache.Bytes(testFace.Path), testFace.FaceIndex);

        Assert.Multiple(() =>
        {
            Assert.That(face.FamilyName, Is.EqualTo(reference.FamilyName), nameof(face.FamilyName));
            Assert.That(face.StyleName, Is.EqualTo(reference.StyleName), nameof(face.StyleName));
            Assert.That(face.UnitsPerEm, Is.EqualTo(reference.UnitsPerEm), nameof(face.UnitsPerEm));
            Assert.That(face.GlyphCount, Is.EqualTo(reference.NumGlyphs), nameof(face.GlyphCount));
            Assert.That(face.Ascender, Is.EqualTo(reference.Ascender), nameof(face.Ascender));
            Assert.That(face.Descender, Is.EqualTo(reference.Descender), nameof(face.Descender));
            Assert.That(face.Height, Is.EqualTo(reference.Height), nameof(face.Height));
        });
    }

    [TestCaseSource(nameof(AllFaces))]
    public void GetAdvanceWidth_EveryGlyph_MatchesFreeType(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_freeType.OpenFace(FontCache.Bytes(testFace.Path), testFace.FaceIndex);

        var mismatches = new List<string>();
        var count = 0;
        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
        {
            var ours = face.GetAdvanceWidth(glyph);
            var expected = reference.GetDesignAdvance(glyph);
            if (ours == expected)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {ours}, FreeType {expected}");
        }

        Assert.That(count, Is.Zero, $"{count} advances differ: {string.Join("; ", mismatches)}");
    }

    /// <summary>Non-variable faces only: DirectWrite takes a variable face's advances from its glyph loads (OT-4 WP5 compares those).</summary>
    [TestCaseSource(nameof(NonVariableFaces))]
    public void GetAdvanceWidth_EveryGlyph_MatchesDirectWrite(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var reference = m_directWrite.OpenFace(testFace.Path, testFace.FaceIndex);

        var expected = reference.GetDesignAdvances();

        Assert.That(expected, Has.Length.EqualTo(face.GlyphCount), "glyph count");
        var mismatches = new List<string>();
        var count = 0;
        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
        {
            var ours = face.GetAdvanceWidth(glyph);
            if (ours == expected[glyph])
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {ours}, DirectWrite {expected[glyph]}");
        }

        Assert.That(count, Is.Zero, $"{count} advances differ: {string.Join("; ", mismatches)}");
    }

    [Test]
    public void GetSvgDocument_CraftedSvgTable_ReturnsDocuments()
    {
        var plain = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0h10v10z\"/></svg>"u8.ToArray();
        var zipped = "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"5\"/></svg>"u8.ToArray();
        var font = WithTable(Arial(), "SVG ", CraftedTables.Svg((10, 12, plain), (20, 20, FontLoaderTests.Compress(zipped))));

        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.GetSvgDocument(10).ToArray(), Is.EqualTo(plain), "first glyph of the plain range");
            Assert.That(face.GetSvgDocument(12).ToArray(), Is.EqualTo(plain), "last glyph of the plain range");
            Assert.That(face.GetSvgDocument(20).ToArray(), Is.EqualTo(zipped), "the gzipped document, inflated");
            Assert.That(face.GetSvgDocument(9).IsEmpty, Is.True, "before the index");
            Assert.That(face.GetSvgDocument(13).IsEmpty, Is.True, "between the ranges");
            Assert.That(face.GetSvgDocument(21).IsEmpty, Is.True, "after the index");
        });
    }

    [Test]
    public void Load_BitmapOnlyCopy_OutlineFormatNoneAndEmptyGlyphs()
    {
        // Minimal EBLC (version 2.0, no strikes) and EBDT (version 2.0) headers: the bitmaps are never read.
        var eblc = new BigEndianWriter().U32(0x00020000).U32(0).ToArray();
        var ebdt = new BigEndianWriter().U32(0x00020000).ToArray();
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca" or "EBLC" or "EBDT")).ToList();
        tables.Add(new CraftedTable("EBLC", eblc));
        tables.Add(new CraftedTable("EBDT", ebdt));

        var face = FontLoader.Load(Build(TrueTypeVersion, tables)).Faces[0];
        var font = face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });
        var bitmaps = Enumerable.Range(0, face.GlyphCount).Select(glyph => font.Render(glyph)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(face.OutlineFormat, Is.EqualTo(OutlineFormat.None));
            Assert.That(bitmaps.All(bitmap => bitmap is { Width: 0, Height: 0 } && bitmap.Pixels.Length == 0), Is.True, "every bitmap is 0 x 0");
            Assert.That(font.GetOutline(face.GetGlyphIndex('A')).Points.Length, Is.Zero, "'A' has no outline");
        });
    }

    /// <summary>The first test face with a (0, 5) format-14 subtable: <c>seguiemj.ttf</c> has one.</summary>
    private static (string Name, byte[] Data, int FaceIndex) FontWithFormat14()
    {
        foreach (var testFace in TestFonts.AllFaces())
        {
            var data = FontCache.Bytes(testFace.Path);
            if (Format14Offset(data, testFace.FaceIndex) >= 0)
                return (testFace.Name, data, testFace.FaceIndex);
        }

        throw new InvalidOperationException("No test face has a format-14 'cmap' subtable; craft one.");
    }

    private static int Format14Offset(byte[] data, int faceIndex)
    {
        var faceOffset = U32(data, 0) == 0x74746366 ? (int)U32(data, 12 + 4 * faceIndex) : 0;
        var tables = ReadTables(data, faceOffset);
        var cmap = tables.SingleOrDefault(table => table.Tag == "cmap")?.Data;
        if (cmap is null)
            return -1;

        for (var i = 0; i < U16(cmap, 2); i++)
        {
            var offset = (int)U32(cmap, 8 + 8 * i);
            if (U16(cmap, 4 + 8 * i) == 0 && U16(cmap, 6 + 8 * i) == 5 && U16(cmap, offset) == 14)
                return offset;
        }

        return -1;
    }

    /// <summary>Every (code point, selector) pair a format-14 subtable maps, default or not.</summary>
    private static IEnumerable<(int CodePoint, int Selector)> Format14Pairs(byte[] data, int faceIndex)
    {
        var faceOffset = U32(data, 0) == 0x74746366 ? (int)U32(data, 12 + 4 * faceIndex) : 0;
        var cmap = ReadTables(data, faceOffset).Single(table => table.Tag == "cmap").Data;
        var offset = Format14Offset(data, faceIndex);
        var records = (int)U32(cmap, offset + 6);
        for (var i = 0; i < records; i++)
        {
            var at = offset + 10 + 11 * i;
            var selector = (int)(U32(cmap, at - 1) & 0xFFFFFF);
            var defaults = (int)U32(cmap, at + 3);
            var nonDefaults = (int)U32(cmap, at + 7);
            if (defaults != 0)
            {
                for (var r = 0; r < (int)U32(cmap, offset + defaults); r++)
                {
                    var range = offset + defaults + 4 + 4 * r;
                    var start = (int)(U32(cmap, range - 1) & 0xFFFFFF);
                    for (var c = 0; c <= cmap[range + 3]; c++)
                        yield return (start + c, selector);
                }
            }

            if (nonDefaults != 0)
            {
                for (var m = 0; m < (int)U32(cmap, offset + nonDefaults); m++)
                    yield return ((int)(U32(cmap, offset + nonDefaults + 4 + 5 * m - 1) & 0xFFFFFF), selector);
            }
        }
    }
}
