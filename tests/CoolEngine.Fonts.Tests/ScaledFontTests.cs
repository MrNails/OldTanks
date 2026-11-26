using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

/// <summary>Which glyphs a case covers: every glyph of the face, or the game's characters.</summary>
public enum GlyphSet
{
    Every,
    GameCharacters,
}

[TestFixture]
public sealed class ScaledFontTests
{
    private const int MaxReported = 5;

    private FreeTypeReference m_freeType = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => m_freeType = new FreeTypeReference();

    [OneTimeTearDown]
    public void OneTimeTearDown() => m_freeType.Dispose();

    /// <summary>Every TrueType face: every glyph at 9, 12, 16, 32 and 72 px, the game's characters at the other sizes 8..24.</summary>
    private static IEnumerable<TestCaseData> FacesAndSizes()
    {
        foreach (var face in TestFonts.TrueTypeFaces())
        {
            foreach (var size in TestFonts.EveryGlyphSizes)
                yield return new TestCaseData(face, size, GlyphSet.Every).SetArgDisplayNames(face.Name, $"{size}px", "every glyph");

            foreach (var size in TestFonts.CharacterSizes.Except(TestFonts.EveryGlyphSizes))
                yield return new TestCaseData(face, size, GlyphSet.GameCharacters).SetArgDisplayNames(face.Name, $"{size}px", "game characters");
        }
    }

    private static IEnumerable<TestCaseData> FacesAndAllSizes()
    {
        foreach (var face in TestFonts.TrueTypeFaces())
        {
            foreach (var size in TestFonts.CharacterSizes.Union(TestFonts.EveryGlyphSizes).Order())
                yield return new TestCaseData(face, size).SetArgDisplayNames(face.Name, $"{size}px");
        }
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetOutline_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(testFace, size);
        using var _ = reference;
        var mismatches = new List<string>();
        var count = 0;

        foreach (var glyph in Glyphs(font.Face, glyphs))
        {
            var ours = font.GetOutline(glyph);
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false).Outline;
            var difference = Compare(ours, expected);
            if (difference is null)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {difference}");
        }

        Assert.That(count, Is.Zero, $"{count} outlines differ: {string.Join("; ", mismatches)}");
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void GetMetrics_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(testFace, size);
        using var _ = reference;
        var mismatches = new List<string>();
        var count = 0;

        foreach (var glyph in Glyphs(font.Face, glyphs))
        {
            var ours = font.GetMetrics(glyph);
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false).Metrics;
            if (ours == expected)
                continue;

