using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// Variable fonts (OT-4 WP5) against FreeType 2.13.2 given the same design coordinates (<c>FT_Set_Var_Design_Coordinates</c>,
/// every axis, then <c>FT_Set_Pixel_Sizes</c>): at every named instance, at each axis's minimum, default and maximum and at
/// interior points, the game's characters and a sample of every glyph at 9, 16, 32 and 72 px, unhinted and (TrueType) hinted
/// in v35 and v40: outlines, metrics, line metrics and bitmaps identical value for value.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed partial class VariationTests
{
    private const int MaxReported = 5;

    /// <summary>The sizes of the comparisons.</summary>
    private static readonly int[] s_sizes = [9, 16, 32, 72];

    /// <summary>About this many glyphs of each face besides the game's characters (every n-th glyph).</summary>
    private const int SampleGlyphs = 160;

    /// <summary>The variable test faces.</summary>
    internal static IEnumerable<TestFace> VariableFaces() =>
    [
        new("SitkaVF.ttf", TestFonts.Windows("SitkaVF.ttf"), 0),
        new("bahnschrift.ttf", TestFonts.Windows("bahnschrift.ttf"), 0),
        new("SourceSans3VF-Upright.ttf", TestFonts.SourceSans("SourceSans3VF-Upright.ttf"), 0),
        new("SourceSans3VF-Upright.otf", TestFonts.SourceSans("SourceSans3VF-Upright.otf"), 0),
    ];

    /// <summary>How a comparison loads its glyphs.</summary>
    public enum Mode
    {
        Unhinted,
        V35,
        V40,
    }

    /// <summary>One set of coordinates to compare at: a label and the user-space value of every axis.</summary>
    public sealed record Setting(string Label, float[] Values)
    {
        public override string ToString() => Label;
    }

    /// <summary>
    /// The coordinates each face is compared at: every named instance; each axis at its minimum, default and maximum (the
    /// others at their defaults); each axis at a quarter and three quarters of its range; all axes at a third of their ranges.
    /// </summary>
    internal static IEnumerable<Setting> Settings(FontFace face)
    {
        var axes = face.Axes;
        var defaults = axes.Select(axis => axis.Default).ToArray();
        for (var i = 0; i < face.NamedInstances.Count; i++)
            yield return new Setting($"instance {i} '{face.NamedInstances[i].Name}'", face.NamedInstances[i].Coordinates.ToArray());

        yield return new Setting("defaults", defaults);
        for (var a = 0; a < axes.Count; a++)
        {
            var axis = axes[a];
            foreach (var (label, value) in new[]
                     {
                         ("minimum", axis.Minimum), ("maximum", axis.Maximum),
                         ("quarter", axis.Minimum + (axis.Maximum - axis.Minimum) / 4),
                         ("three quarters", axis.Minimum + 3 * (axis.Maximum - axis.Minimum) / 4),
                     })
            {
                var values = (float[])defaults.Clone();
                values[a] = value;
                yield return new Setting($"{axis.Tag} {label} {value}", values);
            }
        }

        yield return new Setting("all at a third", axes.Select(axis => axis.Minimum + (axis.Maximum - axis.Minimum) / 3).ToArray());
    }

    private static IEnumerable<TestCaseData> FacesModesAndSettings()
    {
        foreach (var testFace in VariableFaces())
        {
            var face = FontCache.Face(testFace);
            var modes = face.OutlineFormat == OutlineFormat.TrueType ? new[] { Mode.Unhinted, Mode.V35, Mode.V40 } : [Mode.Unhinted];
            foreach (var mode in modes)
            {
                foreach (var setting in Settings(face))
                    yield return new TestCaseData(testFace, mode, setting).SetArgDisplayNames(testFace.Name, mode.ToString(), setting.Label);
            }
        }
    }

    /// <summary>
    /// Every comparison of one face, mode and setting: at each size, the line metrics, and for each glyph of the set its
    /// outline, metrics and bitmap; where FreeType's load fails, ours must throw <see cref="InvalidDataException"/>.
    /// </summary>
    [TestCaseSource(nameof(FacesModesAndSettings))]
    public void Load_AtCoordinates_OutlinesMetricsLineMetricsAndBitmapsMatchFreeType(TestFace testFace, Mode mode, Setting setting)
    {
        var face = FontCache.Face(testFace);
        var data = FontCache.Bytes(testFace.Path);
        var result = Compare(face, data, testFace.FaceIndex, setting.Values, Glyphs(face), mode);
        TestContext.Out.WriteLine(result.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(result.LineMetricsDifferences, Is.Empty, "line metrics");
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.Positive, "some glyphs load in FreeType");
            Assert.That(result.Bitmaps.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    /// <summary>The game's characters and every n-th glyph, so about <see cref="SampleGlyphs"/> more.</summary>
    internal static int[] Glyphs(FontFace face)
    {
        var step = Math.Max(1, face.GlyphCount / SampleGlyphs);
        return TestFonts.GameCharacters().Select(codePoint => face.GetGlyphIndex(codePoint))
            .Concat(Enumerable.Range(0, face.GlyphCount).Where(glyph => glyph % step == 0))
            .Distinct().Order().ToArray();
    }

    /// <summary>The variation options of a setting: every axis by its tag.</summary>
    internal static VariationCoordinate[] Coordinates(FontFace face, float[] values) =>
        face.Axes.Select((axis, i) => new VariationCoordinate(axis.Tag, values[i])).ToArray();

    /// <summary>The 16.16 design coordinates FreeType is given for a setting, converted as the library converts them.</summary>
    internal static long[] Design(float[] values) =>
        values.Select(value => (long)Math.Round(value * 65536.0, MidpointRounding.AwayFromZero)).ToArray();

    /// <summary>Loads each glyph once on each side, in the same order, at each size, and compares every part.</summary>
    internal static Comparison Compare(FontFace face, byte[] data, int faceIndex, float[] values, IEnumerable<int> glyphs, Mode mode)
    {
        var result = new Comparison();
        using var freeType = new FreeTypeVariations(mode == Mode.V35 ? 35 : 40);
        foreach (var size in s_sizes)
        {
            using var reference = freeType.Open(data, faceIndex, Design(values), size);
            var options = new ScaledFontOptions(size)
            {
                Hinting = mode == Mode.Unhinted ? HintingMode.None : HintingMode.Native,
                Interpreter = mode == Mode.V35 ? TrueTypeInterpreterVersion.V35 : TrueTypeInterpreterVersion.V40,
                Variation = Coordinates(face, values),
            };
            var font = face.CreateScaledFont(options);
            if (font.LineMetrics != reference.SizeMetrics)
                result.LineMetricsDifferences.Add($"{size}px: {font.LineMetrics}, FreeType {reference.SizeMetrics}");

            foreach (var glyph in glyphs)
            {
                var error = reference.TryLoad(glyph, mode != Mode.Unhinted, render: true, out var expected);
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
        }

        return result;
    }

    /// <summary>Counts and describes the glyphs whose load differs from FreeType's.</summary>
    internal sealed class Comparison
    {
        private readonly List<string> m_reported = [];

        public List<string> LineMetricsDifferences { get; } = [];

        public int Compared { get; private set; }

        public int Differences { get; private set; }

        public int BothFailed { get; private set; }

        public BitmapComparison Bitmaps { get; } = new();

        public void Add(int size, int glyph, FT_Error error, FreeTypeGlyph? expected, object ours)
        {
            if (error != FT_Error.FT_Err_Ok)
            {
                if (ours is InvalidDataException)
                    BothFailed++;
                else
                    Report(size, glyph, $"FreeType fails ({error}), ours loads");

                return;
            }

            if (ours is InvalidDataException exception)
            {
                Report(size, glyph, $"ours throws ({exception.Message}), FreeType loads");
                return;
            }

            Compared++;
            var (outline, metrics, bitmap) = ((GlyphOutline, GlyphMetrics, GlyphBitmap))ours;
            if (ScaledFontTests.Compare(outline, expected!.Outline) is { } difference)
            {
                Report(size, glyph, difference);
                return;
            }

            if (metrics != expected.Metrics)
            {
                Report(size, glyph, $"{metrics}, FreeType {expected.Metrics}");
                return;
            }

            var before = Bitmaps.Count;
            Bitmaps.Add(glyph, bitmap, expected.Bitmap!);
            if (Bitmaps.Count != before)
                Differences++;
        }

        public override string ToString() =>
            $"{Differences} glyph loads differ ({Compared} compared, {BothFailed} fail in both): " +
            (m_reported.Count > 0 ? string.Join("; ", m_reported) : Bitmaps.ToString());

        private void Report(int size, int glyph, string what)
        {
            Differences++;
            if (m_reported.Count < MaxReported)
                m_reported.Add($"{size}px glyph {glyph}: {what}");
        }
    }
}
