using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Hinting.Cff;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// CFF hinting (OT-4 WP6) on crafted fonts against FreeType 2.13.2: Private DICTs exercising every blue and stem entry, hint
/// and counter masks, stems at blue-zone edges, overlapping, ghost and inverted stems, a seac with hints, the CID-keyed copy,
/// sizes above FreeType's hinting limit (the unhinted retry), and the glyph work budget.
/// </summary>
public sealed partial class CffOutlineSourceTests
{
    // Private DICT operators.
    private const int OpBlueValues = 6, OpOtherBlues = 7, OpFamilyBlues = 8, OpFamilyOtherBlues = 9, OpStdHw = 10, OpStdVw = 11;
    private const int OpBlueScale = 0x109, OpBlueShift = 0x10A, OpBlueFuzz = 0x10B, OpStemSnapH = 0x10C, OpStemSnapV = 0x10D;
    private const int OpForceBold = 0x10E, OpLanguageGroup = 0x111, OpExpansionFactor = 0x112;

    private static readonly int[] s_hintSizes = [8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 32, 48, 72];

    // ==== The WP6 stub test, replaced: Native on a CFF face now hints as FreeType does.

    [Test]
    public void CreateScaledFont_NativeHintingOnCff_HintsAsFreeType()
    {
        var testFace = TestFonts.CffFaces().First();
        var face = FontCache.Face(testFace);
        var glyph = face.GetGlyphIndex('H');

        var hinted = face.CreateScaledFont(new ScaledFontOptions(16));
        var unhinted = face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });
        var result = CffHintingTests.Compare(face, FontCache.Bytes(testFace.Path), 0, null, 16, [glyph]);

        Assert.Multiple(() =>
        {
            Assert.That(ScaledFontTests.Compare(hinted.GetOutline(glyph), Outline(unhinted.GetOutline(glyph))), Is.Not.Null,
                "the hinted outline differs from the unhinted one");
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.EqualTo(1));
        });
    }

    // ==== The CID-keyed copy (WP2's crafted CID-keyed Source Sans): per Font DICT Private DICTs, matrices, units per em.

    [TestCaseSource(nameof(CidVariants))]
    public void Load_NativeCidKeyedCopy_EveryGlyphMatchesFreeType(int variant)
    {
        var font = CidKeyedSourceSans(variant);
        var face = FontLoader.Load(font).Faces[0];

        AssertHintedMatchFreeType(font, Enumerable.Range(0, face.GlyphCount).ToArray(), 9, 16, 32, 72);
    }

    // ==== Private DICTs exercising every blue and stem entry (crafted, well-formed).

    private static IEnumerable<TestCaseData> PrivateDictCases()
    {
        DictOp[] every =
        [
            Delta(OpBlueValues, -12, 0, 486, 498, 638, 650, 712, 724),
            Delta(OpOtherBlues, -222, -210, -180, -170),
            Delta(OpFamilyBlues, -11, 0, 487, 499, 637, 649, 713, 725),
            Delta(OpFamilyOtherBlues, -221, -209),
            Real(OpBlueScale, "0.0625"),
            Numbers(OpBlueShift, 5),
            Numbers(OpBlueFuzz, 0),
            Numbers(OpStdHw, 70),
            Numbers(OpStdVw, 84),
            Delta(OpStemSnapH, 70, 80),
            Delta(OpStemSnapV, 84, 90),
            Numbers(OpForceBold, 1),
            Numbers(OpLanguageGroup, 0),
            Real(OpExpansionFactor, "0.06"),
        ];
        yield return Private("every blue and stem entry", every);
        yield return Private("BlueScale large (overshoot suppressed at every size)",
            [Delta(OpBlueValues, -12, 0, 486, 498, 638, 650), Real(OpBlueScale, "0.5")]);
        yield return Private("BlueScale integer 1 and small zones",
            [Delta(OpBlueValues, -2, 0, 496, 498), Numbers(OpBlueScale, 1)]);
        yield return Private("BlueShift 0, BlueFuzz 3", [Delta(OpBlueValues, -12, 0, 486, 498), Numbers(OpBlueShift, 0), Numbers(OpBlueFuzz, 3)]);
        yield return Private("BlueShift 40 (no forced overshoot), BlueFuzz 1",
            [Delta(OpBlueValues, -12, 0, 486, 498, 638, 650), Numbers(OpBlueShift, 40), Numbers(OpBlueFuzz, 1)]);
        yield return Private("unlikely BlueShift and BlueFuzz (FreeType's defaults instead)",
            [Delta(OpBlueValues, -12, 0, 486, 498), Numbers(OpBlueShift, 5000), Numbers(OpBlueFuzz, -4)]);
        yield return Private("LanguageGroup 1 without BlueValues (synthetic em-box hints)", [Numbers(OpLanguageGroup, 1)]);
        yield return Private("LanguageGroup 1 with ICF dummy zones", [Delta(OpBlueValues, -250, -240, 1100, 1110), Numbers(OpLanguageGroup, 1)]);
        yield return Private("LanguageGroup 1 with real zones (no em-box hints)",
            [Delta(OpBlueValues, -12, 0, 486, 498), Numbers(OpLanguageGroup, 1)]);
        yield return Private("odd counts (an odd BlueValues dropped; odd OtherBlues and FamilyOtherBlues)",
            [Delta(OpBlueValues, -12, 0, 486, 498, 700), Delta(OpOtherBlues, -222, -210, -150), Delta(OpFamilyOtherBlues, -221, -209, -100)]);
        yield return Private("a zone of negative height (rejected)", [Delta(OpBlueValues, 0, -12, 486, 498, 650, 638, 712, 724)]);
        yield return Private("repeated BlueValues (the last one is read)",
            [Delta(OpBlueValues, -40, 0, 400, 450, 600, 660), Delta(OpBlueValues, -12, 0, 486, 498)]);
        yield return Private("family edges more than a pixel away (not taken)",
            [Delta(OpBlueValues, -12, 0, 486, 498), Delta(OpFamilyBlues, -30, -20, 520, 530), Delta(OpFamilyOtherBlues, -260, -240)]);
        yield return Private("no hinting entries (FreeType's defaults)", []);
    }

    private static TestCaseData Private(string name, DictOp[] entries) => new TestCaseData((object)entries).SetArgDisplayNames(name);

    /// <param name="entries">The Private DICT's hinting entries (<see cref="DictOp"/>[], passed as an object: the type is internal).</param>
    [TestCaseSource(nameof(PrivateDictCases))]
    public void Load_NativeCraftedPrivateDict_MatchesFreeType(object entries)
    {
        var font = Craft(false, model =>
        {
            // Keep the entries that are not hinting entries (the width defaults).
            var keep = model.Private!.Dict.Where(entry => entry.Op is 20 or 21);
            model.Private.Dict = [.. keep, .. (DictOp[])entries];
        });
        var face = FontLoader.Load(font).Faces[0];
        var glyphs = CffHintingTests.Glyphs(face, everyGlyph: false).Concat(Enumerable.Range(0, 40)).Distinct().Order().ToArray();

        AssertHintedMatchFreeType(font, glyphs, s_hintSizes);
    }

    /// <summary>
    /// The crafted Private DICTs above reach the branches of <c>cf2_blues_init</c> they are named after (so the comparisons with
    /// FreeType cover them): the synthetic em box, the zone counts, the clamped and default BlueScale, overshoot suppression,
    /// and the sanitised BlueShift and BlueFuzz.
    /// </summary>
    [Test]
    public void Blues_CraftedPrivateDicts_TakeTheirBranches()
    {
        static CffBlues BluesOf(DictOp[] entries, int ppem)
        {
            var font = Craft(false, model => model.Private!.Dict = [.. model.Private.Dict.Where(entry => entry.Op is 20 or 21), .. entries]);
            var hints = FontLoader.Load(font).Faces[0].Data.Cff!.Top.Hints;
            var scale = (int)((uint)FontScale.FromPixelSize(ppem, 1000).YScale + 32u) / 64;
            return new CffBlues(hints, scale);
        }

        var emBox = BluesOf([Numbers(OpLanguageGroup, 1)], 16);
        var icf = BluesOf([Delta(OpBlueValues, -250, -240, 1100, 1110), Numbers(OpLanguageGroup, 1)], 16);
        var realZones = BluesOf([Delta(OpBlueValues, -12, 0, 486, 498), Numbers(OpLanguageGroup, 1)], 16);
        var negative = BluesOf([Delta(OpBlueValues, 0, -12, 486, 498, 650, 638, 712, 724)], 16);
        var odd = BluesOf([Delta(OpBlueValues, -12, 0, 486, 498, 700), Delta(OpOtherBlues, -222, -210, -150)], 16);
        var large = BluesOf([Delta(OpBlueValues, -12, 0, 486, 498, 638, 650), Real(OpBlueScale, "0.5")], 72);
        var defaults9 = BluesOf([], 9);
        var defaults72 = BluesOf([Delta(OpBlueValues, -12, 0, 486, 498)], 72);
        var unlikely = BluesOf([Delta(OpBlueValues, -12, 0, 486, 498), Numbers(OpBlueShift, 5000), Numbers(OpBlueFuzz, -4)], 16);

        Assert.Multiple(() =>
        {
            Assert.That(emBox.DoEmBoxHints, Is.True, "LanguageGroup 1 without zones");
            Assert.That(icf.DoEmBoxHints, Is.True, "LanguageGroup 1 with the ICF dummy zones");
            Assert.That((realZones.DoEmBoxHints, realZones.Count), Is.EqualTo((false, 2)), "LanguageGroup 1 with real zones");
            Assert.That(negative.Count, Is.EqualTo(2), "the zones of negative height are dropped");
            Assert.That(odd.Count, Is.EqualTo(4), "BlueValues' odd value dropped; OtherBlues' odd value paired with the slot past it");
            Assert.That((large.SuppressOvershoot, large.BlueScale), Is.EqualTo((true, (int)CffFixed.DivFix(1 << 16, 12 << 16))),
                "a large BlueScale is clamped to the tallest zone (12 units) and suppresses overshoot at 72 px");
            Assert.That((defaults9.SuppressOvershoot, defaults9.BlueScale, defaults9.BlueShift, defaults9.BlueFuzz),
                Is.EqualTo((true, 2597, 7 << 16, 1 << 16)), "FreeType's defaults; overshoot suppressed at 9 px");
            Assert.That(defaults72.SuppressOvershoot, Is.False, "overshoot kept at 72 px");
            Assert.That((unlikely.BlueShift, unlikely.BlueFuzz), Is.EqualTo((7 << 16, 1 << 16)), "unlikely values replaced");
        });
    }

    /// <summary>
    /// A Font DICT without a Private DICT: FreeType's private values all zero (no default BlueShift, BlueFuzz or BlueScale);
    /// the crafted glyph's stems are still placed by the hint map.
    /// </summary>
    [Test]
    public void Load_NativeFontWithoutPrivateDict_MatchesFreeType()
    {
        var font = CraftedFont(false, HintedGlyph(), [], []);

        AssertHintedMatchFreeType(font, [Crafted], s_hintSizes);
    }

    // ==== FreeType's operand-count rule (cff_parser_run's Stack_Underflow over cfftoken.h's field tables).

    /// <summary>The DICTs swept: each of FreeType's field tables, and a CID-keyed CFF table's Font DICT (read with the Top's).</summary>
    public enum DictUnderTest
    {
        CffTop,
        CffCidFontDict,
        CffPrivate,
        Cff2Top,
        Cff2FontDict,
        Cff2Private,
    }

    private static IEnumerable<TestCaseData> OperandCountKinds() =>
        Enum.GetValues<DictUnderTest>().Select(kind => new TestCaseData(kind).SetArgDisplayNames(kind.ToString()));

    /// <summary>
    /// Every one- and two-byte DICT operator (the operand bytes 28 to 30 and 32 to 254 aside, and 31 and 255, which the two
    /// tokenizers read differently), added to the DICT with no operand, and the callbacks' entries with one operand too few
    /// (FontBBox with 1 and 3, FontMatrix with 5, Private with 1, ROS with 2, MultipleMaster with 4) and FontBBox with its 4:
    /// the font loads exactly when FreeType opens the face; otherwise ours throws <see cref="InvalidDataException"/> (or, for
    /// a MultipleMaster entry in the Top DICT, <see cref="NotSupportedException"/>, which the face is anyway). Small crafted
    /// fonts, well-formed but for the entry.
    /// </summary>
    [TestCaseSource(nameof(OperandCountKinds))]
    public void Load_DictEntryWithTooFewOperands_FailsExactlyWhereFreeTypeFails(DictUnderTest kind)
    {
        var cases = new List<(string Name, byte[] Operands, int Op)>();
        foreach (var op in Enumerable.Range(0, 28).Where(op => op != 12 && op != 27).Concat(Enumerable.Range(0x100, 64)))
            cases.Add(($"{Name(op)} none", [], op));

        foreach (var (op, counts) in new (int, int[])[] { (5, [1, 3, 4]), (0x107, [5]), (18, [1]), (0x11E, [2]), (0x118, [4]) })
        {
            foreach (var count in counts)
                cases.Add(($"{Name(op)} {count}", CffModel.Numbers(Enumerable.Range(1, count).ToArray()), op));
        }

        var differences = new List<string>();
        var baseFont = OperandCountFont(kind, null);
        Assert.That(() => FontLoader.Load(baseFont), Throws.Nothing, "the font without the entry loads");
        using (m_freeType.OpenFace(baseFont, 0))
        {
        }

        var failures = 0;
        foreach (var (name, operands, op) in cases)
        {
            var font = OperandCountFont(kind, new DictOp(op, operands));
            bool freeTypeOpens;
            try
            {
                using var _ = m_freeType.OpenFace(font, 0);
                freeTypeOpens = true;
            }
            catch (InvalidOperationException)
            {
                freeTypeOpens = false;
            }

            string ours;
            try
            {
                FontLoader.Load(font);
                ours = "loads";
            }
            catch (InvalidDataException)
            {
                ours = "fails";
            }
            catch (NotSupportedException) when (op == 0x118 && kind == DictUnderTest.CffTop)
            {
                ours = "fails";
            }

            if (!freeTypeOpens)
                failures++;

            if ((ours == "loads") != freeTypeOpens)
                differences.Add($"{name}: FreeType {(freeTypeOpens ? "opens" : "fails")}, ours {ours}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(differences, Is.Empty);
            Assert.That(failures, Is.Positive, "some entries fail in FreeType");
        });

        static string Name(int op) => op >= 0x100 ? $"12 {op & 0xFF}" : $"{op}";
    }

    /// <summary>A small font whose DICT under test holds <paramref name="entry"/> (none: the base font).</summary>
    private static byte[] OperandCountFont(DictUnderTest kind, DictOp? entry)
    {
        const int glyphs = 4;
        var cff2 = kind is DictUnderTest.Cff2Top or DictUnderTest.Cff2FontDict or DictUnderTest.Cff2Private;
        var model = CffModel.Minimal(glyphs, cff2);
        for (var g = 0; g < glyphs; g++)
            model.CharStrings[g] = Charstring.Of([10, 0, "rmoveto", 200, 0, "rlineto", 0, 300, "rlineto", .. cff2 ? Array.Empty<object>() : ["endchar"]]);

        if (kind == DictUnderTest.CffCidFontDict)
        {
            model.Top.Insert(0, Ros(model));
            model.FontDicts = [new CffFontDictModel { Private = new CffPrivate() }];
            model.FdSelectData = [0, .. new byte[glyphs]];
        }
        else if (cff2)
        {
            model.FontDicts![0].Private = new CffPrivate();
        }
        else
        {
            model.Private = new CffPrivate();
        }

        if (entry is not null)
        {
            var dict = kind switch
            {
                DictUnderTest.CffTop or DictUnderTest.Cff2Top => model.Top,
                DictUnderTest.CffCidFontDict or DictUnderTest.Cff2FontDict => model.FontDicts![0].Dict,
                DictUnderTest.CffPrivate => model.Private!.Dict,
                _ => model.FontDicts![0].Private!.Dict,
            };
            dict.Add(entry);
        }

        return SmallOtto(glyphs, null, model.Write(), cff2 ? "CFF2" : "CFF ");
    }

    [Test]
    public void Load_PrivateDeltaEntryWithoutOperands_LoadsAsFreeType()
    {
        var font = Craft(false, model =>
        {
            var keep = model.Private!.Dict.Where(entry => entry.Op is 20 or 21);
            model.Private.Dict = [.. keep, new DictOp(OpBlueValues, []), new DictOp(OpStemSnapH, [])];
        });
        var face = FontLoader.Load(font).Faces[0];

        AssertHintedMatchFreeType(font, CffHintingTests.Glyphs(face, everyGlyph: false), 12, 32);
    }

    // ==== Stem hints, hint masks and counter masks in crafted charstrings (Arial's tables, 2,048 units per em).

    /// <summary>Blue zones for the crafted charstrings: a baseline zone, an x-height and a cap-height zone, a descender zone.</summary>
    private static readonly DictOp[] s_craftedBlues =
    [
        Delta(OpBlueValues, -30, 0, 1062, 1092, 1466, 1500),
        Delta(OpOtherBlues, -434, -410),
        Numbers(OpBlueShift, 7),
        Numbers(OpBlueFuzz, 1),
    ];

    /// <summary>A glyph with three horizontal and two vertical stems, a hint mask switched mid-contour, and a curve contour.</summary>
    private static byte[] HintedGlyph() => Charstring.Of(
        0, 180, 600, 160, 1300, 166, "hstemhm",
        100, 150, 650, 140, "vstemhm",
        "hintmask", new byte[] { 0xF8 },
        100, 0, "rmoveto", 150, 0, "rlineto", 0, 600, "rlineto", 400, 0, "rlineto", 0, -600, "rlineto", 140, 0, "rlineto",
        "hintmask", new byte[] { 0xA8 },
        0, 1466, "rlineto", -140, 0, "rlineto", 0, -706, "rlineto", -400, 0, "rlineto", 0, 706, "rlineto", -150, 0, "rlineto",
        "hintmask", new byte[] { 0x48 },
        900, -500, "rmoveto", 200, 0, 300, 100, 300, 260, "rrcurveto", 0, 160, -100, 100, -200, 0, "rrcurveto",
        -200, 0, -100, -100, 0, -160, "rrcurveto", 0, -160, 100, -100, 200, -100, "rrcurveto",
        "endchar");

    private static IEnumerable<TestCaseData> HintCharstrings()
    {
        yield return Hint("hint masks switched mid-contour", HintedGlyph());

        yield return Hint("counter mask, then hint masks", Charstring.Of(
            0, 180, 600, 160, 1300, 166, "hstemhm", 100, 150, "vstemhm",
            "cntrmask", new byte[] { 0xE0 },
            "hintmask", new byte[] { 0xF0 },
            100, 0, "rmoveto", 150, 0, "rlineto", 0, 1466, "rlineto", -150, 0, "rlineto",
            "hintmask", new byte[] { 0x50 },
            400, -1466, "rmoveto", 0, 760, "rlineto", 300, 0, "rlineto", 0, -760, "rlineto",
            "endchar"));

        yield return Hint("a counter mask only (no hint mask)", Charstring.Of(
            0, 180, 600, 160, 1300, 166, "hstem",
            "cntrmask", new byte[] { 0xA0 },
            100, 0, "rmoveto", 300, 0, "rlineto", 0, 1466, "rlineto", -300, 0, "rlineto", "endchar"));

        yield return Hint("stems at the blue zones' fuzz edges", Charstring.Of(
            -31, 160, 1094, 80, 1501, 60, "hstemhm", -32, 140, 1093, 70, 1502, 50, "hstemhm",
            "hintmask", new byte[] { 0xFC },
            100, -31, "rmoveto", 300, 0, "rlineto", 0, 1592, "rlineto", -300, 0, "rlineto",
            "hintmask", new byte[] { 0x1C },
            600, -1593, "rmoveto", 300, 0, "rlineto", 0, 1594, "rlineto", -300, 0, "rlineto",
            "endchar"));

        yield return Hint("overlapping and touching stems", Charstring.Of(
            400, 200, -100, 200, 100, 100, -50, 50, "hstem", 800, 100, -100, 100, "hstem",
            100, 400, "rmoveto", 300, 0, "rlineto", 0, 500, "rlineto", -300, 0, "rlineto",
            0, 300, "rmoveto", 300, 0, "rlineto", 0, 200, "rlineto", -300, 0, "rlineto",
            "endchar"));

        yield return Hint("ghost, inverted and zero-width stems", Charstring.Of(
            21, -21, 1478, -20, 400, -60, 120, 0, "hstem",
            100, 0, "rmoveto", 300, 0, "rlineto", 0, 1500, "rlineto", -300, 0, "rlineto",
            600, -1500, "rmoveto", 0, 400, "rlineto", 300, 0, "rlineto", 0, -400, "rlineto",
            "endchar"));

        yield return Hint("a hint mask before the first moveto with implied vstems", Charstring.Of(
            0, 180, 1300, 166, "hstemhm", 100, 150, 400, 150,
            "hintmask", new byte[] { 0xF0 },
            100, 0, "rmoveto", 150, 0, "rlineto", 0, 1466, "rlineto", -150, 0, "rlineto", "endchar"));

        yield return Hint("a hint mask that ignores later stems", Charstring.Of(
            0, 180, "hstemhm", "hintmask", new byte[] { 0x80 }, 600, 160, "hstem",
            100, 0, "rmoveto", 300, 0, "rlineto", 0, 760, "rlineto", -300, 0, "rlineto", "endchar"));

        yield return Hint("a closing segment hinted with the first map", Charstring.Of(
            0, 180, 700, 160, "hstemhm",
            "hintmask", new byte[] { 0x80 },
            100, 0, "rmoveto", 300, 0, "rlineto",
            "hintmask", new byte[] { 0x40 },
            0, 860, "rlineto", -300, 30, "rlineto",
            "endchar"));

        yield return Hint("96 stems", Charstring.Of([.. Stems(96), 0, 0, "rmoveto", 300, 0, "rlineto", 0, 900, "rlineto", "endchar"]));

        yield return Hint("many hint maps over a curve chain", Charstring.Of([
            0, 120, 300, 120, 600, 120, 900, 120, 1200, 120, "hstemhm",
            0, 0, "rmoveto",
            .. Enumerable.Range(0, 20).SelectMany(i => new object[]
            {
                "hintmask", new[] { (byte)(0x80 >> (i % 5) | 0x08 >> (i % 3)) },
                60, 70, 80, 90, 100, -20, "rrcurveto",
            }),
            "endchar",
        ]));
    }

    private static TestCaseData Hint(string name, byte[] charstring) => new TestCaseData(charstring).SetArgDisplayNames(name);

    [TestCaseSource(nameof(HintCharstrings))]
    public void Load_NativeCraftedHints_MatchesFreeType(byte[] charstring)
    {
        var font = CraftedFont(false, charstring, [], [], model => model.Private!.Dict.AddRange(s_craftedBlues));

        AssertHintedMatchFreeType(font, [Crafted], [.. s_hintSizes, 100, 150, 300]);
    }

    /// <summary>The same charstrings in a CFF2 table (its Private DICT's blue zones without blends).</summary>
    [TestCaseSource(nameof(HintCharstrings))]
    public void Load_NativeCraftedHintsCff2_MatchesFreeType(byte[] charstring)
    {
        // CFF2 has no endchar; it is ignored there.
        var font = CraftedFont(true, charstring, [], [], model => model.FontDicts![0].Private!.Dict.AddRange(s_craftedBlues));

        AssertHintedMatchFreeType(font, [Crafted], s_hintSizes);
    }

    /// <summary>
    /// A counter mask whose throw-away hint map places two close stems together, then a hint mask holding fewer of them: the
    /// stems keep the positions the counter map gave them (the counter map's build matters, not only its bookkeeping).
    /// </summary>
    private static IEnumerable<TestCaseData> CounterMaskCases()
    {
        object[] path = [100, 250, "rmoveto", 300, 0, "rlineto", 0, 250, "rlineto", -300, 0, "rlineto"];
        yield return Hint("A and B in the counter mask, A alone after",
            Charstring.Of([300, 40, 60, 40, "hstemhm", "cntrmask", new byte[] { 0xC0 }, "hintmask", new byte[] { 0x80 }, .. path, "endchar"]));
        yield return Hint("A and B in the counter mask, B alone after",
            Charstring.Of([300, 40, 60, 40, "hstemhm", "cntrmask", new byte[] { 0xC0 }, "hintmask", new byte[] { 0x40 }, .. path, "endchar"]));
        yield return Hint("A, B and C in the counter mask, A and C after",
            Charstring.Of([300, 40, 60, 40, 30, 40, "hstemhm", "cntrmask", new byte[] { 0xE0 }, "hintmask", new byte[] { 0xA0 }, .. path, "endchar"]));
    }

    [TestCaseSource(nameof(CounterMaskCases))]
    public void Load_NativeCounterMaskPlacesStems_MatchesFreeType(byte[] charstring) =>
        AssertHintedMatchFreeType(CraftedFont(false, charstring, [], []), [Crafted], [.. Enumerable.Range(9, 16)]);

    /// <summary>
    /// FamilyBlues and FamilyOtherBlues edges just inside, exactly at and past one device pixel from the zones' flat edges
    /// (2,048 units per em at 32 px: 64 units a pixel): only a family edge less than a pixel away replaces the zone's.
    /// </summary>
    [TestCase(63)]
    [TestCase(64)]
    [TestCase(65)]
    [TestCase(-64)]
    [TestCase(127)]
    [TestCase(128)]
    public void Load_NativeFamilyEdgeAtOnePixel_MatchesFreeType(int distance)
    {
        var charstring = Charstring.Of(-20, 20, 1030, 32, "hstem", 100, -20, "rmoveto", 300, 0, "rlineto", 0, 1082, "rlineto", -300, 0,
            "rlineto", "endchar");
        var font = CraftedFont(false, charstring, [], [], model => model.Private!.Dict.AddRange([
            Delta(OpBlueValues, -30, 0, 1062, 1092),
            Delta(OpFamilyBlues, -30, distance, 1062 + distance, 1092 + distance),
            Delta(OpFamilyOtherBlues, -60, -30 + distance),
            Delta(OpOtherBlues, -60, -30),
        ]));

        AssertHintedMatchFreeType(font, [Crafted], 8, 16, 32, 64);
    }

    /// <summary>
    /// Tightly packed stems with fractional edges below the baseline at large sizes (OT-4 WP6's reviewer's generator, the
    /// seeds where it matters): a new stem's centre in the initial map is <c>first + (second - first) / 2</c>, truncated toward
    /// zero, which differs from <c>(first + second) / 2</c> for odd negative sums.
    /// </summary>
    [TestCase(8)]
    [TestCase(47)]
    [TestCase(55)]
    public void Load_NativeTightFractionalStemsBelowBaseline_MatchesFreeType(int seed)
    {
        var random = new Random(seed * 13 + 1);
        var items = new List<object>();
        var position = 0.0;
        var y = -1500.0 - random.Next(0, 50);
        var stems = random.Next(6, 24);
        for (var i = 0; i < stems; i++)
        {
            var min = y + random.Next(1, 3) * 0.5 + (2 * random.Next(0, 30000) + 1) / 65536.0;
            var max = min + random.Next(0, 3) + random.Next(0, 65536) / 65536.0;
            items.Add(Math.Round((min - position) * 65536) / 65536.0);
            items.Add(Math.Round((max - min) * 65536) / 65536.0);
            position = Math.Round((min - position) * 65536) / 65536.0 + position;
            position = Math.Round((max - min) * 65536) / 65536.0 + position;
            y = position;
        }

        items.Add("hstem");
        items.AddRange([100, -1500, "rmoveto"]);
        for (var s = 0; s < 30; s++)
            items.AddRange([s % 2 == 0 ? 7 : -5, random.Next(1, 4) + random.Next(0, 65536) / 65536.0, "rlineto"]);

        items.Add("endchar");

        AssertHintedMatchFreeType(CraftedFont(false, Charstring.Of([.. items]), [], []), [Crafted], 1999, 1700, 1400, 1024, 777);
    }

    /// <summary>
    /// A CFF2 face at variation coordinates re-reads its Private DICT once per Font DICT and size, on the first hinted glyph,
    /// which is charged <see cref="CffTable.PrivateReparseCost"/> before the re-read; later glyphs are not. Without coordinates
    /// nothing is re-read or charged.
    /// </summary>
    [Test]
    public void Run_Cff2AtCoordinates_ChargesThePrivateDictReparseOnce()
    {
        var face = FontCache.Face(TestFonts.CffFaces().Single(face => face.Name.StartsWith("SourceSans3VF", StringComparison.Ordinal)));
        var table = face.Data.Cff!;
        var coords = Variations.VariationInstance.Create(face, face.Data.Variations!, [new VariationCoordinate("wght", 700)]).Normalized;
        var scale = FontScale.FromPixelSize(16, face.UnitsPerEm);
        CffHinter.TryGetTransform(scale.XScale, scale.YScale, face.UnitsPerEm, out var scaleX, out var scaleY);
        var glyph = face.GetGlyphIndex('H');

        // The same hinter twice, each time with a fresh interpreter (whose own blend vector is built, and charged, anew).
        long[] Works(long[]? at)
        {
            var hinter = new CffHinter(table, at);
            var works = new long[2];
            for (var i = 0; i < 2; i++)
            {
                var interpreter = new Type2Interpreter(table, new CffOutlineBuilder(), at);
                interpreter.Run(glyph, hinter, scaleX, scaleY);
                works[i] = interpreter.Work;
            }

            return works;
        }

        var atCoordinates = Works(coords);
        var atDefault = Works(null);
        var cost = table.PrivateReparseCost(0);

        Assert.Multiple(() =>
        {
            Assert.That(cost, Is.EqualTo((long)table.FontDicts[0].PrivateSize * (table.VariationStore.RegionCount + 1)).And.Positive);
            Assert.That(atCoordinates[0] - atCoordinates[1], Is.EqualTo(cost), "the first glyph pays for the re-read");
            Assert.That(atDefault[0], Is.EqualTo(atDefault[1]), "nothing to re-read without coordinates");
        });
    }

    [Test]
    public void Load_Native97Stems_ThrowsWhereFreeTypeFails()
    {
        var font = CraftedFont(false, Charstring.Of([.. Stems(97), 0, 0, "rmoveto", 300, 0, "rlineto", "endchar"]), [], [],
            model => model.Private!.Dict.AddRange(s_craftedBlues));

        AssertHintedFailsAsFreeType(font, "97 stem hints, more than FreeType's 96");
    }

    [Test]
    public void Load_NativeCounterMaskOver96Stems_ThrowsWhereFreeTypeFails()
    {
        var font = CraftedFont(false, Charstring.Of([.. Stems(97), "cntrmask", new byte[13], 0, 0, "rmoveto", 300, 0, "rlineto", "endchar"]),
            [], [], model => model.Private!.Dict.AddRange(s_craftedBlues));

        AssertHintedFailsAsFreeType(font, "97 stem hints, more than FreeType's 96");
    }

    /// <summary>A seac whose base and accent carry stem hints: the accent's hints are offset by its origin.</summary>
    [Test]
    public void Load_NativeSeacGlyph_MatchesFreeType()
    {
        var font = Craft(false, model => model.CharStrings[Crafted] = Charstring.Of(120, 230, 65, 194, "endchar"));
        var face = FontLoader.Load(font).Faces[0];

        AssertHintedMatchFreeType(font, [Crafted, face.GetGlyphIndex('A'), face.GetGlyphIndex('´')], s_hintSizes);
    }

    // ==== Sizes above FreeType's hinting limit: Glyph_Too_Big, then the unhinted retry with grid-fitted metrics.

    /// <summary>
    /// Source Sans 3 has 1,000 units per em, so its hinted scales pass FreeType's limit above 2,000 px. (At exactly 2,000 px
    /// every font unit is two whole pixels, so hinting moves nothing; 1,999 px shows the glyphs still hinted below the limit.)
    /// </summary>
    [TestCase(1999)]
    [TestCase(2000)]
    [TestCase(2001)]
    [TestCase(2048)]
    [TestCase(4096)]
    public void Load_NativeAboveFreeTypeHintingLimit_RetriesUnhintedAsFreeType(int size)
    {
        var testFace = TestFonts.CffFaces().First();
        var face = FontCache.Face(testFace);
        int[] glyphs = [face.GetGlyphIndex('H'), face.GetGlyphIndex('o'), face.GetGlyphIndex('g'), face.GetGlyphIndex('Ж')];

        var result = CffHintingTests.Compare(face, FontCache.Bytes(testFace.Path), 0, null, size, glyphs);
        var hinted = face.CreateScaledFont(new ScaledFontOptions(size));
        var unhinted = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        var sameOutlines = glyphs.Count(glyph => ScaledFontTests.Compare(hinted.GetOutline(glyph), Outline(unhinted.GetOutline(glyph))) is null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.EqualTo(glyphs.Length));
            if (size > 2000)
            {
                Assert.That(sameOutlines, Is.EqualTo(glyphs.Length), "past the limit the outline is the unhinted one");
                Assert.That(glyphs.Select(glyph => hinted.GetMetrics(glyph).Advance % 64), Is.All.Zero, "the metrics stay grid-fitted");
            }
            else if (size < 2000)
            {
                Assert.That(sameOutlines, Is.LessThan(glyphs.Length), "below the limit the glyphs are hinted");
            }
        });
    }

    // ==== The glyph work budget bounds hinting too.

    /// <summary>
    /// 96 stems and a new hint mask before each of 400 line segments: each makes the engine rebuild its hint map (insert 96
    /// stems, adjust, save), work the budget charges. Unhinted, the same glyph is cheap and loads; hinted, it passes the
    /// budget at the same point on every run and throws <see cref="NotSupportedException"/>. With 40 segments it loads hinted
    /// and matches FreeType.
    /// </summary>
    [Test]
    public void Load_NativeHeavilyHintedGlyph_PassesTheGlyphBudget()
    {
        static byte[] Hostile(int segments) => Charstring.Of([
            .. Stems(96), 0, 0, "rmoveto",
            .. Enumerable.Range(0, segments).SelectMany(i => new object[]
            {
                "hintmask", Enumerable.Repeat((byte)(i % 2 == 0 ? 0xFF : 0xFE), 12).ToArray(), 0, 1, "rlineto",
            }),
            "endchar",
        ]);

        var hostile = FontLoader.Load(CraftedFont(false, Hostile(400), [], [], model => model.Private!.Dict.AddRange(s_craftedBlues))).Faces[0];
        var small = CraftedFont(false, Hostile(40), [], [], model => model.Private!.Dict.AddRange(s_craftedBlues));
        var hinted = hostile.CreateScaledFont(new ScaledFontOptions(32));
        var unhinted = hostile.CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });

        Assert.Multiple(() =>
        {
            Assert.That(() => unhinted.GetOutline(Crafted), Throws.Nothing, "unhinted, the glyph is cheap");
            Assert.That(() => hinted.GetOutline(Crafted), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains($"({Type2Interpreter.GlyphWorkBudget} units)"));
            Assert.That(() => hinted.GetOutline(Crafted), Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains($"({Type2Interpreter.GlyphWorkBudget} units)"), "the same on every run");
        });

        AssertHintedMatchFreeType(small, [Crafted], 16, 32);
    }

    // ==== Helpers.

    /// <summary>Delta-encoded DICT operands (each value minus the previous one) of an array entry.</summary>
    private static DictOp Delta(int op, params int[] values) =>
        new(op, CffModel.Numbers(values.Select((value, i) => i == 0 ? value : value - values[i - 1]).ToArray()));

    private static DictOp Numbers(int op, params int[] values) => new(op, CffModel.Numbers(values));

    private static DictOp Real(int op, string value) => new(op, CffModel.Real(value));

    private static FreeTypeOutline Outline(GlyphOutline outline) =>
        new(outline.Points.ToArray(), outline.ContourEndPoints.ToArray());

    /// <summary>Compares the glyphs hinted at each size: outlines, metrics, line metrics and bitmaps identical to FreeType's.</summary>
    private static void AssertHintedMatchFreeType(byte[] font, int[] glyphs, params int[] sizes)
    {
        var face = FontLoader.Load(font).Faces[0];
        var reports = new List<string>();
        var compared = 0;
        foreach (var size in sizes)
        {
            var result = CffHintingTests.Compare(face, font, 0, null, size, glyphs);
            compared += result.Compared;
            reports.AddRange(result.LineMetricsDifferences);
            if (result.Differences > 0)
                reports.Add($"{size}px: {result}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(reports, Is.Empty);
            Assert.That(compared, Is.Positive, "FreeType loads the glyphs");
        });
    }

    /// <summary>FreeType fails to load the crafted glyph hinted, and ours throws <see cref="InvalidDataException"/> with the message.</summary>
    private static void AssertHintedFailsAsFreeType(byte[] font, string message)
    {
        var ours = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(32));
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(font, 0, null, 32);

        Assert.Multiple(() =>
        {
            Assert.That(reference.TryLoad(Crafted, hinting: true, render: false, out _), Is.Not.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Ok),
                "FreeType fails the glyph");
            Assert.That(() => ours.GetOutline(Crafted), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));
        });
    }
}
