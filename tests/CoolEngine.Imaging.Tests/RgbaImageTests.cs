namespace CoolEngine.Imaging.Tests;

[TestFixture]
public sealed class RgbaImageTests
{
    [Test]
    public void Constructor_PixelCountMismatch_ThrowsArgumentException()
    {
        var pixels = new Rgba32[3];

        Assert.Throws<ArgumentException>(() => _ = new RgbaImage(2, 2, pixels));
    }

    [Test]
    public void Indexer_ReturnsRowMajorPixel()
    {
        var pixels = Enumerable.Range(0, 6).Select(i => new Rgba32((byte)i, 0, 0, 255)).ToArray();
        var image = new RgbaImage(3, 2, pixels);

        var pixel = image[2, 1];

        Assert.That(pixel, Is.EqualTo(new Rgba32(5, 0, 0, 255)));
    }

    [TestCase(-1, 0)]
    [TestCase(3, 0)]
    [TestCase(0, -1)]
    [TestCase(0, 2)]
    public void Indexer_OutOfRange_ThrowsArgumentOutOfRangeException(int x, int y)
    {
        var image = new RgbaImage(3, 2, new Rgba32[6]);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = image[x, y]);
    }
}
