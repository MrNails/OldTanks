using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Variations;
using static CoolEngine.Fonts.Tests.Reference.CraftedVariations;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// The strict rules of the variation tables (one test each, with its own message), the cases accepted and read as FreeType
/// reads them (each compared with FreeType), and avar version 2 (<see cref="NotSupportedException"/>).
/// </summary>
public sealed partial class VariationTests
{
    private static readonly (string, double, double, double)[] s_oneAxis = [("wght", 100, 400, 900)];

    private static readonly float[][] s_oneAxisSettings = [[100], [300], [400], [650], [900]];

    // ---- avar ----

    [Test]
    public void Load_AvarVersion2_ThrowsNotSupported() =>
        AssertLoadThrows<NotSupportedException>(VariableArial(s_twoAxes, new CraftedTable("avar", Avar(0x00020000, AvarMap(), AvarMap()))),
            "'avar' table is version 2.0");

    [Test]
    public void Load_AvarHeaderShort_Throws() =>
        AssertLoadThrows<InvalidDataException>(VariableArial(s_twoAxes, new CraftedTable("avar", [0, 1, 0, 0, 0])),
            "shorter than its 8-byte header");

    [Test]
    public void Load_AvarMapPastTable_Throws() =>
        AssertLoadThrows<InvalidDataException>(
            VariableArial(s_twoAxes, new CraftedTable("avar", Avar(0x00010000, AvarMap((0.5, 0.7)), AvarMap())[..^6])),
            "segment map of axis 1");

    /// <summary>
    /// avar maps FreeType applies with defined results (Denys, 2026-11-27: "Apply as FreeType"): each loads and normalizes as
    /// FreeType normalizes it, at coordinates FreeType accepts.
    /// </summary>
    [TestCase("outside", TestName = "Load_AvarCoordinateOutsideRange_AppliedAsFreeType")]
    [TestCase("not ascending", TestName = "Load_AvarFromCoordinatesNotAscending_AppliedAsFreeType")]
    [TestCase("no anchors", TestName = "Load_AvarMapWithoutAnchors_AppliedAsFreeType")]
    public void Load_AvarValueAnomaly_AppliedAsFreeType(string anomaly)
    {
        (double, double)[] map = anomaly switch
        {
            "outside" => AvarMap((0.5, 1.5)),
            "not ascending" => [(-1, -1), (0, 0), (0.5, 0.6), (0.4, 0.7), (1, 1)],
            _ => [(-1, -1), (0.5, 0.7), (1, 1)],
        };
        var font = VariableArial(s_twoAxes, new CraftedTable("avar", Avar(0x00010000, map, AvarMap())));

        Assert.That(FontLoader.Load(font).Faces[0].Data.Variations!.Avar, Is.Not.Null);
        AssertNormalizesAsFreeType(font, [[100, 100], [250, 75], [400, 100], [500, 100], [520, 150], [130, 50]]);
    }