            if (count++ < MaxReported)
                mismatches.Add($"glyph {glyph}: {ours}, FreeType {expected}");
        }

        Assert.That(count, Is.Zero, $"{count} glyphs' metrics differ: {string.Join("; ", mismatches)}");
    }

    [TestCaseSource(nameof(FacesAndAllSizes))]
    public void LineMetrics_Unhinted_MatchFreeType(TestFace testFace, int size)
    {
        var (font, reference) = Open(testFace, size);
        using var _ = reference;

        Assert.That(font.LineMetrics, Is.EqualTo(reference.SizeMetrics));
    }

    [TestCaseSource(nameof(FacesAndSizes))]
    public void Render_Unhinted_EveryGlyph_MatchesFreeType(TestFace testFace, int size, GlyphSet glyphs)
    {
        var (font, reference) = Open(testFace, size);
        using var _ = reference;
        var comparison = new BitmapComparison();

        foreach (var glyph in Glyphs(font.Face, glyphs))
            comparison.Add(glyph, font.Render(glyph), reference.LoadGlyph(glyph, hinting: false, render: true).Bitmap!);

        Assert.Multiple(() =>
        {
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
            Assert.That(comparison.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    /// <summary>
    /// No test font sets <c>OVERLAP_SIMPLE</c> or <c>OVERLAP_COMPOUND</c>, so a copy of Arial with the flag on every glyph checks
    /// FreeType's oversampled rendering of overlapping outlines.
    /// </summary>
    [TestCaseSource(typeof(TestFonts), nameof(TestFonts.EveryGlyphSizes))]
    public void Render_OverlapFlaggedCopy_MatchesFreeType(int size)
    {
        var data = OverlapFlagged(CraftedFonts.Arial());
        var face = FontLoader.Load(data).Faces[0];
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var comparison = new BitmapComparison();

        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
            comparison.Add(glyph, font.Render(glyph), reference.LoadGlyph(glyph, hinting: false, render: true).Bitmap!);

        Assert.Multiple(() =>
        {
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
            Assert.That(comparison.DrawnGlyphs, Is.Positive, "some glyphs draw pixels");
        });
    }

    /// <summary>
    /// No test font has anchor-point components or <c>SCALED_COMPONENT_OFFSET</c>, so crafted composites check them, with a
    /// 2 × 2 transform (FreeType's CORDIC <c>FT_Hypot</c>), an x/y scale and nested <c>USE_MY_METRICS</c>.
    /// </summary>
    [TestCaseSource(typeof(TestFonts), nameof(TestFonts.EveryGlyphSizes))]
    public void GetOutline_CraftedComposites_MatchFreeType(int size)
    {
        var (data, first, count) = CraftedComposites();
        var face = FontLoader.Load(data).Faces[0];
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var comparison = new BitmapComparison();
        var differences = new List<string>();

        for (var glyph = first; glyph < first + count; glyph++)
        {
            var expected = reference.LoadGlyph(glyph, hinting: false, render: true);
            if (Compare(font.GetOutline(glyph), expected.Outline) is { } outline)
                differences.Add($"glyph {glyph} outline: {outline}");

            if (font.GetMetrics(glyph) != expected.Metrics)
                differences.Add($"glyph {glyph} metrics: {font.GetMetrics(glyph)}, FreeType {expected.Metrics}");

            comparison.Add(glyph, font.Render(glyph), expected.Bitmap!);
        }

        Assert.Multiple(() =>
        {
            Assert.That(differences, Is.Empty);
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
            Assert.That(comparison.DrawnGlyphs, Is.EqualTo(count), "every crafted glyph draws pixels");
        });
    }

    /// <summary>
    /// At the largest pixel size a glyph is taller than one band of the rasteriser's cell grid, so it is rendered band by band,
    /// as FreeType renders it when its render pool overflows.
    /// </summary>
    [TestCase('W', false)]
    [TestCase('@', false)]
    [TestCase('g', true)]
    public void Render_GlyphTallerThanOneBand_MatchesFreeType(char character, bool overlap)
    {
        const int size = 4096;
        var data = overlap ? OverlapFlagged(CraftedFonts.Arial()) : CraftedFonts.Arial();
        var face = FontLoader.Load(data).Faces[0];
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var glyph = face.GetGlyphIndex(character);

        var bitmap = font.Render(glyph);
        var comparison = new BitmapComparison();
        comparison.Add(glyph, bitmap, reference.LoadGlyph(glyph, hinting: false, render: true).Bitmap!);

        var scale = overlap ? 4 : 1;
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Height * scale, Is.GreaterThan(Rasterization.CoverageRasterizer.BandHeight(bitmap.Width * scale)),
                "the glyph spans several bands");
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
        });
    }

    [TestCaseSource(typeof(TestFonts), nameof(TestFonts.EveryGlyphSizes))]
    public void Render_ColrGlyph_DrawsPlainOutline(int size)
    {
        var testFace = new TestFace("seguiemj.ttf", TestFonts.Windows("seguiemj.ttf"), 0);
        var (font, reference) = Open(testFace, size);
        using var _ = reference;
        var glyph = font.Face.GetGlyphIndex(0x1F600);
        Assert.That(glyph, Is.Not.Zero, "U+1F600 is mapped");

        var comparison = new BitmapComparison();
        var bitmap = font.Render(glyph);
        comparison.Add(glyph, bitmap, reference.LoadGlyph(glyph, hinting: false, render: true).Bitmap!);

        Assert.Multiple(() =>
        {
            Assert.That(comparison.Count, Is.Zero, comparison.ToString());
            Assert.That(bitmap.Pixels.Any(pixel => pixel != 0), Is.True, "the plain outline is drawn");
        });
    }

    /// <summary>
    /// Crafted squares at 4096 px: one of 32,000 × 32,000 pixels, past the 16384² limit; one of 65,534 × 65,534, inside the
    /// ±32,767 box rule but past what an int holds (it used to overflow). Both throw before anything is allocated.
    /// </summary>
    [TestCase(8000)]
    [TestCase(16383)]
    public void Render_BitmapOverMaxGlyphPixelCount_ThrowsNotSupported(int halfSide)
    {
        // A left side bearing equal to the x minimum keeps the square centred on the origin.
        var left = halfSide == 16383 ? -16384 : -halfSide;
        var square = CraftedTables.SimpleGlyphFromPoints([(left, -halfSide), (left, halfSide), (halfSide, halfSide), (halfSide, -halfSide)]);
        var (data, glyph) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 1, (short)left, square);
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(4096) { Hinting = HintingMode.None });

        var before = GC.GetAllocatedBytesForCurrentThread();
        var exception = Assert.Throws<NotSupportedException>(() => font.Render(glyph));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain($"more than {ScaledFont.MaxGlyphPixelCount} pixels"));
            Assert.That(allocated, Is.LessThan(1024 * 1024), "nothing large is allocated");
        });
    }

    /// <summary>
    /// A zigzag of 3,000 edges, each 16,000 pixels tall at 4096 px, across a 12,000 × 16,000 bitmap: every one of its 184 bands
    /// walks every edge. It used to render for minutes; the work budget stops it within a few seconds.
    /// </summary>
    [Test]
    public void Render_WorkOverBudget_ThrowsNotSupported()
    {
        var points = Enumerable.Range(0, 3000).Select(i => (2 * i, i % 2 == 0 ? 0 : 8000)).ToArray();
        var (data, glyph) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 1, CraftedTables.SimpleGlyphFromPoints(points));
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(4096) { Hinting = HintingMode.None });

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var exception = Assert.Throws<NotSupportedException>(() => font.Render(glyph));
        watch.Stop();
        TestContext.Out.WriteLine($"The zigzag failed after {watch.Elapsed.TotalSeconds:F2} s.");

        Assert.That(exception!.Message, Does.Contain("work units"));
    }

    private static IEnumerable<TestCaseData> FreeTypeOverflowCases()
    {
        yield return new TestCaseData("segoeui.ttf", 2048, 159).SetArgDisplayNames("segoeui.ttf", "2048px", "glyph 159");
        yield return new TestCaseData("segoeui.ttf", 2048, 1043).SetArgDisplayNames("segoeui.ttf", "2048px", "glyph 1043");
        yield return new TestCaseData("seguiemj.ttf", 1024, 21584).SetArgDisplayNames("seguiemj.ttf", "1024px", "glyph 21584");
        yield return new TestCaseData("seguiemj.ttf", 1024, 22632).SetArgDisplayNames("seguiemj.ttf", "1024px", "glyph 22632");
        yield return new TestCaseData("segoeui.ttf", 4096, 159).SetArgDisplayNames("segoeui.ttf", "4096px", "glyph 159");
        yield return new TestCaseData("segoeui.ttf", 4096, 983).SetArgDisplayNames("segoeui.ttf", "4096px", "glyph 983");
        yield return new TestCaseData("seguiemj.ttf", 1500, 38240).SetArgDisplayNames("seguiemj.ttf", "1500px", "glyph 38240");
        yield return new TestCaseData("seguiemj.ttf", 1500, 47627).SetArgDisplayNames("seguiemj.ttf", "1500px", "glyph 47627");
        yield return new TestCaseData(null, 15, -1).SetArgDisplayNames("crafted 2,062-pixel-wide sliver", "15px");
    }

    /// <summary>
    /// FreeType's smooth rasteriser gives up (<c>Raster_Overflow</c>) when one pixel row holds more cells than its render pool;
    /// ours renders (Denys: "Ours works, documented"). The crafted case: a copy of Arial with 16 units per em and a 2,200-unit
    /// wide sliver at 15 px whose sloped top edge crosses about 2,000 cells within one pixel row.
    /// </summary>
    [TestCaseSource(nameof(FreeTypeOverflowCases))]
    public void Render_GlyphWhereFreeTypeOverflows_Renders(string? windowsFont, int size, int glyph)
    {
        byte[] data;
        if (windowsFont is null)
        {
            var bar = CraftedTables.SimpleGlyphFromPoints([(0, 1), (2200, 2), (2200, 1)]);
            var small = CraftedFonts.Patch(CraftedFonts.Arial(), "head", head => CraftedFonts.WriteU16(head, 18, 16));
            (data, glyph) = CraftedFonts.AppendGlyphs(small, 1, bar);
        }
        else
        {
            data = FontCache.Bytes(TestFonts.Windows(windowsFont));
        }

        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var (load, render) = reference.TryLoadAndRender(glyph);
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });

        var bitmap = font.Render(glyph);

        Assert.Multiple(() =>
        {
            Assert.That(load, Is.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Ok), "FreeType loads the glyph");
            Assert.That(render, Is.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Raster_Overflow), "FreeType's rasteriser overflows");
            Assert.That(bitmap.Pixels.Any(pixel => pixel != 0), Is.True, "ours renders the glyph");
        });
    }

    /// <summary>
    /// FreeType refuses to oversample an overlapping glyph more than 8,191 pixels wide (its spans are 16-bit) and returns
    /// <c>Raster_Overflow</c>; ours renders it (Denys: "Ours works, documented"). The crafted glyph is an 8,400 × 4 pixel bar on
    /// whole pixels at 4096 px, whose coverage is exact both ways, so the flagged glyph renders equal to the same bar unflagged,
    /// as FreeType renders that.
    /// </summary>
    [Test]
    public void Render_OverlapGlyphWiderThan8191Px_Renders()
    {
        const int size = 4096;
        var bar = CraftedTables.SimpleGlyphFromPoints([(0, 0), (0, 2), (4200, 2), (4200, 0)]);
        var flagged = (byte[])bar.Clone();
        flagged[10 + 2 + 2] |= 0x40; // OVERLAP_SIMPLE on the first point's flag
        var (data, plain) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 1, bar, flagged);
        var overlapping = plain + 1;

        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(size);
        var (_, flaggedError) = reference.TryLoadAndRender(overlapping);
        var expected = reference.LoadGlyph(plain, hinting: false, render: true).Bitmap!;
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });

        var ours = font.Render(overlapping);

        Assert.Multiple(() =>
        {
            Assert.That(flaggedError, Is.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Raster_Overflow), "FreeType refuses the overlapping bar");
            Assert.That(ours.Width, Is.GreaterThan(8191));
            Assert.That((ours.Width, ours.Height, ours.Left, ours.Top), Is.EqualTo((expected.Width, expected.Height, expected.Left, expected.Top)));
            Assert.That(ours.Pixels, Is.EqualTo(expected.Pixels), "the same coverage as the bar without the flag");
        });
    }

    /// <summary>
    /// FreeType refuses an outline over 32,763 points (its point count is a signed 16-bit value with four phantom points); ours
    /// takes up to 65,535 (Denys: "Ours works, documented"). The crafted glyph is a sawtooth of 32,800 points.
    /// </summary>
    [Test]
    public void Render_OutlineOver32763Points_Renders()
    {
        var points = Enumerable.Range(0, 32800).Select(i => (i, i % 2 == 0 ? 0 : 200)).ToArray();
        var (data, glyph) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 1, CraftedTables.SimpleGlyphFromPoints(points));
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(16);
        var (load, _) = reference.TryLoadAndRender(glyph);
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });

        var outline = font.GetOutline(glyph);
        var bitmap = font.Render(glyph);

        Assert.Multiple(() =>
        {
            Assert.That(load, Is.Not.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Ok), "FreeType refuses the glyph");
            Assert.That(outline.Points.Length, Is.EqualTo(32800));
            Assert.That(bitmap.Pixels.Any(pixel => pixel != 0), Is.True, "ours renders the glyph");
        });
    }

    /// <summary>
    /// FreeType treats these faces as bitmap-only: it ignores <c>glyf</c> beside a <c>CBLC</c>/<c>CBDT</c> or <c>sbix</c>
    /// strike, and a face whose only outline is <c>.notdef</c> with an <c>EBLC</c> strike is not scalable; so a pixel size no
    /// strike has fails (<c>Invalid_Pixel_Size</c>). Ours renders the <c>glyf</c> outlines (Denys: "Ours works, documented";
    /// Phase B review). Each strike is 20 px; the glyph is rendered at 32 px.
    /// </summary>
    [TestCase("glyf + CBLC/CBDT strike")]
    [TestCase("glyf + sbix strike")]
    [TestCase(".notdef-only glyf + EBLC strike")]
    public void Render_OutlinesFreeTypeTreatsAsBitmapOnly_Renders(string variant)
    {
        const int size = 32;
        var arial = CraftedFonts.Arial();
        var glyphCount = CraftedFonts.U16(CraftedFonts.Table(arial, "maxp"), 4);
        var (data, glyph) = variant switch
        {
            "glyf + CBLC/CBDT strike" => (CraftedFonts.WithTable(CraftedFonts.WithTable(arial, "CBLC", CraftedTables.BitmapLocations(3, 20)),
                "CBDT", CraftedTables.BitmapData(3)), 36),
            "glyf + sbix strike" => (CraftedFonts.WithTable(arial, "sbix", CraftedTables.Sbix(glyphCount, 20)), 36),
            _ => (CraftedFonts.WithTable(CraftedFonts.WithTable(NotdefOnly(arial), "EBLC", CraftedTables.BitmapLocations(2, 20)),
                "EBDT", CraftedTables.BitmapData(2)), 0),
        };

        using var reference = m_freeType.OpenFace(data, 0);
        var error = reference.TrySetPixelSize(size);
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });

        var bitmap = font.Render(glyph);

        Assert.Multiple(() =>
        {
            Assert.That(reference.IsScalable, Is.False, "FreeType: not scalable");
            Assert.That(reference.NumFixedSizes, Is.EqualTo(1), "FreeType: one strike");
            Assert.That(error, Is.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Invalid_Pixel_Size), "FreeType refuses 32 px");
            Assert.That(font.Face.OutlineFormat, Is.EqualTo(OutlineFormat.TrueType));
            Assert.That(bitmap.Pixels.Any(pixel => pixel != 0), Is.True, "ours renders the glyph");
        });
    }

    /// <summary>
    /// FreeType refuses a simple glyph of 4,095 or more contours (<c>Invalid_Outline</c>: its contour count must stay below
    /// 0xFFF); ours takes any count its 65,535 points allow (Denys: "Ours works, documented"; Phase B review). The crafted glyph
    /// is 4,095 thin triangles side by side.
    /// </summary>
    [Test]
    public void Render_SimpleGlyphWith4095Contours_Renders()
    {
        var contours = Enumerable.Range(0, 4095).Select(k => new[] { (2 * k, 0), (2 * k + 1, 400), (2 * k + 2, 0) }).ToArray();
        var (data, glyph) = CraftedFonts.AppendGlyphs(CraftedFonts.Arial(), 1, CraftedTables.SimpleGlyphFromPoints(contours));
        using var reference = m_freeType.OpenFace(data, 0);
        reference.SetPixelSize(16);
        var (load, _) = reference.TryLoadAndRender(glyph);
        var font = FontLoader.Load(data).Faces[0].CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });

        var outline = font.GetOutline(glyph);
        var bitmap = font.Render(glyph);

        Assert.Multiple(() =>
        {
            Assert.That(load, Is.EqualTo(FreeTypeSharp.FT_Error.FT_Err_Invalid_Outline), "FreeType refuses the glyph");
            Assert.That(outline.ContourEndPoints.Length, Is.EqualTo(4095));
            Assert.That(bitmap.Pixels.Any(pixel => pixel != 0), Is.True, "ours renders the glyph");
        });
    }

    /// <summary>A copy whose only outlined glyph is <c>.notdef</c> (glyph 0): every other glyph is made empty.</summary>
    private static byte[] NotdefOnly(byte[] font) =>
        CraftedFonts.WithGlyphs(font, CraftedFonts.ReadGlyphs(font).Select((glyph, index) => index == 0 ? glyph : []).ToList());

    [TestCase(0)]
    [TestCase(4097)]
    public void CreateScaledFont_PixelSizeOutOfRange_Throws(int size)
    {
        var face = FontCache.Face(new TestFace("Arial", TestFonts.RepositoryArial, 0));

        Assert.That(() => face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None }),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void GetOutline_GlyphIndexOutOfRange_Throws()
    {
        var face = FontCache.Face(new TestFace("Arial", TestFonts.RepositoryArial, 0));
        var font = face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });

        Assert.Multiple(() =>
        {
            Assert.That(() => font.GetOutline(face.GlyphCount), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => font.GetOutline(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    private (ScaledFont Font, FreeTypeFace Reference) Open(TestFace testFace, int size)
    {
        var face = FontCache.Face(testFace);
        var font = face.CreateScaledFont(new ScaledFontOptions(size) { Hinting = HintingMode.None });
        var reference = m_freeType.OpenFace(FontCache.Bytes(testFace.Path), testFace.FaceIndex);
        reference.SetPixelSize(size);

        return (font, reference);
    }

    /// <summary>A copy with <c>OVERLAP_SIMPLE</c> on every simple glyph's first point and <c>OVERLAP_COMPOUND</c> on every first component.</summary>
    private static byte[] OverlapFlagged(byte[] font)
    {
        var glyphs = CraftedFonts.ReadGlyphs(font);
        foreach (var glyph in glyphs.Where(glyph => glyph.Length >= 10))
        {
            var contours = (short)CraftedFonts.U16(glyph, 0);
            if (contours > 0)
            {
                var flags = 10 + 2 * contours + 2 + CraftedFonts.U16(glyph, 10 + 2 * contours);
                glyph[flags] |= 0x40;
            }
            else if (contours < 0)
            {
                CraftedFonts.WriteU16(glyph, 10, CraftedFonts.U16(glyph, 10) | 0x0400);
            }
        }

        return CraftedFonts.WithGlyphs(font, glyphs);
    }

    /// <summary>Arial with composites appended: anchors, scaled offsets, transforms and nested <c>USE_MY_METRICS</c>.</summary>
    private static (byte[] Font, int First, int Count) CraftedComposites()
    {
        const int words = 0x0001, xy = 0x0002, scale = 0x0008, more = 0x0020, xyScale = 0x0040, twoByTwo = 0x0080;
        const int useMyMetrics = 0x0200, scaledOffset = 0x0800;

        var arial = CraftedFonts.Arial();
        var face = FontLoader.Load(arial).Faces[0];
        var o = face.GetGlyphIndex('o');
        var acute = face.GetGlyphIndex(0xB4);
        var bar = face.GetGlyphIndex('|');
        var first = face.GlyphCount;

        // Anchors: point 0 of the acute meets point 3 of the 'o'.
        var anchored = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(words | xy | more).U16(o).U16(0).U16(0)
            .U16(words | more).U16(acute).U16(3).U16(0)
            .U16(words).U16(bar).U16(7).U16(1)
            .ToArray();

        // A uniform scale with SCALED_COMPONENT_OFFSET, then an x/y scale with it, the second carrying USE_MY_METRICS.
        var scaled = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(words | xy | scale | scaledOffset | more).U16(o).U16(301).U16(-157 & 0xFFFF).U16(0x3000)
            .U16(words | xy | xyScale | scaledOffset | useMyMetrics).U16(acute).U16(-211 & 0xFFFF).U16(477).U16(0x4CCD).U16(0x2E14)
            .ToArray();

        // 2 x 2 transforms (a shear and a rotation-like matrix) with and without SCALED_COMPONENT_OFFSET.
        var transformed = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(words | xy | twoByTwo | scaledOffset | more).U16(o).U16(613).U16(97).U16(0x399A).U16(0x0CCD).U16(0xECCD).U16(0x4666)
            .U16(words | xy | twoByTwo).U16(bar).U16(-389 & 0xFFFF).U16(251).U16(0x2D41).U16(0xD2BF).U16(0x2D41).U16(0x2D41)
            .ToArray();

        // A composite of the composites above, taking the metrics of the second (which takes its own component's).
        var nested = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(0).U16(0)
            .U16(words | xy | more).U16(first).U16(50).U16(0)
            .U16(words | xy | useMyMetrics).U16(first + 1).U16(-40 & 0xFFFF).U16(30)
            .ToArray();

        var (font, start) = CraftedFonts.AppendGlyphs(arial, 2, anchored, scaled, transformed, nested);
        return (font, start, 4);
    }

    private static IEnumerable<int> Glyphs(FontFace face, GlyphSet set) => set == GlyphSet.Every
        ? Enumerable.Range(0, face.GlyphCount)
        : TestFonts.GameCharacters().Select(codePoint => face.GetGlyphIndex(codePoint)).Distinct().Order();

    internal static string? Compare(GlyphOutline ours, FreeTypeOutline expected)
    {
        var points = ours.Points.Span;
        var contours = ours.ContourEndPoints.Span;
        if (!contours.SequenceEqual(expected.ContourEnds))
            return $"contour ends [{string.Join(", ", contours.ToArray())}], FreeType [{string.Join(", ", expected.ContourEnds)}]";

        if (points.Length != expected.Points.Length)
            return $"{points.Length} points, FreeType {expected.Points.Length}";

        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] != expected.Points[i])
                return $"point {i} {points[i]}, FreeType {expected.Points[i]}";
        }

        return null;
    }
}

