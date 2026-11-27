using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Variations;
using static CoolEngine.Fonts.Tests.Reference.CraftedVariations;

namespace CoolEngine.Fonts.Tests;

/// <summary>The CFF2 Private DICT at any coordinates (FreeType's <c>cff_load_private_dict</c> with a blend vector).</summary>
public sealed partial class VariationTests
{
    /// <summary>A StdHW entry of <c>100 40 -20 1 blend</c>: a value and its deltas for two regions of one axis.</summary>
    private static readonly byte[] s_blendedStdHw = [239, 179, 119, 140, 23, 10];

    /// <summary>
    /// The blended value is FreeType's sum: the default plus each delta times its region's weight (<c>FT_MulFix</c>), the
    /// weights of <c>cff_blend_build_vector</c>: at 0.5 the region (0, 1, 1) weighs 1/2 and (-1, -1, 0) nothing (100 + 20);
    /// at -0.25 the first nothing and the second 1/4 (100 - 5); at the default, both nothing.
    /// </summary>
    [TestCase(0x8000, 120)]
    [TestCase(-0x4000, 95)]
    [TestCase(0, 100)]
    public void Parse_BlendedPrivateEntryAtCoordinates_WeighsDeltasAsFreeType(int coordinate, int expected)
    {
        var storeBytes = Store(1, [[(0, 1, 1)], [(-1, -1, 0)]], ([0, 1], [], true));
        var parsed = new ParsedTables(storeBytes.Length, unlimited: true);
        var store = ItemVariationStore.Parse(storeBytes, 0, parsed);

        var entries = CffDict.Parse(s_blendedStdHw, true, "Private DICT", parsed, store, [coordinate]);

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Operator, Is.EqualTo(10));
            Assert.That(entries[0].Operands.Select(operand => (operand.Kind, operand.Fixed)), Is.EqualTo(new[] { (DictOperandKind.Blended, expected << 16) }));
        });
    }

    /// <summary>
    /// Source Sans 3 VF's Private DICT at its default coordinates reads as at load (every delta weighs 0); at its last named
    /// instance a blended value differs, and the local subroutines and vsindex stay those read at load.
    /// </summary>
    [Test]
    public void PrivateDictAt_SourceSansVariable_DefaultEqualsLoadAndInstanceBlends()
    {
        var face = FontCache.Face(VariableFaces().Single(face => face.Name.EndsWith(".otf", StringComparison.Ordinal)));
        var table = face.Data.Cff!;
        var instance = face.NamedInstances[^1];
        var coords = VariationInstance.Create(face, face.Data.Variations!, Coordinates(face, [.. instance.Coordinates])).Normalized;

        var atDefault = Describe(table.PrivateDictAt(0, new long[face.Axes.Count]));
        var atLoad = Describe(CffDict.Parse(table.Data.Span.Slice(table.FontDicts[0].PrivateOffset, table.FontDicts[0].PrivateSize), true,
            "Private DICT", new ParsedTables(table.Data.Length, unlimited: true), table.VariationStore));
        var atInstance = Describe(table.PrivateDictAt(0, coords));

        Assert.Multiple(() =>
        {
            Assert.That(table.IsCff2, Is.True);
            Assert.That(atDefault, Is.EqualTo(atLoad));
            Assert.That(atInstance, Is.Not.EqualTo(atLoad), "a blended entry varies");
            Assert.That(atInstance.Count, Is.EqualTo(atLoad.Count));
        });

        static List<string> Describe(List<DictEntry> entries) =>
            entries.Select(entry => $"{entry.Operator}: {string.Join(" ", entry.Operands.Select(operand => $"{operand.Kind}/{operand.Position}/{operand.Fixed}"))}").ToList();
    }
}
