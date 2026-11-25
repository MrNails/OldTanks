using System.IO.Compression;
using SixLabors.ImageSharp.Formats.Png;
using PngDecoder = CoolEngine.Imaging.Png.PngDecoder;

namespace CoolEngine.Imaging.Tests;

[TestFixture]
public sealed class PngDecoderTests
{
    private const string ContainerTexture = "Container.png";
    private const int VariantWidth = 37;
    private const int VariantHeight = 23;

    private const byte GrayscaleType = 0;
    private const byte RgbType = 2;
    private const byte PaletteType = 3;
    private const byte RgbaType = 6;

    private static readonly (PngColorType ColorType, PngBitDepth[] BitDepths)[] s_variants =
    [
        (PngColorType.Grayscale, [PngBitDepth.Bit1, PngBitDepth.Bit2, PngBitDepth.Bit4, PngBitDepth.Bit8, PngBitDepth.Bit16]),
        (PngColorType.Rgb, [PngBitDepth.Bit8, PngBitDepth.Bit16]),
        (PngColorType.Palette, [PngBitDepth.Bit1, PngBitDepth.Bit2, PngBitDepth.Bit4, PngBitDepth.Bit8]),
        (PngColorType.GrayscaleWithAlpha, [PngBitDepth.Bit8, PngBitDepth.Bit16]),
        (PngColorType.RgbWithAlpha, [PngBitDepth.Bit8, PngBitDepth.Bit16]),
    ];

    private static IEnumerable<TestCaseData> PngTextures() =>
        TestAssets.PngTextures().Select(path => new TestCaseData(path).SetArgDisplayNames(path));

    private static IEnumerable<TestCaseData> AllVariants() => Variants(maxBitDepth: 16);

    /// <summary>GDI+ treats 16-bit PNG samples as linear light, so it is compared only up to 8 bits.</summary>
    private static IEnumerable<TestCaseData> VariantsUpTo8Bits() => Variants(maxBitDepth: 8);

    private static IEnumerable<TestCaseData> Variants(int maxBitDepth)
    {
        foreach (var (colorType, bitDepths) in s_variants)
        {
            foreach (var bitDepth in bitDepths.Where(depth => (int)depth <= maxBitDepth))
            {
                foreach (var interlace in new[] { PngInterlaceMode.None, PngInterlaceMode.Adam7 })
                {
                    yield return new TestCaseData(colorType, bitDepth, interlace)
                        .SetArgDisplayNames(colorType.ToString(), bitDepth.ToString(), interlace.ToString());
                }
            }
        }
    }

    [TestCaseSource(nameof(PngTextures))]
    public void Decode_TextureAssets_MatchImageSharpExactly(string texture)
    {
        var data = TestAssets.Read(texture);

        var ours = PngDecoder.Decode(data, flipVertically: false);
        var reference = ReferenceDecoders.ImageSharp(data);

        AssertIdentical(ours, reference);
    }

    [TestCaseSource(nameof(PngTextures))]
    public void Decode_TextureAssets_MatchWindowsExactly(string texture)
    {
        var data = TestAssets.Read(texture);

        var ours = PngDecoder.Decode(data, flipVertically: false);
        var reference = ReferenceDecoders.Windows(data);

        AssertIdentical(ours, reference);
    }

    [TestCaseSource(nameof(AllVariants))]
    public void Decode_EncodedVariants_MatchImageSharpExactly(PngColorType colorType, PngBitDepth bitDepth, PngInterlaceMode interlace)
    {
        var data = TestImages.EncodePng(VariantWidth, VariantHeight, colorType, bitDepth, interlace);

        var ours = PngDecoder.Decode(data, flipVertically: false);
        var reference = ReferenceDecoders.ImageSharp(data);

        AssertIdentical(ours, reference);
    }

    [TestCaseSource(nameof(VariantsUpTo8Bits))]
    public void Decode_EncodedVariants_MatchWindowsExactly(PngColorType colorType, PngBitDepth bitDepth, PngInterlaceMode interlace)
    {
        var data = TestImages.EncodePng(VariantWidth, VariantHeight, colorType, bitDepth, interlace);

        var ours = PngDecoder.Decode(data, flipVertically: false);
        var reference = ReferenceDecoders.Windows(data);

        AssertIdentical(ours, reference);
    }

