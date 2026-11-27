using System.Diagnostics;
using System.Text.RegularExpressions;
using CoolEngine.Fonts.Hinting.TrueType;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// The library's work budgets for hinting (Denys, 2026-11-27: "Glyph and size budgets"), which FreeType does not have, and the
/// fixes of WP4's first review: INSTCTRL selector 2, the twilight touch flags fpgm leaves, the SHZ clamp, duplicate hdmx
/// records, vertical tables in non-TrueType faces. The hostile fonts here never reach FreeType.
/// </summary>
public sealed partial class TrueTypeHinterTests
{
    /// <summary>
    /// The largest single charge here (IUP over a 65,535-point glyph and its 4 phantom points), the most a budget can be
    /// passed by.
    /// </summary>
    private const long LargestCharge = 65_539;

    /// <summary>A 65,535-point glyph looping SHZ (the reviewer's shape: one load took 304.6 s): refused within the glyph budget.</summary>
    [Test]
    public void GetOutline_GlyphLoopingShzOverManyPoints_ThrowsNotSupported()
    {
        var (font, glyph) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 4, ManyPointGlyph(65535, ShzProgram(loop: true)));
        var font16 = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(16));

        var watch = Stopwatch.StartNew();
        var exception = Assert.Throws<NotSupportedException>(() => font16.GetOutline(glyph));
        TestContext.Out.WriteLine($"Refused in {watch.Elapsed.TotalMilliseconds:F0} ms: {exception!.Message}");

        AssertChargedJustPast(exception, TrueTypeHinter.GlyphWorkBudget);
    }

    /// <summary>
    /// A composite of 64 components, each running a program of about 1,000,000 cheap instructions (FreeType's per-run limit;
    /// the reviewer's shape: about 25 min per load at 65,535 components): the components share one glyph budget.
    /// </summary>
    [Test]
    public void GetOutline_CompositeOfLongPrograms_ThrowsNotSupported()
    {
        var (font, component) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 4, ManyPointGlyph(1, CheapLoop(7800)));
        var composite = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0);
        for (var i = 0; i < 64; i++)
            composite.U16(0x0002 | (i < 63 ? 0x0020 : 0)).U16(component).U8(0).U8(0);

        (font, var glyph) = CraftedFonts.AppendGlyphs(font, 4, composite.ToArray());
        var face = FontLoader.Load(font).Faces[0];
        var font16 = face.CreateScaledFont(new ScaledFontOptions(16));

        var watch = Stopwatch.StartNew();
        var exception = Assert.Throws<NotSupportedException>(() => font16.GetOutline(glyph));
        TestContext.Out.WriteLine($"Refused in {watch.Elapsed.TotalMilliseconds:F0} ms: {exception!.Message}");

        Assert.Multiple(() =>
        {
            AssertChargedJustPast(exception, TrueTypeHinter.GlyphWorkBudget);
            Assert.That(() => font16.GetOutline(component), Throws.Nothing, "one such component alone stays within the budget");
        });
    }

    /// <summary>
    /// A prep looping SHZ over a 65,535-point twilight zone (the reviewer's shape: CreateScaledFont took 346.7 s): refused by
    /// <c>CreateScaledFont</c> within the size budget.
    /// </summary>
    [Test]
    public void CreateScaledFont_PrepLoopingShzOverTwilight_ThrowsNotSupported()
    {
        var prefix = new Bytecode().Push(0).Op(Bytecode.Szps).ToArray();
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "prep", ShzProgram(loop: true, prefix));
        font = CraftedPrograms.WithMaxp(font, CraftedPrograms.MaxTwilightPoints, 65531);
        font = CraftedFonts.WithTable(font, "cvt ", new byte[2 * 32768]);
        var face = FontLoader.Load(font).Faces[0];

        var watch = Stopwatch.StartNew();
        var exception = Assert.Throws<NotSupportedException>(() => face.CreateScaledFont(new ScaledFontOptions(16)));
        TestContext.Out.WriteLine($"Refused in {watch.Elapsed.TotalMilliseconds:F0} ms: {exception!.Message}");

        AssertChargedJustPast(exception, TrueTypeHinter.SizeWorkBudget);
    }

    /// <summary>A cvt of 16 M entries (32 MiB) is refused before its arrays are allocated (finding 6).</summary>
    [Test]
    public void CreateScaledFont_HugeCvt_ThrowsNotSupportedBeforeAllocating()
    {
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "cvt ", new byte[2 * 16 * 1024 * 1024]);
        var face = FontLoader.Load(font).Faces[0];
        var before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Multiple(() =>
        {
            Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16)),
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("the size's set-up"));
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThan(1024 * 1024), "no cvt arrays allocated");
            Assert.That(face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None }).GetOutline(36).Points.Length,
                Is.Positive, "unhinted loads need no budget");
        });
    }

    /// <summary>The heaviest real glyph and size measured (see the budgets' comments) stay far under the budgets.</summary>
    [Test]
    public void Budgets_HeaviestRealGlyphAndSize_StayFarUnder()
    {
        var arial = FontCache.File(TestFonts.Windows("arial.ttf")).Faces[0];
        var arialScale = FontScale.FromPixelSize(16, arial.UnitsPerEm);
        var hinter = new TrueTypeHinter(arial.Data, arialScale, TrueTypeInterpreterVersion.V40);
        new GlyfGlyphLoader(arial.Data, arialScale, hinter).Load(395);

        var cambria = FontCache.File(TestFonts.Windows("cambria.ttc")).Faces[0];
        var cambriaHinter = new TrueTypeHinter(cambria.Data, FontScale.FromPixelSize(9, cambria.UnitsPerEm),
            TrueTypeInterpreterVersion.V35);

        TestContext.Out.WriteLine($"arial.ttf glyph 395 at 16 px, v40: {hinter.GlyphWork}; cambria.ttc set-up at 9 px, v35: " +
            $"{cambriaHinter.SizeWork}.");
        Assert.Multiple(() =>
        {
            Assert.That(hinter.GlyphWork, Is.Positive.And.LessThan(TrueTypeHinter.GlyphWorkBudget / 50));
            Assert.That(cambriaHinter.SizeWork, Is.Positive.And.LessThan(TrueTypeHinter.SizeWorkBudget / 50));
        });
    }

    /// <summary>
    /// INSTCTRL selectors 2 and 3 together in prep: FreeType resets the graphics state (selector 2) before it reads selector 3,
    /// so v40 keeps backward compatibility (and takes no hdmx advance); selector 3 alone and selector 2 alone as FreeType.
    /// </summary>
    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void Load_PrepInstCtrlSelectors2And3_AsFreeType(bool selector2, bool selector3)
    {
        var program = new Bytecode().Op(Bytecode.SvtcaX).Push(1).Op(0x2E).Push(1, 100).Op(Bytecode.Shpix)
            .Op(Bytecode.SvtcaY).Push(2).Op(0x2E).Push(2, 100).Op(Bytecode.Shpix).ToArray();
        var tail = new Bytecode();
        if (selector2)
            tail.Push(2, 2).Op(Bytecode.InstCtrl);

        if (selector3)
            tail.Push(4, 3).Op(Bytecode.InstCtrl);

        var font = CraftedPrograms.WithPrograms(GlyphFont(program), Table("fpgm"), Concat(Table("prep"), tail.ToArray()));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>
    /// fpgm touches twilight point 5 in y; FreeType clears only the twilight points' positions before prep, not their touch
    /// flags, so a v40 glyph program's DELTAP on it (which needs the flag in backward compatibility) moves it.
    /// </summary>
    [Test]
    public void GlyphProgram_TwilightTouchFlagsFromFpgm_AsFreeType()
    {
        var fpgmTail = new Bytecode().Op(Bytecode.SvtcaY).Push(0).Op(Bytecode.Szp0).Push(5).Op(0x2E).Push(1).Op(Bytecode.Szp0)
            .ToArray();
        var program = new Bytecode().Op(Bytecode.SvtcaY).Op(0x4B, 0x5E).Push(0).Op(Bytecode.Szp0).Push(0x0F, 5, 1).Op(0x5D)
            .Push(5).Op(Bytecode.Srp0).Push(1).Op(Bytecode.Mdrp).ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), Concat(Table("fpgm"), fpgmTail), Table("prep"));
        font = CraftedPrograms.WithMaxp(font, CraftedPrograms.MaxTwilightPoints, 40);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>
    /// SHZ in a composite nested in another composite, where FreeType 2.13.2's limit (the last contour's end, without the
    /// zone's first point) reaches past the zone and FreeType writes outside it (so FreeType never sees this font): ours moves
    /// only the zone's points, and the nested composite's points equal its own top-level load, shifted by its offset.
    /// </summary>
    [TestCase(TrueTypeInterpreterVersion.V35)]
    [TestCase(TrueTypeInterpreterVersion.V40)]
    public void GetOutline_ShzInNestedComposite_MovesOnlyItsZone(TrueTypeInterpreterVersion version)
    {
        var arial = CraftedFonts.Arial();
        var arialFace = FontLoader.Load(arial).Faces[0];
        int capitalA = arialFace.GetGlyphIndex('A'), o = arialFace.GetGlyphIndex('o'), l = arialFace.GetGlyphIndex('l');
        var own = new Bytecode().Op(Bytecode.SvtcaY).Push(0, 64).Op(Bytecode.Shpix).Push(0).Op(0x12).Push(1).Op(0x36).ToArray();
        var inner = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(0x0001 | 0x0002 | 0x0020).U16(o).U16(0).U16(0)
            .U16(0x0001 | 0x0002 | 0x0100).U16(l).U16(1200).U16(0).U16(own.Length);
        foreach (var b in own)
            inner.U8(b);

        var (font, innerGlyph) = CraftedFonts.AppendGlyphs(arial, 3, inner.ToArray());
        var outer = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(0x0001 | 0x0002 | 0x0020).U16(capitalA).U16(0).U16(0)
            .U16(0x0001 | 0x0002).U16(innerGlyph).U16(2000).U16(0);
        (font, var outerGlyph) = CraftedFonts.AppendGlyphs(font, 3, outer.ToArray());
        var scaled = FontLoader.Load(font).Faces[0].CreateScaledFont(new ScaledFontOptions(16) { Interpreter = version });

        var alone = scaled.GetOutline(innerGlyph).Points.ToArray();
        var nested = scaled.GetOutline(outerGlyph).Points.ToArray();
        var before = scaled.GetOutline(capitalA).Points.Length;
        var part = nested[before..];
        var dx = part[0].X - alone[0].X;

        Assert.Multiple(() =>
        {
            Assert.That(part, Has.Length.EqualTo(alone.Length));
            Assert.That(part.Select(point => point with { X = point.X - dx }), Is.EqualTo(alone));
        });
    }

    /// <summary>
    /// Two hdmx records for one pixel size: the table is ignored, as OTS drops it (FreeType picks one of the two through its
    /// unstable sort, so FreeType is no reference here): the font loads, and hinted advances are those of the font without
    /// an hdmx.
    /// </summary>
    [Test]
    public void Load_HdmxDuplicatePixelSize_TableIgnored()
    {
        var withDuplicates = CraftedFonts.WithTable(CraftedFonts.Arial(), "hdmx", Hdmx((12, 7), (16, 11), (12, 9)));
        var withoutHdmx = CraftedFonts.WithoutTable(CraftedFonts.Arial(), "hdmx");
        var face = FontLoader.Load(withDuplicates).Faces[0];
        var reference = FontLoader.Load(withoutHdmx).Faces[0];
        var glyphs = Glyphs(face, GlyphSet.GameCharacters).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(face.Data.Hdmx!.FindWidths(12), Is.EqualTo(-1));
            Assert.That(face.Data.Hdmx!.FindWidths(16), Is.EqualTo(-1), "the whole table is ignored");
            foreach (var size in new[] { 12, 16 })
            {
                var ours = face.CreateScaledFont(new ScaledFontOptions(size) { Interpreter = TrueTypeInterpreterVersion.V35 });
                var expected = reference.CreateScaledFont(new ScaledFontOptions(size) { Interpreter = TrueTypeInterpreterVersion.V35 });
                Assert.That(glyphs.Select(ours.GetMetrics), Is.EqualTo(glyphs.Select(expected.GetMetrics)), $"{size} px");
            }
        });
    }

    /// <summary>
    /// 65,534 functions numbered 1..65,534 (so not entry n for number n, and FreeType searches its table linearly) and a glyph
    /// program calling the last one 1,000 times (DUP and CALL): each call costs one unit of work for its lookup (the
    /// reviewer's shape took 47.5 s for one glyph load before the function index).
    /// </summary>
    [Test]
    public void GetOutline_CallsOfLastOfManySparseFunctions_CostLittle()
    {
        const int functions = 65534;
        var fpgm = new Bytecode();
        for (var k = 1; k <= functions; k++)
        {
            PushNumber(fpgm, k);
            fpgm.Op(Bytecode.Fdef, Bytecode.SvtcaY, Bytecode.Endf);
        }

        var program = new Bytecode();
        PushNumber(program, functions);
        for (var i = 0; i < 1000; i++)
            program.Op(0x20, Bytecode.Call);

        var font = CraftedPrograms.WithPrograms(GlyphFont(program.ToArray()), fpgm.ToArray(), null);
        font = CraftedPrograms.WithMaxp(font, CraftedPrograms.MaxFunctionDefs, functions);
        var face = FontLoader.Load(font).Faces[0];
        var glyph = face.GetGlyphIndex('A');
        var scale = FontScale.FromPixelSize(16, face.UnitsPerEm);

        var watch = Stopwatch.StartNew();
        var hinter = new TrueTypeHinter(face.Data, scale, TrueTypeInterpreterVersion.V40);
        var created = watch.Elapsed;
        new GlyfGlyphLoader(face.Data, scale, hinter).Load(glyph);
        TestContext.Out.WriteLine($"Set-up {created.TotalMilliseconds:F0} ms, work {hinter.SizeWork}; glyph load " +
            $"{(watch.Elapsed - created).TotalMilliseconds:F0} ms, work {hinter.GlyphWork}.");

        Assert.Multiple(() =>
        {
            Assert.That(hinter.GlyphWork, Is.InRange(4_000, 10_000), "1,000 DUPs, CALLs and two-instruction functions");
            Assert.That(hinter.SizeWork, Is.LessThan(500_000), "65,534 definitions of a few units each");
        });
    }

    /// <summary>
    /// Sparse function numbers, a function redefined in prep, an undefined function and an instruction definition redefined
    /// in prep: the glyph program's calls reach the same definitions as in FreeType.
    /// </summary>
    [Test]
    public void GlyphProgram_SparseAndRedefinedFunctions_AsFreeType()
    {
        static void Define(Bytecode code, int number, int point, int amount) =>
            code.Push(number).Op(Bytecode.Fdef).Push(point, amount).Op(Bytecode.Shpix, Bytecode.Endf);

        var fpgm = new Bytecode();
        Define(fpgm, 3, 1, 64);
        Define(fpgm, 1000, 2, 32);
        Define(fpgm, 7, 3, 16);
        fpgm.Push(0x28).Op(Bytecode.Idef).Push(4, 8).Op(Bytecode.Shpix, Bytecode.Endf);
        var prep = new Bytecode();
        Define(prep, 3, 1, 128);
        prep.Push(0x28).Op(Bytecode.Idef).Push(4, 24).Op(Bytecode.Shpix, Bytecode.Endf);
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(3).Op(Bytecode.Call).Push(1000).Op(Bytecode.Call).Push(7).Op(Bytecode.Call).Op(0x28)
            .Push(5).Op(Bytecode.Call).Push(5, 8).Op(Bytecode.Shpix).ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), fpgm.ToArray(), prep.ToArray());
        font = CraftedPrograms.WithMaxp(font, CraftedPrograms.MaxInstructionDefs, 1);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>
    /// A chain of instructed composites over a many-point glyph: each level's program copies all the points again, charged
    /// to the glyph's work budget before the copy. A short chain is charged 7 units per point per level; the reviewer's chain
    /// (250 levels over 65,531 points: 771 MiB allocated per load before) is refused within a bounded allocation.
    /// </summary>
    [TestCase(10, 2_000, false)]
    [TestCase(250, 65_531, true)]
    public void GetOutline_ChainOfInstructedComposites_ChargesItsCopies(int depth, int points, bool refused)
    {
        var arial = CraftedFonts.Arial();
        var first = FontLoader.Load(arial).Faces[0].GlyphCount;
        var glyphs = new List<byte[]> { ManyPointGlyph(points, [Bytecode.SvtcaY]) };
        for (var i = 1; i <= depth; i++)
        {
            glyphs.Add(new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
                .U16(0x0002 | 0x0100).U16(first + i - 1).U8(0).U8(0).U16(1).U8(Bytecode.SvtcaY).ToArray());
        }

        var (font, _) = CraftedFonts.AppendGlyphs(arial, depth + 1, glyphs.ToArray());
        var face = FontLoader.Load(font).Faces[0];
        var scale = FontScale.FromPixelSize(16, face.UnitsPerEm);
        var hinter = new TrueTypeHinter(face.Data, scale, TrueTypeInterpreterVersion.V40);
        var loader = new GlyfGlyphLoader(face.Data, scale, hinter);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();

        if (refused)
            Assert.Throws<NotSupportedException>(() => loader.Load(first + depth));
        else
            loader.Load(first + depth);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"{depth} levels over {points} points: {watch.Elapsed.TotalMilliseconds:F0} ms, work " +
            $"{hinter.GlyphWork}, allocated {allocated / 1048576.0:F1} MiB.");

        var copies = 7L * depth * (points + 4);
        Assert.Multiple(() =>
        {
            if (refused)
            {
                Assert.That(hinter.GlyphWork, Is.GreaterThan(TrueTypeHinter.GlyphWorkBudget));
                Assert.That(allocated, Is.LessThan(64L * 1024 * 1024), "refused before most copies");
            }
            else
            {
                Assert.That(hinter.GlyphWork, Is.GreaterThanOrEqualTo(copies).And.LessThan(copies + 1_000_000));
            }
        });
    }

    /// <summary>Pushes a function number up to 65,535 (PUSHW values are signed, so larger ones are added up).</summary>
    private static void PushNumber(Bytecode code, int number)
    {
        if (number <= 32767)
            code.Push(number);
        else
            code.Push(32767, number - 32767).Op(Bytecode.Add);
    }

    /// <summary>hdmx records out of order but distinct are sorted, as FreeType sorts them: the advances match FreeType's.</summary>
    [Test]
    public void GetMetrics_HdmxRecordsOutOfOrder_AsFreeType()
    {
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "hdmx", Hdmx((32, 30), (12, 7), (16, 11)));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>A face without glyf outlines never reads vhea or vmtx, so broken ones there do not stop its load.</summary>
    [Test]
    public void Load_CffFaceWithBrokenVerticalTables_Loads()
    {
        var otf = (byte[])FontCache.Bytes(TestFonts.SourceSans("SourceSans3-Regular.otf")).Clone();
        var font = CraftedFonts.WithTable(CraftedFonts.WithTable(otf, "vhea", new byte[10]), "vmtx", new byte[2]);
        var broken = WithVertical(CraftedFonts.Arial(), vhea => vhea[..10], vmtx => vmtx);

        Assert.Multiple(() =>
        {
            Assert.That(FontLoader.Load(font).Faces[0].OutlineFormat, Is.EqualTo(OutlineFormat.Cff));
            Assert.That(() => FontLoader.Load(broken), Throws.TypeOf<InvalidDataException>(), "a TrueType face reads them");
        });
    }

    private static void AssertChargedJustPast(Exception exception, long budget)
    {
        var charged = long.Parse(Regex.Match(exception.Message, @"charged (\d+)").Groups[1].Value);
        Assert.That(charged, Is.GreaterThan(budget).And.AtMost(budget + LargestCharge), exception.Message);
    }

    /// <summary>An hdmx table for Arial with the given (pixel size, width) records, every glyph at that width.</summary>
    private static byte[] Hdmx(params (int Ppem, int Width)[] records)
    {
        var glyphCount = CraftedFonts.U16(Table("maxp"), 4);
        var size = (glyphCount + 2 + 3) & ~3;
        var table = new byte[8 + size * records.Length];
        CraftedFonts.WriteU16(table, 2, records.Length);
        CraftedFonts.WriteU32(table, 4, (uint)size);
        for (var i = 0; i < records.Length; i++)
        {
            var at = 8 + size * i;
            table[at] = (byte)records[i].Ppem;
            table[at + 1] = (byte)records[i].Width;
            for (var g = 0; g < glyphCount; g++)
                table[at + 2 + g] = (byte)records[i].Width;
        }

        return table;
    }

    /// <summary>A simple glyph of one contour of <paramref name="count"/> points, all at (0, 0), running <paramref name="program"/>.</summary>
    private static byte[] ManyPointGlyph(int count, byte[] program)
    {
        var writer = new BigEndianWriter().U16(1).U16(0).U16(0).U16(0).U16(0).U16(count - 1).U16(program.Length);
        foreach (var b in program)
            writer.U8(b);

        // On-curve flags with zero coordinates (x and y "same"), repeated in runs of 256.
        var left = count;
        while (left > 0)
        {
            if (left == 1)
            {
                writer.U8(0x31);
                break;
            }

            var take = Math.Min(left, 256);
            writer.U8(0x39).U8(take - 1);
            left -= take;
        }

        return writer.ToArray();
    }

    /// <summary>SVTCA[y] repeated <paramref name="body"/> times, then a jump back to the start (until a limit stops it).</summary>
    private static byte[] CheapLoop(int body) =>
        new Bytecode().Repeat(Bytecode.SvtcaY, body).Push(-(body + 3) & 0xFFFF).Op(Bytecode.Jmpr).ToArray();

    /// <summary>NPUSHB 255 zeros and 255 SHZ[0], optionally looped with a backward jump.</summary>
    private static byte[] ShzProgram(bool loop, byte[]? prefix = null)
    {
        var bytecode = new Bytecode();
        if (prefix is not null)
            bytecode.Op(prefix.Select(b => (int)b).ToArray());

        var start = bytecode.Length;
        bytecode.Op(0x40, 255).Op(new int[255]).Repeat(0x36, 255);
        if (loop)
        {
            var jumpAt = bytecode.Length + 3;
            bytecode.Push(-(jumpAt - start) & 0xFFFF).Op(Bytecode.Jmpr);
        }

        return bytecode.ToArray();
    }
}
