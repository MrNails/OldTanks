using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// The strict rules of the <c>vhea</c> and <c>vmtx</c> tables the hinter reads for the vertical phantom points (OT-4 WP4), as
/// those of <c>hhea</c> and <c>hmtx</c>: read only when both are present, each checked at load.
/// </summary>
public sealed partial class TrueTypeHinterTests
{
    [Test]
    public void Load_VheaShorterThan36Bytes_Throws()
    {
        var font = WithVertical(CraftedFonts.Arial(), vhea => vhea[..34], vmtx => vmtx);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<InvalidDataException>().With.Message.Contains("'vhea' table is 34 bytes"));
    }

    [TestCase(0)]
    [TestCase(1675)]
    public void Load_VheaLongMetricsOutOfRange_Throws(int count)
    {
        var font = WithVertical(CraftedFonts.Arial(), vhea =>
        {
            CraftedFonts.WriteU16(vhea, 34, count);
            return vhea;
        }, vmtx => vmtx);

        Assert.That(() => FontLoader.Load(font),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains($"numOfLongVerMetrics is {count}"));
    }

    [Test]
    public void Load_VmtxShorterThanItsMetrics_Throws()
    {
        var font = WithVertical(CraftedFonts.Arial(), vhea => vhea, vmtx => vmtx[..^2]);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<InvalidDataException>().With.Message.Contains("'vmtx' table is"));
    }

    /// <summary>A <c>vhea</c> without <c>vmtx</c>, or a <c>vmtx</c> without <c>vhea</c>, is not read (FreeType's vertical_info stays off).</summary>
    [TestCase("vhea")]
    [TestCase("vmtx")]
    public void Load_OneVerticalTableAlone_NotRead(string present)
    {
        var both = WithVertical(CraftedFonts.Arial(), vhea => vhea[..10], vmtx => vmtx[..2]);
        var font = CraftedFonts.WithoutTable(both, present == "vhea" ? "vmtx" : "vhea");

        Assert.That(FontLoader.Load(font).Faces[0].Data.Vmtx, Is.Null);
    }

    /// <summary>Two faces sharing one <c>vmtx</c> table but reading it with different <c>vhea</c> counts (the same-inputs rule).</summary>
    [Test]
    public void Load_CollectionSharedVmtxConflictingInputs_Throws()
    {
        var font = WithVertical(CraftedFonts.Arial(), vhea => vhea, vmtx => vmtx);
        var first = CraftedFonts.ReadTables(font);
        var second = first.Select(table =>
        {
            if (table.Tag == "vmtx")
                return table;

            var copy = (byte[])table.Data.Clone();
            if (table.Tag == "vhea")
                CraftedFonts.WriteU16(copy, 34, CraftedFonts.U16(copy, 34) - 1);

            return new CraftedTable(table.Tag, copy);
        }).ToList();

        Assert.That(() => FontLoader.Load(CraftedPrograms.SharedCollection([first, second])),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("share the 'vmtx' table"));
    }

    /// <summary>Two faces sharing <c>vhea</c> and <c>vmtx</c> read them with the same inputs and load.</summary>
    [Test]
    public void Load_CollectionSharedVerticalTablesSameInputs_Loads()
    {
        var font = WithVertical(CraftedFonts.Arial(), vhea => vhea, vmtx => vmtx);
        var first = CraftedFonts.ReadTables(font);
        var second = first.Select(table => table.Tag is "vhea" or "vmtx" ? table : new CraftedTable(table.Tag, (byte[])table.Data.Clone()))
            .ToList();
        var faces = FontLoader.Load(CraftedPrograms.SharedCollection([first, second])).Faces;

        Assert.Multiple(() =>
        {
            Assert.That(faces, Has.Count.EqualTo(2));
            Assert.That(faces[1].Data.Vmtx, Is.SameAs(faces[0].Data.Vmtx), "one parsed copy");
        });
    }

    /// <summary>Arial with a <c>vhea</c> (all glyphs long metrics) and a <c>vmtx</c>, each passed through a change.</summary>
    private static byte[] WithVertical(byte[] font, Func<byte[], byte[]> vhea, Func<byte[], byte[]> vmtx)
    {
        var glyphCount = CraftedFonts.U16(CraftedFonts.Table(font, "maxp"), 4);
        var header = new byte[36];
        CraftedFonts.WriteU32(header, 0, 0x00011000);
        CraftedFonts.WriteU16(header, 34, glyphCount);
        var metrics = new byte[4 * glyphCount];
        for (var g = 0; g < glyphCount; g++)
        {
            CraftedFonts.WriteU16(metrics, 4 * g, 2048);
            CraftedFonts.WriteU16(metrics, 4 * g + 2, 100 + g % 50);
        }

        font = CraftedFonts.WithTable(font, "vhea", vhea(header));
        return CraftedFonts.WithTable(font, "vmtx", vmtx(metrics));
    }
}