    [Test]
    public void Decode_GrayscaleWithTransparentColor_MakesMatchingSamplesTransparent()
    {
        var data = TestPng.Build(2, 1, 8, GrayscaleType, [[10, 20]], [("tRNS", [0, 20], false)]);

        var image = PngDecoder.Decode(data, flipVertically: false);

        Assert.That(image.Pixels, Is.EqualTo(new[] { new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 0) }));
    }

    [Test]
    public void Decode_RgbWithTransparentColor_MakesMatchingPixelsTransparent()
    {
        var data = TestPng.Build(2, 1, 8, RgbType, [[1, 2, 3, 4, 5, 6]], [("tRNS", [0, 4, 0, 5, 0, 6], false)]);

        var image = PngDecoder.Decode(data, flipVertically: false);

        Assert.That(image.Pixels, Is.EqualTo(new[] { new Rgba32(1, 2, 3, 255), new Rgba32(4, 5, 6, 0) }));
    }

    [Test]
    public void Decode_PaletteIndexPastPalette_ReturnsOpaqueBlack()
    {
        var data = TestPng.Build(1, 1, 8, PaletteType, [[5]], [("PLTE", [10, 20, 30, 40, 50, 60], false)]);

        var image = PngDecoder.Decode(data, flipVertically: false);

        Assert.That(image.Pixels, Is.EqualTo(new[] { new Rgba32(0, 0, 0, 255) }));
    }

    [Test]
    public void Decode_CorruptCriticalChunkCrc_ThrowsInvalidDataException()
    {
        var data = TestAssets.Read(ContainerTexture);

        // IHDR data starts at byte 16 (signature, length, type); flip a bit of the width's low byte.
        data[19] ^= 0x01;

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_TruncatedData_ThrowsInvalidDataException()
    {
        var data = TestAssets.Read(ContainerTexture);
        var firstHalf = data[..(data.Length / 2)];

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(firstHalf, flipVertically: false));
    }

    [Test]
    public void Decode_HeaderOverPixelLimit_ThrowsNotSupportedException()
    {
        var data = TestPng.Build(20000, 20000, 8, RgbaType, []);

        Assert.Throws<NotSupportedException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_InvalidBitDepthForColorType_ThrowsInvalidDataException()
    {
        var data = TestPng.Build(1, 1, 4, RgbType, [[0, 0]]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_UnknownCriticalChunk_ThrowsNotSupportedException()
    {
        var data = TestPng.Build(1, 1, 8, RgbaType, [[9, 8, 7, 6]], [("ABCD", [1, 2, 3], false)]);

        Assert.Throws<NotSupportedException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_UnknownAncillaryChunk_IsIgnored()
    {
        var data = TestPng.Build(1, 1, 8, RgbaType, [[9, 8, 7, 6]], [("abCd", [1, 2, 3], false)]);

        var image = PngDecoder.Decode(data, flipVertically: false);

        Assert.That(image.Pixels, Is.EqualTo(new[] { new Rgba32(9, 8, 7, 6) }));
    }

    [Test]
    public void Decode_AncillaryChunkWithBadCrc_ThrowsInvalidDataException()
    {
        var data = TestPng.Build(1, 1, 8, RgbaType, [[9, 8, 7, 6]], [("abCd", [1, 2, 3], true)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_ExtraDataAfterLastRow_ThrowsInvalidDataException()
    {
        // A 1x1 header with two scanlines in the zlib stream.
        var data = TestPng.Build(1, 1, 8, RgbaType, [[9, 8, 7, 6], [1, 2, 3, 4]]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_WrongAdler32_ThrowsInvalidDataException()
    {
        var imageData = TestPng.Deflate([[9, 8, 7, 6]]);

        // The Adler-32 is the last 4 bytes of the zlib stream.
        imageData[^1] ^= 0xFF;
        var data = TestPng.Assemble(1, 1, 8, RgbaType, imageData);

        var exception = Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.StartWith("PNG image data"), "our own message");
            Assert.That(exception.InnerException, Is.Not.Null, "the inflater's exception is kept as the inner exception");
        });
    }

    [TestCase(4)]
    [TestCase(256)]
    public void Decode_PaletteLongerThanBitDepthAllows_ThrowsInvalidDataException(int entries)
    {
        var palette = Enumerable.Range(0, entries * 3).Select(i => (byte)i).ToArray();
        var data = TestPng.Build(8, 1, 1, PaletteType, [[0b1011_0010]], [("PLTE", palette, false)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_TransparencyLongerThanPalette_ThrowsInvalidDataException()
    {
        var data = TestPng.Build(2, 1, 8, PaletteType, [[0, 1]], [("PLTE", [10, 20, 30, 40, 50, 60], false), ("tRNS", [0, 128, 255], false)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_PaletteInGrayscaleImage_ThrowsInvalidDataException()
    {
        var data = TestPng.Build(2, 1, 8, GrayscaleType, [[77, 200]], [("PLTE", [10, 20, 30], false)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_TransparencyAfterImageData_ThrowsInvalidDataException()
    {
        var data = TestPng.Assemble(2, 1, 8, GrayscaleType, TestPng.Deflate([[10, 20]]), chunksAfterIdat: [("tRNS", [0, 20], false)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_MissingIend_ThrowsInvalidDataException()
    {
        var data = TestPng.Assemble(1, 1, 8, RgbaType, TestPng.Deflate([[9, 8, 7, 6]]), writeEnd: false);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_InvalidPaletteLengthInRgbImage_ThrowsInvalidDataException()
    {
        var data = TestPng.Build(1, 1, 8, RgbType, [[4, 5, 6]], [("PLTE", [1, 2, 3, 4, 5, 6, 7], false)]);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_NonEmptyIend_ThrowsInvalidDataException()
    {
        var data = TestPng.Assemble(1, 1, 8, RgbaType, TestPng.Deflate([[9, 8, 7, 6]]), chunksAfterIdat: [("IEND", [1], false)], writeEnd: false);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    // PNG 3 §11.3.1.1: tRNS sample bits above the image's bit depth are ignored (masked off) by the decoder.
    [TestCase(GrayscaleType, (byte)8, new byte[] { 10, 20 }, new byte[] { 0x01, 0x14 }, 1, TestName = "{m}(gray 8-bit, tRNS 0x0114)")]
    [TestCase(GrayscaleType, (byte)4, new byte[] { 0x2F }, new byte[] { 0x00, 0x1F }, 1, TestName = "{m}(gray 4-bit, tRNS 0x001F)")]
    [TestCase(RgbType, (byte)8, new byte[] { 1, 2, 3, 4, 5, 6 }, new byte[] { 0x80, 0x04, 0x00, 0x05, 0x00, 0x06 }, 1, TestName = "{m}(RGB 8-bit, tRNS red 0x8004)")]
    public void Decode_TransparentColorWithHighBits_MasksThem(byte colorType, byte bitDepth, byte[] scanline, byte[] transparency, int transparentPixel)
    {
        var data = TestPng.Build(2, 1, bitDepth, colorType, [scanline], [("tRNS", transparency, false)]);

        var image = PngDecoder.Decode(data, flipVertically: false);

        var expectedAlpha = Enumerable.Range(0, image.Pixels.Length).Select(i => i == transparentPixel ? (byte)0 : (byte)255);
        Assert.That(image.Pixels.Select(pixel => pixel.A), Is.EqualTo(expectedAlpha));
    }

    [Test]
    public void Decode_ZlibPresetDictionary_ThrowsInvalidDataException()
    {
        // A zlib stream with FDICT set (and a valid FCHECK): CMF, FLG, DICTID, the deflate data, Adler-32.
        using var deflated = new MemoryStream();
        using (var deflate = new DeflateStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write([0, 9, 8, 7, 6]);

        const byte cmf = 0x78;
        var flg = (byte)0x20;
        while ((cmf * 256 + flg) % 31 != 0)
            flg++;

        byte[] imageData = [cmf, flg, 0, 0, 0, 1, .. deflated.ToArray(), 0, 0, 0, 0];
        var data = TestPng.Assemble(1, 1, 8, RgbaType, imageData);

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
    }

    [Test]
    public void Decode_ImageDataTooSmallForHeader_ThrowsWithoutLargeAllocation()
    {
        const long allocationLimit = 16L * 1024 * 1024;
        var data = TestPng.Build(16384, 16384, 8, RgbaType, []);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(data, flipVertically: false));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(allocationLimit));
    }

    private static void AssertIdentical(RgbaImage ours, (int Width, int Height, Rgba32[] Pixels) reference)
    {
        Assert.That((ours.Width, ours.Height), Is.EqualTo((reference.Width, reference.Height)), "Size");

        var difference = PixelComparison.Compare(ours.Pixels, reference.Pixels);
        var firstMismatch = -1;
        for (var i = 0; i < ours.Pixels.Length && firstMismatch < 0; i++)
        {
            if (ours.Pixels[i] != reference.Pixels[i])
                firstMismatch = i;
        }

        Assert.That(
            firstMismatch,
            Is.EqualTo(-1),
            () => $"{difference}; first mismatch at ({firstMismatch % ours.Width}, {firstMismatch / ours.Width}): " +
                  $"ours {ours.Pixels[firstMismatch]}, reference {reference.Pixels[firstMismatch]}");
    }
}
