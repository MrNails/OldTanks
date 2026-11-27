using System.Text;
using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;
using static CoolEngine.Fonts.Tests.Reference.CraftedFonts;

namespace CoolEngine.Fonts.Tests;

[TestFixture]
public sealed partial class CffOutlineSourceTests
{
    private const int MaxReported = 5;

    // The glyph crafted charstrings replace, and the sizes they are compared at.
    private const int Crafted = 1;
    private static readonly int[] s_craftedSizes = [9, 32, 72];

    // Type 2 local subroutine numbers are biased by 107 below 1,240 subroutines.
    private const int Bias = 107;

    private FreeTypeReference m_freeType = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => m_freeType = new FreeTypeReference();

    [OneTimeTearDown]
    public void OneTimeTearDown() => m_freeType.Dispose();

    // ==== Fidelity: every glyph of Source Sans 3 OTF and its CFF2 variable font (default instance) against FreeType.

    /// <summary>Every CFF and CFF2 face: every glyph at 9, 12, 16, 32 and 72 px, the game's characters at the other sizes 8..24.</summary>
    private static IEnumerable<TestCaseData> FacesAndSizes()
    {
        foreach (var face in TestFonts.CffFaces())
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
                yield return new TestCaseData(face, size, GlyphSet.Every).SetArgDisplayNames(face.Name, $"{size}px", "every glyph");

            foreach (var size in TestFonts.CharacterSizes.Except(TestFonts.EveryGlyphSizes))
                yield return new TestCaseData(face, size, GlyphSet.GameCharacters).SetArgDisplayNames(face.Name, $"{size}px", "game characters");
        }
    }

    private static IEnumerable<TestCaseData> FacesAndAllSizes()
    {
        foreach (var face in TestFonts.CffFaces())
        {
            foreach (var size in TestFonts.CharacterSizes.Union(TestFonts.EveryGlyphSizes).Order())
                yield return new TestCaseData(face, size).SetArgDisplayNames(face.Name, $"{size}px");
        }
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetOutline_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, size);
        using var _ = reference;

        var mismatches = CompareOutlines(font, reference, Glyphs(font.Face, glyphs));

        Assert.That(mismatches, Is.Empty);
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetMetrics_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, size);
        using var _ = reference;

        var mismatches = CompareMetrics(font, reference, Glyphs(font.Face, glyphs));

        Assert.That(mismatches, Is.Empty);
    }

    [TestCaseSource(nameof(FacesAndAllSizes))]
    public void LineMetrics_Unhinted_MatchFreeType(TestFace testFace, int size)
    {
        var (font, reference) = Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, size);
        using var _ = reference;

        Assert.That(font.LineMetrics, Is.EqualTo(reference.SizeMetrics));
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void Render_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, size);
        using var _ = reference;

        var comparison = CompareBitmaps(font, reference, Glyphs(font.Face, glyphs));

        Assert.Multiple(() =>
        {
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
            Assert.That(comparison.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    // ==== The corpus: every CFF face reachable without a download.

    /// <summary>Every OpenType CFF file in <c>C:\Windows\Fonts</c> (on this machine ROGFonts-Regular_0.otf) and the two Source Sans 3 OTFs.</summary>
    private static IEnumerable<TestCaseData> CorpusFiles() =>
        Directory.EnumerateFiles(TestFonts.WindowsFonts, "*.otf")
            .Concat(TestFonts.CffFaces().Select(face => face.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    [TestCaseSource(nameof(CorpusFiles))]
    public void Render_CorpusFace_EveryGlyphMatchesFreeType(string path)
    {
        var data = FontCache.Bytes(path);
        var faces = FontLoader.Load(data).Faces;
        var differences = new List<string>();

        for (var faceIndex = 0; faceIndex < faces.Count; faceIndex++)
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
            {
                var (font, reference) = Open(data, faceIndex, size);
                using var _ = reference;
                var glyphs = Enumerable.Range(0, font.Face.GlyphCount).ToArray();
                differences.AddRange(CompareOutlines(font, reference, glyphs).Select(d => $"face {faceIndex} {size}px outline {d}"));
                differences.AddRange(CompareMetrics(font, reference, glyphs).Select(d => $"face {faceIndex} {size}px metrics {d}"));
                var bitmaps = CompareBitmaps(font, reference, glyphs);
                if (bitmaps.Count > 0)
                    differences.Add($"face {faceIndex} {size}px {bitmaps}");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(faces.Select(face => face.OutlineFormat), Is.All.AnyOf(OutlineFormat.Cff, OutlineFormat.Cff2));
            Assert.That(differences, Is.Empty);
        });
    }

    // ==== The test model: a CFF table taken apart and laid out again reads as the original.

    [TestCase(false)]
    [TestCase(true)]
    public void GetOutline_RewrittenSourceSansTable_MatchesFreeType(bool cff2)
    {
        var font = CffModel.FromFont(Source(cff2), cff2).InFont(Source(cff2));

        AssertGlyphsMatchFreeType(font, Enumerable.Range(0, 400).ToArray(), 16, 72);
    }

    // ==== CID-keyed fonts (a crafted CID-keyed copy of SourceSans3-Regular.otf).

    /// <summary>
    /// The variants: two Font DICTs chosen by a format-3 FDSelect; three by a format-0 one, with a sheared Font DICT matrix, a
    /// Font DICT of 2,000 units per em and a Private DICT shared by two Font DICTs; a Top DICT matrix with an offset, one Font
    /// DICT inheriting it and one with its own matrix concatenated to it.
    /// </summary>
    private static IEnumerable<TestCaseData> CidVariants()
    {
        yield return new TestCaseData(0).SetArgDisplayNames("two FDs, FDSelect 3");
        yield return new TestCaseData(1).SetArgDisplayNames("three FDs, FDSelect 0, FD matrices");
        yield return new TestCaseData(2).SetArgDisplayNames("Top matrix with offset");
    }

    [TestCaseSource(nameof(CidVariants))]
    public void GetOutline_CidKeyedCopy_EveryGlyphMatchesFreeType(int variant)
    {
        var font = CidKeyedSourceSans(variant);
        var face = FontLoader.Load(font).Faces[0];

        AssertGlyphsMatchFreeType(font, Enumerable.Range(0, face.GlyphCount).ToArray(), 9, 16, 32, 72);
    }

    // ==== seac: endchar with accent operands.

    [TestCase(false)]
    [TestCase(true)]
    public void GetOutline_SeacGlyph_MatchesFreeType(bool withWidth)
    {
        // 'A' (Standard Encoding 65) with 'acute' (194) raised and moved right.
        var items = new List<object> { 120, 230, 65, 194, "endchar" };
        if (withWidth)
            items.Insert(0, 600);

        var font = Craft(false, model => model.CharStrings[Crafted] = Charstring.Of([.. items]));

        var outline = AssertGlyphsMatchFreeType(font, [Crafted], s_craftedSizes);
        Assert.That(outline.Points.Length, Is.GreaterThan(10), "the base and the accent are drawn");
    }

    /// <summary>
    /// A small font (80 glyphs, each a box of its own size) whose charset is predefined: ISOAdobe (offset 0), Expert (1) or
    /// Expert Subset (2). Glyph 1 is a seac of comma (code 44, SID 13) and period (code 46, SID 15), which these charsets put
    /// at different glyphs.
    /// </summary>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void GetOutline_SeacWithPredefinedCharset_MatchesFreeType(int charset)
    {
        const int glyphs = 80;
        var model = CffModel.Minimal(glyphs, isCff2: false);
        model.CharsetData = null;
        model.CharsetOffset = charset;
        for (var g = 0; g < glyphs; g++)
            model.CharStrings[g] = Charstring.Of(10 * g, 0, "rmoveto", 20 + 5 * g, 0, "rlineto", 0, 30 + 7 * g, "rlineto", "endchar");

        model.CharStrings[Crafted] = Charstring.Of(100, 300, 44, 46, "endchar");
        var font = SmallOtto(glyphs, model);

        var outline = AssertGlyphsMatchFreeType(font, [Crafted, .. Enumerable.Range(0, glyphs)], 32);
        Assert.That(outline.Points.Length, Is.EqualTo(6), "both components drawn");
    }

    [Test]
    public void GetOutline_SeacCodeWithoutGlyph_ThrowsWhereFreeTypeFails() =>
        AssertFailsAsFreeType(Craft(false, model => model.CharStrings[Crafted] = Charstring.Of(0, 0, 65, 300, "endchar")),
            "seac character code 300");

    [Test]
    public void GetOutline_SeacInsideSeacAccent_ThrowsWhereFreeTypeFails()
    {
        // The accent 'B' (66) is itself a seac.
        var font = Craft(false, model =>
        {
            var b = FindGlyphBySid(model, 35);
            model.CharStrings[b] = Charstring.Of(0, 0, 65, 194, "endchar");
            model.CharStrings[Crafted] = Charstring.Of(0, 0, 65, 66, "endchar");
        });

        AssertFailsAsFreeType(font, "a seac inside a seac accent");
    }

    // ==== Crafted charstrings read as FreeType reads them (operators, leniencies, the arithmetic and storage operators).

    private static IEnumerable<TestCaseData> AsFreeTypeCases()
    {
        object[] box = [100, 100, "rmoveto", 300, 0, "rlineto", 0, 300, "rlineto", -300, 0, "rlineto"];
        yield return Case("lines and closing duplicate", [.. box, 0, -300, "rlineto", "endchar"]);
        yield return Case("zero-length lines", [100, 100, "rmoveto", 0, 0, "rlineto", 300, 0, 0, 0, 0, 300, "rlineto", "endchar"]);
        yield return Case("hlineto and vlineto", [100, 100, "rmoveto", 300, 200, -150, -100, "hlineto", 50, 80, -40, "vlineto", "endchar"]);
        yield return Case("rrcurveto, extra operands dropped", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, "rrcurveto", "endchar"]);
        yield return Case("rcurveline and rlinecurve", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, 80, "rcurveline",
            10, 0, 0, 10, 10, 20, 30, 40, 50, 60, "rlinecurve", "endchar"]);
        yield return Case("vvcurveto, 4n + 1", [100, 100, "rmoveto", 5, 10, 20, 30, 40, 0, 20, 30, 40, "vvcurveto", "endchar"]);
        yield return Case("vvcurveto, 4n + 2 skips two", [100, 100, "rmoveto", 5, 6, 10, 20, 30, 40, "vvcurveto", "endchar"]);
        yield return Case("hhcurveto, 4n + 1 and 4n + 3", [100, 100, "rmoveto", 5, 10, 20, 30, 40, "hhcurveto",
            7, 8, 9, 10, 20, 30, 40, "hhcurveto", "endchar"]);
        yield return Case("hvcurveto and vhcurveto", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, 80, 90, "hvcurveto",
            10, 20, 30, 40, 50, "vhcurveto", 10, 20, 30, 40, 50, 60, "vhcurveto", "endchar"]);
        yield return Case("flex", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 50, "flex", "endchar"]);
        yield return Case("hflex", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, "hflex", 0, 100, "rlineto", "endchar"]);
        yield return Case("hflex1", [100, 100, "rmoveto", 10, 20, 30, 40, 50, 60, 70, 80, 90, "hflex1", 0, 100, "rlineto", "endchar"]);
        yield return Case("flex1, last dx", [100, 100, "rmoveto", 10, 2, 30, 4, 50, 6, 70, 8, 90, 10, 110, "flex1", "endchar"]);
        yield return Case("flex1, last dy", [100, 100, "rmoveto", 2, 10, 4, 30, 6, 50, 8, 70, 10, 90, 110, "flex1", "endchar"]);
        yield return Case("width before the first moveto", [500, 100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("hmoveto and vmoveto", [400, 100, "hmoveto", 300, 300, "rlineto", 100, "vmoveto", 300, 0, "rlineto", "endchar"]);
        yield return Case("two moves, empty path", [100, 100, "rmoveto", 200, 200, "rmoveto", 300, 0, "rlineto", "endchar"]);
        yield return Case("line before any moveto", [300, 300, "rlineto", 0, 200, "rlineto", "endchar"]);
        yield return Case("no endchar", [100, 100, "rmoveto", 300, 300, "rlineto", 0, 200, "rlineto"]);
        yield return Case("stems and hintmask", [10, 20, 30, 40, "hstemhm", 50, 60, "vstemhm", "hintmask", new byte[] { 0xE0 },
            100, 100, "rmoveto", 300, 0, "rlineto", "hintmask", new byte[] { 0x40 }, 0, 300, "rlineto", "endchar"]);
        yield return Case("implied vstem in hintmask", [10, 20, "hstemhm", 30, 40, 50, 60, "hintmask", new byte[] { 0xC0 },
            100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("hintmask with no stems reads no byte", ["hintmask", 100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("cntrmask", [10, 20, 30, 40, "hstemhm", "cntrmask", new byte[] { 0xC0 }, 100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("hstem after the mask is ignored", [10, 20, "hstem", 100, 100, "rmoveto", 77, 88, "hstem", 300, 300, "rlineto",
            "hintmask", new byte[] { 0x80 }, 0, 100, "rlineto", "endchar"]);
        yield return Case("hintmask with operands after the mask reads its bytes as charstring",
            [10, 20, "hstemhm", "hintmask", new byte[] { 0x80 }, 100, 100, "rmoveto", 1, 2, "hintmask", new byte[] { 0x8B, 0x8B },
                "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("zero-length line after a new mask", [10, 20, "hstemhm", 100, 100, "rmoveto", "hintmask", new byte[] { 0x80 },
            0, 0, "rlineto", 300, 0, "rlineto", 0, 300, "rlineto", "endchar"]);
        yield return Case("reserved operators clear the stack", [5, "op:0", 6, "op:2", 7, "op:9", 8, "op:13", 9, "op:17", 4, "vsindex", 3,
            "blend", 100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("reserved escapes clear the stack", [5, new byte[] { 12, 8 }, 6, new byte[] { 12, 38 }, 7, "dotsection",
            100, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("callothersubr and pop keep the stack", [100, 100, new byte[] { 12, 16 }, new byte[] { 12, 17 }, "rmoveto",
            300, 300, "rlineto", "endchar"]);
        yield return Case("add, sub, mul, div, neg, abs", [100, 7, 3, "div", "add", 10, 4, "mul", 25, "sub", "neg", "abs", "rmoveto",
            300, 333, 7, "div", "rlineto", "endchar"]);
        yield return Case("sqrt", [10000, "sqrt", 2, "sqrt", "add", 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("and, or, not, eq, ifelse", [1, 0, "and", 1, 0, "or", "add", 0, "not", "add", 5, 5, "eq", "add", 100, "mul",
            100, 200, 1, 2, "ifelse", "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("dup, exch, drop", [150, "dup", 9, "drop", 100, "exch", "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("put and get, out of range ignored", [250, 3, "put", 99, 32, "put", 33, "get", 3, "get", 120, "rmoveto",
            300, 300, "rlineto", "endchar"]);
        yield return Case("index, out of range", [100, 200, 0, "index", "drop", 9, "index", "drop", -1, "index", "drop", "rmoveto",
            300, 300, "rlineto", "endchar"]);
        yield return Case("roll rolls the bottom of the stack", [100, 100, "rmoveto", 10, 20, 30, 40, 2, 1, "roll", "rlineto",
            100, 50, 25, 10, 3, -1, "roll", "rlineto", "endchar"]);
        yield return Case("16.16 operands", [100.5, 100.25, "rmoveto", 300.75, 0.0078125, "rlineto", -0.5, 300.125, "rlineto", "endchar"]);
        yield return Case("hintmask cut by the end reads zeros", [10, 20, "hstemhm", 100, 100, "rmoveto", 300, 300, "rlineto", "hintmask"]);
        yield return Case("number cut by the end reads zeros", [100, 100, "rmoveto", 300, 300, "rlineto", new byte[] { 28, 1 }]);
        yield return Case("short integers", [new byte[] { 28, 0x01, 0x2C }, 100, "rmoveto", 300, 300, "rlineto", "endchar"]);
        yield return Case("a degenerate curve", [100, 100, "rmoveto", 0, 0, 0, 0, 0, 0, "rrcurveto", 300, 0, "rlineto", "endchar"]);
        yield return Case("a curve back to the start", [100, 100, "rmoveto", 300, 0, "rlineto", 0, 100, -100, 100, -200, -200, "rrcurveto",
            "endchar"]);
        yield return Case("subroutines", [100, 100, "rmoveto", 1 - Bias, "callsubr", 0 - Bias, "callgsubr", "endchar"],
            [Charstring.Of(300, 0, "rlineto", "return"), Charstring.Of(0, 300, "rlineto")], [Charstring.Of(-300, 0, "rlineto", "return")]);
        yield return Case("endchar inside a subroutine", [100, 100, "rmoveto", 300, 300, "rlineto", 0 - Bias, "callsubr", 0, 300, "rlineto"],
            [Charstring.Of("endchar")]);
        yield return Case("subroutines nested 16 deep", [100, 100, "rmoveto", 0 - Bias, "callsubr", "endchar"], Chain(16));
    }

    private static IEnumerable<TestCaseData> FailsAsFreeTypeCases()
    {
        yield return Fails("operand read past the stack", [100, 100, "rmoveto", 300, "rlineto", "endchar"], "reading past the operands");
        yield return Fails("stack underflow", ["rmoveto", "endchar"], "underflow");
        yield return Fails("stack overflow at 49 operands", [.. Enumerable.Repeat<object>(1, 49), "hlineto", "endchar"], "overflow");
        yield return Fails("16.16 subroutine number", [100.5, "callsubr", "endchar"], "16.16 operand where an integer");
        yield return Fails("subroutine out of range", [100, 100, "rmoveto", 5, "callsubr", "endchar"], "local subroutine 5");
        yield return Fails("global subroutine out of range", [-200, "callgsubr", "endchar"], "global subroutine -200");
        yield return Fails("return from the charstring", [100, 100, "rmoveto", "return", "endchar"], "a return from the charstring");
        yield return Fails("subroutines nested 17 deep", [100, 100, "rmoveto", 0 - Bias, "callsubr", "endchar"], "nested more than 16",
            Chain(17));
        yield return Fails("97 stems and a hintmask", [.. Stems(97), "hintmask", new byte[13], 100, 100, "rmoveto", "endchar"],
            "97 stem hints");
        yield return Fails("97 stems and a moveto", [.. Stems(97), 100, 100, "rmoveto", 300, 300, "rlineto", "endchar"], "97 stem hints");
        yield return Fails("seac with three operands", [65, 194, 10, "endchar"], "underflow");
    }

    [TestCaseSource(nameof(AsFreeTypeCases))]
    public void GetOutline_CraftedCharstring_MatchesFreeType(byte[] charstring, List<byte[]> subrs, List<byte[]> globalSubrs)
    {
        var font = CraftedFont(false, charstring, subrs, globalSubrs);

        AssertGlyphsMatchFreeType(font, [Crafted], s_craftedSizes);
    }

    [TestCaseSource(nameof(FailsAsFreeTypeCases))]
    public void GetOutline_CraftedCharstring_ThrowsWhereFreeTypeFails(byte[] charstring, string message, List<byte[]> subrs)
    {
        AssertFailsAsFreeType(CraftedFont(false, charstring, subrs, []), message);
    }

    [Test]
    public void GetOutline_StackOf48Operands_MatchesFreeType() =>
        AssertGlyphsMatchFreeType(CraftedFont(false, Charstring.Of([100, 100, "rmoveto", .. Alternating(48), "rlineto", "endchar"]), [], []),
            [Crafted], s_craftedSizes);

    [Test]
    public void GetOutline_NinetySixStems_MatchesFreeType() =>
        AssertGlyphsMatchFreeType(CraftedFont(false, Charstring.Of([.. Stems(96), "hintmask", new byte[12], 100, 100, "rmoveto", 300, 300,
            "rlineto", "endchar"]), [], []), [Crafted], s_craftedSizes);

    /// <summary>
    /// The random operator against FreeType with its cff driver's "random-seed" property set to 0, when FreeType seeds from
    /// the Private DICT's initialRandomSeed: present (12345; -77, made positive; a repeated entry, the last read), absent
    /// (987654321, FreeType's replacement for 0), and no Private DICT at all (0: every number is 1/65536). Three loads of the
    /// glyph in turn, each drawing three random numbers, so the state carried from load to load is compared too.
    /// </summary>
    [TestCase("12345")]
    [TestCase("-77")]
    [TestCase("repeated")]
    [TestCase("absent")]
    [TestCase("no Private DICT")]
    public void GetOutline_RandomOperator_MatchesFreeTypeWithRandomSeed0(string seed)
    {
        var charstring = Charstring.Of("random", 1000, "mul", "random", 1000, "mul", "rmoveto", 300, 0, "rlineto", "random", 1000,
            "mul", 300, "rlineto", "endchar");
        var font = CraftedFont(false, charstring, [], [], model =>
        {
            DictOp[] entries = seed switch
            {
                "12345" => [new DictOp(0x113, CffModel.Numbers(12345))],
                "-77" => [new DictOp(0x113, CffModel.Numbers(-77))],
                "repeated" => [new DictOp(0x113, CffModel.Numbers(5)), new DictOp(0x113, CffModel.Numbers(12345))],
                _ => [new DictOp(10, CffModel.Numbers(50))],
            };
            model.Private = seed == "no Private DICT" ? null : new CffPrivate { Dict = [.. entries] };
        });

        using var freeType = new SeededFreeType(randomSeed: 0);
        using var reference = freeType.Open(font);
        reference.SetPixelSize(1000);
        var ours = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(1000) { Hinting = HintingMode.None });
        var differences = new List<string>();
        for (var load = 0; load < 3; load++)
        {
            if (ScaledFontTests.Compare(ours.GetOutline(Crafted), reference.LoadGlyph(Crafted, hinting: false, render: false).Outline) is
                { } difference)
                differences.Add($"load {load}: {difference}");
        }

        Assert.That(differences, Is.Empty);
    }

    // ==== CFF2 charstrings.

    private static IEnumerable<TestCaseData> Cff2AsFreeTypeCases()
    {
        yield return Case("explicit return and endchar are ignored", [100, 100, "rmoveto", 7, "return", 300, 300, "rlineto", 8, "endchar",
            0, 200, "rlineto"]);
        yield return Case("vsindex and blend at the default", [1, "vsindex", 100, 100, 5, 6, 7, 8, 2, "blend", "rmoveto", 300, 0, 7, 9, 1,
            "blend", "rlineto", 0, 300, "rlineto"]);
        yield return Case("513 operands", [.. Enumerable.Repeat<object>(2, 513), "hlineto"]);
        yield return Case("stems take no width", [10, 20, 30, "hstem", "hintmask", new byte[] { 0x80 }, 100, 100, "rmoveto", 300, 300, "rlineto"]);
        yield return Case("arithmetic escapes are unknown", [100, 7, 3, new byte[] { 12, 10 }, 100, 100, "rmoveto", 300, 300, "rlineto"]);
        yield return Case("blend without regions", [2, "vsindex", 100, 100.5, 2, "blend", "rmoveto", 300, 0, 2, "blend", "rlineto",
            0, 300, "rlineto"]);
        yield return Case("blended values are 16.16", [100, 100, "rmoveto", 300, 0, 7, 1, "blend", 0, 300, "rlineto"]);
    }

    [TestCaseSource(nameof(Cff2AsFreeTypeCases))]
    public void GetOutline_Cff2CraftedCharstring_MatchesFreeType(byte[] charstring, List<byte[]> subrs, List<byte[]> globalSubrs)
    {
        AssertGlyphsMatchFreeType(CraftedFont(true, charstring, subrs, globalSubrs), [Crafted], s_craftedSizes);
    }

    private static IEnumerable<TestCaseData> Cff2FailsCases()
    {
        yield return Fails("514 operands", [.. Enumerable.Repeat<object>(2, 514), "hlineto"], "overflow");
        yield return Fails("vsindex after blend", [100, 1, 1, "blend", 0, "vsindex", 100, "rmoveto"], "vsindex after a blend");
        yield return Fails("vsindex out of range", [5, "vsindex", 100, 1, 1, "blend", 100, "rmoveto"], "vsindex 5, but the variation store has 3");
        yield return Fails("blend count past the stack", [100, 600, "blend", 100, "rmoveto"], "more than the 513");
        yield return Fails("blend of too few operands", [100, 3, "blend", 100, "rmoveto"], "stack holds 1");
    }

    [TestCaseSource(nameof(Cff2FailsCases))]
    public void GetOutline_Cff2CraftedCharstring_ThrowsWhereFreeTypeFails(byte[] charstring, string message, List<byte[]> subrs)
    {
        AssertFailsAsFreeType(CraftedFont(true, charstring, subrs, []), message);
    }

    // ==== Ours works where FreeType fails.

    [Test]
    public void Render_GlyphOver32767Points_RendersWhereFreeTypeFails()
    {
        // 36,000 points: FreeType's outlines hold 32,767 (FT_OUTLINE_POINTS_MAX); ours up to 65,535.
        var font = CraftedFont(false, Charstring.Of([100, 100, "rmoveto", .. Enumerable.Repeat<object[]>([2 - Bias, "callsubr"], 10)
            .SelectMany(x => x), "endchar"]), Staircase(), []);
        var face = FontLoader.Load(font).Faces[0];
        var scaled = face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(font, 0);
        reference.SetPixelSize(16);

        Assert.Multiple(() =>
        {
            Assert.That(reference.TryLoadAndRender(Crafted).Load, Is.Not.EqualTo(FT_Error.FT_Err_Ok), "FreeType fails");
            Assert.That(scaled.GetOutline(Crafted).Points.Length, Is.EqualTo(36001));
            Assert.That(scaled.Render(Crafted).Pixels, Has.Some.Not.Zero);
        });
    }

    [Test]
    public void GetOutline_GlyphOver65535Points_ThrowsNotSupported()
    {
        var font = CraftedFont(false, Charstring.Of([100, 100, "rmoveto", .. Enumerable.Repeat<object[]>([2 - Bias, "callsubr"], 20)
            .SelectMany(x => x), "endchar"]), Staircase(), []);
        var scaled = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });

        Assert.That(() => scaled.GetOutline(Crafted), Throws.TypeOf<NotSupportedException>().With.Message.Contains("65535 points"));
    }

    [Test]
    public void Load_Cff2FdSelectFormat4_LoadsWhereFreeTypeFails()
    {
        // Two identical Font DICTs chosen by a format-4 FDSelect: FreeType 2.13.2 does not read format 4 and fails the face.
        var vf = Source(true);
        var font = Craft(true, model =>
        {
            model.FontDicts!.Add(new CffFontDictModel { Dict = model.FontDicts[0].Dict, Private = model.FontDicts[0].Private!.Clone() });
            var glyphs = model.CharStrings.Count;
            model.FdSelectData = new BigEndianWriter().U8(4).U32(2).U32(0).U16(0).U32((uint)(glyphs / 2)).U16(1).U32((uint)glyphs).ToArray();
        });

        Assert.That(() => m_freeType.OpenFace(font, 0), Throws.InvalidOperationException, "FreeType fails the face");

        // Ours reads it: every glyph as FreeType reads the original font.
        var ours = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(vf, 0);
        reference.SetPixelSize(32);
        Assert.That(CompareOutlines(ours, reference, Enumerable.Range(0, ours.Face.GlyphCount)), Is.Empty);
    }

    // ==== Native hinting (OT-4 WP6): CffOutlineSourceTests.Hinting.cs.

    // ==== Strict rules (CffTable's remarks), one test each.

    [Test]
    public void Load_CffHeaderShort_Throws() => AssertInvalid(WithCff(false, [1, 0, 4]), "shorter than its 4-byte header");

    [Test]
    public void Load_CffMajorVersion_Throws() => AssertInvalid(PatchCff(false, cff => cff[0] = 2), "major version is 2, not 1");

    [Test]
    public void Load_CffHeaderSizeBelow4_Throws() => AssertInvalid(PatchCff(false, cff => cff[2] = 3), "header size is 3, less than 4");

    [Test]
    public void Load_CffHeaderOffsetSizeAbove4_Throws() => AssertInvalid(PatchCff(false, cff => cff[3] = 5), "header offset size is 5");

    [Test]
    public void Load_Cff2HeaderShort_Throws() => AssertInvalid(WithCff(true, [2, 0, 5, 0]), "shorter than its 5-byte header");

    [Test]
    public void Load_Cff2MajorVersion_Throws() => AssertInvalid(PatchCff(true, cff => cff[0] = 1), "major version is 1, not 2");

    [Test]
    public void Load_Cff2HeaderSizeBelow5_Throws() => AssertInvalid(PatchCff(true, cff => cff[2] = 4), "header size is 4, less than 5");

    [Test]
    public void Load_Cff2TopDictPastTable_Throws() => AssertInvalid(WithCff(true, [2, 0, 5, 0, 10]), "Top DICT (10 bytes at 5) runs past");

    [Test]
    public void Load_IndexHeaderPastTable_Throws() => AssertInvalid(WithCff(false, [1, 0, 4, 4, 0]), "Name INDEX at 4 runs past");

    [Test]
    public void Load_IndexOffsetSizeOutOfRange_Throws() =>
        AssertInvalid(PatchCff(false, cff => cff[4 + 2] = 5), "Name INDEX has offset size 5");

    [Test]
    public void Load_IndexOffsetsPastTable_Throws() =>
        AssertInvalid(WithCff(false, [1, 0, 4, 4, 0x10, 0x00, 4, 0, 0, 0, 1]), "Name INDEX of 4096 elements has its offsets past");

    [Test]
    public void Load_IndexFirstOffsetNot1_Throws() =>
        AssertInvalid(PatchCff(false, cff => WriteU32(cff, 4 + 3, 2)), "Name INDEX has first offset 2");

    [Test]
    public void Load_IndexOffsetsDecrease_Throws()
    {
        // The global Subrs INDEX: its second offset made smaller than the first.
        var table = CffModel.FromFont(Source(false), false).Write();
        var at = GlobalSubrsOffset(table);
        WriteU32(table, at + 3 + 4, 0);
        AssertInvalid(WithTable(Source(false), "CFF ", table), "global Subrs INDEX has offset 0 for element 1, smaller");
    }

    [Test]
    public void Load_IndexElementPastTable_Throws() =>
        AssertInvalid(PatchCff(false, cff => WriteU32(cff, 4 + 3 + 4, 0x00FFFFFF)), "Name INDEX has element 0 ending at");

    [Test]
    public void Load_NameIndexOfTwoFonts_Throws() =>
        AssertInvalid(Craft(false, model => model.NameList.Add("Second"u8.ToArray())), "Name INDEX holds 2 fonts");

    [Test]
    public void Load_TopDictIndexEmpty_Throws()
    {
        var w = new BigEndianWriter().U8(1).U8(0).U8(4).U8(4);
        CffModel.WriteIndex(w, ["A"u8.ToArray()], false);
        CffModel.WriteIndex(w, [], false);
        AssertInvalid(WithCff(false, [.. w.ToArray(), 0, 0, 0, 0]), "Top DICT INDEX is empty");
    }

    [Test]
    public void Load_DictReservedOperandByte27_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(13, [27]))), "reserved operand byte 27");

    [Test]
    public void Load_DictOperandsPastFreeTypeStack_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x10E, CffModel.Numbers([.. Enumerable.Repeat(1, 96)])))),
            "more than 95 operands before an operator");

    [Test]
    public void Load_Cff2DictOperandsPastFreeTypeStack_Throws() =>
        AssertInvalid(Craft(true, model => model.Top.Add(new DictOp(0x10E, CffModel.Numbers([.. Enumerable.Repeat(1, 513)])))),
            "more than 512 operands before an operator");

    [Test]
    public void Load_DictEndsInsideTwoByteOperator_Throws() =>
        AssertInvalid(PatchTopDictTail(false, [12]), "ends inside the two-byte operator");

    // ---- Repeated DICT entries: refused where OTS refuses them, else the last read, as FreeType reads it; every occurrence's
    // operands are checked, as FreeType fails the face on a malformed one.

    [Test]
    public void Load_PrivateOfOneOperandBeforeValidOne_ThrowsWhereFreeTypeFails() =>
        AssertFailsAtLoadAsFreeType(Craft(false, model => model.Top.Insert(0, new DictOp(18, CffModel.Numbers(5)))),
            "gives Private 1 operands");

    [Test]
    public void Load_PrivateOfNegativeSizeBeforeValidOne_ThrowsWhereFreeTypeFails() =>
        AssertFailsAtLoadAsFreeType(Craft(false, model => model.Top.Insert(0, new DictOp(18, CffModel.Numbers(-5, 100)))),
            "neither may be negative");

    [Test]
    public void Load_FontMatrixOfFiveOperandsBeforeValidOne_ThrowsWhereFreeTypeFails() =>
        AssertFailsAtLoadAsFreeType(Craft(false, model =>
        {
            model.Top.Add(new DictOp(0x107, CffModel.Numbers(1, 0, 0, 1, 0)));
            model.Top.Add(Matrix("0.001", "0", "0", "0.001", "0", "0"));
        }), "gives FontMatrix 5 operands");

    [Test]
    public void Load_CharStringsTwice_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Insert(0, new DictOp(17, CffModel.Numbers(5)))),
            "Top DICT has the entry CharStrings twice, which OTS refuses");

    [Test]
    public void Load_CustomCharsetTwice_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Insert(0, new DictOp(15, CffModel.Numbers(100)))),
            "has two charset entries of its own, which OTS refuses");

    [Test]
    public void Load_RosTwice_Throws() =>
        AssertInvalid(CidKeyed(model => model.Top.Insert(0, Ros(model))), "Top DICT has the entry ROS twice");

    [Test]
    public void Load_FdArrayTwice_Throws() =>
        AssertInvalid(CidKeyed(model => model.Top.Insert(0, new DictOp(0x124, CffModel.Numbers(5)))), "Top DICT has the entry FDArray twice");

    [Test]
    public void Load_FdSelectTwice_Throws() =>
        AssertInvalid(CidKeyed(model => model.Top.Insert(0, new DictOp(0x125, CffModel.Numbers(5)))), "Top DICT has the entry FDSelect twice");

    [Test]
    public void Load_VstoreTwice_Throws() =>
        AssertInvalid(Craft(true, model => model.Top.Insert(0, new DictOp(24, CffModel.Numbers(5)))), "Top DICT has the entry vstore twice");

    [Test]
    public void Load_TopLevelPrivateSubrsTwice_Throws() =>
        AssertInvalid(Craft(false, model => model.Private!.Dict.Add(new DictOp(19, CffModel.Numbers(5)))), "has the entry Subrs twice");

    [Test]
    public void Load_PredefinedThenCustomCharset_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model => model.Top.Insert(0, new DictOp(15, CffModel.Numbers(0)))));

    [Test]
    public void Load_EncodingTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model =>
        {
            model.Top.Insert(0, new DictOp(16, CffModel.Numbers(70000)));
            model.EncodingOffset = 1;
        }));

    [Test]
    public void Load_PrivateTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model => model.Top.Insert(0, new DictOp(18, CffModel.Numbers(10, 70000)))));

    [Test]
    public void Load_CharstringTypeTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model =>
        {
            model.Top.Add(new DictOp(0x106, CffModel.Numbers(1)));
            model.Top.Add(new DictOp(0x106, CffModel.Numbers(2)));
        }));

    [Test]
    public void Load_FontMatrixTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model =>
        {
            model.Top.Add(Matrix("0.002", "0", "0", "0.002", "0", "0"));
            model.Top.Add(Matrix("0.001", "0", "0.0003", "0.001", "0", "0"));
        }));

    [Test]
    public void Load_FontDictPrivateTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(CidKeyed(model => model.FontDicts![1].Dict.Add(new DictOp(18, CffModel.Numbers(3, 5)))), 2000, 2400);

    [Test]
    public void Load_FontDictPrivateSubrsTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(CidKeyed(model => model.FontDicts![1].Private!.Dict.Add(new DictOp(19, CffModel.Numbers(-9)))), 2000, 2400);

    [Test]
    public void Load_Cff2VsindexTwice_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(true, model =>
        {
            model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(22, CffModel.Numbers(1)));
            model.FontDicts[0].Private!.Dict.Insert(1, new DictOp(22, CffModel.Numbers(0)));
        }));

    [Test]
    public void Load_DictEntryOperandCount_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x106, CffModel.Numbers(2, 2)))), "gives CharstringType 2 operands");

    [Test]
    public void Load_DictOffsetRealOperand_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x106, CffModel.Real("2.0")))), "gives CharstringType a real operand");

    [Test]
    public void Load_PrivateOperandCount_Throws() =>
        AssertInvalid(Craft(false, model => { model.Private = null; model.Top.Add(new DictOp(18, CffModel.Numbers(5))); }),
            "gives Private 1 operands; it takes two integers");

    [Test]
    public void Load_PrivateNegative_Throws() =>
        AssertInvalid(Craft(false, model => { model.Private = null; model.Top.Add(new DictOp(18, CffModel.Numbers(-5, 100))); }),
            "neither may be negative");

    [Test]
    public void Load_PrivatePastTable_Throws() =>
        AssertInvalid(Craft(false, model => { model.Private = null; model.Top.Add(new DictOp(18, CffModel.Numbers(100000, 100000))); }),
            "Private DICT of Top DICT (100000 bytes at 100000) runs past");

    [Test]
    public void Load_FontMatrixOperandCount_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x107, CffModel.Numbers(1, 0, 0, 1, 0)))),
            "gives FontMatrix 5 operands; it takes six numbers");

    [Test]
    public void Load_RosOperandCount_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x11E, CffModel.Numbers(391, 392)))),
            "gives ROS 2 operands; it takes three integers");

    [Test]
    public void Load_RosRegistryNotSid_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x11E, CffModel.Numbers(65535, 392, 0)))),
            "ROS registry 65535 and ordering 392");

    [Test]
    public void Load_OffsetOutsideTable_Throws() =>
        AssertInvalid(Craft(false, model => { model.CharsetData = null; model.CharsetOffset = 5_000_000; }),
            "has a charset offset of 5000000, outside the table");

    [Test]
    public void Load_NegativeOffset_Throws() =>
        AssertInvalid(Craft(false, model =>
        {
            model.CharsetData = null;
            model.Top.Add(new DictOp(15, CffModel.Numbers(-7)));
        }), "has a charset offset of -7, outside the table");

    /// <summary>A 5-byte integer (29) is read unsigned, as FreeType's 64-bit cff_parse_integer reads it: -7 is 4,294,967,289.</summary>
    [Test]
    public void Load_FiveByteOffsetWithTopBitSet_Throws() =>
        AssertInvalid(Craft(false, model => { model.CharsetData = null; model.CharsetOffset = -7; }),
            "has a charset offset of 4294967289, outside the table");

    [Test]
    public void Load_CharstringTypeNot2_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Add(new DictOp(0x106, CffModel.Numbers(1)))), "CharstringType is 1");

    [Test]
    public void Load_NoCharStrings_Throws()
    {
        var model = CffModel.FromFont(Source(false), false);
        var table = model.Write();
        PatchTopDictOperator(table, 17, 21); // CharStrings becomes nominalWidthX, which a Top DICT does not have
        AssertInvalid(WithTable(Source(false), "CFF ", table), "Top DICT has no CharStrings entry");
    }

    [Test]
    public void Load_CharStringsOffset0_Throws()
    {
        var model = CffModel.FromFont(Source(false), false);
        var table = model.Write();
        PatchTopDictOffset(table, 17, 0);
        AssertInvalid(WithTable(Source(false), "CFF ", table), "CharStrings offset of 0");
    }

    [Test]
    public void Load_CharStringsCountNotMaxp_Throws() =>
        AssertInvalid(Craft(false, model => model.CharStrings.RemoveAt(model.CharStrings.Count - 1)),
            "CharStrings INDEX holds 2477 glyphs, but 'maxp' counts 2478");

    [Test]
    public void Load_CharsetFormat_Throws() => AssertInvalid(Craft(false, model => model.CharsetData![0] = 3), "charset has format 3");

    [Test]
    public void Load_CharsetPastTable_Throws() => AssertInvalid(AtTableEnd(false, null, 15, [0, 0, 1]), "format-0 charset at");

    [Test]
    public void Load_CharsetRangePastSid65535_Throws() =>
        AssertInvalid(Craft(false, model => model.CharsetData = new BigEndianWriter().U8(2).U16(0xFFF0).U16(5000).ToArray()),
            "past SID 65,535");

    [Test]
    public void Load_PredefinedCharsetTooSmall_Throws() =>
        AssertInvalid(Craft(false, model => { model.CharsetData = null; model.CharsetOffset = 0; }),
            "predefined ISOAdobe charset has 229 glyphs, fewer than the font's 2478");

    [Test]
    public void Load_EncodingFormat_Throws() => AssertInvalid(Craft(false, model => model.EncodingData = [2, 0]), "encoding has format 2");

    [Test]
    public void Load_EncodingPastTable_Throws() =>
        AssertInvalid(AtTableEnd(false, model => model.EncodingData = [0, 0], 16, [0, 200, 1, 2]), "encoding at");

    [Test]
    public void Load_EncodingSupplementsPastTable_Throws() =>
        AssertInvalid(AtTableEnd(false, model => model.EncodingData = [0, 0], 16, [0x80, 1, 65, 200]), "supplements past the table");

    [Test]
    public void Load_CidWithoutFdArray_Throws() =>
        AssertInvalid(Craft(false, model => model.Top.Insert(0, Ros(model))), "CID-keyed (it has an ROS entry) but has no FDArray");

    [Test]
    public void Load_Cff2WithoutFdArray_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts = null), "CFF2 table's Top DICT has no FDArray entry");

    [Test]
    public void Load_FdArrayEmpty_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts!.Clear()), "FDArray is empty");

    [Test]
    public void Load_FontDictWithRos_Throws() =>
        AssertInvalid(CidKeyed(model => model.FontDicts![1].Dict.Add(Ros(model))), "Font DICT 1 has an ROS entry");

    [Test]
    public void Load_CidWithoutFdSelect_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = null), "CID-keyed but has no FDSelect");

    [Test]
    public void Load_Cff2SeveralFontDictsWithoutFdSelect_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts!.Add(new CffFontDictModel { Private = model.FontDicts[0].Private })),
            "has 2 Font DICTs but no FDSelect");

    [Test]
    public void Load_FdSelectFormat_Throws() => AssertInvalid(CidKeyed(model => model.FdSelectData![0] = 2), "FDSelect has format 2");

    [Test]
    public void Load_FdSelectFormat4InCff_Throws() => AssertInvalid(CidKeyed(model => model.FdSelectData![0] = 4), "FDSelect has format 4");

    [Test]
    public void Load_FdSelectPastTable_Throws() =>
        AssertInvalid(AtTableEnd(false, model => MakeCidKeyed(model, 0), 0x125, [0, 0, 1]), "format-0 FDSelect at");

    [Test]
    public void Load_FdSelectNoRanges_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = new BigEndianWriter().U8(3).U16(0).U16(2478).ToArray()), "has no ranges");

    [Test]
    public void Load_FdSelectFirstRangeNot0_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = FdSelect3((1, 0), (2478, -1))), "first range at glyph 1, not 0");

    [Test]
    public void Load_FdSelectRangesNotAscending_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = FdSelect3((0, 0), (500, 1), (500, 0), (2478, -1))),
            "range 2 starts at glyph 500, not after");

    [Test]
    public void Load_FdSelectSentinelBelowGlyphCount_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = FdSelect3((0, 0), (2000, -1))), "sentinel is 2000, below the glyph count 2478");

    [Test]
    public void Load_FdSelectSentinelNotAfterLastRange_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = FdSelect3((0, 0), (3000, 1), (2500, -1))),
            "sentinel 2500 is not after its last range");

    [Test]
    public void Load_FdSelectFontDictOutOfRange_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = FdSelect3((0, 0), (10, 2), (2478, -1))), "names Font DICT 2, but the FDArray has 2");

    [Test]
    public void Load_FdSelectFormat0FontDictOutOfRange_Throws() =>
        AssertInvalid(CidKeyed(model => model.FdSelectData = [0, .. Enumerable.Repeat((byte)5, 2478)]),
            "gives glyph 0 Font DICT 5, but the FDArray has 2");

    [Test]
    public void Load_VariationStoreFormat_Throws() =>
        AssertInvalid(Craft(true, model => WriteU16(model.VariationStore!, 2, 2)), "only format 1 exists");

    [Test]
    public void Load_VariationStoreHeaderPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore = [0, 4, 0, 1, 0, 0]), "variation store's header at");

    [Test]
    public void Load_VstoreLeavingNoRoomForLength_Throws() =>
        AssertInvalid(AtTableEnd(true, null, 24, [0]), "vstore entry");

    [Test]
    public void Load_VariationStoreDataOffsetsPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore = Raw(new BigEndianWriter().U16(1).U32(8).U16(100))),
            "100 data offsets run past");

    [Test]
    public void Load_VariationStoreRegionListPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore = Raw(new BigEndianWriter().U16(1).U32(1000).U16(0))),
            "region list at");

    [Test]
    public void Load_VariationStoreRegionsPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore = Raw(new BigEndianWriter().U16(1).U32(8).U16(0).U16(1).U16(50))),
            "50 regions of 1 axes run past");

    [Test]
    public void Load_VariationStoreDataPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore =
            Raw(new BigEndianWriter().U16(1).U32(12).U16(1).U32(1000).U16(1).U16(0))), "data 0 at");

    [Test]
    public void Load_VariationStoreRegionIndicesPastTable_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore =
            Raw(new BigEndianWriter().U16(1).U32(12).U16(1).U32(16).U16(1).U16(0).U16(0).U16(0).U16(10))),
            "data 0 has 10 region indices past");

    [Test]
    public void Load_VariationStoreRegionIndexOutOfRange_Throws() =>
        AssertInvalid(Craft(true, model => model.VariationStore = Vstore(regionCount: 1, regionIndices: [1])), "names region 1, but the store has 1");

    [Test]
    public void Load_Cff2PrivateBlendVsindexOutOfRange_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(22, CffModel.Numbers(9)))),
            "blends with vsindex 9");

    [Test]
    public void Load_Cff2PrivateBlendCountNotInteger_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(23, [.. CffModel.Numbers(1, 2, 3), .. CffModel.Real("1.0")]))),
            "blend whose count is a real operand");

    [Test]
    public void Load_Cff2PrivateBlendCountPastStack_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(23, CffModel.Numbers(1, 600)))),
            "blends 600 values");

    [Test]
    public void Load_Cff2PrivateBlendTooFewOperands_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(23, CffModel.Numbers(1, 2, 3)))),
            "has only 2 operands");

    [Test]
    public void Load_Cff2PrivateVsindexAfterBlend_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Add(new DictOp(22, CffModel.Numbers(0)))),
            "sets vsindex after a blend");

    [Test]
    public void Load_SubrsOffsetOutsideTable_Throws() =>
        AssertInvalid(Craft(false, model => { model.Private!.Subrs = null; model.Private.Dict.Add(new DictOp(19, CffModel.Numbers(-3))); }),
            "Subrs offset of -3, outside the table");

    // ---- Not supported.

    [Test]
    public void Load_MultipleMaster_ThrowsNotSupported() =>
        Assert.That(() => FontLoader.Load(Craft(false, model => model.Top.Add(new DictOp(0x118, CffModel.Numbers(2, 1))))),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Multiple Master"));

    [Test]
    public void Load_FdArrayOver256_ThrowsNotSupported() =>
        Assert.That(() => FontLoader.Load(Craft(true, model =>
            {
                var fd = model.FontDicts![0];
                model.FontDicts = Enumerable.Repeat(fd, 257).ToList();
                model.FdSelectData = new BigEndianWriter().U8(3).U16(1).U16(0).U8(0).U16(2478).ToArray();
            })),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("257 Font DICTs, more than the 256"));

    // ---- Accepted, read as FreeType reads them.

    [Test]
    public void Load_DictTrailingOperands_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(PatchTopDictTail(false, [139, 140, 29, 0, 0]));

    [Test]
    public void Load_DictTrailingRealCutByEnd_LoadsAsFreeType() => AssertLoadsAsFreeType(PatchTopDictTail(false, [30, 0x12]));

    [Test]
    public void Load_DictUnknownOperators_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model =>
        {
            foreach (var op in new[] { 22, 23, 24, 25, 26, 31, 255, 0x1FF, 0x127 })
                model.Top.Add(new DictOp(op, CffModel.Numbers(7, 8)));
        }));

    [Test]
    public void Load_PrivateOfSize0_LoadsAsFreeType()
    {
        // A minimal font (no subroutines) whose Top DICT has a Private entry of size 0: no Private DICT.
        var font = CraftedFont(false, Charstring.Of(100, 100, "rmoveto", 300, 300, "rlineto", "endchar"), [], [], model =>
        {
            model.Private = null;
            model.Top.Add(new DictOp(18, CffModel.Numbers(0, 100)));
        });

        AssertGlyphsMatchFreeType(font, [0, Crafted, 2], 32);
    }

    [Test]
    public void Load_CharsetCoveringMoreGlyphs_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(false, model => model.CharsetData = new BigEndianWriter().U8(2).U16(1).U16(5000).ToArray()));

    [Test]
    public void Load_FdSelectSentinelAboveGlyphCount_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(CidKeyed(model => model.FdSelectData = FdSelect3((0, 0), (1000, 1), (9000, -1))), 0, 999, 1000, 2477);

    [Test]
    public void Load_Cff2SingleFontDictFdSelect_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Craft(true, model => model.FdSelectData = [9, 9, 9]), 0, 100);

    // ==== Load structure: the same-inputs rule and the budgets.

    [Test]
    public void Load_CollectionFacesSharingCffTable_ParsesItOnce()
    {
        var font = Source(false);
        var collection = SharedCollection(font, 2, ownCopies: false);

        Assert.Multiple(() =>
        {
            Assert.That(SfntReader.CountParses(collection), Is.EqualTo(SfntReader.CountParses(font)));
            Assert.That(FontLoader.Load(collection).Faces[1].OutlineFormat, Is.EqualTo(OutlineFormat.Cff));
        });
    }

    [Test]
    public void Load_CollectionFacesSharingCffTableWithOtherGlyphCounts_Throws() =>
        Assert.That(() => FontLoader.Load(SharedCollection(Source(false), 2, ownCopies: true)),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("share the 'CFF ' table"));

    [Test]
    public void Load_SourceSansOtfs_StayWithinBudgets()
    {
        // The CFF parse charges its walks and reserves its arrays: both stay within the per-file budgets' measured range.
        foreach (var face in TestFonts.CffFaces())
        {
            var data = FontCache.Bytes(face.Path);
            Assert.Multiple(() =>
            {
                Assert.That(SfntReader.MeasureWork(data) / (double)data.Length, Is.LessThan(2.0), face.Name);
                Assert.That(SfntReader.MeasureParsedBytes(data) / (double)data.Length, Is.LessThan(1.0), face.Name);
            });
        }
    }

    [Test]
    public void Load_IndexClaimingHugeCount_ThrowsBeforeAllocating() =>
        AssertInvalid(PatchCff(true, cff =>
        {
            var at = 5 + U16(cff, 3);
            WriteU32(cff, at, 0x7FFFFFFF);
            cff[at + 4] = 4;
        }), "global Subrs INDEX of 2147483647 elements has its offsets past");

    // ==== Fix round 1.

    /// <summary>
    /// The reviewer's matrices: Top FontMatrix 0.001 0.001 0 0.001 0 0 and Font DICT FontMatrix 32767 0 -1 -32767 0 0, whose
    /// concatenation has yy = -2^31 (the int.MinValue on which Math.Abs threw). In the reference's 64-bit FT_Fixed its
    /// absolute value is +2^31, so the normalisation keeps the glyphs' signs: they read as FreeType reads them.
    /// </summary>
    [Test]
    public void GetOutline_ConcatenatedMatrixWrapping_MatchesFreeType()
    {
        var font = CidWithMatrices(Matrix("0.001", "0.001", "0", "0.001", "0", "0"),
            new DictOp(0x107, CffModel.Numbers(32767, 0, -1, -32767, 0, 0)));

        AssertOutlinesAndMetricsMatchFreeType(font, [0, 1, 36, 100, 500], 16);
    }

    /// <summary>
    /// FontMatrix -32768 0 0 -32768 0 0: its elements are -2^31, whose 64-bit absolute value passes 0x7FFFFFFF, so the
    /// reference's FT_Matrix_Check finds the matrix unlikely (identity, 1 unit per em), as ours does; with a Font DICT matrix
    /// of 0.001 (1,000 units per em) the glyphs are then scaled by 1/1000, as FreeType scales them.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void GetOutline_MatrixCheckOfIntMinElements_MatchesFreeType(bool cid)
    {
        var top = new DictOp(0x107, CffModel.Numbers(-32768, 0, 0, -32768, 0, 0));
        var font = cid
            ? CidWithMatrices(top, Matrix("0.001", "0", "0", "0.001", "0", "0"))
            : Craft(false, model => model.Top.Add(top));

        AssertOutlinesAndMetricsMatchFreeType(font, [0, 1, 36, 100, 500], 16);
    }

    /// <summary>flex1 whose dx sum is exactly -32768.0: cf2_fixedAbs wraps, so the last operand goes to y, as in FreeType.</summary>
    [Test]
    public void GetOutline_Flex1DxSumOfIntMin_MatchesFreeType() =>
        AssertGlyphsMatchFreeType(CraftedFont(false, Charstring.Of(-10000, 5, -10000, 5, -10000, 5, -2000, 5, -768, 5, 40, "flex1",
            "endchar"), [], []), [Crafted], 9, 32);

    /// <summary>A 2 KB CID-keyed font of 10 glyphs and 3 Font DICTs (each with Source Sans 3's Private DICT): within the file's
    /// parsed-memory budget thanks to its fixed allowance; read as FreeType reads it.</summary>
    [Test]
    public void Load_SmallCidFontWithThreeFontDicts_LoadsAsFreeType()
    {
        var font = SmallCidFont(glyphs: 10, fontDicts: 3);

        Assert.That(font.Length, Is.LessThan(2500));
        AssertGlyphsMatchFreeType(font, [.. Enumerable.Range(0, 10)], 16, 72);
    }

    [Test]
    public void Load_IndexWithoutOffsetSize_Throws() => AssertInvalid(WithCff(false, [1, 0, 4, 4, 0, 1]), "has no offset size byte");

    [Test]
    public void Load_Format1CharsetPastTable_Throws() => AssertInvalid(AtTableEnd(false, null, 15, [1, 0, 1]), "format-1 charset at");

    [Test]
    public void Load_Format2CharsetPastTable_Throws() => AssertInvalid(AtTableEnd(false, null, 15, [2, 0, 1, 0]), "format-2 charset at");

    [Test]
    public void Load_FdSelectRangeCountPastTable_Throws() =>
        AssertInvalid(AtTableEnd(false, model => MakeCidKeyed(model, 0), 0x125, [3, 0]), "format-3 FDSelect at");

    [Test]
    public void Load_FdSelectRangesPastTable_Throws() =>
        AssertInvalid(AtTableEnd(false, model => MakeCidKeyed(model, 0), 0x125, [3, 0, 5, 0, 0]), "FDSelect of 5 ranges runs past");

    [Test]
    public void Load_Cff2FdSelectFormat4RangesPastTable_Throws() =>
        AssertInvalid(Craft(true, model =>
        {
            model.FontDicts!.Add(new CffFontDictModel { Private = model.FontDicts[0].Private });
            model.FdSelectData = new BigEndianWriter().U8(4).U32(70000).U32(0).U16(0).ToArray();
        }), "CFF2 table's format-4 FDSelect of 70000 ranges runs past");

    [Test]
    public void Load_Cff2PrivateBlendWithoutOperands_Throws() =>
        AssertInvalid(Craft(true, model => model.FontDicts![0].Private!.Dict.Insert(0, new DictOp(23, []))), "has a blend without operands");

    /// <summary>
    /// The budgets bite on a CFF table: 256 Font DICTs whose Private DICTs overlap one 30 KB run (each a different size, so each
    /// a different DICT, parsed in turn) pass the file's parsed-memory budget: <see cref="NotSupportedException"/>, at a CFF DICT.
    /// </summary>
    [Test]
    public void Load_FontDictsOverOverlappingPrivateDicts_ThrowsNotSupported()
    {
        var font = OverlappingPrivates(fontDicts: 256, privateSize: 30_000);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>()
            .With.Message.Contains("bytes per byte of the file plus").And.Message.Contains("a CFF DICT of"));
    }

    /// <summary>A CFF DICT's bytes are charged as work and its entries reserved as memory: 1,000 more Private DICT bytes add
    /// 1,000 units of work and 36,000 accounted bytes; with the work limit just below the load's, the CFF parse throws.</summary>
    [Test]
    public void Load_CffDictBytes_ChargedAndReserved()
    {
        var smaller = Craft(false, model => model.Private!.Dict.Add(new DictOp(13, CffModel.Numbers([.. Enumerable.Repeat(0, 10)]))));
        var larger = Craft(false, model =>
        {
            model.Private!.Dict.Add(new DictOp(13, CffModel.Numbers([.. Enumerable.Repeat(0, 10)])));
            for (var i = 0; i < 100; i++)
                model.Private.Dict.Add(new DictOp(13, CffModel.Numbers([.. Enumerable.Repeat(0, 9)])));
        });
        var work = SfntReader.MeasureWork(larger);

        Assert.Multiple(() =>
        {
            Assert.That(work - SfntReader.MeasureWork(smaller), Is.EqualTo(1000));
            Assert.That(SfntReader.MeasureParsedBytes(larger) - SfntReader.MeasureParsedBytes(smaller), Is.EqualTo(36 * 1000));
            Assert.That(() => SfntReader.ReadFaces(larger, work - 1), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains("more work than this library allows"));
        });
    }

    // ==== Fix round 2: the glyph work budget (Denys's "Glyph and size budgets", 2026-11-27).

    /// <summary>
    /// The reviewer's fan-out (10 calls per level, 7 levels, more than 20,000,000 operators): FreeType fails it at its
    /// per-run operator limit, and so does our interpreter without the glyph budget (InvalidDataException); with the budget,
    /// the load stops first, at exactly 1,000,001 units (every operator here is charged 1), with NotSupportedException.
    /// </summary>
    [Test]
    public void GetOutline_GlyphOverWorkBudget_ThrowsNotSupported()
    {
        var font = CraftedFont(false, Charstring.Of(0 - Bias, "callsubr", "endchar"), FanOut(), []);
        var face = FontLoader.Load(font).Faces[0];
        var scaled = face.CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });
        var budgeted = new Type2Interpreter(face.Data.Cff!, new CffOutlineBuilder());
        var unlimited = new Type2Interpreter(face.Data.Cff!, new CffOutlineBuilder()) { WorkBudget = long.MaxValue };
        using var reference = m_freeType.OpenFace(font, 0);
        reference.SetPixelSize(32);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Multiple(() =>
        {
            Assert.That(() => scaled.GetOutline(Crafted), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains("more work than this library allows for one glyph (1000000 units)"));
            TestContext.Out.WriteLine($"Refused after {watch.ElapsedMilliseconds} ms.");
            Assert.That(() => budgeted.Run(Crafted), Throws.TypeOf<NotSupportedException>());
            Assert.That(budgeted.Work, Is.EqualTo(Type2Interpreter.GlyphWorkBudget + 1));
            Assert.That(() => unlimited.Run(Crafted), Throws.TypeOf<InvalidDataException>().With.Message.Contains("more than 19999999 operators"));
            Assert.That(reference.TryLoadAndRender(Crafted).Load, Is.Not.EqualTo(FT_Error.FT_Err_Ok), "FreeType fails at its limit");
        });
    }

    /// <summary>
    /// A seac whose two components are each a heavy glyph (a fan-out of over half the budget): the components share the glyph's
    /// budget, so the second one passes it, though each run alone stays within it; FreeType draws the glyph.
    /// </summary>
    [Test]
    public void GetOutline_SeacOfTwoHeavyComponents_ThrowsNotSupported()
    {
        // Subroutines: level 0 calls level 1 eight times, then five levels of ten calls, then a leaf of two numbers and two drops.
        List<byte[]> subrs = [Charstring.Of([.. Enumerable.Repeat<object[]>([1 - Bias, "callsubr"], 8).SelectMany(x => x), "return"])];
        for (var level = 1; level < 5; level++)
            subrs.Add(Charstring.Of([.. Enumerable.Repeat<object[]>([level + 1 - Bias, "callsubr"], 10).SelectMany(x => x), "return"]));

        subrs.Add(Charstring.Of(1, 1, "drop", "drop", "return"));
        var font = CraftedFont(false, Charstring.Of(0, 0, 66, 65, "endchar"), subrs, [], model =>
        {
            // The minimal charset gives glyph g SID g, so codes 65 ('A', SID 34) and 66 ('B', SID 35) name glyphs 34 and 35.
            model.CharStrings[34] = Charstring.Of(0 - Bias, "callsubr", 100, 100, "rmoveto", 300, 300, "rlineto", "endchar");
            model.CharStrings[35] = Charstring.Of(0 - Bias, "callsubr", 100, 100, "rmoveto", 300, 0, "rlineto", "endchar");
        });
        var face = FontLoader.Load(font).Faces[0];
        var single = new Type2Interpreter(face.Data.Cff!, new CffOutlineBuilder());
        single.Run(34);
        var scaled = face.CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(font, 0);
        reference.SetPixelSize(32);

        Assert.Multiple(() =>
        {
            Assert.That(single.Work, Is.InRange(Type2Interpreter.GlyphWorkBudget / 2 + 1, Type2Interpreter.GlyphWorkBudget),
                "one component alone fits, two do not");
            Assert.That(() => scaled.GetOutline(Crafted), Throws.TypeOf<NotSupportedException>().With.Message.Contains("for one glyph"));
            Assert.That(reference.TryLoadAndRender(Crafted).Load, Is.EqualTo(FT_Error.FT_Err_Ok), "FreeType draws it");
        });
    }

    /// <summary>
    /// The measurement behind <see cref="Type2Interpreter.GlyphWorkBudget"/>: every glyph of the CFF corpus charges at most
    /// 1,140 units (SourceSans3VF-Upright.otf glyph 2299), far under the budget of 1,000,000.
    /// </summary>
    [Test]
    public void GetOutline_CorpusHeaviestGlyph_StaysFarUnderWorkBudget()
    {
        long heaviest = 0;
        var where = "";
        // The Source Sans 3 files and every CFF face of C:\Windows\Fonts\*.otf (OT-4 WP6: no font is hard-coded from there).
        foreach (var testFace in CffHintingTests.Faces())
        {
            var path = testFace.Path;
            var face = FontCache.Face(testFace);
            var interpreter = new Type2Interpreter(face.Data.Cff!, new CffOutlineBuilder());
            for (var glyph = 0; glyph < face.GlyphCount; glyph++)
            {
                interpreter.Run(glyph);
                if (interpreter.Work <= heaviest)
                    continue;

                heaviest = interpreter.Work;
                where = $"{Path.GetFileName(path)} glyph {glyph}";
            }
        }

        TestContext.Out.WriteLine($"Heaviest glyph: {heaviest} units ({where}).");
        Assert.That(heaviest * 100, Is.LessThanOrEqualTo(Type2Interpreter.GlyphWorkBudget));
    }

    // ==== Helpers.

    /// <summary>A store's bytes with its 2-byte length field first.</summary>
    private static byte[] Raw(BigEndianWriter body)
    {
        var bytes = body.ToArray();
        return new BigEndianWriter().U16(bytes.Length).Bytes(bytes).ToArray();
    }

    /// <summary>Source Sans 3 OTF made CID-keyed with one Font DICT, with these Top DICT and Font DICT matrices (null: none).</summary>
    private static byte[] CidWithMatrices(DictOp? top, DictOp? fd) => Craft(false, model =>
    {
        var glyphs = model.CharStrings.Count;
        var priv = model.Private!;
        model.Private = null;
        model.Top.Insert(0, Ros(model));
        if (top is not null)
            model.Top.Add(top);

        model.CharsetData = new BigEndianWriter().U8(2).U16(1).U16(glyphs - 2).ToArray();
        model.FontDicts = [new CffFontDictModel { Private = priv, Dict = fd is null ? [] : [fd] }];
        model.FdSelectData = FdSelect3((0, 0), (glyphs, -1));
    });

    /// <summary>A small CID-keyed font: <paramref name="glyphs"/> glyphs, <paramref name="fontDicts"/> Font DICTs, each with Source
    /// Sans 3's Private DICT (no subroutines), a format-0 FDSelect.</summary>
    private static byte[] SmallCidFont(int glyphs, int fontDicts)
    {
        var privateDict = CffModel.FromFont(Source(false), false).Private!.Dict;
        var model = CffModel.Minimal(glyphs, isCff2: false);
        model.Top.Insert(0, Ros(model));
        for (var g = 0; g < glyphs; g++)
            model.CharStrings[g] = Charstring.Of(500, 10 * g, 0, "rmoveto", 20 + 5 * g, 0, "rlineto", 0, 30 + 7 * g, "rlineto",
                -10, 20, -30, 40, -50, 60, "rrcurveto", "endchar");

        model.FontDicts = Enumerable.Range(0, fontDicts).Select(_ => new CffFontDictModel { Private = new CffPrivate { Dict = [.. privateDict] } })
            .ToList();
        model.FdSelectData = [0, .. Enumerable.Range(0, glyphs).Select(g => (byte)(g % fontDicts))];
        return SmallOtto(glyphs, model);
    }

    /// <summary>
    /// A small CID-keyed font whose first Font DICT has a Private DICT of <paramref name="privateSize"/> bytes (entries of an
    /// operator unknown to a Private DICT) and whose other Font DICTs point into it, each one byte shorter.
    /// </summary>
    private static byte[] OverlappingPrivates(int fontDicts, int privateSize)
    {
        const int glyphs = 10;
        var model = CffModel.Minimal(glyphs, isCff2: false);
        model.Top.Insert(0, Ros(model));
        var big = new List<DictOp>();
        for (var i = 0; i < privateSize / 2; i++)
            big.Add(new DictOp(13, CffModel.Numbers(0)));

        model.FontDicts = Enumerable.Range(0, fontDicts)
            .Select(i => new CffFontDictModel { Private = new CffPrivate { Dict = i == 0 ? big : [new DictOp(10, CffModel.Numbers(i))] } })
            .ToList();
        model.FdSelectData = [0, .. Enumerable.Range(0, glyphs).Select(_ => (byte)0)];
        var table = model.Write();

        // Each Font DICT is "29 size 29 offset 18"; point Font DICTs 1.. at the first one's Private DICT, shorter by their index.
        var fdArray = CffModel.Integer(TopDictEntries(table).Entries.Single(entry => entry.Op == 0x124), 0);
        var count = U16(table, fdArray);
        var dataBase = fdArray + 2 + 1 + 4 * (count + 1) - 1;
        var firstSize = (int)U32(table, dataBase + (int)U32(table, fdArray + 3) + 1);
        var firstOffset = (int)U32(table, dataBase + (int)U32(table, fdArray + 3) + 6);
        for (var i = 1; i < count; i++)
        {
            var start = dataBase + (int)U32(table, fdArray + 3 + 4 * i);
            WriteU32(table, start + 1, (uint)(firstSize - i));
            WriteU32(table, start + 6, (uint)firstOffset);
        }

        return SmallOtto(glyphs, null, table);
    }

    /// <summary>FreeType fails to open the face, and ours throws <see cref="InvalidDataException"/> with the message at load.</summary>
    private void AssertFailsAtLoadAsFreeType(byte[] data, string message) =>
        Assert.Multiple(() =>
        {
            Assert.That(() => m_freeType.OpenFace(data, 0).Dispose(), Throws.InvalidOperationException, "FreeType fails the face");
            AssertInvalid(data, message);
        });

    /// <summary>Compares outlines and metrics (not bitmaps); a glyph FreeType fails to load must throw here too.</summary>
    private void AssertOutlinesAndMetricsMatchFreeType(byte[] data, int[] glyphs, int size)
    {
        var face = FontLoader.Load(data).Faces[0];
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var differences = new List<string>();
        foreach (var glyph in glyphs)
        {
            if (reference.TryLoadAndRender(glyph).Load != FT_Error.FT_Err_Ok)
            {
                if (!ThrowsInvalidData(() => font.GetOutline(glyph)))
                    differences.Add($"glyph {glyph}: FreeType fails, ours loads");

                continue;
            }

            var expected = reference.LoadGlyph(glyph, hinting: false, render: false);
            if (ScaledFontTests.Compare(font.GetOutline(glyph), expected.Outline) is { } difference)
                differences.Add($"glyph {glyph}: {difference}");
            else if (font.GetMetrics(glyph) != expected.Metrics)
                differences.Add($"glyph {glyph}: metrics {font.GetMetrics(glyph)}, FreeType {expected.Metrics}");
        }

        Assert.That(differences, Is.Empty);
    }

    private static bool ThrowsInvalidData(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static TestCaseData Case(string name, object[] items, List<byte[]>? subrs = null, List<byte[]>? globalSubrs = null) =>
        new TestCaseData(Charstring.Of(items), subrs ?? [], globalSubrs ?? []).SetArgDisplayNames(name);

    private static TestCaseData Fails(string name, object[] items, string message, List<byte[]>? subrs = null) =>
        new TestCaseData(Charstring.Of(items), message, subrs ?? []).SetArgDisplayNames(name);

    /// <summary><paramref name="count"/> stem hints in hstem operators of 24 (the 48-operand stack).</summary>
    private static object[] Stems(int count)
    {
        var items = new List<object>();
        for (var done = 0; done < count;)
        {
            var n = Math.Min(24, count - done);
            for (var i = 0; i < n; i++)
            {
                items.Add(10);
                items.Add(5);
            }

            items.Add("hstemhm");
            done += n;
        }

        return [.. items];
    }

    /// <summary>Operand pairs drawing a staircase: 10 0, 0 10, ...</summary>
    private static object[] Alternating(int count) =>
        Enumerable.Range(0, count).Select(i => (object)(i % 4 is 0 or 3 ? 10 : 0)).ToArray();

    /// <summary>Subroutines 0..n-1 each calling the next; the last draws.</summary>
    private static List<byte[]> Chain(int depth)
    {
        var subrs = new List<byte[]>();
        for (var i = 0; i < depth - 1; i++)
            subrs.Add(Charstring.Of(i + 1 - Bias, "callsubr", "return"));

        subrs.Add(Charstring.Of(300, 0, "rlineto", 0, 300, "rlineto", "return"));
        return subrs;
    }

    /// <summary>Subroutines whose calls fan out 10 ways per level, seven levels: more than 20,000,000 operators.</summary>
    private static List<byte[]> FanOut()
    {
        var subrs = new List<byte[]>();
        for (var level = 0; level < 7; level++)
        {
            var items = new List<object>();
            for (var i = 0; i < 10; i++)
            {
                items.Add(level + 1 - Bias);
                items.Add("callsubr");
            }

            items.Add("return");
            subrs.Add(Charstring.Of([.. items]));
        }

        subrs.Add(Charstring.Of(1, 1, "drop", "drop", "return"));
        return subrs;
    }

    /// <summary>Subroutine 0 draws 24 staircase points, 1 calls 0 fifteen times, 2 calls 1 ten times: 3,600 points.</summary>
    private static List<byte[]> Staircase() =>
    [
        Charstring.Of([.. Alternating(48), "rlineto", "return"]),
        Charstring.Of([.. Enumerable.Repeat<object[]>([0 - Bias, "callsubr"], 15).SelectMany(x => x), "return"]),
        Charstring.Of([.. Enumerable.Repeat<object[]>([1 - Bias, "callsubr"], 10).SelectMany(x => x), "return"]),
    ];

    private static byte[] Source(bool cff2) =>
        FontCache.Bytes(TestFonts.SourceSans(cff2 ? "SourceSans3VF-Upright.otf" : "SourceSans3-Regular.otf"));

    /// <summary>A copy of Source Sans 3 (OTF or the CFF2 VF) with its CFF table changed through the model.</summary>
    private static byte[] Craft(bool cff2, Action<CffModel> change)
    {
        var font = Source(cff2);
        var model = CffModel.FromFont(font, cff2);
        change(model);
        return model.InFont(font);
    }

    /// <summary>A copy of Source Sans 3 OTF made CID-keyed (two Font DICTs, format-3 FDSelect), then changed.</summary>
    private static byte[] CidKeyed(Action<CffModel> change) => Craft(false, model =>
    {
        MakeCidKeyed(model, 0);
        change(model);
    });

    private static byte[] CidKeyedSourceSans(int variant) => Craft(false, model => MakeCidKeyed(model, variant));

    /// <summary>Turns the model CID-keyed: ROS, a CID charset (CID = glyph index), the Private DICT moved to Font DICTs.</summary>
    private static void MakeCidKeyed(CffModel model, int variant)
    {
        var glyphs = model.CharStrings.Count;
        var priv = model.Private!;
        model.Private = null;
        model.EncodingData = null;
        model.EncodingOffset = 0;
        model.Top.Insert(0, Ros(model));
        model.Top.Add(new DictOp(0x122, CffModel.Numbers(glyphs)));
        model.CharsetData = new BigEndianWriter().U8(2).U16(1).U16(glyphs - 2).ToArray();

        switch (variant)
        {
            case 0:
                model.FontDicts = [new CffFontDictModel { Private = priv }, new CffFontDictModel { Private = priv.Clone() }];
                model.FdSelectData = FdSelect3((0, 0), (glyphs / 2, 1), (glyphs, -1));
                break;

            case 1:
                model.FontDicts =
                [
                    new CffFontDictModel { Private = priv },
                    new CffFontDictModel { Private = priv.Clone(), Dict = [Matrix("0.001", "0", "0.0002", "0.001", "0", "0")] },
                    new CffFontDictModel { Private = priv, Dict = [Matrix("0.0005", "0", "0", "0.0005", "0", "0")] },
                ];
                model.FdSelectData = [0, .. Enumerable.Range(0, glyphs).Select(g => (byte)(g % 3))];
                break;

            default:
                model.Top.Add(Matrix("0.001", "0", "0", "0.001", "0.01", "0.02"));
                model.FontDicts =
                [
                    new CffFontDictModel { Private = priv },
                    new CffFontDictModel { Private = priv.Clone(), Dict = [Matrix("1", "0", "0.25", "1", "5", "0")] },
                ];
                model.FdSelectData = FdSelect3((0, 0), (glyphs / 3, 1), (glyphs, -1));
                break;
        }
    }

    private static DictOp Ros(CffModel model)
    {
        model.Strings.Add("Adobe"u8.ToArray());
        model.Strings.Add("Identity"u8.ToArray());
        var adobe = 391 + model.Strings.Count - 2;
        return new DictOp(0x11E, CffModel.Numbers(adobe, adobe + 1, 0));
    }

    private static DictOp Matrix(params string[] values) => new(0x107, values.SelectMany(CffModel.Real).ToArray());

    /// <summary>A format-3 FDSelect: (first glyph, Font DICT) ranges, the last with Font DICT -1 the sentinel.</summary>
    private static byte[] FdSelect3(params (int First, int Fd)[] ranges)
    {
        var w = new BigEndianWriter().U8(3).U16(ranges.Length - 1);
        foreach (var (first, fd) in ranges)
        {
            w.U16(first);
            if (fd >= 0)
                w.U8(fd);
        }

        return w.ToArray();
    }

    /// <summary>A CFF2 variation store (its length first) of one axis, the regions given, one data entry of the indices given.</summary>
    private static byte[] Vstore(int regionCount, ushort[] regionIndices)
    {
        var body = new BigEndianWriter().U16(1).U32(8 + 4).U16(1).U32((uint)(8 + 4 + 4 + 6 * regionCount));
        body.U16(1).U16(regionCount);
        for (var i = 0; i < regionCount; i++)
            body.U16(0).U16(0x4000).U16(0x4000);

        body.U16(0).U16(0).U16(regionIndices.Length);
        foreach (var index in regionIndices)
            body.U16(index);

        var bytes = body.ToArray();
        return new BigEndianWriter().U16(bytes.Length).Bytes(bytes).ToArray();
    }

    /// <summary>
    /// A CFF or CFF2 font of Arial's tables (no <c>glyf</c>) with a minimal table whose glyph 1 is <paramref name="charstring"/>,
    /// with these local (Private DICT) and global subroutines; a CFF2 one has a variation store of two regions.
    /// </summary>
    private static byte[] CraftedFont(bool cff2, byte[] charstring, List<byte[]> subrs, List<byte[]> globalSubrs,
        Action<CffModel>? change = null)
    {
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca")).ToList();
        var glyphs = U16(Table(Arial(), "maxp"), 4);
        var model = CffModel.Minimal(glyphs, cff2);
        model.CharStrings[Crafted] = charstring;
        model.GlobalSubrs = globalSubrs;
        var priv = new CffPrivate { Subrs = subrs.Count > 0 ? subrs : null };
        if (cff2)
        {
            model.FontDicts![0].Private = priv;
            model.VariationStore = Vstore2();
        }
        else
        {
            model.Private = priv;
        }

        change?.Invoke(model);
        tables.Add(new CraftedTable(cff2 ? "CFF2" : "CFF ", model.Write()));
        return Build(OpenTypeCffVersion, tables);
    }

    /// <summary>
    /// An OpenType CFF font of <paramref name="glyphs"/> glyphs from Arial's naming and metric tables (cut to the glyph count,
    /// a small <c>cmap</c>, <c>post</c> 3.0) and the model's CFF table (or, with <paramref name="tag"/> <c>CFF2</c>, its CFF2
    /// table).
    /// </summary>
    private static byte[] SmallOtto(int glyphs, CffModel? model, byte[]? table = null, string tag = "CFF ")
    {
        var arial = Arial();
        var maxp = Table(arial, "maxp")[..6];
        WriteU32(maxp, 0, 0x00005000);
        WriteU16(maxp, 4, glyphs);
        var hhea = Table(arial, "hhea");
        WriteU16(hhea, 34, glyphs);
        var post = Table(arial, "post")[..32];
        WriteU32(post, 0, 0x00030000);
        var tables = new List<CraftedTable>
        {
            new("head", Table(arial, "head")),
            new("hhea", hhea),
            new("maxp", maxp),
            new("hmtx", Table(arial, "hmtx")[..(4 * glyphs)]),
            new("cmap", CraftedTables.Cmap((3, 1, CraftedTables.Format4([('A', 'A' + glyphs - 2, 1 - 'A', 0), (0xFFFF, 0xFFFF, 1, 0)])))),
            new("name", TinyName()),
            new("post", post),
            new("OS/2", Table(arial, "OS/2")),
            new(tag, table ?? model!.Write()),
        };

        return Build(OpenTypeCffVersion, tables);
    }

    /// <summary>A <c>name</c> table of a family and a style name (Windows, Unicode BMP).</summary>
    private static byte[] TinyName()
    {
        var family = Encoding.BigEndianUnicode.GetBytes("Tiny");
        var style = Encoding.BigEndianUnicode.GetBytes("Regular");
        var w = new BigEndianWriter().U16(0).U16(2).U16(6 + 12 * 2);
        w.U16(3).U16(1).U16(0x409).U16(1).U16(family.Length).U16(0);
        w.U16(3).U16(1).U16(0x409).U16(2).U16(style.Length).U16(family.Length);
        return w.Bytes(family).Bytes(style).ToArray();
    }

    /// <summary>A variation store of three data entries: vsindex 0 blends one region, vsindex 1 two, vsindex 2 none.</summary>
    private static byte[] Vstore2()
    {
        // format, region list offset, 3 data offsets; region list (1 axis, 2 regions); data 0 (1 region), data 1 (2 regions),
        // data 2 (no region).
        var body = new BigEndianWriter().U16(1).U32(20).U16(3).U32(20 + 4 + 12).U32(20 + 4 + 12 + 8).U32(20 + 4 + 12 + 8 + 10);
        body.U16(1).U16(2).U16(0).U16(0x4000).U16(0x4000).U16(0xC000).U16(0xC000).U16(0);
        body.U16(0).U16(0).U16(1).U16(0);
        body.U16(0).U16(0).U16(2).U16(0).U16(1);
        body.U16(0).U16(0).U16(0);
        var bytes = body.ToArray();
        return new BigEndianWriter().U16(bytes.Length).Bytes(bytes).ToArray();
    }

    private static int FindGlyphBySid(CffModel model, int sid)
    {
        // The charset of Source Sans 3 is format 1: ranges of (first SID, count - 1) from glyph 1.
        var data = model.CharsetData!;
        var glyph = 1;
        for (var p = 1; p < data.Length; p += 3)
        {
            var first = U16(data, p);
            var left = data[p + 2];
            if (sid >= first && sid <= first + left)
                return glyph + sid - first;

            glyph += left + 1;
        }

        throw new InvalidOperationException($"No glyph has SID {sid}.");
    }

    /// <summary>Where the global Subrs INDEX starts in a written CFF table (after the Name, Top DICT and String INDEXes).</summary>
    private static int GlobalSubrsOffset(byte[] table)
    {
        var (_, names) = CffModel.ReadIndex(table, 4, false);
        var (_, tops) = CffModel.ReadIndex(table, names, false);
        var (_, strings) = CffModel.ReadIndex(table, tops, false);
        return strings;
    }

    /// <summary>
    /// The model (changed) written with <paramref name="bytes"/> appended to the table and the Top DICT entry
    /// <paramref name="op"/> pointing at them: a structure that runs past the end of the table.
    /// </summary>
    private static byte[] AtTableEnd(bool cff2, Action<CffModel>? change, int op, byte[] bytes)
    {
        var model = CffModel.FromFont(Source(cff2), cff2);
        change?.Invoke(model);
        var table = model.Write();
        var length = table.Length;
        table = [.. table, .. bytes];
        PatchTopDictOffset(table, op, length);
        return WithCff(cff2, table);
    }

    /// <summary>A copy of Source Sans with the CFF table replaced by these bytes.</summary>
    private static byte[] WithCff(bool cff2, byte[] table) => WithTable(Source(cff2), cff2 ? "CFF2" : "CFF ", table);

    /// <summary>A copy of Source Sans with its CFF table (as the model writes it) patched.</summary>
    private static byte[] PatchCff(bool cff2, Action<byte[]> patch)
    {
        var table = CffModel.FromFont(Source(cff2), cff2).Write();
        patch(table);
        return WithCff(cff2, table);
    }

    /// <summary>The CFF table with bytes appended to the Top DICT (its INDEX offsets and all later offsets adjusted by the writer).</summary>
    private static byte[] PatchTopDictTail(bool cff2, byte[] tail) => Craft(cff2, model => model.Top.Add(new DictOp(-1, tail)));

    /// <summary>Replaces the operator byte of a one-byte Top DICT entry in a written CFF table (no layout change).</summary>
    private static void PatchTopDictOperator(byte[] table, int op, byte replacement)
    {
        var (start, entries) = TopDictEntries(table);
        var position = start;
        foreach (var entry in entries)
        {
            position += entry.Operands.Length;
            if (entry.Op == op)
            {
                table[position] = replacement;
                return;
            }

            position += entry.Op >= 0x100 ? 2 : 1;
        }

        throw new InvalidOperationException($"No operator {op} in the Top DICT.");
    }

    /// <summary>Sets the 5-byte offset operand of a Top DICT entry in a written CFF table.</summary>
    private static void PatchTopDictOffset(byte[] table, int op, int value)
    {
        var (start, entries) = TopDictEntries(table);
        var position = start;
        foreach (var entry in entries)
        {
            if (entry.Op == op)
            {
                WriteU32(table, position + 1, (uint)value);
                return;
            }

            position += entry.Operands.Length + (entry.Op >= 0x100 ? 2 : 1);
        }

        throw new InvalidOperationException($"No operator {op} in the Top DICT.");
    }

    /// <summary>Where the Top DICT's bytes start in a written CFF table, and its entries.</summary>
    private static (int Start, List<DictOp> Entries) TopDictEntries(byte[] table)
    {
        if (table[0] == 2)
            return (5, CffModel.ParseDict(table.AsSpan(5, U16(table, 3)).ToArray()));

        var (_, names) = CffModel.ReadIndex(table, 4, false);
        var (tops, _) = CffModel.ReadIndex(table, names, false);
        return (names + 2 + 1 + 4 * 2, CffModel.ParseDict(tops[0]));
    }

    /// <summary>A collection of <paramref name="faces"/> faces sharing the font's tables; with <paramref name="ownCopies"/>,
    /// faces after the first have their own maxp (one glyph more), hhea, hmtx and cmap.</summary>
    private static byte[] SharedCollection(byte[] font, int faces, bool ownCopies)
    {
        var tables = ReadTables(font);
        var shared = new List<CraftedTable>(tables);
        var extra = new List<CraftedTable>();
        if (ownCopies)
        {
            var maxp = Table(font, "maxp");
            WriteU16(maxp, 4, U16(maxp, 4) + 1);
            extra.Add(new CraftedTable("maxp", maxp));
            extra.Add(new CraftedTable("hhea", Table(font, "hhea")));
            extra.Add(new CraftedTable("hmtx", [.. Table(font, "hmtx"), 0, 0]));
            extra.Add(new CraftedTable("cmap", Table(font, "cmap")));
        }

        var w = new BigEndianWriter();
        var header = 12 + 4 * faces;
        var directory = 12 + 16 * tables.Count;
        var dataStart = header + faces * directory;
        var positions = new Dictionary<CraftedTable, int>();
        var position = dataStart;
        foreach (var table in shared.Concat(extra))
        {
            positions[table] = position;
            position += (table.Data.Length + 3) & ~3;
        }

        w.Bytes("ttcf"u8).U16(1).U16(0).U32((uint)faces);
        for (var i = 0; i < faces; i++)
            w.U32((uint)(header + i * directory));

        for (var i = 0; i < faces; i++)
        {
            var own = i > 0 && ownCopies;
            var list = tables.OrderBy(table => table.Tag, StringComparer.Ordinal)
                .Select(table => own && extra.FirstOrDefault(e => e.Tag == table.Tag) is { } copy ? copy : table).ToList();
            w.U32(OpenTypeCffVersion).U16(list.Count).U16(0).U16(0).U16(0);
            foreach (var table in list)
                w.Bytes(Encoding.ASCII.GetBytes(table.Tag)).U32(0).U32((uint)positions[table]).U32((uint)table.Data.Length);
        }

        foreach (var table in shared.Concat(extra))
        {
            w.Bytes(table.Data);
            w.Bytes(new byte[((table.Data.Length + 3) & ~3) - table.Data.Length]);
        }

        return w.ToArray();
    }

    private (ScaledFont Font, FreeTypeFace Reference) Open(byte[] data, int faceIndex, int size)
    {
        var face = FontLoader.Load(data).Faces[faceIndex];
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        var reference = m_freeType.OpenFace(data, faceIndex);
        reference.SetPixelSize(size);

        return (font, reference);
    }

    /// <summary>Compares outlines, metrics and bitmaps of the glyphs at each size; returns the last size's outline of the first glyph.</summary>
    private GlyphOutline AssertGlyphsMatchFreeType(byte[] data, int[] glyphs, params int[] sizes)
    {
        var differences = new List<string>();
        GlyphOutline first = GlyphOutline.Empty;
        foreach (var size in sizes)
        {
            var (font, reference) = Open(data, 0, size);
            using var _ = reference;
            differences.AddRange(CompareOutlines(font, reference, glyphs).Select(d => $"{size}px outline {d}"));
            differences.AddRange(CompareMetrics(font, reference, glyphs).Select(d => $"{size}px metrics {d}"));
            var bitmaps = CompareBitmaps(font, reference, glyphs);
            if (bitmaps.Count > 0)
                differences.Add($"{size}px {bitmaps}");

            first = font.GetOutline(glyphs[0]);
        }

        Assert.That(differences, Is.Empty);
        return first;
    }

    /// <summary>FreeType fails to load the crafted glyph, and ours throws <see cref="InvalidDataException"/> with the message.</summary>
    private void AssertFailsAsFreeType(byte[] data, string message)
    {
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(32);

        Assert.Multiple(() =>
        {
            Assert.That(reference.TryLoadAndRender(Crafted).Load, Is.Not.EqualTo(FT_Error.FT_Err_Ok), "FreeType fails the glyph");
            Assert.That(() => font.GetOutline(Crafted), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));
        });
    }

    /// <summary>The font loads, its first glyphs (and these) read as FreeType reads them at 32 px.</summary>
    private void AssertLoadsAsFreeType(byte[] data, params int[] glyphs) =>
        AssertGlyphsMatchFreeType(data, [.. Enumerable.Range(0, 50), .. glyphs], 32);

    private static void AssertInvalid(byte[] font, string message) =>
        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));

    private static IEnumerable<int> Glyphs(FontFace face, GlyphSet set) => set == GlyphSet.Every
        ? Enumerable.Range(0, face.GlyphCount)
        : TestFonts.GameCharacters().Select(codePoint => face.GetGlyphIndex(codePoint)).Distinct().Order();

    private static List<string> CompareOutlines(ScaledFont font, FreeTypeFace reference, IEnumerable<int> glyphs)
    {
        var mismatches = new List<string>();
        var count = 0;
        foreach (var glyph in glyphs)
        {
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false).Outline;
            if (ScaledFontTests.Compare(font.GetOutline(glyph), expected) is not { } difference)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {difference}");
        }

        if (count > MaxReported)
            mismatches.Add($"{count} glyphs differ in all");

        return mismatches;
    }

    private static List<string> CompareMetrics(ScaledFont font, FreeTypeFace reference, IEnumerable<int> glyphs)
    {
        var mismatches = new List<string>();
        var count = 0;
        foreach (var glyph in glyphs)
        {
            var ours = font.GetMetrics(glyph);
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false).Metrics;
            if (ours == expected)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {ours}, FreeType {expected}");
        }

        if (count > MaxReported)
            mismatches.Add($"{count} glyphs differ in all");

        return mismatches;
    }

    private static BitmapComparison CompareBitmaps(ScaledFont font, FreeTypeFace reference, IEnumerable<int> glyphs)
    {
        var comparison = new BitmapComparison();
        foreach (var glyph in glyphs)
            comparison.Add(glyph, font.Render(glyph), reference.LoadGlyph(glyph, hinting: false, render: true).Bitmap!);

        return comparison;
    }
}