/// <summary>Collects bitmap differences: glyphs that differ, and by how much where the boxes agree.</summary>
internal sealed class BitmapComparison
{
    private const int MaxReported = 5;

    private readonly List<string> m_reported = [];
    private int m_differentPixels;
    private long m_differenceSum;
    private int m_maxDifference;
    private int m_comparedPixels;

    public int Count { get; private set; }

    /// <summary>Glyphs whose reference bitmap has at least one covered pixel, so a pass is not vacuous.</summary>
    public int DrawnGlyphs { get; private set; }

    public void Add(int glyph, GlyphBitmap ours, FreeTypeBitmap expected)
    {
        if (expected.Pixels.Any(pixel => pixel != 0))
            DrawnGlyphs++;

        if ((ours.Width, ours.Height, ours.Left, ours.Top) != (expected.Width, expected.Height, expected.Left, expected.Top))
        {
            Report(glyph, $"box {ours.Width}x{ours.Height} at ({ours.Left}, {ours.Top}), FreeType {expected.Width}x{expected.Height} at ({expected.Left}, {expected.Top})");
            return;
        }

        var differing = 0;
        for (var i = 0; i < ours.Pixels.Length; i++)
        {
            var difference = Math.Abs(ours.Pixels[i] - expected.Pixels[i]);
            if (difference == 0)
                continue;

            differing++;
            m_differenceSum += difference;
            m_maxDifference = Math.Max(m_maxDifference, difference);
        }

        if (differing == 0)
            return;

        m_differentPixels += differing;
        m_comparedPixels += ours.Pixels.Length;
        Report(glyph, $"{differing} of {ours.Pixels.Length} pixels differ");
    }

    public override string ToString() =>
        $"{Count} bitmaps differ; {m_differentPixels} pixels differ (of {m_comparedPixels} in those glyphs), max {m_maxDifference}, " +
        $"mean {(m_differentPixels == 0 ? 0 : (double)m_differenceSum / m_differentPixels):F2} over differing pixels: {string.Join("; ", m_reported)}";

    private void Report(int glyph, string what)
    {
        if (Count++ < MaxReported)
            m_reported.Add($"glyph {glyph}: {what}");
    }
}
