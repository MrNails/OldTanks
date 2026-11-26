using CoolEngine.Fonts.Rasterization;
using CoolEngine.Fonts.Tests.Reference;

namespace CoolEngine.Fonts.Tests;

[TestFixture]
public sealed class RasterizerTests
{
    private const int Size = 16;
    private const int Pixel = 64;

    private FreeTypeReference m_freeType = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => m_freeType = new FreeTypeReference();

    [OneTimeTearDown]
    public void OneTimeTearDown() => m_freeType.Dispose();

    /// <summary>Synthetic outlines in a 16 × 16 bitmap, in 26.6 coordinates.</summary>
    private static IEnumerable<TestCaseData> Outlines()
    {
        yield return Case("square on pixel edges", [Square(2 * Pixel, 2 * Pixel, 10 * Pixel, 10 * Pixel)]);
        yield return Case("square offset by 1/64", [Square(2 * Pixel + 1, 2 * Pixel + 1, 10 * Pixel + 1, 10 * Pixel + 1)]);
        yield return Case("square offset by 1/2 pixel", [Square(2 * Pixel + 32, 2 * Pixel + 32, 10 * Pixel + 32, 10 * Pixel + 32)]);
        yield return Case("thin triangle", [[On(64, 64), On(15 * Pixel, 2 * Pixel + 5), On(80, 100)]]);
        yield return Case("two overlapping contours wound alike",
            [Square(2 * Pixel, 2 * Pixel, 9 * Pixel, 9 * Pixel), Square(5 * Pixel + 17, 5 * Pixel + 9, 13 * Pixel + 3, 14 * Pixel)]);
        yield return Case("two overlapping contours wound opposite",
            [Square(2 * Pixel, 2 * Pixel, 9 * Pixel, 9 * Pixel), Reverse(Square(5 * Pixel + 17, 5 * Pixel + 9, 13 * Pixel + 3, 14 * Pixel))]);
        yield return Case("quadratic circle", [QuadraticCircle(8 * Pixel + 7, 8 * Pixel - 5, 6 * Pixel + 21)]);
        yield return Case("cubic circle", [CubicCircle(8 * Pixel - 3, 8 * Pixel + 11, 6 * Pixel + 13)]);
        yield return Case("self-intersecting bow-tie", [[On(Pixel + 10, Pixel + 20), On(15 * Pixel, 15 * Pixel - 7), On(15 * Pixel - 3, Pixel), On(Pixel, 15 * Pixel)]]);
        yield return Case("1/64-wide sliver", [Square(7 * Pixel + 30, Pixel, 7 * Pixel + 31, 15 * Pixel)]);
    }

    [TestCaseSource(nameof(Outlines))]
    public void Render_SyntheticOutline_MatchesFreeType(GlyphOutline outline)
    {
        var ours = GlyphRenderer.Rasterize(outline, Size, Size);
        var expected = m_freeType.RenderOutline(outline, Size, Size);

        Assert.Multiple(() =>
        {
            Assert.That(ours, Is.EqualTo(expected));
            Assert.That(expected.Any(pixel => pixel != 0), Is.True, "the case draws something");
        });
    }

    private static TestCaseData Case(string name, OutlinePoint[][] contours)
    {
        var points = contours.SelectMany(contour => contour).ToArray();
        var ends = new int[contours.Length];
        var end = -1;
        for (var i = 0; i < contours.Length; i++)
        {
            end += contours[i].Length;
            ends[i] = end;
        }

        return new TestCaseData(new GlyphOutline(points, ends)).SetArgDisplayNames(name);
    }

    private static OutlinePoint On(int x, int y) => new(x, y, OutlinePointKind.OnCurve);

    /// <summary>A clockwise rectangle, as TrueType winds outer contours.</summary>
    private static OutlinePoint[] Square(int left, int bottom, int right, int top) =>
        [On(left, bottom), On(left, top), On(right, top), On(right, bottom)];

    private static OutlinePoint[] Reverse(OutlinePoint[] contour) => contour.Reverse().ToArray();

    /// <summary>Eight quadratic control points on the circle's circumscribed octagon, the on-curve points implied midway.</summary>
    private static OutlinePoint[] QuadraticCircle(int cx, int cy, int r)
    {
        var points = new OutlinePoint[8];
        var outer = r / Math.Cos(Math.PI / 8);
        for (var i = 0; i < 8; i++)
        {
            var angle = -Math.PI * 2 * i / 8;
            points[i] = new OutlinePoint(cx + (int)Math.Round(outer * Math.Cos(angle)), cy + (int)Math.Round(outer * Math.Sin(angle)),
                OutlinePointKind.Quadratic);
        }

        return points;
    }

    /// <summary>Four cubic arcs with the usual 0.5523 handle length.</summary>
    private static OutlinePoint[] CubicCircle(int cx, int cy, int r)
    {
        var k = (int)Math.Round(r * 0.5522847498);
        OutlinePoint Cubic(int x, int y) => new(x, y, OutlinePointKind.Cubic);

        return
        [
            On(cx + r, cy), Cubic(cx + r, cy - k), Cubic(cx + k, cy - r),
            On(cx, cy - r), Cubic(cx - k, cy - r), Cubic(cx - r, cy - k),
            On(cx - r, cy), Cubic(cx - r, cy + k), Cubic(cx - k, cy + r),
            On(cx, cy + r), Cubic(cx + k, cy + r), Cubic(cx + r, cy + k),
        ];
    }
}
