using CoolEngine.Fonts.Tests.Reference;
using static CoolEngine.Fonts.Tests.Reference.CraftedVariations;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// Copies of Arial made variable with crafted, well-formed variation tables, so each path of FreeType's variation code runs
/// (the real test faces need not use them all): shared and embedded tuples, intermediate regions, private and shared point
/// numbers with interpolation, phantom-point deltas (advances without <c>HVAR</c>), composite offsets, empty glyphs,
/// <c>avar</c>, <c>HVAR</c> with and without an advance map, <c>VVAR</c> beside <c>vmtx</c>, <c>MVAR</c>, <c>cvar</c>, and the
/// hinting hooks (GETINFO bit 10, GETVARIATION, GETDATA). Compared with FreeType at several coordinates, unhinted and hinted.
/// </summary>
public sealed partial class VariationTests
{
    private static readonly (string, double, double, double)[] s_twoAxes = [("wght", 100, 400, 900), ("wdth", 50, 100, 150)];

    private static readonly float[][] s_twoAxisSettings =
    [
        [100, 100], [250, 100], [400, 100], [650, 100], [900, 100], [400, 50], [400, 75], [400, 125], [400, 150], [900, 150],
        [100, 50], [775, 112.5f], [1000, 30],
    ];

    [TestCase(Mode.Unhinted)]
    [TestCase(Mode.V35)]
    [TestCase(Mode.V40)]
    public void Load_CraftedGvarTuples_MatchFreeType(Mode mode)
    {
        var (font, glyphs) = CraftedGvarFont();

        AssertCraftedMatches(font, glyphs, s_twoAxisSettings, mode);
    }

    [TestCase(Mode.Unhinted, false, false)]
    [TestCase(Mode.V35, false, false)]
    [TestCase(Mode.V40, false, false)]
    [TestCase(Mode.Unhinted, true, false)]
    [TestCase(Mode.V40, true, false)]
    [TestCase(Mode.V35, false, true)]
    [TestCase(Mode.V40, false, true)]
    public void Load_CraftedHvarMvarCvar_MatchFreeType(Mode mode, bool advanceMap, bool verticalMetrics)
    {
        var (font, glyphs) = CraftedMetricsFont(advanceMap, verticalMetrics);
        float[][] settings = [[100], [250], [400], [700], [900]];

        AssertCraftedMatches(font, glyphs, settings, mode);
    }

    /// <summary>
    /// A glyph program that moves points by GETINFO (selector 8: bit 10 says the face has a blend), by each GETVARIATION value
    /// and by GETDATA: without coordinates FreeType has no blend (GETVARIATION and GETDATA are undefined opcodes, which stop the
    /// program); with coordinates, even the defaults, it has.
    /// </summary>
    [TestCase(Mode.V35)]
    [TestCase(Mode.V40)]
    public void GlyphProgram_GetInfoGetVariationGetData_MatchFreeType(Mode mode)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        // MDAP[0] touches points 1 to 4 in y first, so v40's backward compatibility lets SHPIX move them.
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(1).Op(0x2E).Push(2).Op(0x2E).Push(3).Op(0x2E).Push(4).Op(0x2E)
            .Push(1, 8).Op(0x88).Op(Bytecode.Shpix)        // point 1 by GETINFO(8)
            .Op(0x91)                                       // GETVARIATION: one 2.14 value per axis
            .Push(2).Op(0x23, Bytecode.Shpix)               // SWAP; point 2 by the second axis's value
            .Push(3).Op(0x23, Bytecode.Shpix)               // point 3 by the first axis's value
            .Push(4).Op(0x92).Op(Bytecode.Shpix)            // point 4 by GETDATA
            .ToArray();
        var font = VariableArialWithProgram(program, h);