    /// <summary>
    /// Coordinates an avar maps outside -1..1 are refused where FreeType refuses them (its FT_Set_Var_Design_Coordinates
    /// fails): a map value above 1, and a repeated fromCoordinate (FreeType's FT_MulDiv by 0 gives 0x7FFFFFFF).
    /// </summary>
    [TestCase("outside", 650f, TestName = "CreateScaledFont_AvarMapsPastOne_RefusedAsFreeType")]
    [TestCase("repeated", 250f, TestName = "CreateScaledFont_AvarRepeatedFromCoordinate_RefusedAsFreeType")]
    public void CreateScaledFont_AvarMapsOutsideRange_RefusedAsFreeType(string anomaly, float weight)
    {
        (double, double)[] map = anomaly == "outside" ? AvarMap((0.5, 1.5)) : [(0, 0), (0, 0.5), (1, 1)];
        var font = VariableArial(s_twoAxes, new CraftedTable("avar", Avar(0x00010000, map, AvarMap())));
        var face = FontLoader.Load(font).Faces[0];
        using var freeType = new FreeTypeVariations();

        Assert.Multiple(() =>
        {
            Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new("wght", weight)] }),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("FreeType refuses such coordinates"));
            Assert.That(() => freeType.Open(font, 0, Design([weight, 100]), 16),
                Throws.InvalidOperationException.With.Message.Contains("FT_Set_Var_Design_Coordinates"));
        });
    }

    /// <summary>An avar of another version, or of another axis count (version 2.0 too), is ignored by FreeType, and so here.</summary>
    [TestCase(0x00030000u, 2, TestName = "Load_AvarUnknownVersion_IgnoredAsFreeType")]
    [TestCase(0x00010000u, 1, TestName = "Load_AvarAxisCountOtherThanFvar_IgnoredAsFreeType")]
    [TestCase(0x00020000u, 1, TestName = "Load_AvarVersion2OtherAxisCount_IgnoredAsFreeType")]
    public void Load_AvarFreeTypeIgnores_IgnoredAsFreeType(uint version, int maps)
    {
        var avar = Avar(version, [.. Enumerable.Repeat(AvarMap((0.5, 0.7)), maps)]);
        var font = VariableArial(s_twoAxes, new CraftedTable("avar", avar));
        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.Data.Variations!.Avar, Is.Null);
            AssertNormalizesAsFreeType(font, [[650, 100], [900, 125], [250, 75]]);
        });
    }

    // ---- gvar ----

    [Test]
    public void Load_GvarHeaderShort_Throws() =>
        AssertLoadThrows<InvalidDataException>(VariableArial(s_twoAxes, new CraftedTable("gvar", new byte[12])), "shorter than its 20-byte header");

    [Test]
    public void Load_GvarVersion2_Throws() =>
        AssertLoadThrows<InvalidDataException>(PatchGvar(gvar => CraftedFonts.WriteU32(gvar, 0, 0x00020000)), "version is 0x00020000");

    [Test]
    public void Load_GvarAxisCountOtherThanFvar_Throws() =>
        AssertLoadThrows<InvalidDataException>(PatchGvar(gvar => CraftedFonts.WriteU16(gvar, 4, 3)), "3 axes, but 'fvar' has 2");

    [Test]
    public void Load_GvarSharedTuplesPastTable_Throws() =>
        AssertLoadThrows<InvalidDataException>(PatchGvar(gvar => CraftedFonts.WriteU32(gvar, 8, (uint)gvar.Length)), "shared tuples at");

    [Test]
    public void Load_GvarGlyphOffsetsPastTable_Throws() =>
        AssertLoadThrows<InvalidDataException>(
            VariableArial(s_twoAxes, new CraftedTable("gvar", Gvar(2, [], ArialGlyphCount(), new Dictionary<int, byte[]>())[..40])),
            "glyph offsets run past");

    [Test]
    public void Load_GvarGlyphOffsetDecreasing_Throws() =>
        AssertLoadThrows<InvalidDataException>(PatchGvar(gvar => CraftedFonts.WriteU32(gvar, 20 + 4 * 2, 9)), "smaller than the one before");

    [Test]
    public void Load_GvarGlyphOffsetPastTable_Throws() =>
        AssertLoadThrows<InvalidDataException>(PatchGvar(gvar =>
        {
            var count = CraftedFonts.U16(gvar, 12);
            CraftedFonts.WriteU32(gvar, 20 + 4 * count, (uint)gvar.Length);
        }), "lies past the");

    [Test]
    public void Load_GvarGlyphHeaderPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ => [0, 1]), "has its header past");

    [Test]
    public void Load_GvarSerializedDataPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ => new BigEndianWriter().U16(0).U16(100).ToArray()), "serialized data at 100");

    [Test]
    public void Load_GvarTupleHeaderPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ => new BigEndianWriter().U16(1).U16(4).U16(0).ToArray()), "tuple 0 header at 4");

    [Test]
    public void Load_GvarPeakTuplePastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ => new BigEndianWriter().U16(1).U16(4).U16(0).U16(0x8000).U16(0).ToArray()),
            "peak tuple past");

    [Test]
    public void Load_GvarIntermediateTuplesPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(
            GvarWithH(_ => new BigEndianWriter().U16(1).U16(4).U16(0).U16(0xC000).U16(0x4000).U16(0).U16(0).U16(0).ToArray()),
            "intermediate tuples past");

    [Test]
    public void Load_GvarSharedTupleIndexOutOfRange_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(n => TupleStore(2, [new CraftedTuple(null, 2, null, null, [], Zeros(n + 4), Zeros(n + 4))], null)),
            "names shared tuple 2, but 'gvar' has 1");

    [Test]
    public void Load_GvarTupleDataPastGlyphData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(n =>
        {
            var data = TupleStore(2, [new CraftedTuple([1, 0], 0, null, null, [], Zeros(n + 4), Zeros(n + 4))], null);
            CraftedFonts.WriteU16(data, 4, CraftedFonts.U16(data, 4) + 1);
            return data;
        }), "bytes of data at");

    [Test]
    public void Load_GvarTupleWithoutSharedPoints_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(n => TupleStore(2, [new CraftedTuple([1, 0], 0, null, null, null, Zeros(n + 4), Zeros(n + 4))], null)),
            "uses the shared point numbers, but");

    [Test]
    public void Load_GvarPointRunPastCount_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(n =>
        {
            // Two points declared, a run of three.
            var tuple = new BigEndianWriter().U8(2).U8(2).U8(1).U8(1).U8(1).Bytes(PackDeltas([1, 2])).Bytes(PackDeltas([1, 2])).ToArray();
            return TupleData(tuple, 2, 0);
        }), "a run of 3 points where 2 of 2 remain");

    [Test]
    public void Load_GvarPointNumbersPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ => TupleData(new BigEndianWriter().U8(3).U8(0x82).U16(1).ToArray(), 2, 0)),
            "point numbers run past");

    [Test]
    public void Load_GvarDeltaRunPastCount_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ =>
            TupleData(new BigEndianWriter().Bytes(PackPoints([1, 2])).U8(2).U8(1).U8(1).U8(1).Bytes(PackDeltas([0, 0])).ToArray(), 2, 0)),
            "a run of 3 deltas where 2 of 2 remain");

    [Test]
    public void Load_GvarDeltasPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(GvarWithH(_ =>
            TupleData(new BigEndianWriter().Bytes(PackPoints([1, 2])).U8(0x41).U16(5).ToArray(), 2, 0)), "deltas past its data");

    /// <summary>
    /// Accepted and read as FreeType reads it: point numbers at or past the glyph's points (ignored), bytes left in a tuple's
    /// data, and a <c>gvar</c> glyph count below <c>maxp</c>'s (later glyphs have no variations).
    /// </summary>
    [TestCase(Mode.Unhinted)]
    [TestCase(Mode.V40)]
    public void Load_GvarAcceptedQuirks_ReadAsFreeType(Mode mode)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var o = arial.GetGlyphIndex('o');
        var n = arial.Data.Glyf!.VariationPointCount(h);
        var hData = TupleStore(2, [new CraftedTuple([1, 0], 0, null, null, [2, n + 7, 60000], [40, 50, 60], [-30, 20, 10])], null);
        CraftedFonts.WriteU16(hData, 4, CraftedFonts.U16(hData, 4) + 3);
        hData = [.. hData, 0xAB, 0xCD, 0xEF];
        var oData = TupleStore(2, [new CraftedTuple([1, 0], 0, null, null, [0], [33], [44])], null);
        var gvar = Gvar(2, [], Math.Max(h, o) + 1, new Dictionary<int, byte[]> { [h] = hData, [o] = oData });
        var font = VariableArial(s_twoAxes, new CraftedTable("gvar", gvar));

        AssertCraftedMatches(font, [h, o, Math.Max(h, o) + 5], [[900, 100], [650, 100]], mode);
    }

    // ---- cvar ----

    [Test]
    public void Load_CvarHeaderShort_Throws() =>
        AssertLoadThrows<InvalidDataException>(VariableArial(s_oneAxis, new CraftedTable("cvar", [0, 1])), "shorter than its version");

    [Test]
    public void Load_CvarSharedTupleWithoutGvar_Throws() =>
        AssertLoadThrows<InvalidDataException>(
            VariableArial(s_oneAxis, new CraftedTable("cvar", Cvar(1, [new CraftedTuple(null, 0, null, null, [3], [5], null)], null))),
            "names shared tuple 0, but 'gvar' has 0");

    [Test]
    public void Load_CvarDeltasPastData_Throws() =>
        AssertLoadThrows<InvalidDataException>(VariableArial(s_oneAxis,
                new CraftedTable("cvar", new BigEndianWriter().U32(0x00010000).Bytes(TupleData(new BigEndianWriter().Bytes(PackPoints([1])).U8(0x40).ToArray(), 1, 4)).ToArray())),
            "'cvar' table's tuple 0 has its deltas past");

    /// <summary>
    /// Accepted and read as FreeType reads it: a cvar of another version (ignored); a tuple using shared point numbers that are
    /// "all points" (FreeType applies nothing for it); cvt indices past the cvt (ignored).
    /// </summary>
    [TestCase(0x00020000u, Mode.V35)]
    [TestCase(0x00010000u, Mode.V35)]
    [TestCase(0x00010000u, Mode.V40)]
    public void Load_CvarAcceptedQuirks_HintAsFreeType(uint version, Mode mode)
    {
        var arialBytes = CraftedFonts.Arial();
        var arial = FontLoader.Load(arialBytes).Faces[0];
        var cvt = CraftedFonts.Table(arialBytes, "cvt ").Length / 2;
        var cvar = Cvar(1,
        [
            new CraftedTuple([1], 0, null, null, null, Enumerable.Range(0, cvt).Select(i => i % 11 - 5).ToArray(), null),
            new CraftedTuple([1], 0, null, null, [4, 9, cvt + 3], [60, -90, 70], null),
        ], sharedPoints: []);
        CraftedFonts.WriteU32(cvar, 0, version);
        var font = VariableArial(s_oneAxis, new CraftedTable("cvar", cvar));
        var glyphs = new[] { 'H', 'o', 'W', 'a', 'e', 'g' }.Select(c => arial.GetGlyphIndex(c)).ToArray();

        AssertCraftedMatches(font, glyphs, s_oneAxisSettings, mode, requireChange: false);
    }

    // ---- item variation store, delta-set index maps, HVAR, MVAR ----

    private static readonly (double, double, double)[][] s_region = [[(0, 1, 1)]];

    /// <summary>
    /// A broken HVAR store (each case one that FreeType's tt_var_load_item_variation_store fails) is treated as absent, as
    /// FreeType ignores it (Denys, 2026-11-27): the font loads, and its advances come from the varied outline, equal to
    /// FreeType's. The table lies last in the file, so a read past it is past the file for FreeType too.
    /// </summary>
    [TestCase("format", TestName = "Load_HvarStoreFormat2_IgnoredAsFreeType")]
    [TestCase("no data", TestName = "Load_HvarStoreWithoutData_IgnoredAsFreeType")]
    [TestCase("axes", TestName = "Load_HvarStoreAxisCountOtherThanFvar_IgnoredAsFreeType")]
    [TestCase("regions", TestName = "Load_HvarStoreOf32768Regions_IgnoredAsFreeType")]
    [TestCase("words", TestName = "Load_HvarStoreMoreWordDeltasThanRegions_IgnoredAsFreeType")]
    [TestCase("region count", TestName = "Load_HvarStoreEntryBlendingMoreRegionsThanStore_IgnoredAsFreeType")]
    [TestCase("region index", TestName = "Load_HvarStoreRegionIndexOutOfRange_IgnoredAsFreeType")]
    [TestCase("deltas", TestName = "Load_HvarStoreDeltaSetsPastTable_IgnoredAsFreeType")]
    public void Load_HvarBrokenStore_IgnoredAsFreeType(string rule)
    {
        var store = rule switch
        {
            "format" => Patch(Store(1, s_region, ([0], [[1]], false)), 0, 2),
            "no data" => new BigEndianWriter().U16(1).U32(8).U16(0).U16(1).U16(0).ToArray(),
            "axes" => Store(2, [[(0, 1, 1), (0, 0, 0)]], ([0], [[1]], false)),
            "regions" => Store(1, Enumerable.Repeat(s_region[0], 32768).ToArray(), ([0], [[1]], false)),
            // One region of one axis: the entry starts at 12 + 4 + 6 = 22, its word delta count at 24, its region count at 26.
            "words" => Patch(Store(1, s_region, ([0], [[1]], false)), 24, 2),
            "region count" => Patch(Store(1, s_region, ([0], [[1]], true)), 26, 2),
            "region index" => Store(1, s_region, ([1], [[1]], false)),
            _ => Store(1, s_region, ([0], [[1], [2], [3], [4]], true))[..^6],
        };

        AssertHvarIgnoredAsFreeType(Hvar(store));
    }

    /// <summary>A broken HVAR advance map (each case one FreeType's loader fails) is treated as absent, as FreeType ignores it.</summary>
    [TestCase("format", TestName = "Load_HvarIndexMapFormat2_IgnoredAsFreeType")]
    [TestCase("reserved", TestName = "Load_HvarIndexMapReservedEntryBits_IgnoredAsFreeType")]
    [TestCase("past", TestName = "Load_HvarIndexMapEntriesPastTable_IgnoredAsFreeType")]
    [TestCase("outer", TestName = "Load_HvarIndexMapOuterIndexOutOfRange_IgnoredAsFreeType")]
    [TestCase("inner", TestName = "Load_HvarIndexMapInnerIndexOutOfRange_IgnoredAsFreeType")]
    public void Load_HvarBrokenIndexMap_IgnoredAsFreeType(string rule)
    {
        var store = Store(1, s_region, ([0], [[1], [2]], false));
        var map = rule switch
        {
            "format" => Patch(IndexMap((0, 0)), 0, 2, 1),
            "reserved" => Patch(IndexMap((0, 0)), 1, 0xFF, 1),
            "past" => IndexMap((0, 0), (0, 1), (0, 1))[..^8],
            "outer" => IndexMap((0, 0), (1, 0)),
            _ => IndexMap((0, 0), (0, 2)),
        };

        AssertHvarIgnoredAsFreeType(Hvar(store, map));
    }

    [Test]
    public void Load_HvarHeaderShort_IgnoredAsFreeType() => AssertHvarIgnoredAsFreeType([0, 1, 0, 0]);

    [Test]
    public void Load_HvarStoreOffsetZero_IgnoredAsFreeType() =>
        AssertHvarIgnoredAsFreeType(Patch(Hvar(Store(1, s_region, ([0], [[1]], false))), 4, 0, 4));

    /// <summary>A broken MVAR (each case one FreeType's ft_var_load_mvar fails) is treated as absent: the line metrics stay, as in FreeType.</summary>
    [TestCase("header", TestName = "Load_MvarHeaderShort_IgnoredAsFreeType")]
    [TestCase("records", TestName = "Load_MvarRecordsPastTable_IgnoredAsFreeType")]
    [TestCase("store", TestName = "Load_MvarRecordsWithoutStore_IgnoredAsFreeType")]
    [TestCase("item", TestName = "Load_MvarRecordItemOutOfRange_IgnoredAsFreeType")]
    public void Load_MvarBroken_IgnoredAsFreeType(string rule)
    {
        var mvar = Mvar(Store(1, s_region, ([0], [[90], [-40]], false)), ("hasc", 0, 0), ("hdsc", 0, 1));
        mvar = rule switch
        {
            "header" => [0, 1, 0, 0],
            "records" => Patch(mvar, 8, 2000),
            "store" => Patch(mvar, 10, 0),
            _ => Patch(mvar, 12 + 6, 7),
        };
        var font = TableLast(VariableArial(s_oneAxis, new CraftedTable("MVAR", mvar)), "MVAR");
        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.Data.Variations!.Mvar, Is.Null, "the table is absent");
            AssertCraftedMatches(font, [face.GetGlyphIndex('H')], s_oneAxisSettings, Mode.Unhinted, requireChange: false);
            Assert.That(face.CreateScaledFont(new ScaledFontOptions(64) { Variation = [new("wght", 900)] }).LineMetrics,
                Is.EqualTo(face.CreateScaledFont(new ScaledFontOptions(64)).LineMetrics), "the line metrics stay the default's");
        });
    }

    /// <summary>A value record size below 8 is read with FreeType's stride of 8, as FreeType reads it.</summary>
    [Test]
    public void Load_MvarRecordSizeBelow8_ReadWithFreeTypesStride()
    {
        var store = Store(1, s_region, ([0], [[90], [-40], [25]], false));
        var mvar = Patch(Mvar(store, ("hasc", 0, 0), ("hdsc", 0, 1), ("hlgp", 0, 2)), 6, 6);
        var font = VariableArial(s_oneAxis, new CraftedTable("MVAR", mvar));

        AssertCraftedMatches(font, [FontLoader.Load(font).Faces[0].GetGlyphIndex('H')], s_oneAxisSettings, Mode.Unhinted);
    }

    /// <summary>
    /// Ours works, documented (mentor, 2026-11-27): 12-byte value records are stepped by their declared size, so they vary the
    /// line metrics as the same table of 8-byte records does; FreeType steps by 8, misreads them and keeps its default metrics.
    /// </summary>
    [Test]
    public void Load_MvarRecordSize12_SteppedByDeclaredSize()
    {
        var store = Store(1, s_region, ([0], [[300], [-200], [100]], false));
        (string, int, int)[] records = [("hasc", 0, 0), ("hdsc", 0, 1), ("hlgp", 0, 2)];
        var wide = VariableArial(s_oneAxis, new CraftedTable("MVAR", MvarSized(store, 12, records)));
        var narrow = VariableArial(s_oneAxis, new CraftedTable("MVAR", MvarSized(store, 8, records)));
        var options = new ScaledFontOptions(100) { Variation = [new("wght", 900)] };
        using var freeType = new FreeTypeVariations();
        using var wideAtDefault = freeType.Open(wide, 0, Design([400]), 100);
        using var wideAtMaximum = freeType.Open(wide, 0, Design([900]), 100);
        using var narrowAtMaximum = freeType.Open(narrow, 0, Design([900]), 100);
        var ours = FontLoader.Load(wide).Faces[0].CreateScaledFont(options).LineMetrics;

        Assert.Multiple(() =>
        {
            Assert.That(ours, Is.EqualTo(FontLoader.Load(narrow).Faces[0].CreateScaledFont(options).LineMetrics));
            Assert.That(ours, Is.EqualTo(narrowAtMaximum.SizeMetrics), "as FreeType varies the 8-byte table");
            Assert.That(wideAtMaximum.SizeMetrics, Is.EqualTo(wideAtDefault.SizeMetrics), "FreeType does not vary the 12-byte one");
            Assert.That(ours, Is.Not.EqualTo(wideAtDefault.SizeMetrics), "the documented difference");
        });
    }

    /// <summary>
    /// Repeated and unsorted MVAR tags are read as FreeType reads them (Denys, 2026-11-27: "Follow FreeType"): of several
    /// records of one tag, the last whose delta is not 0 wins.
    /// </summary>
    [TestCase(Mode.Unhinted)]
    [TestCase(Mode.V40)]
    public void Load_MvarRepeatedAndUnsortedTags_ReadAsFreeType(Mode mode)
    {
        var store = Store(1, s_region, ([0], [[80], [-40], [30], [0], [150], [-70]], true));
        var mvar = Mvar(store, ("hlgp", 0, 2), ("hasc", 0, 0), ("hdsc", 0, 1), ("hasc", 0, 4), ("hasc", 0, 3), ("hdsc", 0, 5), ("hdsc", 0, 3));
        var font = VariableArial(s_oneAxis, new CraftedTable("MVAR", mvar));
        var face = FontLoader.Load(font).Faces[0];

        AssertCraftedMatches(font, [face.GetGlyphIndex('H')], s_oneAxisSettings, mode);
    }

    // ---- fvar ----

    [Test]
    public void Load_FvarTagNotPrintable_Throws()
    {
        var fvar = Fvar([("wght", 100, 400, 900, 0, 256), ("wd\u0001h", 50, 100, 150, 0, 257)], []);

        AssertLoadThrows<InvalidDataException>(CraftedFonts.WithTable(CraftedFonts.Arial(), "fvar", fvar), "axis 1 has the tag 0x77640168");
    }

    [Test]
    public void Load_FvarTagRepeated_Throws()
    {
        var fvar = Fvar([("wght", 100, 400, 900, 0, 256), ("wght", 50, 100, 150, 0, 257)], []);

        AssertLoadThrows<InvalidDataException>(CraftedFonts.WithTable(CraftedFonts.Arial(), "fvar", fvar), "axis 1 repeats the tag 'wght'");
    }

    /// <summary>
    /// A collection of 300 faces sharing one fvar of 32,511 instances (the reviewer's case: 1,121 MB allocated before): the
    /// axes and instances are built once and shared, so the load allocates a bounded amount (measured 5.5 MB on 2026-11-27, of
    /// which the fvar's read-only lists take about 3.6 MB; the bound, 32 MB, leaves room and is far below the 1,121 MB of before).
    /// </summary>
    [Test]
    public void Load_CollectionSharingFvar_SharesAxesAndNamedInstances()
    {
        var arial = CraftedFonts.Arial();
        var instances = Enumerable.Range(0, 32511).Select(i => (256, new double[] { 100 + i % 800, 50 + i % 100 })).ToArray();
        var fvar = new CraftedTable("fvar", Fvar([("wght", 100, 400, 900, 0, 256), ("wdth", 50, 100, 150, 0, 256)], instances));
        var shared = CraftedFonts.ReadTables(arial).Append(fvar).ToList();
        var font = CraftedPrograms.SharedCollection(Enumerable.Repeat((IReadOnlyList<CraftedTable>)shared, 300).ToList());

        var before = GC.GetAllocatedBytesForCurrentThread();
        var file = FontLoader.Load(font);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"Loading {file.Faces.Count} faces allocated {allocated / 1048576.0:F1} MB.");

        Assert.Multiple(() =>
        {
            Assert.That(file.Faces, Has.Count.EqualTo(300));
            Assert.That(file.Faces[299].NamedInstances, Has.Count.EqualTo(32511));
            Assert.That(file.Faces.All(face => ReferenceEquals(face.NamedInstances, file.Faces[0].NamedInstances)), Is.True);
            Assert.That(file.Faces.All(face => ReferenceEquals(face.Axes, file.Faces[0].Axes)), Is.True);
            Assert.That(allocated, Is.LessThan(32L * 1024 * 1024));
        });
    }

    /// <summary>
    /// A collection whose two faces share one fvar but each have their own name table (the usual shape of a variable collection
    /// with per-face names) loads, both faces variable as in FreeType, each naming its axis and instances from its own name
    /// table; FreeType's FT_MM_Var is the same for both faces.
    /// </summary>
    [Test]
    public void Load_CollectionSharingFvarWithOwnNames_NamesEachFaceFromItsOwnTable()
    {
        var fvar = new CraftedTable("fvar", Fvar([("wght", 100, 400, 900, 0, 256)], [(257, [100.0]), (258, [900.0])]));
        var others = CraftedFonts.ReadTables(CraftedFonts.Arial()).Where(table => table.Tag != "name").ToList();
        var font = CraftedPrograms.SharedCollection(
        [
            [.. others, fvar, new CraftedTable("name", NameTable(("Weight", 256), ("Thin", 257), ("Black", 258)))],
            [.. others, fvar, new CraftedTable("name", NameTable(("Gewicht", 256), ("Dünn", 257), ("Schwarz", 258)))],
        ]);
        var file = FontLoader.Load(font);
        using var freeType = new FreeTypeVariations();
        using var first = freeType.Open(font, 0, null, 0);
        using var second = freeType.Open(font, 1, null, 0);
        var (axes0, instances0) = freeType.ReadMMVar(font, 0);
        var (axes1, instances1) = freeType.ReadMMVar(font, 1);

        Assert.Multiple(() =>
        {
            Assert.That(file.Faces.Select(face => face.IsVariable), Is.EqualTo(new[] { first.HasMultipleMasters, second.HasMultipleMasters }));
            Assert.That(file.Faces.All(face => face.IsVariable), Is.True);
            Assert.That(file.Faces[0].Axes.Select(axis => axis.Name), Is.EqualTo(new[] { "Weight" }));
            Assert.That(file.Faces[1].Axes.Select(axis => axis.Name), Is.EqualTo(new[] { "Gewicht" }));
            Assert.That(file.Faces[0].NamedInstances.Select(instance => instance.Name), Is.EqualTo(new[] { "Thin", "Black" }));
            Assert.That(file.Faces[1].NamedInstances.Select(instance => instance.Name), Is.EqualTo(new[] { "D?nn", "Schwarz" }),
                "converted to ASCII as FamilyName is");
            Assert.That(axes1.Select(axis => axis.NameId), Is.EqualTo(axes0.Select(axis => axis.NameId)));
            Assert.That(instances1.Select(instance => instance.NameId), Is.EqualTo(instances0.Select(instance => instance.NameId)));
            Assert.That(file.Faces.Select(face => face.NamedInstances.Select(instance => Design([.. instance.Coordinates])).ToList()),
                Is.All.EqualTo(instances0.Take(2).Select(instance => instance.Coordinates).ToList()));
        });

        // A Windows Unicode (3, 1, 0x409) name table of these strings and IDs, sorted by ID.
        static byte[] NameTable(params (string Text, int Id)[] names)
        {
            var strings = names.Select(name => System.Text.Encoding.BigEndianUnicode.GetBytes(name.Text)).ToArray();
            var w = new BigEndianWriter().U16(0).U16(names.Length).U16(6 + 12 * names.Length);
            var offset = 0;
            for (var i = 0; i < names.Length; i++)
            {
                w.U16(3).U16(1).U16(0x409).U16(names[i].Id).U16(strings[i].Length).U16(offset);
                offset += strings[i].Length;
            }

            foreach (var bytes in strings)
                w.Bytes(bytes);

            return w.ToArray();
        }
    }

    /// <summary>The shared lists are read-only: no face can change what another reports.</summary>
    [Test]
    public void NamedInstances_SharedLists_AreReadOnly()
    {
        var face = FontCache.Face(VariableFaces().First());

        Assert.Multiple(() =>
        {
            Assert.That(face.Axes, Is.Not.InstanceOf<VariationAxis[]>());
            Assert.That(face.NamedInstances, Is.Not.InstanceOf<NamedInstance[]>());
            Assert.That(face.NamedInstances[0].Coordinates, Is.Not.InstanceOf<float[]>());
            Assert.That(() => ((IList<float>)face.NamedInstances[0].Coordinates)[0] = 1, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<NamedInstance>)face.NamedInstances)[0] = face.NamedInstances[1], Throws.TypeOf<NotSupportedException>());
        });
    }

    /// <summary>
    /// fvar tables whose lists pass the file's parsed-memory budget make the load NotSupported: a collection of 12 faces, each
    /// with its own fvar of one axis and 32,511 instances (about 260 KB each, about 2.7 MB kept each, past 8 bytes per byte of
    /// the file plus 1 MiB once several are parsed); the other tables shared.
    /// </summary>
    [Test]
    public void Load_FvarsOverParsedMemoryBudget_ThrowNotSupported()
    {
        var shared = CraftedFonts.ReadTables(CraftedFonts.Arial());
        var faces = Enumerable.Range(0, 12).Select(face =>
        {
            var instances = Enumerable.Range(0, 32511).Select(i => (256, new double[] { 100 + (i + face) % 800 })).ToArray();
            return (IReadOnlyList<CraftedTable>)shared.Append(new CraftedTable("fvar", Fvar([("wght", 100, 400, 900, 0, 256)], instances))).ToList();
        }).ToList();
        var font = CraftedPrograms.SharedCollection(faces);
        TestContext.Out.WriteLine($"The file is {font.Length} bytes.");

        AssertLoadThrows<NotSupportedException>(font, "the 'fvar' records");
    }

    /// <summary>The measurement behind FvarTable's accounting: what one fvar of 32,511 instances of two axes allocates.</summary>
    [Test]
    [Explicit("A measurement.")]
    public void Measure_FvarParseAllocation()
    {
        var arial = CraftedFonts.Arial();
        var instances = Enumerable.Range(0, 32511).Select(i => (256, new double[] { 100 + i % 800, 50 + i % 100 })).ToArray();
        var font = CraftedFonts.WithTable(arial, "fvar", Fvar([("wght", 100, 400, 900, 0, 256), ("wdth", 50, 100, 150, 0, 256)], instances));
        FontLoader.Load(arial);
        FontLoader.Load(font);

        var before = GC.GetAllocatedBytesForCurrentThread();
        FontLoader.Load(arial);
        var plain = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        FontLoader.Load(font);
        var withFvar = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"fvar parse: {withFvar - plain} bytes, {(withFvar - plain) / 32511.0:F1} per instance.");
        Assert.Pass();
    }

    /// <summary>
    /// Accepted and read as FreeType reads it: an HVAR or MVAR of another major version (ignored: the advances come from the
    /// phantom points, the line metrics stay), an empty advance map (as no map), an MVAR without records.
    /// </summary>
    [TestCase("HVAR version", Mode.Unhinted)]
    [TestCase("HVAR version", Mode.V35)]
    [TestCase("MVAR version", Mode.Unhinted)]
    [TestCase("empty map", Mode.Unhinted)]
    [TestCase("empty map", Mode.V40)]
    [TestCase("no records", Mode.Unhinted)]
    public void Load_MetricsTablesAcceptedQuirks_ReadAsFreeType(string quirk, Mode mode)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var n = arial.Data.Glyf!.VariationPointCount(h);
        var store = Store(1, s_region, ([0], Enumerable.Range(0, arial.GlyphCount).Select(g => new[] { g % 40 - 20 }).ToArray(), false));
        var hvar = quirk switch
        {
            "HVAR version" => Patch(Hvar(store), 0, 2),
            "empty map" => Hvar(store, IndexMap()),
            _ => Hvar(store),
        };
        var mvarStore = Store(1, s_region, ([0], [[90], [-30]], false));
        var mvar = quirk switch
        {
            "MVAR version" => Patch(Mvar(mvarStore, ("hasc", 0, 0), ("hdsc", 0, 1)), 0, 2),
            "no records" => Mvar(mvarStore),
            _ => Mvar(mvarStore, ("hasc", 0, 0), ("hdsc", 0, 1)),
        };
        var gvar = Gvar(1, [], arial.GlyphCount, new Dictionary<int, byte[]>
        {
            [h] = TupleStore(1, [new CraftedTuple([1], 0, null, null, [1, n + 1], [30, 70], [10, 0])], null),
        });
        var font = VariableArial(s_oneAxis, new CraftedTable("HVAR", hvar), new CraftedTable("MVAR", mvar), new CraftedTable("gvar", gvar));

        AssertCraftedMatches(font, [h, arial.GetGlyphIndex('o'), arial.GetGlyphIndex(' ')], s_oneAxisSettings, mode);
    }

    // ---- CFF2 and the same-inputs rule ----

    [Test]
    public void Load_Cff2StoreAxisCountOtherThanFvar_Throws()
    {
        var otf = FontCache.Bytes(TestFonts.SourceSans("SourceSans3VF-Upright.otf"));
        var fvar = Fvar([("wght", 200, 400, 900, 0, 256), ("wdth", 50, 100, 150, 0, 257)], []);

        AssertLoadThrows<InvalidDataException>(CraftedFonts.WithTable(CraftedFonts.WithoutTable(otf, "avar", "HVAR", "MVAR", "STAT"), "fvar", fvar),
            "CFF2 variation store has 1 axes, but 'fvar' has 2");
    }

    /// <summary>Two faces of a collection that share a <c>gvar</c> but give it different axis counts break the same-inputs rule.</summary>
    [Test]
    public void Load_CollectionSharingGvarWithOtherAxisCounts_Throws()
    {
        var arial = CraftedFonts.Arial();
        var gvar = Gvar(1, [], ArialGlyphCount(), new Dictionary<int, byte[]>());
        var shared = CraftedFonts.ReadTables(arial).Append(new CraftedTable("gvar", gvar)).ToList();
        var first = shared.Append(new CraftedTable("fvar", Fvar([("wght", 100, 400, 900, 0, 256)], []))).ToList();
        var second = shared.Append(new CraftedTable("fvar", Fvar([("wght", 100, 400, 900, 0, 256), ("wdth", 50, 100, 150, 0, 257)], []))).ToList();

        AssertLoadThrows<InvalidDataException>(CraftedPrograms.SharedCollection([first, second]), "share the 'gvar' table");
    }

    // ---- budgets ----

    /// <summary>
    /// A glyph of 65,000 points whose 40 tuples each move every point: an unhinted load passes
    /// <see cref="GlyfGlyphLoader.VariationWorkBudget"/> at a deterministic count and throws <see cref="NotSupportedException"/>;
    /// a hinted load passes the hinter's glyph budget, which it shares.
    /// </summary>
    [Test]
    public void Load_GvarOverGlyphBudget_ThrowsNotSupported()
    {
        const int points = 65_000;
        var (font, glyph) = HeavyGvarFont(points, tuples: 40, zeroDeltas: true);
        var face = FontLoader.Load(font).Faces[0];
        var options = new ScaledFontOptions(16) { Hinting = HintingMode.None, Variation = [new("wght", 900)] };
        var unhinted = face.CreateScaledFont(options);
        var hinted = face.CreateScaledFont(options with { Hinting = HintingMode.Native });

        // The points with the four phantom points: 2 each to start, plus 1 per tuple for its scalar (one axis); then each tuple
        // that applies charges 4 per point (its x and y deltas read, each point's delta added): the fourth passes the budget.
        const long count = points + 4;
        const long charged = 2 * count + 40 + 4 * 4 * count;

        Assert.Multiple(() =>
        {
            Assert.That(() => unhinted.GetOutline(glyph), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains($"({GlyfGlyphLoader.VariationWorkBudget} units; charged {charged})"));
            Assert.That(() => hinted.GetOutline(glyph), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains("one glyph load"));
            Assert.That(() => unhinted.GetOutline(0), Throws.Nothing, "other glyphs load");
        });
    }

    /// <summary>A heavy glyph within the budget (30,000 points, FreeType's outlines stop at 32,767) loads as FreeType loads it.</summary>
    [Test]
    public void Load_GvarUnderGlyphBudget_MatchesFreeType()
    {
        var (font, glyph) = HeavyGvarFont(30_000, tuples: 2, zeroDeltas: false);
        var face = FontLoader.Load(font).Faces[0];
        var result = Compare(face, font, 0, [900], [glyph], Mode.Unhinted);

        Assert.Multiple(() =>
        {
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.EqualTo(s_sizes.Length));
        });
    }

    /// <summary>An MVAR of many records over many regions passes <see cref="VariationInstance.SetUpWorkBudget"/>.</summary>
    [Test]
    public void CreateScaledFont_MvarOverSetUpBudget_ThrowsNotSupported()
    {
        var regions = Enumerable.Repeat(s_region[0], 600).ToArray();
        var store = Store(1, regions, (Enumerable.Range(0, 600).ToArray(), [Enumerable.Repeat(1, 600).ToArray()], false));
        var records = Enumerable.Repeat(("hasc", 0, 0), 1000).ToArray();
        var face = FontLoader.Load(VariableArial(s_oneAxis, new CraftedTable("MVAR", Mvar(store, records)))).Faces[0];

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new("wght", 900)] }),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Setting up the variation coordinates"));
    }

    /// <summary>
    /// Ours works (Denys's "Ours works, documented"): a composite of 32,768 components with <c>gvar</c> data. FreeType 2.13.2
    /// keeps the component count in 16 bits there (<c>limit = (short)num_subglyphs</c>) and fails the load; FreeType 2.14.3's
    /// interpolation overflowed the heap on the same shape (Project Zero, June 2026), so this crafted font is never given to the
    /// native reference. Here the last component's offset takes its delta, as the spec says.
    /// </summary>
    [Test]
    public void Load_CompositeOf32768ComponentsWithGvar_VariesTheLastOffset()
    {
        const int components = 32_768;
        var arial = CraftedFonts.Arial();
        var face = FontLoader.Load(arial).Faces[0];
        var space = face.GetGlyphIndex(' ');
        var period = face.GetGlyphIndex('.');

        // Every component but the last is the empty space glyph; the last is the period, offset by (0, 0).
        var composite = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0);
        for (var i = 0; i < components; i++)
        {
            var last = i == components - 1;
            composite.U16(0x0002 | (last ? 0 : 0x0020)).U16(last ? period : space).U8(0).U8(0);
        }

        var (font, glyph) = CraftedFonts.AppendGlyphs(arial, 1, composite.ToArray());
        var data = TupleStore(1, [new CraftedTuple([1], 0, null, null, [components - 1], [100], [-50])], null);
        font = CraftedFonts.WithTable(font, "fvar", Fvar([("wght", 100, 400, 900, 0, 256)], []));
        font = CraftedFonts.WithTable(font, "gvar", Gvar(1, [], face.GlyphCount + 1, new Dictionary<int, byte[]> { [glyph] = data }));
        var varied = FontLoader.Load(font).Faces[0];

        var options = new ScaledFontOptions(64) { Hinting = HintingMode.None };
        var atDefault = varied.CreateScaledFont(options with { Variation = [new("wght", 400)] }).GetOutline(glyph);
        var atMaximum = varied.CreateScaledFont(options with { Variation = [new("wght", 900)] }).GetOutline(glyph);
        var period64 = varied.CreateScaledFont(options).GetOutline(period);
        var shift = (int)FixedMath.MulFix(100, FontScale.FromPixelSize(64, varied.UnitsPerEm).XScale);
        var drop = (int)FixedMath.MulFix(50, FontScale.FromPixelSize(64, varied.UnitsPerEm).YScale);

        Assert.Multiple(() =>
        {
            Assert.That(atDefault.Points.ToArray(), Is.EqualTo(period64.Points.ToArray()), "at the default, the period unmoved");
            Assert.That(atMaximum.Points.ToArray(), Is.EqualTo(period64.Points.ToArray().Select(p => p with { X = p.X + shift, Y = p.Y - drop })),
                "at the maximum, the period moved by the last component's delta (100, -50) scaled");
        });
    }

    // ---- helpers ----

    private static void AssertLoadThrows<T>(byte[] font, string message) where T : Exception =>
        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<T>().With.Message.Contains(message));

    /// <summary>The normalized coordinates equal FreeType's blend coordinates at each setting.</summary>
    private static void AssertNormalizesAsFreeType(byte[] font, float[][] settings)
    {
        var face = FontLoader.Load(font).Faces[0];
        using var freeType = new FreeTypeVariations();
        foreach (var values in settings)
        {
            using var reference = freeType.Open(font, 0, Design(values), 0);
            var ours = VariationInstance.Create(face, face.Data.Variations!, Coordinates(face, values)).Normalized;
            Assert.That(ours, Is.EqualTo(reference.BlendCoordinates(face.Axes.Count)), $"[{string.Join(", ", values)}]");
        }
    }

    private static int ArialGlyphCount() => FontLoader.Load(CraftedFonts.Arial()).Faces[0].GlyphCount;

    private static int[] Zeros(int count) => new int[count];

    /// <summary>
    /// A tuple variation store of one embedded tuple (peak 1 on the first axis, private points flagged) whose serialized data is
    /// <paramref name="tuple"/>; <paramref name="prefix"/> is what precedes the store in its table (4 for <c>cvar</c>'s version).
    /// </summary>
    private static byte[] TupleData(byte[] tuple, int axisCount, int prefix)
    {
        var w = new BigEndianWriter().U16(1).U16(prefix + 4 + 4 + 2 * axisCount).U16(tuple.Length).U16(0xA000).U16(F2Dot14(1));
        for (var i = 1; i < axisCount; i++)
            w.U16(0);

        return w.Bytes(tuple).ToArray();
    }

    /// <summary>A two-axis Arial whose gvar has one shared tuple and gives 'H' (of n points) the data <paramref name="hData"/>.</summary>
    private static byte[] GvarWithH(Func<int, byte[]> hData)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var gvar = Gvar(2, [[1, 0]], arial.GlyphCount, new Dictionary<int, byte[]> { [h] = hData(arial.Data.Glyf!.VariationPointCount(h)) });
        return VariableArial(s_twoAxes, new CraftedTable("gvar", gvar));
    }

    /// <summary>A two-axis Arial whose gvar (one shared tuple, valid data for 'H') is changed by <paramref name="patch"/>.</summary>
    private static byte[] PatchGvar(Action<byte[]> patch)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var n = arial.Data.Glyf!.VariationPointCount(h);
        var gvar = Gvar(2, [[1, 0]], arial.GlyphCount, new Dictionary<int, byte[]>
        {
            [h] = TupleStore(2, [new CraftedTuple([1, 0], 0, null, null, [], Zeros(n + 4), Zeros(n + 4))], null),
        });
        patch(gvar);
        return VariableArial(s_twoAxes, new CraftedTable("gvar", gvar));
    }

    /// <summary>
    /// One-axis Arial with <paramref name="hvar"/> last in the file and gvar deltas on 'H' including its advance phantom point:
    /// the broken HVAR is absent here, and the glyphs (their advances from the varied phantom points) equal FreeType's.
    /// </summary>
    private static void AssertHvarIgnoredAsFreeType(byte[] hvar)
    {
        var arial = FontLoader.Load(CraftedFonts.Arial()).Faces[0];
        var h = arial.GetGlyphIndex('H');
        var n = arial.Data.Glyf!.VariationPointCount(h);
        var gvar = Gvar(1, [], arial.GlyphCount, new Dictionary<int, byte[]>
        {
            [h] = TupleStore(1, [new CraftedTuple([1], 0, null, null, [1, n + 1], [30, 70], [10, 0])], null),
        });
        var font = TableLast(VariableArial(s_oneAxis, new CraftedTable("HVAR", hvar), new CraftedTable("gvar", gvar)), "HVAR");

        Assert.Multiple(() =>
        {
            Assert.That(FontLoader.Load(font).Faces[0].Data.Variations!.Hvar, Is.Null, "the table is absent");
            AssertCraftedMatches(font, [h, arial.GetGlyphIndex('o'), arial.GetGlyphIndex(' ')], s_oneAxisSettings, Mode.Unhinted);
        });
    }

    /// <summary>A copy of <paramref name="data"/> with big-endian values written at <paramref name="offset"/> (16 bits, or 32 with <paramref name="size"/> 4).</summary>
    private static byte[] Patch(byte[] data, int offset, int value, int size = 2)
    {
        var copy = (byte[])data.Clone();
        if (size == 4)
            CraftedFonts.WriteU32(copy, offset, (uint)value);
        else if (size == 1)
            copy[offset] = (byte)value;
        else
            CraftedFonts.WriteU16(copy, offset, value);

        return copy;
    }

    /// <summary>
    /// Arial with one axis (wght), a glyph of <paramref name="points"/> points on one contour appended, and gvar tuples (peak
    /// wght 1, all points) of zero deltas or of small ones.
    /// </summary>
    private static (byte[] Font, int Glyph) HeavyGvarFont(int points, int tuples, bool zeroDeltas)
    {
        var arial = CraftedFonts.Arial();
        var count = FontLoader.Load(arial).Faces[0].GlyphCount;

        // One contour of on-curve points, all at (0, 0) (x and y the same as before: no coordinate bytes), repeated flags.
        var glyph = new BigEndianWriter().U16(1).U16(0).U16(0).U16(0).U16(0).U16(points - 1).U16(0);
        const int flags = 0x01 | 0x10 | 0x20 | 0x08;
        for (var left = points; left > 0; left -= 256)
            glyph.U8(flags).U8(Math.Min(255, left - 1));

        var (font, index) = CraftedFonts.AppendGlyphs(arial, 1, glyph.ToArray());
        var deltas = Enumerable.Range(0, points + 4).Select(i => zeroDeltas ? 0 : i % 7 - 3).ToArray();
        var store = TupleStore(1, [.. Enumerable.Range(0, tuples).Select(_ => new CraftedTuple([1], 0, null, null, [], deltas, deltas))], null);
        var gvar = Gvar(1, [], count + 1, new Dictionary<int, byte[]> { [index] = store });
        font = CraftedFonts.WithTable(font, "fvar", Fvar([("wght", 100, 400, 900, 0, 256)], []));
        return (CraftedFonts.WithTable(font, "gvar", gvar), index);
    }
}
