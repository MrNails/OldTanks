using System.Text;
using CoolEngine.Fonts.Hinting.TrueType;
using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// Hinting behaviours that the test faces do not all exercise, checked on real fonts with known FreeType failures and on
/// copies of Arial with crafted tables and programs: FreeType's default mode against its pedantic mode, INSTCTRL, the vertical
/// phantom points from <c>vhea</c>/<c>vmtx</c>, GETINFO and the double run of <c>prep</c>, v40's backward compatibility, and
/// FreeType's tricky fonts.
/// </summary>
public sealed partial class TrueTypeHinterTests
{
    private static readonly int[] s_pedanticSizes = [9, 12, 16, 32, 72];

    /// <summary>monbaiti.ttf's <c>prep</c> fails in FreeType, so every hinted load fails; ours throws for each glyph.</summary>
    [TestCase(TrueTypeInterpreterVersion.V35)]
    [TestCase(TrueTypeInterpreterVersion.V40)]
    public void Load_Monbaiti_ThrowsWhereFreeTypeFails(TrueTypeInterpreterVersion version)
    {
        var path = TestFonts.Windows("monbaiti.ttf");
        var face = FontCache.File(path).Faces[0];
        var glyphs = Enumerable.Range(0, face.GlyphCount).ToArray();
        int failed = 0, differences = 0;
        var reports = new List<string>();

        foreach (var size in s_pedanticSizes)
        {
            var result = CompareGlyphs(face, FontCache.Bytes(path), 0, glyphs, size, version, Part.Outline);
            failed += result.BothFailed;
            differences += result.Differences;
            if (result.Differences > 0)
                reports.Add($"{size}px: {result}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(differences, Is.Zero, string.Join(" | ", reports));
            Assert.That(failed, Is.EqualTo(glyphs.Length * s_pedanticSizes.Length), "every load fails in FreeType and throws here");
        });
    }

    /// <summary>
    /// The loads of SourceSans3-Regular.ttf (sizes 9, 12, 16, 32, 72) whose glyph programs fail with <c>FT_LOAD_PEDANTIC</c>
    /// but which FreeType's default mode hints, 178 in each interpreter version: ours hints them identically to the default mode.
    /// </summary>
    [Test]
    public void GetOutline_SourceSansPedanticOnlyFailures_HintAsFreeTypeDefaultMode()
    {
        var path = TestFonts.SourceSans("SourceSans3-Regular.ttf");
        var data = FontCache.Bytes(path);
        var face = FontCache.File(path).Faces[0];
        var pedanticOnly = new Dictionary<TrueTypeInterpreterVersion, int>();
        var differences = new List<string>();

        foreach (var version in new[] { TrueTypeInterpreterVersion.V35, TrueTypeInterpreterVersion.V40 })
        {
            pedanticOnly[version] = 0;
            using var freeType = new FreeTypeHinting(FreeTypeHinting.VersionNumber(version));
            foreach (var size in s_pedanticSizes)
            {
                using var pedantic = freeType.Open(data, 0, size);
                using var standard = freeType.Open(data, 0, size);
                var font = face.CreateScaledFont(new ScaledFontOptions(size) { Interpreter = version });

                for (var glyph = 0; glyph < face.GlyphCount; glyph++)
                {
                    var strict = pedantic.TryLoad(glyph, false, out _, pedantic: true);
                    var error = standard.TryLoad(glyph, false, out var expected);
                    var ours = font.GetOutline(glyph);
                    if (strict == FT_Error.FT_Err_Ok || error != FT_Error.FT_Err_Ok)
                        continue;

                    pedanticOnly[version]++;
                    if (ScaledFontTests.Compare(ours, expected!.Outline) is { } difference)
                        differences.Add($"{version} {size}px glyph {glyph}: {difference}");
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(pedanticOnly[TrueTypeInterpreterVersion.V35], Is.EqualTo(178), "v35 loads failing only in pedantic mode");
            Assert.That(pedanticOnly[TrueTypeInterpreterVersion.V40], Is.EqualTo(178), "v40 loads failing only in pedantic mode");
            Assert.That(differences, Is.Empty);
        });
    }

    /// <summary>
    /// A <c>prep</c> that turns hinting off with INSTCTRL selector 1: FreeType loads unhinted (no glyph program, no phantom
    /// rounding, no ROUND_XY_TO_GRID), still grid-fits the metrics and takes the <c>hdmx</c> advance; ours does the same.
    /// </summary>
    [Test]
    public void Load_PrepDisablesHinting_LoadsAsFreeType()
    {
        var prep = new Bytecode().Push(1, 1).Op(Bytecode.InstCtrl).ToArray();
        var font = CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep));
        var face = FontLoader.Load(font).Faces[0];
        var hinted = FontCache.Face(HintedFaces().First()).CreateScaledFont(new ScaledFontOptions(12));
        var unhinted = face.CreateScaledFont(new ScaledFontOptions(12) { Hinting = HintingMode.None });
        var disabled = face.CreateScaledFont(new ScaledFontOptions(12));
        var glyph = face.GetGlyphIndex('W');

