using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;

// The hinter fixtures run in parallel (each test owns its FreeType library and its ScaledFont; the font cache is concurrent).
[assembly: LevelOfParallelism(16)]

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// The TrueType bytecode hinter (OT-4 WP4) against FreeType 2.13.2 set to the same interpreter version: hinted outlines,
/// metrics, line metrics and bitmaps of every glyph of every hinted test face, identical value for value; a glyph whose load
/// fails in FreeType's default mode throws <see cref="InvalidDataException"/> here.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed partial class TrueTypeHinterTests
{
    private const int MaxReported = 5;

    /// <summary>The hinted TrueType faces: the plan's list, with mingliub.ttc (a FreeType "tricky" font, vhea and vmtx).</summary>
    internal static IEnumerable<TestFace> HintedFaces() =>
    [
        new("Arial (repository)", TestFonts.RepositoryArial, 0),
        new("Times New Roman (repository)", TestFonts.RepositoryTimes, 0),
        new("SourceSans3-Regular.ttf", TestFonts.SourceSans("SourceSans3-Regular.ttf"), 0),
        new("arial.ttf", TestFonts.Windows("arial.ttf"), 0),
        new("segoeui.ttf", TestFonts.Windows("segoeui.ttf"), 0),
        new("cambria.ttc #0", TestFonts.Windows("cambria.ttc"), 0),
        new("cambria.ttc #1", TestFonts.Windows("cambria.ttc"), 1),
        new("SitkaVF.ttf", TestFonts.Windows("SitkaVF.ttf"), 0),
        new("bahnschrift.ttf", TestFonts.Windows("bahnschrift.ttf"), 0),
        new("mingliub.ttc #0", TestFonts.Windows("mingliub.ttc"), 0),
    ];

    private static IEnumerable<TestCaseData> FacesAndSizes()
    {
        foreach (var face in HintedFaces())
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
                yield return new TestCaseData(face, size, GlyphSet.Every).SetArgDisplayNames(face.Name, $"{size}px", "every glyph");

            foreach (var size in TestFonts.CharacterSizes.Except(TestFonts.EveryGlyphSizes))
                yield return new TestCaseData(face, size, GlyphSet.GameCharacters)
                    .SetArgDisplayNames(face.Name, $"{size}px", "game characters");
        }
    }

    private static IEnumerable<TestCaseData> FacesAndAllSizes()
    {
        foreach (var face in HintedFaces())
        {
            foreach (var size in TestFonts.CharacterSizes.Union(TestFonts.EveryGlyphSizes).Order())
                yield return new TestCaseData(face, size).SetArgDisplayNames(face.Name, $"{size}px");
        }
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetOutline_V40_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V40, Part.Outline);

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetOutline_V35_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V35, Part.Outline);

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetMetrics_V40_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V40, Part.Metrics);

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetMetrics_V35_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V35, Part.Metrics);

    [TestCaseSource(nameof(FacesAndSizes))]
    public void Render_V40_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V40, Part.Bitmap);

    [TestCaseSource(nameof(FacesAndSizes))]
    public void Render_V35_EveryGlyph_MatchesFreeType(TestFace face, int size, GlyphSet glyphs) =>
        AssertGlyphsMatch(face, size, glyphs, TrueTypeInterpreterVersion.V35, Part.Bitmap);

    [TestCaseSource(nameof(FacesAndAllSizes))]
    public void LineMetrics_V40_MatchFreeType(TestFace face, int size) =>
        AssertLineMetricsMatch(face, size, TrueTypeInterpreterVersion.V40);

    [TestCaseSource(nameof(FacesAndAllSizes))]
    public void LineMetrics_V35_MatchFreeType(TestFace face, int size) =>
        AssertLineMetricsMatch(face, size, TrueTypeInterpreterVersion.V35);

    /// <summary>What a comparison checks.</summary>
    internal enum Part
    {
        Outline,
        Metrics,
        Bitmap,
    }

    /// <summary>
    /// Loads each glyph once on each side, in the same order (the twilight zone persists between glyphs, in FreeType and here),
    /// and compares the part; where FreeType's load fails, ours must throw <see cref="InvalidDataException"/>.
    /// </summary>
    internal static void AssertGlyphsMatch(TestFace testFace, int size, GlyphSet set, TrueTypeInterpreterVersion version, Part part)
    {
        var face = FontCache.Face(testFace);
        var data = FontCache.Bytes(testFace.Path);
        var result = CompareGlyphs(face, data, testFace.FaceIndex, Glyphs(face, set), size, version, part);

        Assert.Multiple(() =>
        {
            Assert.That(result.Differences, Is.Zero, result.ToString());
            Assert.That(result.Compared, Is.Positive, "some glyphs load in FreeType");
            if (part == Part.Bitmap)
                Assert.That(result.Bitmaps.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    /// <summary>The comparison of <see cref="AssertGlyphsMatch"/> over any font bytes.</summary>
    internal static ComparisonResult CompareGlyphs(FontFace face, byte[] data, int faceIndex, IEnumerable<int> glyphs, int size,
        TrueTypeInterpreterVersion version, Part part)
    {
        using var freeType = new FreeTypeHinting(FreeTypeHinting.VersionNumber(version));
        using var reference = freeType.Open(data, faceIndex, size);
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Interpreter = version });
        var result = new ComparisonResult();

        foreach (var glyph in glyphs)
        {
            var error = reference.TryLoad(glyph, part == Part.Bitmap, out var expected);
            object? ours;
            try
            {
                ours = part switch
                {
                    Part.Outline => font.GetOutline(glyph),
                    Part.Metrics => font.GetMetrics(glyph),
                    _ => font.Render(glyph),
                };
            }
            catch (InvalidDataException exception)
            {
                ours = exception;
            }

            result.Add(glyph, error, expected, ours, part);
        }

        return result;
    }

    private static void AssertLineMetricsMatch(TestFace testFace, int size, TrueTypeInterpreterVersion version)
    {
        var face = FontCache.Face(testFace);
        using var freeType = new FreeTypeHinting(FreeTypeHinting.VersionNumber(version));
        using var reference = freeType.Open(FontCache.Bytes(testFace.Path), testFace.FaceIndex, size);
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Interpreter = version });

        Assert.That(font.LineMetrics, Is.EqualTo(reference.SizeMetrics));
    }

    internal static IEnumerable<int> Glyphs(FontFace face, GlyphSet set) => set == GlyphSet.Every
        ? Enumerable.Range(0, face.GlyphCount)
        : TestFonts.GameCharacters().Select(codePoint => face.GetGlyphIndex(codePoint)).Distinct().Order();

    /// <summary>Counts and describes the glyphs whose hinted load differs from FreeType's.</summary>
    internal sealed class ComparisonResult
    {
        private readonly List<string> m_reported = [];

        public int Compared { get; private set; }

        public int Differences { get; private set; }

        /// <summary>Glyphs whose load fails in FreeType and throws here.</summary>
        public int BothFailed { get; private set; }

        public BitmapComparison Bitmaps { get; } = new();

        public void Add(int glyph, FT_Error error, FreeTypeGlyph? expected, object? ours, Part part)
        {
            if (error != FT_Error.FT_Err_Ok)
            {
                if (ours is InvalidDataException)
                    BothFailed++;
                else
                    Report(glyph, $"FreeType fails ({error}), ours loads");

                return;
            }

            if (ours is InvalidDataException exception)
            {
                Report(glyph, $"ours throws ({exception.Message}), FreeType loads");
                return;
            }

            Compared++;
            switch (part)
            {
                case Part.Outline:
                    if (ScaledFontTests.Compare((GlyphOutline)ours!, expected!.Outline) is { } difference)
                        Report(glyph, difference);
                    break;

                case Part.Metrics:
                    if ((GlyphMetrics)ours! != expected!.Metrics)
                        Report(glyph, $"{ours}, FreeType {expected.Metrics}");
                    break;

                default:
                    var before = Bitmaps.Count;
                    Bitmaps.Add(glyph, (GlyphBitmap)ours!, expected!.Bitmap!);
                    if (Bitmaps.Count != before)
                        Differences++;
                    break;
            }
        }

        public override string ToString() =>
            $"{Differences} glyphs differ ({Compared} compared, {BothFailed} fail in both): " +
            (m_reported.Count > 0 ? string.Join("; ", m_reported) : Bitmaps.ToString());

        private void Report(int glyph, string what)
        {
            Differences++;
            if (m_reported.Count < MaxReported)
                m_reported.Add($"glyph {glyph}: {what}");
        }
    }
}
