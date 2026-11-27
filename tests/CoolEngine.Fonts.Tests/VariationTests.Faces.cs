using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Variations;
using static CoolEngine.Fonts.Tests.Reference.CraftedVariations;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// A variable face's axes and named instances against FreeType's <c>FT_MM_Var</c> and DirectWrite; coordinate normalization
/// against FreeType's blend coordinates; <see cref="FontFace.IsVariable"/> as FreeType decides it; the options' checks.
/// </summary>
public sealed partial class VariationTests
{
    private static IEnumerable<TestFace> VariableFacesAndOthers() => VariableFaces().Concat(TestFonts.AllFaces()).DistinctBy(face => face.Name);

    [TestCaseSource(nameof(VariableFaces))]
    public void Axes_EveryVariableFace_MatchFreeTypeMMVar(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var freeType = new FreeTypeVariations();
        var (axes, _) = freeType.ReadMMVar(FontCache.Bytes(testFace.Path), testFace.FaceIndex);

        Assert.Multiple(() =>
        {
            Assert.That(face.IsVariable, Is.True);
            Assert.That(face.Axes.Select(axis => axis.Tag), Is.EqualTo(axes.Select(axis => axis.Tag)));
            Assert.That(face.Axes.Select(axis => VariationInstance.ToFixed(axis.Minimum)), Is.EqualTo(axes.Select(axis => axis.Minimum)));
            Assert.That(face.Axes.Select(axis => VariationInstance.ToFixed(axis.Default)), Is.EqualTo(axes.Select(axis => axis.Default)));
            Assert.That(face.Axes.Select(axis => VariationInstance.ToFixed(axis.Maximum)), Is.EqualTo(axes.Select(axis => axis.Maximum)));
            Assert.That(face.Axes.Select(axis => axis.IsHidden), Is.EqualTo(axes.Select(axis => (axis.Flags & 1) != 0)));
        });
    }

    /// <summary>
    /// The named instances are <c>FT_MM_Var</c>'s, without the default instance FreeType synthesizes (last) when none of
    /// <c>fvar</c>'s instances lies at the defaults: a documented difference.
    /// </summary>
    [TestCaseSource(nameof(VariableFaces))]
    public void NamedInstances_EveryVariableFace_MatchFreeTypeMMVarWithoutSynthesizedDefault(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var freeType = new FreeTypeVariations();
        var (axes, instances) = freeType.ReadMMVar(FontCache.Bytes(testFace.Path), testFace.FaceIndex);
        var defaults = axes.Select(axis => axis.Default).ToArray();
        var synthesized = face.NamedInstances.All(instance => !Design([.. instance.Coordinates]).SequenceEqual(defaults));
        var expected = synthesized ? instances[..^1] : instances;

        Assert.Multiple(() =>
        {
            Assert.That(face.NamedInstances.Select(instance => Design([.. instance.Coordinates])),
                Is.EqualTo(expected.Select(instance => instance.Coordinates)));
            if (synthesized)
                Assert.That(instances[^1].Coordinates, Is.EqualTo(defaults), "FreeType's synthesized default instance");
        });
    }

    [TestCaseSource(nameof(VariableFaces))]
    public void AxesAndNamedInstances_EveryVariableFace_MatchDirectWrite(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        using var directWrite = new DirectWriteReference();
        var (axes, instances) = DirectWriteVariations.Read(directWrite, testFace.Path, testFace.FaceIndex);

        Assert.Multiple(() =>
        {
            Assert.That(face.Axes.Select(axis => (axis.Tag, axis.Minimum, axis.Default, axis.Maximum, axis.IsHidden, axis.Name)),
                Is.EqualTo(axes.Select(axis => (axis.Tag, axis.Minimum, axis.Default, axis.Maximum, axis.IsHidden, axis.Name))));
            Assert.That(face.NamedInstances.Select(instance => instance.Coordinates.ToArray()),
                Is.EqualTo(instances.Select(instance => instance.Coordinates)));
            Assert.That(face.NamedInstances.Select(instance => instance.Name), Is.EqualTo(instances.Select(instance => instance.Name)));
        });
    }

