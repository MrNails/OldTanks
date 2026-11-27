using System.Text;
using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// CFF and CFF2 hinting (OT-4 WP6, <see cref="HintingMode.Native"/> on a CFF face) against FreeType 2.13.2's Adobe engine with
/// the cff driver's defaults: every glyph of each CFF face reachable without a download (Source Sans 3 OTF, its CFF2 variable
/// font at the default instance, at every named instance and at each axis's extremes, and every CFF face of the
/// <c>*.otf</c> files in <c>C:\Windows\Fonts</c>) at 9, 12, 16, 32 and 72 px, and the game's characters at every size from 8 to 24 px: outlines,
/// metrics, line metrics and bitmaps identical value for value. Where FreeType's load fails, ours must throw
/// <see cref="InvalidDataException"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class CffHintingTests
{
    /// <summary>
    /// The CFF faces: the two Source Sans 3 OTFs in the repository (the fixed set) and every CFF or CFF2 face of the
    /// <c>*.otf</c> files in <c>C:\Windows\Fonts</c> on the machine running the tests (on the machine of OT-4 WP6, only
    /// <c>ROGFonts-Regular_0.otf</c>, a vendor font not shipped with Windows).
    /// </summary>
    internal static IEnumerable<TestFace> Faces() => [.. TestFonts.CffFaces(), .. WindowsCffFaces()];

    /// <summary>Every CFF or CFF2 face of the <c>*.otf</c> files in <c>C:\Windows\Fonts</c>, in file name order.</summary>
    internal static IEnumerable<TestFace> WindowsCffFaces()
    {
        foreach (var path in Directory.EnumerateFiles(TestFonts.WindowsFonts, "*.otf").Order(StringComparer.OrdinalIgnoreCase))
        {
            var faces = FontLoader.Load(FontCache.Bytes(path)).Faces;
            for (var i = 0; i < faces.Count; i++)
            {
                if (faces[i].OutlineFormat is OutlineFormat.Cff or OutlineFormat.Cff2)
                    yield return new TestFace(faces.Count > 1 ? $"{Path.GetFileName(path)} #{i}" : Path.GetFileName(path), path, i);
            }
        }
    }

    private static IEnumerable<TestCaseData> FacesAndSizes()
    {
        foreach (var face in Faces())
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
                yield return new TestCaseData(face, size, true).SetArgDisplayNames(face.Name, $"{size}px", "every glyph");

            foreach (var size in TestFonts.CharacterSizes.Except(TestFonts.EveryGlyphSizes))
                yield return new TestCaseData(face, size, false).SetArgDisplayNames(face.Name, $"{size}px", "game characters");
        }
    }

    private static IEnumerable<TestCaseData> FacesAndAllSizes()
    {
        foreach (var face in Faces())
        {
            foreach (var size in TestFonts.CharacterSizes.Union(TestFonts.EveryGlyphSizes).Order())
                yield return new TestCaseData(face, size).SetArgDisplayNames(face.Name, $"{size}px");
        }
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void Load_Native_EveryGlyph_OutlinesMetricsAndBitmapsMatchFreeType(TestFace testFace, int size, bool everyGlyph)
    {
        var face = FontCache.Face(testFace);
        var data = FontCache.Bytes(testFace.Path);

        var result = Compare(face, data, testFace.FaceIndex, null, size, Glyphs(face, everyGlyph));
        TestContext.Out.WriteLine(result.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(result.LineMetricsDifferences, Is.Empty, "line metrics");
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.Positive, "some glyphs load in FreeType");
            Assert.That(result.Bitmaps.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    [TestCaseSource(nameof(FacesAndAllSizes))]
    public void LineMetrics_Native_MatchFreeType(TestFace testFace, int size)
    {
        var face = FontCache.Face(testFace);
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, null, size);

        var font = face.CreateScaledFont(new ScaledFontOptions(size));

        Assert.That(font.LineMetrics, Is.EqualTo(reference.SizeMetrics));
    }

    /// <summary>
    /// The CFF2 variable font at every named instance and at each axis's minimum and maximum: its Private DICT's blue zones and
    /// stem values blended at the coordinates, as FreeType's <c>cff_load_private_dict</c> re-reads it for the size's vector.
    /// </summary>
    private static IEnumerable<TestCaseData> InstancesAndSizes()
    {
        var testFace = TestFonts.CffFaces().Single(face => face.Name.StartsWith("SourceSans3VF", StringComparison.Ordinal));
        var face = FontCache.Face(testFace);
        foreach (var setting in Settings(face))
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
                yield return new TestCaseData(testFace, setting, size, true).SetArgDisplayNames(setting.Label, $"{size}px", "every glyph");

            foreach (var size in TestFonts.CharacterSizes.Except(TestFonts.EveryGlyphSizes))
                yield return new TestCaseData(testFace, setting, size, false).SetArgDisplayNames(setting.Label, $"{size}px", "game characters");
        }
    }

    [TestCaseSource(nameof(InstancesAndSizes))]
    public void Load_NativeAtCoordinates_EveryGlyph_OutlinesMetricsAndBitmapsMatchFreeType(TestFace testFace,
        VariationTests.Setting setting, int size, bool everyGlyph)
    {
        var face = FontCache.Face(testFace);
        var data = FontCache.Bytes(testFace.Path);

        var result = Compare(face, data, testFace.FaceIndex, setting.Values, size, Glyphs(face, everyGlyph));
        TestContext.Out.WriteLine(result.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(result.LineMetricsDifferences, Is.Empty, "line metrics");
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.Positive, "some glyphs load in FreeType");
        });
    }

    /// <summary>
    /// The reference's cff driver runs with FreeType 2.13.2's defaults, which this library reproduces: the Adobe hinting
    /// engine (<c>hinting-engine</c> <c>FT_HINTING_ADOBE</c>, 1) and no stem darkening (<c>no-stem-darkening</c> true).
    /// </summary>
    [Test]
    public unsafe void CffDriver_Properties_AreFreeType2132Defaults()
    {
        FT_LibraryRec_* library;
        FreeTypeReference.Check(FT_Init_FreeType(&library), "FT_Init_FreeType");
        try
        {
            var engine = GetProperty(library, "hinting-engine");
            var noStemDarkening = GetProperty(library, "no-stem-darkening");
            int major, minor, patch;
            FT_Library_Version(library, &major, &minor, &patch);
            var version = new Version(major, minor, patch);

            Assert.Multiple(() =>
            {
                Assert.That(version, Is.EqualTo(new Version(2, 13, 2)));
                Assert.That(engine, Is.EqualTo(1), "FT_HINTING_ADOBE");
                Assert.That(noStemDarkening, Is.EqualTo(1), "no stem darkening");
            });
        }
        finally
        {
            FT_Done_FreeType(library);
        }

        static uint GetProperty(FT_LibraryRec_* library, string name)
        {
            var module = Encoding.ASCII.GetBytes("cff\0");
            var property = Encoding.ASCII.GetBytes(name + "\0");
            uint value = 0xDEADBEEF;
            fixed (byte* modulePointer = module)
            fixed (byte* propertyPointer = property)
                FreeTypeReference.Check(FT_Property_Get(library, modulePointer, propertyPointer, &value), "FT_Property_Get");

            // FT_Bool properties write one byte.
            return name == "no-stem-darkening" ? value & 0xFF : value;
        }
    }

    /// <summary>Every named instance and each axis at its minimum and maximum (the others at their defaults).</summary>
    internal static IEnumerable<VariationTests.Setting> Settings(FontFace face)
    {
        var axes = face.Axes;
        var defaults = axes.Select(axis => axis.Default).ToArray();
        for (var i = 0; i < face.NamedInstances.Count; i++)
            yield return new VariationTests.Setting($"instance {i} '{face.NamedInstances[i].Name}'", face.NamedInstances[i].Coordinates.ToArray());

        for (var a = 0; a < axes.Count; a++)
        {
            foreach (var (label, value) in new[] { ("minimum", axes[a].Minimum), ("maximum", axes[a].Maximum) })
            {
                var values = (float[])defaults.Clone();
                values[a] = value;
                yield return new VariationTests.Setting($"{axes[a].Tag} {label} {value}", values);
            }
        }
    }

    /// <summary>Every glyph, or the game's characters.</summary>
    internal static int[] Glyphs(FontFace face, bool everyGlyph) => everyGlyph
        ? Enumerable.Range(0, face.GlyphCount).ToArray()
        : TestFonts.GameCharacters().Select(codePoint => face.GetGlyphIndex(codePoint)).Distinct().Order().ToArray();

    /// <summary>
    /// Loads each glyph once on each side, hinted, in the same order, at one size (and the design coordinates, if any), and
    /// compares the line metrics and every glyph's outline, metrics and bitmap.
    /// </summary>
    internal static VariationTests.Comparison Compare(FontFace face, byte[] data, int faceIndex, float[]? values, int size,
        IEnumerable<int> glyphs)
    {
        var result = new VariationTests.Comparison();
        using var freeType = new FreeTypeVariations();
        using var reference = freeType.Open(data, faceIndex, values is null ? null : VariationTests.Design(values), size);
        var options = new ScaledFontOptions(size)
        {
            Hinting = HintingMode.Native,
            Variation = values is null ? [] : VariationTests.Coordinates(face, values),
        };
        var font = face.CreateScaledFont(options);
        if (font.LineMetrics != reference.SizeMetrics)
            result.LineMetricsDifferences.Add($"{size}px: {font.LineMetrics}, FreeType {reference.SizeMetrics}");

        foreach (var glyph in glyphs)
        {
            var error = reference.TryLoad(glyph, hinting: true, render: true, out var expected);
            object ours;
            try
            {
                ours = (font.GetOutline(glyph), font.GetMetrics(glyph), font.Render(glyph));
            }
            catch (InvalidDataException exception)
            {
                ours = exception;
            }

            result.Add(size, glyph, error, expected, ours);
        }

        return result;
    }
}
