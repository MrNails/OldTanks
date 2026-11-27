using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// Crafted programs for each error and limit class, compared with FreeType 2.13.2's default (non-pedantic) mode: an error in
/// <c>fpgm</c> or <c>prep</c> fails every hinted load (ours: <see cref="InvalidDataException"/>), an error in a glyph program
/// stops it and keeps the moves made before (FreeType ignores the error), and what FreeType skips (out-of-range references,
/// a stack underflow) is skipped. Each case runs on a copy of the repository's Arial, at 12 and 32 px, v35 and v40, over the
/// game's characters, so FreeType only ever sees conforming fonts with crafted programs.
/// </summary>
public sealed partial class TrueTypeHinterTests
{
    private static readonly int[] s_craftedSizes = [12, 32];

    /// <summary>The totals of one crafted font's comparisons.</summary>
    private readonly record struct CraftedResult(int Glyphs, int Compared, int BothFailed, int Differences, string Report);

    [Test]
    public void Prep_StackUnderflow_PopsZerosAsFreeType()
    {
        // ADD and MDAP with an empty stack: FreeType's default mode pops zeros (pedantic mode would fail).
        var prep = new Bytecode().Op(Bytecode.Add, Bytecode.Pop, Bytecode.Szp0).Op(0x00).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep)));

        AssertLoadsAsFreeType(result);
    }

    [Test]
    public void GlyphProgram_StackUnderflow_PopsZerosAsFreeType()
    {
        // MDAP[1] with an empty stack rounds point 0; DIV of zeros would divide by zero, so stop with a plain ADD.
        var program = new Bytecode().Op(Bytecode.SvtcaX, Bytecode.Mdap1, Bytecode.Add, Bytecode.Pop, Bytecode.IupX).ToArray();
        var result = CompareCrafted(GlyphFont(program));

        AssertLoadsAsFreeType(result);
    }

    [Test]
    public void Prep_JumpBeforeProgramStart_FailsAsFreeType()
    {
        var prep = new Bytecode().Push(-100 & 0xFFFF).Op(Bytecode.Jmpr).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep)));

        AssertFailsAsFreeType(result);
    }

    [Test]
    public void Prep_JumpPastProgramEnd_EndsAsFreeType()
    {
        // A forward jump past the end of a program ends it normally (no call frame is open).
        var prep = new Bytecode().Push(1000).Op(Bytecode.Jmpr).Op(Bytecode.Debug).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep)));

        AssertLoadsAsFreeType(result);
    }

    [Test]
    public void GlyphProgram_JumpBeforeProgramStart_KeepsEarlierMovesAsFreeType()
    {
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(1).Op(Bytecode.Mdap1)
            .Push(-200 & 0xFFFF).Op(Bytecode.Jmpr).Push(2).Op(Bytecode.Mdap1).ToArray();
        var result = CompareCrafted(GlyphFont(program));

        AssertLoadsAsFreeType(result);
    }

    [Test]
    public void Prep_UndefinedFunction_FailsAsFreeType()
    {
        var prep = new Bytecode().Push(250).Op(Bytecode.Call).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep)));

        AssertFailsAsFreeType(result);
    }

    [Test]
    public void GlyphProgram_UndefinedFunction_KeepsEarlierMovesAsFreeType()
    {
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(1).Op(Bytecode.Mdap1)
            .Push(250).Op(Bytecode.Call).Push(2).Op(Bytecode.Mdap1).ToArray();
        var result = CompareCrafted(GlyphFont(program));

        AssertLoadsAsFreeType(result);
    }

    [Test]
    public void Fpgm_Error_FailsEverySizeAsFreeType()
    {
        var fpgm = Concat(Table("fpgm"), new Bytecode().Op(Bytecode.Debug).ToArray());
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, Table("prep")));

        AssertFailsAsFreeType(result);
    }

    [Test]
    public void Prep_RunawayBackwardJump_FailsAsFreeType()
    {
        // PUSHW -3; JMPR jumps back to the PUSHW forever: past the backward-jump budget FreeType stops (Execution_Too_Long).
        var prep = new Bytecode().Push(-3 & 0xFFFF).Op(Bytecode.Jmpr).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), prep));

        AssertFailsAsFreeType(result);
    }

    [Test]
    public void GlyphProgram_RunawayBackwardJump_KeepsEarlierMovesAsFreeType()
    {
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(1).Op(Bytecode.Mdap1).Push(-3 & 0xFFFF).Op(Bytecode.Jmpr).ToArray();
        var result = CompareCrafted(GlyphFont(program));

        AssertLoadsAsFreeType(result);
    }

    [TestCase(17_000, false, TestName = "Prep_LoopCallWithinBudget_LoadsAsFreeType")]
    [TestCase(17_725, true, TestName = "Prep_LoopCallOverBudget_FailsAsFreeType")]
    public void Prep_LoopCall_AsFreeType(int count, bool fails)
    {
        // Arial's cvt has 792 entries, so prep's LOOPCALL budget is 300 + 22 * 792 = 17,724 iterations.
        var fpgm = new Bytecode().Push(0).Op(Bytecode.Fdef, Bytecode.SvtcaY, Bytecode.Endf).ToArray();
        var prep = new Bytecode().Push(count, 0).Op(Bytecode.LoopCall).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, prep));

        if (fails)
            AssertFailsAsFreeType(result);
        else
            AssertLoadsAsFreeType(result);
    }

    [TestCase(3_984, false, TestName = "Prep_MillionInstructions_LoadsAsFreeType")]
    [TestCase(3_985, true, TestName = "Prep_OverMillionInstructions_FailsAsFreeType")]
    public void Prep_InstructionBudget_AsFreeType(int count, bool fails)
    {
        // A function of 251 instructions (250 SVTCA and its ENDF) called by LOOPCALL: with the PUSHW and the LOOPCALL, 3,984
        // calls execute 999,986 instructions and 3,985 calls would execute 1,000,237, past FreeType's 1,000,000.
        var fpgm = new Bytecode().Push(0).Op(Bytecode.Fdef).Repeat(Bytecode.SvtcaY, 250).Op(Bytecode.Endf).ToArray();
        var prep = new Bytecode().Push(count, 0).Op(Bytecode.LoopCall).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, prep));

        if (fails)
            AssertFailsAsFreeType(result);
        else
            AssertLoadsAsFreeType(result);
    }

    [Test]
    public void StorageOutOfRange_SkippedAsFreeType()
    {
        // WS past the storage is skipped; RS past it reads 0, which then selects point 0.
        var prep = new Bytecode().Push(30000, 7).Op(Bytecode.Ws).ToArray();
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(30000).Op(Bytecode.Rs, Bytecode.Mdap1)
            .Push(30000, 5).Op(Bytecode.Ws).ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), Table("fpgm"), Concat(Table("prep"), prep));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void CvtOutOfRange_SkippedAsFreeType()
    {
        // WCVTP past the cvt is skipped; RCVT past it reads 0; MIRP with an out-of-range entry moves nothing but sets rp0.
        var prep = new Bytecode().Push(30000, 64).Op(Bytecode.Wcvtp).ToArray();
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(30000).Op(Bytecode.Rcvt, Bytecode.Mdap1)
            .Push(2, 30000).Op(0xFD).Push(3, 4).Op(Bytecode.Srp0, Bytecode.MdrpRound).ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), Table("fpgm"), Concat(Table("prep"), prep));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void TwilightOutOfRange_SkippedAsFreeType()
    {
        // MDAP on a twilight point past the zone is skipped; the next MDAP in the glyph zone runs.
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(0).Op(Bytecode.Szp0).Push(60000).Op(Bytecode.Mdap1)
            .Push(1).Op(Bytecode.Szp0).Push(1).Op(Bytecode.Mdap1).ToArray();
        var font = CraftedPrograms.WithMaxp(GlyphFont(program), CraftedPrograms.MaxTwilightPoints, 4);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [TestCase(32, false, TestName = "Prep_CallNesting32Frames_LoadsAsFreeType")]
    [TestCase(33, true, TestName = "Prep_CallNesting33Frames_FailsAsFreeType")]
    public void Prep_CallNesting_AsFreeType(int depth, bool fails)
    {
        // Function i calls function i + 1; the last returns. FreeType's call stack holds 32 frames.
        var fpgm = new Bytecode();
        for (var i = 0; i < depth - 1; i++)
            fpgm.Push(i).Op(Bytecode.Fdef).Push(i + 1).Op(Bytecode.Call, Bytecode.Endf);

        fpgm.Push(depth - 1).Op(Bytecode.Fdef, Bytecode.Endf);
        var prep = new Bytecode().Push(0).Op(Bytecode.Call).ToArray();
        var result = CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm.ToArray(), prep));

        if (fails)
            AssertFailsAsFreeType(result);
        else
            AssertLoadsAsFreeType(result);
    }

    [Test]
    public void Prep_RecursiveCall_FailsAsFreeType()
    {
        var fpgm = new Bytecode().Push(0).Op(Bytecode.Fdef).Push(0).Op(Bytecode.Call, Bytecode.Endf).ToArray();
        var prep = new Bytecode().Push(0).Op(Bytecode.Call).ToArray();

        AssertFailsAsFreeType(CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, prep)));
    }

    [Test]
    public void Prep_StackOverflow_FailsAsFreeType()
    {
        // maxStackElements 0: the stack holds the 32 values FreeType adds; a 40-value push overflows it.
        var prep = new Bytecode().Push(Enumerable.Range(1, 40).ToArray()).ToArray();
        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), null, prep),
            CraftedPrograms.MaxStackElements, 0);

        AssertFailsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void Prep_StackAt32ExtraValues_LoadsAsFreeType()
    {
        var prep = new Bytecode().Push(Enumerable.Range(1, 32).ToArray()).Op(Bytecode.Clear).ToArray();
        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), null, prep),
            CraftedPrograms.MaxStackElements, 0);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [TestCase(new[] { 1, 0, Bytecode.Div }, TestName = "Prep_DivideByZero_FailsAsFreeType")]
    [TestCase(new[] { Bytecode.Debug }, TestName = "Prep_DebugOpcode_FailsAsFreeType")]
    [TestCase(new[] { 0x28 }, TestName = "Prep_UndefinedOpcode_FailsAsFreeType")]
    [TestCase(new[] { Bytecode.Endf }, TestName = "Prep_EndfOutsideFunction_FailsAsFreeType")]
    [TestCase(new[] { 0xFFFF, Bytecode.Sloop }, TestName = "Prep_NegativeLoop_FailsAsFreeType")]
    public void Prep_Error_FailsAsFreeType(int[] code)
    {
        // Values below 256 before the opcode are pushed; 0xFFFF pushes -1.
        var bytecode = new Bytecode();
        var opcode = code[^1];
        if (code.Length > 1)
            bytecode.Push(code[..^1]);

        bytecode.Op(opcode);
        var prep = Concat(Table("prep"), bytecode.ToArray());

        AssertFailsAsFreeType(CompareCrafted(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), prep)));
    }

    [Test]
    public void Prep_UndefinedOpcodeWithInstructionDefinition_RunsItAsFreeType()
    {
        // fpgm defines opcode 0x28 as SVTCA[x]; prep uses it, so prep runs on.
        var fpgm = Concat(Table("fpgm"), new Bytecode().Push(0x28).Op(Bytecode.Idef, Bytecode.SvtcaX, Bytecode.Endf).ToArray());
        var prep = Concat(Table("prep"), new Bytecode().Op(0x28).ToArray());
        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, prep),
            CraftedPrograms.MaxInstructionDefs, 1);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void Fpgm_TooManyInstructionDefinitions_FailsAsFreeType()
    {
        // maxInstructionDefs 0: FreeType allocates none, so an IDEF fails.
        var fpgm = Concat(Table("fpgm"), new Bytecode().Push(0x28).Op(Bytecode.Idef, Bytecode.SvtcaX, Bytecode.Endf).ToArray());
        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm, Table("prep")),
            CraftedPrograms.MaxInstructionDefs, 0);

        AssertFailsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void Fpgm_SixtyFourFunctionsWithMaxpZero_LoadsAsFreeType()
    {
        // FreeType allocates at least 64 function definitions whatever maxp.maxFunctionDefs says.
        var fpgm = new Bytecode();
        for (var i = 0; i < 64; i++)
            fpgm.Push(i).Op(Bytecode.Fdef, Bytecode.SvtcaY, Bytecode.Endf);

        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm.ToArray(), null),
            CraftedPrograms.MaxFunctionDefs, 0);

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void Fpgm_SixtyFiveFunctionsWithMaxpZero_FailsAsFreeType()
    {
        var fpgm = new Bytecode();
        for (var i = 0; i < 65; i++)
            fpgm.Push(i).Op(Bytecode.Fdef, Bytecode.SvtcaY, Bytecode.Endf);

        var font = CraftedPrograms.WithMaxp(CraftedPrograms.WithPrograms(CraftedFonts.Arial(), fpgm.ToArray(), null),
            CraftedPrograms.MaxFunctionDefs, 0);

        AssertFailsAsFreeType(CompareCrafted(font));
    }

    [Test]
    public void GlyphProgram_FunctionDefinition_StopsAsFreeType()
    {
        // FDEF is not allowed in a glyph program: FreeType stops there and ignores the error.
        var program = new Bytecode().Op(Bytecode.SvtcaY).Push(1).Op(Bytecode.Mdap1)
            .Push(0).Op(Bytecode.Fdef, Bytecode.Endf).Push(2).Op(Bytecode.Mdap1).ToArray();

        AssertLoadsAsFreeType(CompareCrafted(GlyphFont(program)));
    }

    [Test]
    public void GlyphProgram_StorageAndCvtWrites_LastForOneLoadAsFreeType()
    {
        // Every glyph moves point 1 by storage 0 and point 2 by cvt 0, after adding 64 to each. FreeType's glyph programs
        // write to copies of the storage and the cvt that last for one glyph load (shared by a composite's programs), so
        // every glyph sees the values prep left, plus its own components' writes.
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(1, 0).Op(Bytecode.Rs, Bytecode.Shpix)
            .Push(0, 0).Op(Bytecode.Rs).Push(64).Op(Bytecode.Add, Bytecode.Ws)
            .Push(0, 0).Op(Bytecode.Rcvt).Push(64).Op(Bytecode.Add, Bytecode.Wcvtp)
            .Push(2, 0).Op(Bytecode.Rcvt, Bytecode.Shpix)
            .ToArray();

        AssertLoadsAsFreeType(CompareCrafted(EveryGlyphFont(program)));
    }

    [Test]
    public void GlyphProgram_WcvtfWrite_AsFreeType()
    {
        // Every glyph moves point 2 by cvt 0, then writes 200 font units there with WCVTF. FreeType's WCVTF writes to the cvt
        // the program reads at that moment: the size's own cvt, unless a WCVTP or DELTAC of the same glyph load has made the
        // load's working copy first. Here none has, so the write reaches the size's cvt and later glyphs see it.
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(2, 0).Op(Bytecode.Rcvt, Bytecode.Shpix)
            .Push(0, 200).Op(0x70)
            .ToArray();

        AssertLoadsAsFreeType(CompareCrafted(EveryGlyphFont(program)));
    }

    [Test]
    public void GlyphProgram_TwilightWrites_PersistAsFreeType()
    {
        // Each glyph moves twilight point 0 up by 1/64, then places its point 1 from that twilight point: the twilight zone
        // persists between glyph loads in FreeType, so later glyphs see earlier glyphs' moves.
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(0).Op(Bytecode.Szp2).Push(0, 1).Op(Bytecode.Shpix)
            .Push(1).Op(Bytecode.Szp2).Push(0).Op(Bytecode.Szp0).Push(0).Op(Bytecode.Srp0).Push(1).Op(Bytecode.Mdrp)
            .ToArray();

        AssertLoadsAsFreeType(CompareCrafted(EveryGlyphFont(program)));
    }

    /// <summary>A copy of Arial whose glyph for 'A' runs <paramref name="program"/>.</summary>
    private static byte[] GlyphFont(byte[] program)
    {
        var font = CraftedFonts.Arial();
        var glyph = FontLoader.Load(font).Faces[0].GetGlyphIndex('A');
        return CraftedPrograms.WithGlyphProgram(font, glyph, program);
    }

    /// <summary>A copy of Arial whose every simple glyph runs <paramref name="program"/>.</summary>
    private static byte[] EveryGlyphFont(byte[] program)
    {
        var font = CraftedFonts.Arial();
        var glyphs = CraftedFonts.ReadGlyphs(font);
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (glyphs[i].Length >= 10 && (short)CraftedFonts.U16(glyphs[i], 0) > 0)
                glyphs[i] = CraftedPrograms.ReplaceInstructions(glyphs[i], program);
        }

        return CraftedFonts.WithGlyphs(font, glyphs);
    }

    private static byte[] Table(string tag) => CraftedFonts.Table(CraftedFonts.Arial(), tag);

    private static byte[] Concat(byte[] first, byte[] second) => [.. first, .. second];

    /// <summary>Outlines and metrics of the game's characters at 12 and 32 px, v35 and v40, against FreeType.</summary>
    private static CraftedResult CompareCrafted(byte[] font)
    {
        var face = FontLoader.Load(font).Faces[0];
        var glyphs = Glyphs(face, GlyphSet.GameCharacters).ToArray();
        int total = 0, compared = 0, bothFailed = 0, differences = 0;
        var report = new List<string>();

        foreach (var version in new[] { TrueTypeInterpreterVersion.V35, TrueTypeInterpreterVersion.V40 })
        {
            foreach (var size in s_craftedSizes)
            {
                foreach (var part in new[] { Part.Outline, Part.Metrics })
                {
                    var result = CompareGlyphs(face, font, 0, glyphs, size, version, part);
                    total += glyphs.Length;
                    compared += result.Compared;
                    bothFailed += result.BothFailed;
                    differences += result.Differences;
                    if (result.Differences > 0)
                        report.Add($"{version} {size}px {part}: {result}");
                }
            }
        }

        return new CraftedResult(total, compared, bothFailed, differences, string.Join(" | ", report));
    }

    private static void AssertLoadsAsFreeType(CraftedResult result) =>
        Assert.Multiple(() =>
        {
            Assert.That(result.Differences, Is.Zero, result.Report);
            Assert.That(result.Compared, Is.EqualTo(result.Glyphs), "every glyph loads in FreeType and here");
        });

    private static void AssertFailsAsFreeType(CraftedResult result) =>
        Assert.Multiple(() =>
        {
            Assert.That(result.Differences, Is.Zero, result.Report);
            Assert.That(result.BothFailed, Is.EqualTo(result.Glyphs), "every glyph fails in FreeType and throws here");
        });
}