    /// <summary>
    /// The normalized coordinates (after <c>avar</c>) of user values across each axis, out-of-range and fractional ones
    /// among them, equal FreeType's <c>FT_Get_Var_Blend_Coordinates</c> after <c>FT_Set_Var_Design_Coordinates</c>.
    /// </summary>
    [TestCaseSource(nameof(VariableFaces))]
    public void Normalize_ValuesAcrossEveryAxis_MatchFreeTypeBlendCoordinates(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        var data = FontCache.Bytes(testFace.Path);
        using var freeType = new FreeTypeVariations();
        var differences = new List<string>();
        var compared = 0;
        for (var a = 0; a < face.Axes.Count; a++)
        {
            var axis = face.Axes[a];
            var span = axis.Maximum - axis.Minimum;
            for (var step = -4; step <= 44; step++)
            {
                var values = face.Axes.Select(other => other.Default).ToArray();
                values[a] = axis.Minimum + span * step / 40f + (step % 3) * 0.0137f;
                var ours = VariationInstance.Create(face, face.Data.Variations!, Coordinates(face, values)).Normalized;
                using var reference = freeType.Open(data, testFace.FaceIndex, Design(values), 0);
                var expected = reference.BlendCoordinates(face.Axes.Count);
                compared++;
                if (!ours.SequenceEqual(expected))
                    differences.Add($"{axis.Tag} {values[a]}: [{string.Join(", ", ours)}], FreeType [{string.Join(", ", expected)}]");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(differences, Is.Empty);
            Assert.That(compared, Is.Positive);
        });
    }

    /// <summary>The glyphs at an axis's minimum and maximum differ: the comparisons at those coordinates are not vacuous.</summary>
    [TestCaseSource(nameof(VariableFaces))]
    public void CreateScaledFont_EachAxisExtreme_ChangesGlyphs(TestFace testFace)
    {
        var face = FontCache.Face(testFace);
        var glyphs = Glyphs(face);
        foreach (var axis in face.Axes.Where(axis => !axis.IsHidden))
        {
            var minimum = face.Axes.Select(other => other == axis ? axis.Minimum : other.Default).ToArray();
            var maximum = face.Axes.Select(other => other == axis ? axis.Maximum : other.Default).ToArray();

            Assert.That(Signature(face, minimum, glyphs, Mode.Unhinted), Is.Not.EqualTo(Signature(face, maximum, glyphs, Mode.Unhinted)),
                $"axis {axis.Tag}");
        }
    }

    /// <summary><see cref="FontFace.IsVariable"/> is FreeType's <c>FT_FACE_FLAG_MULTIPLE_MASTERS</c> for every test face.</summary>
    [TestCaseSource(nameof(VariableFacesAndOthers))]
    public void IsVariable_EveryTestFace_MatchesFreeType(TestFace testFace)
    {
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, null, 0);

