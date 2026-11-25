namespace CoolEngine.Imaging.Tests;

[TestFixture]
public sealed class ImageDecoderTests
{
    private const string ContainerTexture = "Container.png";

    private static IEnumerable<TestCaseData> OtherData()
    {
        yield return new TestCaseData((object)Array.Empty<byte>()).SetArgDisplayNames("Empty");
        yield return new TestCaseData((object)"GIF89a"u8.ToArray()).SetArgDisplayNames("GIF89a");
        yield return new TestCaseData((object)new byte[] { 0xFF, 0xD8 }).SetArgDisplayNames("FF D8");
    }

    [Test]
    public void DetectFormat_PngSignature_ReturnsPng()
    {
        byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

        var format = ImageDecoder.DetectFormat(signature);

        Assert.That(format, Is.EqualTo(ImageFormat.Png));
    }

    [Test]
    public void DetectFormat_JpegStartOfImage_ReturnsJpeg()
    {
        byte[] startOfImage = [0xFF, 0xD8, 0xFF, 0xE0];

        var format = ImageDecoder.DetectFormat(startOfImage);

        Assert.That(format, Is.EqualTo(ImageFormat.Jpeg));
    }

    [TestCaseSource(nameof(OtherData))]
    public void DetectFormat_OtherData_ReturnsUnknown(byte[] data)
    {
        var format = ImageDecoder.DetectFormat(data);

        Assert.That(format, Is.EqualTo(ImageFormat.Unknown));
    }

    [Test]
    public void Decode_UnknownFormat_ThrowsNotSupportedException()
    {
        var data = new byte[64];
        "GIF89a"u8.CopyTo(data);

        Assert.Throws<NotSupportedException>(() => ImageDecoder.Decode(data));
    }

    [Test]
    public void Decode_FlipVertically_ReversesRowOrder()
    {
        var data = TestAssets.Read(ContainerTexture);

        var upright = ImageDecoder.Decode(data);
        var flipped = ImageDecoder.Decode(data, flipVertically: true);

        Assert.That((flipped.Width, flipped.Height), Is.EqualTo((upright.Width, upright.Height)));
        for (var y = 0; y < upright.Height; y++)
        {
            var uprightRow = upright.Pixels.AsSpan(y * upright.Width, upright.Width).ToArray();
            var flippedRow = flipped.Pixels.AsSpan((upright.Height - 1 - y) * upright.Width, upright.Width).ToArray();
            Assert.That(flippedRow, Is.EqualTo(uprightRow), $"Row {y}");
        }
    }

    [Test]
    public async Task LoadAsync_TextureAsset_MatchesLoad()
    {
        var path = Path.Combine(TestAssets.TexturesDirectory, ContainerTexture);

        var loadedAsync = await ImageDecoder.LoadAsync(path);
        var loaded = ImageDecoder.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That((loadedAsync.Width, loadedAsync.Height), Is.EqualTo((loaded.Width, loaded.Height)));
            Assert.That(loadedAsync.Pixels, Is.EqualTo(loaded.Pixels));
        });
    }
}