        Assert.Multiple(() =>
        {
            AssertLoadsAsFreeType(CompareCrafted(font));
            Assert.That(disabled.GetOutline(glyph).Points.ToArray(), Is.EqualTo(unhinted.GetOutline(glyph).Points.ToArray()),
                "the outline is the unhinted one");
            Assert.That(hinted.GetOutline(glyph).Points.ToArray(), Is.Not.EqualTo(unhinted.GetOutline(glyph).Points.ToArray()),
                "Arial's own prep hints 'W'");
        });
    }

    /// <summary>A <c>prep</c> that opts out of v40's backward compatibility (INSTCTRL selector 3): x moves and the hdmx advance apply.</summary>
    [Test]
    public void Load_PrepDisablesBackwardCompatibility_LoadsAsFreeType()
    {
        var prep = new Bytecode().Push(4, 3).Op(Bytecode.InstCtrl).ToArray();
        var font = CraftedPrograms.WithPrograms(CraftedFonts.Arial(), Table("fpgm"), Concat(Table("prep"), prep));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>A glyph program switching v40's backward compatibility off for itself (INSTCTRL selector 3 outside prep).</summary>
    [Test]
    public void GlyphProgram_DisablesBackwardCompatibility_LoadsAsFreeType()
    {
        var program = new Bytecode().Push(4, 3).Op(Bytecode.InstCtrl, Bytecode.SvtcaX).Push(1).Op(Bytecode.Mdap1)
            .Push(2, 100).Op(Bytecode.Shpix).Op(Bytecode.IupX, Bytecode.IupY).Push(3, 100).Op(Bytecode.Shpix).ToArray();

        AssertLoadsAsFreeType(CompareCrafted(GlyphFont(program)));
    }

    /// <summary>
    /// GETINFO, MPPEM and MPS as FreeType answers them in each program: <c>prep</c> stores GETINFO in storage 0 and counts its
    /// runs in storage 1 (FreeType runs it twice on a new size, the storage cleared once, the second run with the grayscale or
    /// ClearType flags), and the glyph moves its points by those values and by its own GETINFO, MPPEM and MPS.
    /// </summary>
    [Test]
    public void GlyphProgram_GetInfoAndPrepRuns_AsFreeType()
    {
        var prep = new Bytecode().Push(0, 0x3FFF).Op(0x88, Bytecode.Ws)
            .Push(1, 1).Op(Bytecode.Rs).Push(1).Op(Bytecode.Add, Bytecode.Ws).ToArray();
        var program = new Bytecode().Op(Bytecode.SvtcaY)
            .Push(1, 0).Op(Bytecode.Rs, Bytecode.Shpix)                         // point 1 by prep's GETINFO
            .Push(3, 1).Op(Bytecode.Rs).Push(4096).Op(0x63).Op(Bytecode.Shpix)  // point 3 by the prep run count, in pixels
            .Push(4, 0x3FFF).Op(0x88).Op(Bytecode.Shpix)                       // point 4 by the glyph's GETINFO
            .Push(5).Op(0x4B).Op(Bytecode.Shpix)                                // point 5 by MPPEM
            .Push(6).Op(0x4C).Op(Bytecode.Shpix)                                // point 6 by MPS
            .ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), Table("fpgm"), Concat(Table("prep"), prep));

        AssertLoadsAsFreeType(CompareCrafted(font));
    }

    /// <summary>
    /// The vertical phantom points: with <c>vhea</c> and <c>vmtx</c> FreeType takes each glyph's top side bearing and advance
    /// height from them (<c>TT_Get_VMetrics</c>), without them from <c>OS/2</c>; in v40 their x is half the advance width. A
    /// simple glyph's and a composite's program place points from the phantom points, so the outlines show them.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Load_VerticalPhantomPoints_AsFreeType(bool withVmtx)
    {
        var (font, glyphs) = PhantomFont(withVmtx);
        var face = FontLoader.Load(font).Faces[0];
        int compared = 0, differences = 0;
        var reports = new List<string>();

        foreach (var version in new[] { TrueTypeInterpreterVersion.V35, TrueTypeInterpreterVersion.V40 })
        {
            foreach (var size in s_craftedSizes)
            {
                var result = CompareGlyphs(face, font, 0, glyphs, size, version, Part.Outline);
                compared += result.Compared;
                differences += result.Differences;
                if (result.Differences > 0)
                    reports.Add($"{version} {size}px: {result}");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(differences, Is.Zero, string.Join(" | ", reports));
            Assert.That(compared, Is.EqualTo(glyphs.Length * 4));
            Assert.That(face.Data.Vmtx is not null, Is.EqualTo(withVmtx), "the face reads vmtx only when it has one");
        });
    }

    /// <summary>The vertical phantom points come from vmtx: the copy with vmtx hints differently from the one without.</summary>
    [Test]
    public void GetOutline_VmtxTopSideBearings_ChangeThePhantomPoints()
    {
        var (withVmtx, glyphs) = PhantomFont(true);
        var (without, _) = PhantomFont(false);
        var options = new ScaledFontOptions(32) { Interpreter = TrueTypeInterpreterVersion.V35 };
        var a = FontLoader.Load(withVmtx).Faces[0].CreateScaledFont(options).GetOutline(glyphs[0]).Points.ToArray();
        var b = FontLoader.Load(without).Faces[0].CreateScaledFont(options).GetOutline(glyphs[0]).Points.ToArray();

        Assert.That(a, Is.Not.EqualTo(b));
    }

    /// <summary>FreeType's tricky fonts: mingliub.ttc's faces by name (MingLiU-ExtB, PMingLiU-ExtB, MingLiU_HKSCS-ExtB).</summary>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void IsTricky_Mingliub_AsFreeType(int faceIndex)
    {
        var path = TestFonts.Windows("mingliub.ttc");
        var face = FontCache.File(path).Faces[faceIndex];
        using var freeType = new FreeTypeHinting(40);
        using var reference = freeType.Open(FontCache.Bytes(path), faceIndex, 12);

        Assert.Multiple(() =>
        {
            Assert.That(reference.IsTricky, Is.True, "FreeType flags the face as tricky");
            Assert.That(TrickyFonts.IsTricky(face.Data), Is.True);
        });
    }

    /// <summary>
    /// Tricky by family name, after skipping a PDF subset tag: "ABCDLC+Sans" is not tricky (the tag hides "DLC"), "DFKai-SB"
    /// and "XDFKai-SB Bold" are; Arial is not.
    /// </summary>
    [TestCase("Arial", false)]
    [TestCase("DFKai-SB", true)]
    [TestCase("XDFKai-SB Bold", true)]
    [TestCase("ABCDLC+Sans", false)]
    [TestCase("abcDLC+Sans", true)]
    public void IsTricky_FamilyName_AsFreeType(string family, bool tricky)
    {
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "name", NameTable(family));
        var face = FontLoader.Load(font).Faces[0];
        using var freeType = new FreeTypeHinting(40);
        using var reference = freeType.Open(font, 0, 12);

        Assert.Multiple(() =>
        {
            Assert.That(face.FamilyName, Is.EqualTo(family));
            Assert.That(reference.IsTricky, Is.EqualTo(tricky), "FreeType");
            Assert.That(TrickyFonts.IsTricky(face.Data), Is.EqualTo(tricky));
            AssertLoadsAsFreeType(CompareCrafted(font));
        });
    }

    /// <summary>
    /// Tricky by table signature: no <c>cvt </c>, an <c>fpgm</c> and a <c>prep</c> whose lengths and computed checksums are those
    /// FreeType lists for NEC's fadpop7.ttf (the directory's stored checksums are wrong, as FreeType never reads them). In v40
    /// the face hints without backward compatibility, as FreeType hints it.
    /// </summary>
    [Test]
    public void IsTricky_TableSignature_AsFreeType()
    {
        var fpgm = CraftedPrograms.ProgramWithChecksum(0xE5, 0x40C92555);
        var prep = CraftedPrograms.ProgramWithChecksum(0x117C, 0xA39B58E3);
        var program = new Bytecode().Op(Bytecode.SvtcaX).Push(1).Op(Bytecode.Mdap1).Push(2, 100).Op(Bytecode.Shpix).ToArray();
        var font = CraftedPrograms.WithPrograms(GlyphFont(program), fpgm, prep, keepCvt: false);
        var tables = CraftedFonts.ReadTables(font);
        font = CraftedFonts.Build(CraftedFonts.Version(font), tables, checksums: false);
        var face = FontLoader.Load(font).Faces[0];
        using var freeType = new FreeTypeHinting(40);
        using var reference = freeType.Open(font, 0, 12);

        Assert.Multiple(() =>
        {
            Assert.That(reference.IsTricky, Is.True, "FreeType");
            Assert.That(TrickyFonts.IsTricky(face.Data), Is.True);
            AssertLoadsAsFreeType(CompareCrafted(font));
        });
    }

    /// <summary>
    /// Arial with <c>vhea</c> and (optionally) <c>vmtx</c>, and two glyphs whose programs read the phantom points: 'A' places
    /// point 0 from pp3 in y and moves point 1 by pp3's x distance to point 0; an appended composite of 'o' does the same with
    /// its own phantom points (scaled, as for a simple glyph).
    /// </summary>
    private static (byte[] Font, int[] Glyphs) PhantomFont(bool withVmtx)
    {
        var arial = CraftedFonts.Arial();
        var arialFace = FontLoader.Load(arial).Faces[0];
        var a = arialFace.GetGlyphIndex('A');
        var o = arialFace.GetGlyphIndex('o');
        var pointsA = CraftedPrograms.PointCount(arial, a);
        var pointsO = CraftedPrograms.PointCount(arial, o);

        static byte[] Program(int points) => new Bytecode()
            .Op(Bytecode.SvtcaY).Push(points + 2).Op(Bytecode.Srp0).Push(0).Op(Bytecode.MdrpRound)
            .Push(points + 3).Op(Bytecode.Srp0).Push(1).Op(Bytecode.Mdrp)
            .Op(Bytecode.SvtcaX).Push(points + 2, 0).Op(0x4A).Push(2).Op(0x23).Op(Bytecode.SvtcaY).Op(Bytecode.Shpix)
            .ToArray();

        var font = CraftedPrograms.WithGlyphProgram(arial, a, Program(pointsA));
        var instructions = Program(pointsO);
        var composite = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(0x0001 | 0x0002 | 0x0100).U16(o).U16(37).U16(0)
            .U16(instructions.Length);
        foreach (var b in instructions)
            composite.U8(b);

        (font, var first) = CraftedFonts.AppendGlyphs(font, 1, composite.ToArray());
        var glyphCount = first + 1;

        var vhea = new byte[36];
        CraftedFonts.WriteU32(vhea, 0, 0x00011000);
        CraftedFonts.WriteU16(vhea, 34, glyphCount - 7);
        font = CraftedFonts.WithTable(font, "vhea", vhea);
        if (withVmtx)
        {
            var longMetrics = glyphCount - 7;
            var vmtx = new byte[4 * longMetrics + 2 * 7];
            for (var g = 0; g < glyphCount; g++)
            {
                var bearing = (short)(37 * g % 411 - 150);
                if (g < longMetrics)
                {
                    CraftedFonts.WriteU16(vmtx, 4 * g, 1900 + 3 * g);
                    CraftedFonts.WriteU16(vmtx, 4 * g + 2, bearing);
                }
                else
                {
                    CraftedFonts.WriteU16(vmtx, 4 * longMetrics + 2 * (g - longMetrics), bearing);
                }
            }

            font = CraftedFonts.WithTable(font, "vmtx", vmtx);
        }

        return (font, [a, first]);
    }

    /// <summary>A <c>name</c> table with one family name (ID 1) and style "Regular" (ID 2), Windows Unicode English.</summary>
    internal static byte[] NameTable(string family)
    {
        var familyBytes = Encoding.BigEndianUnicode.GetBytes(family);
        var styleBytes = Encoding.BigEndianUnicode.GetBytes("Regular");
        var writer = new BigEndianWriter().U16(0).U16(2).U16(6 + 2 * 12)
            .U16(3).U16(1).U16(0x409).U16(1).U16(familyBytes.Length).U16(0)
            .U16(3).U16(1).U16(0x409).U16(2).U16(styleBytes.Length).U16(familyBytes.Length);
        foreach (var b in familyBytes.Concat(styleBytes))
            writer.U8(b);

        return writer.ToArray();
    }
}