        Assert.That(FontCache.Face(testFace).IsVariable, Is.EqualTo(reference.HasMultipleMasters));
    }

    /// <summary>
    /// An <c>fvar</c> whose header FreeType's check refuses makes the face not variable, in FreeType and here (each crafted case
    /// is well-formed apart from that header).
    /// </summary>
    [TestCase(0, 0x00020000u, TestName = "IsVariable_FvarVersion2_FalseAsFreeType")]
    [TestCase(10, 16u, TestName = "IsVariable_FvarAxisSize16_FalseAsFreeType")]
    [TestCase(8, 0u, TestName = "IsVariable_FvarNoAxes_FalseAsFreeType")]
    [TestCase(14, 9u, TestName = "IsVariable_FvarInstanceSizeOdd_FalseAsFreeType")]
    [TestCase(12, 5u, TestName = "IsVariable_FvarInstancesPastTable_FalseAsFreeType")]
    public void IsVariable_FvarHeaderFreeTypeRefuses_FalseAsFreeType(int offset, uint value)
    {
        var fvar = Fvar([("wght", 100, 400, 900, 0, 256)], [(257, [400.0])]);
        if (offset == 0)
            CraftedFonts.WriteU32(fvar, 0, value);
        else
            CraftedFonts.WriteU16(fvar, offset, (int)value);

        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "fvar", fvar);
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(font, 0, null, 0);
        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.IsVariable, Is.False.And.EqualTo(reference.HasMultipleMasters));
            Assert.That(face.Axes, Is.Empty);
            Assert.That(face.NamedInstances, Is.Empty);
        });
    }

    /// <summary>An axis whose minimum is above its default takes its default as all three values, as FreeType's <c>FT_MM_Var</c> gives it.</summary>
    [Test]
    public void Axes_RangeOutOfOrder_CollapsedToDefaultAsFreeType()
    {
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "fvar",
            Fvar([("wght", 500, 400, 900, 0, 256), ("wdth", 50, 100, 150, 1, 257)], []));
        using var freeType = new FreeTypeVariations();
        var (axes, _) = freeType.ReadMMVar(font, 0);
        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.Axes[0], Is.EqualTo(new VariationAxis("wght", 400, 400, 400, "", false)));
            Assert.That(face.Axes[1], Is.EqualTo(new VariationAxis("wdth", 50, 100, 150, "", true)));
            Assert.That(face.Axes.Select(axis => VariationInstance.ToFixed(axis.Minimum)), Is.EqualTo(axes.Select(axis => axis.Minimum)));
            Assert.That(face.Axes.Select(axis => VariationInstance.ToFixed(axis.Maximum)), Is.EqualTo(axes.Select(axis => axis.Maximum)));
        });
    }

    [Test]
    public void CreateScaledFont_UnknownAxisTag_Throws()
    {
        var face = FontCache.Face(VariableFaces().First());

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new VariationCoordinate("XXXX", 1)] }),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("no 'XXXX' axis"));
    }

    [Test]
    public void CreateScaledFont_RepeatedAxisTag_Throws()
    {
        var face = FontCache.Face(VariableFaces().First());
        var tag = face.Axes[0].Tag;

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new(tag, 1), new(tag, 2)] }),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("names one twice"));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void CreateScaledFont_ValueNotFinite_Throws(float value)
    {
        var face = FontCache.Face(VariableFaces().First());

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new(face.Axes[0].Tag, value)] }),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("not a finite number"));
    }

    [Test]
    public void CreateScaledFont_CoordinatesForNonVariableFace_Throws()
    {
        var face = FontCache.Face(new TestFace("Arial", TestFonts.RepositoryArial, 0));

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Variation = [new("wght", 700)] }),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("not variable"));
    }

    /// <summary>
    /// A CFF (not CFF2) face with a valid <c>fvar</c> is variable, as in FreeType; coordinates for it are a library limit
    /// (FreeType varies only its HVAR advances and MVAR metrics, and refuses coordinates when the fvar has named instances).
    /// </summary>
    [Test]
    public void CreateScaledFont_CoordinatesForCffFace_ThrowsNotSupported()
    {
        var otf = FontCache.Bytes(TestFonts.SourceSans("SourceSans3-Regular.otf"));
        var font = CraftedFonts.WithTable(otf, "fvar", Fvar([("wght", 200, 400, 900, 0, 256)], []));
        var face = FontLoader.Load(font).Faces[0];
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(font, 0, null, 0);

        Assert.Multiple(() =>
        {
            Assert.That(face.IsVariable, Is.True.And.EqualTo(reference.HasMultipleMasters));
            Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None, Variation = [new("wght", 700)] }),
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("not CFF2").And.Message.Contains("HVAR advances and MVAR metrics"));
        });
    }
}