        AssertCraftedMatches(font, [h], [[400, 100], [100, 100], [900, 50], [650, 125]], mode);
        AssertNoCoordinatesMatch(font, [h], mode);
    }

    /// <summary>Compares the crafted font at each setting (every axis given) with FreeType given the same coordinates.</summary>
    private static void AssertCraftedMatches(byte[] font, int[] glyphs, float[][] settings, Mode mode, bool requireChange = true)
    {
        var face = FontLoader.Load(font).Faces[0];
        var reports = new List<string>();
        var compared = 0;
        var signatures = new HashSet<string>();
        foreach (var values in settings)
        {
            var result = Compare(face, font, 0, values, glyphs, mode);
            compared += result.Compared;
            if (result.Differences > 0 || result.LineMetricsDifferences.Count > 0)
                reports.Add($"[{string.Join(", ", values)}]: {result} {string.Join("; ", result.LineMetricsDifferences)}");

            signatures.Add(Signature(face, values, glyphs, mode));
        }

        Assert.Multiple(() =>
        {
            Assert.That(reports, Is.Empty);
            Assert.That(compared, Is.EqualTo(settings.Length * glyphs.Length * s_sizes.Length), "every glyph loads in FreeType");
            if (requireChange)
                Assert.That(signatures, Has.Count.GreaterThan(1), "the coordinates change the glyphs (the comparison is not vacuous)");
        });
    }

    /// <summary>The 32 px outlines, metrics and line metrics of the glyphs at a setting, as text, to tell settings apart.</summary>
    internal static string Signature(FontFace face, float[] values, IEnumerable<int> glyphs, Mode mode)
    {
        var font = face.CreateScaledFont(new ScaledFontOptions(32)
        {
            Hinting = mode == Mode.Unhinted ? HintingMode.None : HintingMode.Native,
            Interpreter = mode == Mode.V35 ? TrueTypeInterpreterVersion.V35 : TrueTypeInterpreterVersion.V40,
            Variation = Coordinates(face, values),
        });

        return font.LineMetrics + string.Concat(glyphs.Select(glyph =>
            $"{font.GetMetrics(glyph)}{string.Join(",", font.GetOutline(glyph).Points.ToArray())}"));
    }

    /// <summary>The same comparison without coordinates (no blend), as FreeType's face on which no variation function was called.</summary>
    private static void AssertNoCoordinatesMatch(byte[] font, int[] glyphs, Mode mode)
    {
        var face = FontLoader.Load(font).Faces[0];
        var version = mode == Mode.V35 ? TrueTypeInterpreterVersion.V35 : TrueTypeInterpreterVersion.V40;
        foreach (var size in s_sizes)
        {
            var result = TrueTypeHinterTests.CompareGlyphs(face, font, 0, glyphs, size, version, TrueTypeHinterTests.Part.Outline);
            Assert.That(result.Differences, Is.Zero, $"{size}px without coordinates: {result}");
        }
    }

    /// <summary>
    /// Arial with two axes (wght, wdth), an <c>avar</c> map on wght, and <c>gvar</c> data for 'H' (an embedded tuple with
    /// private points and a phantom-point delta, an intermediate tuple over all points, a shared tuple with shared points),
    /// for 'Á' (a composite: component offsets and phantom points) and for the space (an empty glyph: phantom points only).
    /// </summary>
    private static (byte[] Font, int[] Glyphs) CraftedGvarFont()
    {
        var arialBytes = CraftedFonts.Arial();
        var arial = FontLoader.Load(arialBytes).Faces[0];
        var glyf = arial.Data.Glyf!;
        var h = arial.GetGlyphIndex('H');
        var aacute = arial.GetGlyphIndex(0xC1);
        var space = arial.GetGlyphIndex(' ');
        var o = arial.GetGlyphIndex('o');
        var n = glyf.VariationPointCount(h);
        var components = glyf.VariationPointCount(aacute);

        int[] Pattern(int count, Func<int, int> value) => Enumerable.Range(0, count).Select(value).ToArray();

        var hData = TupleStore(2,
        [
            new CraftedTuple([1, 0], 0, null, null, [0, 2, 5, n + 1], [30, -20, 15, 40], [10, 0, -25, 0]),
            new CraftedTuple([0, 0.5], 0, [0, 0.2], [0, 0.8], [], Pattern(n + 4, i => i % 7 - 3), Pattern(n + 4, i => i % 5 - 2)),
            new CraftedTuple(null, 0, null, null, null, [12, -7, 9], [0, 11, 0]),
            new CraftedTuple(null, 1, null, null, [1, 4], [200, -300], [-150, 90]),
        ], sharedPoints: [1, 3, n]);
        var aacuteData = TupleStore(2,
        [
            new CraftedTuple([1, 0], 0, null, null, [], Pattern(components + 4, i => i == components + 1 ? 30 : 17 * (i + 1)),
                Pattern(components + 4, i => i < components ? -9 * (i + 1) : 0)),
        ], sharedPoints: null);
        var spaceData = TupleStore(2,
        [
            new CraftedTuple([1, 1], 0, null, null, [], [10, 50, 0, 0], [0, 0, 0, 0]),
        ], sharedPoints: null);

        var gvar = Gvar(2, [[-1, 0], [0.5, -1]], arial.GlyphCount,
            new Dictionary<int, byte[]> { [h] = hData, [aacute] = aacuteData, [space] = spaceData });
        var avar = Avar(0x00010000, AvarMap((-0.5, -0.3), (0.5, 0.7)), []);
        var font = VariableArial(s_twoAxes, new CraftedTable("gvar", gvar), new CraftedTable("avar", avar));
        return (font, [0, h, aacute, space, o]);
    }

    /// <summary>
    /// Arial with one axis (wght) and: <c>gvar</c> deltas on 'H' including its phantom points (which <c>HVAR</c> makes FreeType
    /// drop); <c>HVAR</c> deltas for every glyph (through an advance map, if asked); <c>MVAR</c> deltas for the ascender,
    /// descender and line gap (and a tag FreeType ignores); <c>cvar</c> deltas for every cvt entry; and, if asked, <c>vhea</c>,
    /// <c>vmtx</c> and <c>VVAR</c>, with a glyph program on 'H' that moves points by the vertical phantom points.
    /// </summary>
    private static (byte[] Font, int[] Glyphs) CraftedMetricsFont(bool advanceMap, bool verticalMetrics)
    {
        var arialBytes = CraftedFonts.Arial();
        var arial = FontLoader.Load(arialBytes).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var aacute = arial.GetGlyphIndex(0xC1);
        var space = arial.GetGlyphIndex(' ');
        var count = arial.GlyphCount;
        var n = arial.Data.Glyf!.VariationPointCount(h);

        var gvar = Gvar(1, [], count, new Dictionary<int, byte[]>
        {
            [h] = TupleStore(1, [new CraftedTuple([1], 0, null, null, [0, 3, n, n + 1, n + 2, n + 3], [40, -30, 25, 60, 5, 7],
                [-20, 15, 0, 0, 33, -44])], null),
        });

        var region = new[] { new[] { (0.0, 1.0, 1.0) }, new[] { (-1.0, -1.0, 0.0) } };
        byte[] hvar;
        if (advanceMap)
        {
            var store = Store(1, region, ([0, 1], [[100, -40], [-60, 25], [7, 300]], true));
            hvar = Hvar(store, IndexMap(Enumerable.Range(0, count - 5).Select(g => (0, g % 3)).ToArray()));
        }
        else
        {
            var store = Store(1, region, ([0, 1], Enumerable.Range(0, count).Select(g => new[] { g % 50 - 25, g % 7 * 3 }).ToArray(), false));
            hvar = Hvar(store);
        }

        var mvarStore = Store(1, region, ([0, 1], [[80, -10], [-40, 30], [30, 30], [500, 0]], true));
        var mvar = Mvar(mvarStore, ("hasc", 0, 0), ("hcla", 0, 3), ("hdsc", 0, 1), ("hlgp", 0, 2), ("zzzz", 0, 3));
        var cvtEntries = CraftedFonts.Table(arialBytes, "cvt ").Length / 2;
        var cvar = Cvar(1,
        [
            new CraftedTuple([1], 0, null, null, [], Enumerable.Range(0, cvtEntries).Select(i => i % 9 - 4).ToArray(), null),
            new CraftedTuple([-1], 0, null, null, [0, 5, 17, 300], [64, -100, 20, 11], null),
        ], null);

        var tables = new List<CraftedTable>
        {
            new("gvar", gvar), new("HVAR", hvar), new("MVAR", mvar), new("cvar", cvar),
        };

        var font = VariableArial([("wght", 100, 400, 900)], [.. tables]);
        if (verticalMetrics)
        {
            var vhea = new BigEndianWriter().U32(0x00011000).U16(1800).U16(-500 & 0xFFFF).U16(0).U16(2400).U16(0).U16(0).U16(2000)
                .U16(1).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(count).ToArray();
            var vmtx = new BigEndianWriter();
            for (var g = 0; g < count; g++)
                vmtx.U16(2300 + g % 11).U16(120 + g % 13);

            var vvar = Hvar(Store(1, region, ([0, 1], Enumerable.Range(0, count).Select(g => new[] { g % 40 - 20, -(g % 9) }).ToArray(), false)));
            var program = new Bytecode().Op(Bytecode.SvtcaY)
                .Push(0, n + 2).Op(0x46, Bytecode.Shpix)   // point 0 by GC[cur] of pp3
                .Push(1, n + 3).Op(0x46, Bytecode.Shpix)   // point 1 by GC[cur] of pp4
                .ToArray();
            font = CraftedFonts.WithTable(font, "vhea", vhea);
            font = CraftedFonts.WithTable(font, "vmtx", vmtx.ToArray());
            font = CraftedFonts.WithTable(font, "VVAR", vvar);
            font = CraftedPrograms.WithGlyphProgram(font, h, program);
        }

        return (font, [0, h, aacute, space, arial.GetGlyphIndex('o'), arial.GetGlyphIndex('W')]);
    }

    /// <summary>Arial with two axes and no other variation table, whose glyph <paramref name="glyph"/> runs <paramref name="program"/>.</summary>
    private static byte[] VariableArialWithProgram(byte[] program, int glyph) =>
        CraftedPrograms.WithGlyphProgram(VariableArial(s_twoAxes), glyph, program);
}
