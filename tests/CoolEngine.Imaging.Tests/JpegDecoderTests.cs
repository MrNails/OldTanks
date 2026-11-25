using CoolEngine.Imaging.Jpeg;
using SixLabors.ImageSharp;
using JpegColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegColorType;
using JpegEncoder = SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder;
using Rgba32 = CoolEngine.Imaging.Rgba32;

namespace CoolEngine.Imaging.Tests;

[TestFixture]
public sealed class JpegDecoderTests
{
    private const int VariantWidth = 61;
    private const int VariantHeight = 35;

    private const byte Sof0 = 0xC0;
    private const byte Sof1 = 0xC1;
    private const byte Sof2 = 0xC2;
    private const byte Dht = 0xC4;
    private const byte Sos = 0xDA;
    private const byte Dqt = 0xDB;
    private const byte App0 = 0xE0;

    // 4:2:0 for the crafted files: luma alternating by block, flat chroma.
    private static readonly CraftedJpeg.Component[] s_crafted420 =
    [
        new(1, 2, 2, (row, column) => 128 + (row + column) % 2 * 40),
        new(2, 1, 1, (_, _) => 128),
        new(3, 1, 1, (_, _) => 128),
    ];

    private static readonly JpegColorType[] s_variantColorTypes =
    [
        JpegColorType.YCbCrRatio444,
        JpegColorType.YCbCrRatio422,
        JpegColorType.YCbCrRatio420,
        JpegColorType.YCbCrRatio411,
        JpegColorType.Luminance,
    ];

    public static IEnumerable<TestCaseData> EncodedVariants()
    {
        foreach (var colorType in s_variantColorTypes)
        foreach (var progressive in new[] { false, true })
        foreach (var restartInterval in new[] { 0, 3 })
        foreach (var interleaved in new[] { true, false })
            yield return new TestCaseData(colorType, progressive, restartInterval, interleaved);

        foreach (var progressive in new[] { false, true })
            yield return new TestCaseData(JpegColorType.Rgb, progressive, 0, true);
    }

    // ImageSharp's encoder writes DRI without RST markers for these baseline variants (its docs: RestartInterval is supported for progressive only).
    private static readonly (JpegColorType ColorType, bool Progressive, int RestartInterval, bool Interleaved)[] s_malformedByImageSharpEncoder =
    [
        (JpegColorType.YCbCrRatio444, false, 3, true),
        (JpegColorType.Luminance, false, 3, true),
        (JpegColorType.Luminance, false, 3, false),
    ];

    public static IEnumerable<TestCaseData> WellFormedEncodedVariants() =>
        EncodedVariants().Where(data => !s_malformedByImageSharpEncoder.Contains(
            ((JpegColorType)data.Arguments[0]!, (bool)data.Arguments[1]!, (int)data.Arguments[2]!, (bool)data.Arguments[3]!)));

    public static IEnumerable<TestCaseData> MalformedEncodedVariants() =>
        s_malformedByImageSharpEncoder.Select(v => new TestCaseData(v.ColorType, v.Progressive, v.RestartInterval, v.Interleaved));

    [TestCaseSource(typeof(TestAssets), nameof(TestAssets.JpegTextures))]
    public void Decode_TextureAssets_MatchWindowsWithinTolerance(string relativePath)
    {
        var encoded = TestAssets.Read(relativePath);

        var actual = JpegDecoder.Decode(encoded, flipVertically: false);
        var expected = ReferenceDecoders.Windows(encoded);

        AssertWithinTolerance(actual, expected, IsSubsampled(encoded), $"{relativePath} vs Windows");
    }

    [TestCaseSource(typeof(TestAssets), nameof(TestAssets.JpegTextures))]
    public void Decode_TextureAssets_MatchImageSharpWithinTolerance(string relativePath)
    {
        var encoded = TestAssets.Read(relativePath);

        var actual = JpegDecoder.Decode(encoded, flipVertically: false);
        var expected = ReferenceDecoders.ImageSharp(encoded);

        AssertWithinTolerance(actual, expected, IsSubsampled(encoded), $"{relativePath} vs ImageSharp");
    }

    [TestCaseSource(nameof(WellFormedEncodedVariants))]
    public void Decode_EncodedVariants_MatchImageSharpWithinTolerance(JpegColorType colorType, bool progressive, int restartInterval, bool interleaved)
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, colorType, progressive, restartInterval, interleaved);

        var actual = JpegDecoder.Decode(encoded, flipVertically: false);
        var expected = ReferenceDecoders.ImageSharp(encoded);

        AssertWithinTolerance(actual, expected, IsSubsampled(colorType), $"{colorType} progressive={progressive} restart={restartInterval} interleaved={interleaved} vs ImageSharp");
    }

    [TestCaseSource(nameof(WellFormedEncodedVariants))]
    public void Decode_EncodedVariants_MatchWindowsWithinTolerance(JpegColorType colorType, bool progressive, int restartInterval, bool interleaved)
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, colorType, progressive, restartInterval, interleaved);

        var actual = JpegDecoder.Decode(encoded, flipVertically: false);
        var expected = ReferenceDecoders.Windows(encoded);

        AssertWithinTolerance(actual, expected, IsSubsampled(colorType), $"{colorType} progressive={progressive} restart={restartInterval} interleaved={interleaved} vs Windows");
    }

    [TestCaseSource(nameof(MalformedEncodedVariants))]
    public void Decode_RestartIntervalWithoutMarkers_ThrowsInvalidDataException(JpegColorType colorType, bool progressive, int restartInterval, bool interleaved)
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, colorType, progressive, restartInterval, interleaved);

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_ArithmeticCodedFrame_ThrowsNotSupportedException()
    {
        var encoded = BaselineVariant();
        encoded[FindSegment(encoded, Sof0) + 1] = 0xC9;

        Assert.That(() => JpegDecoder.Decode(encoded, flipVertically: false), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Decode_TwelveBitPrecision_ThrowsNotSupportedException()
    {
        var encoded = BaselineVariant();
        encoded[FindSegment(encoded, Sof0) + 4] = 12;

        Assert.That(() => JpegDecoder.Decode(encoded, flipVertically: false), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Decode_CmykJpeg_ThrowsNotSupportedException()
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.Cmyk, progressive: false, restartInterval: 0, interleaved: true);

        Assert.That(() => JpegDecoder.Decode(encoded, flipVertically: false), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Decode_MissingHuffmanTable_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        var withoutTables = new List<byte>(encoded.Length);
        var offset = 2;
        withoutTables.AddRange(encoded.AsSpan(0, 2));

        // Copy every segment up to the scan header except the DHT ones, then the rest of the file unchanged.
        while (encoded[offset + 1] != Sos)
        {
            var length = 2 + (encoded[offset + 2] << 8 | encoded[offset + 3]);
            if (encoded[offset + 1] != Dht)
                withoutTables.AddRange(encoded.AsSpan(offset, length));
            offset += length;
        }

        withoutTables.AddRange(encoded.AsSpan(offset));

        Assert.Multiple(() =>
        {
            Assert.That(withoutTables.Count, Is.LessThan(encoded.Length), "the variant has DHT segments before its scan");
            Assert.That(() => JpegDecoder.Decode(withoutTables.ToArray(), flipVertically: false), Throws.TypeOf<InvalidDataException>());
        });
    }

    [Test]
    public void Decode_NoFrame_ThrowsInvalidDataException()
    {
        byte[] encoded = [0xFF, 0xD8, 0xFF, 0xD9];

        Assert.That(() => JpegDecoder.Decode(encoded, flipVertically: false), Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void Decode_FrameOverPixelLimit_ThrowsNotSupportedException()
    {
        var encoded = BaselineVariant();
        var sof = FindSegment(encoded, Sof0);
        encoded[sof + 5] = 0xFF;
        encoded[sof + 6] = 0xFF;
        encoded[sof + 7] = 0xFF;
        encoded[sof + 8] = 0xFF;

        Assert.That(() => JpegDecoder.Decode(encoded, flipVertically: false), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Decode_TruncatedScanData_ThrowsInvalidDataException()
    {
        var encoded = TestAssets.Read("wall-texture.jpg");
        var sos = FindSegment(encoded, Sos);
        var scanStart = sos + 2 + (encoded[sos + 2] << 8 | encoded[sos + 3]);
        var truncated = encoded.AsSpan(0, scanStart + (encoded.Length - scanStart) / 2).ToArray();

        AssertThrowsInvalidData(truncated);
    }

    [Test]
    public void Decode_FlipVertically_ReversesRowOrder()
    {
        var encoded = TestAssets.Read("wall-texture.jpg");

        var upright = JpegDecoder.Decode(encoded, flipVertically: false);
        var flipped = JpegDecoder.Decode(encoded, flipVertically: true);

        Assert.That((flipped.Width, flipped.Height), Is.EqualTo((upright.Width, upright.Height)));
        Assert.Multiple(() =>
        {
            for (var y = 0; y < upright.Height; y++)
            {
                var uprightRow = upright.Pixels.AsSpan(y * upright.Width, upright.Width);
                var flippedRow = flipped.Pixels.AsSpan((upright.Height - 1 - y) * upright.Width, upright.Width);
                Assert.That(flippedRow.SequenceEqual(uprightRow), Is.True, $"row {y}");
            }
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void Decode_TruncatedRestartScan_ThrowsInvalidDataException(int restartInterval)
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.YCbCrRatio420, progressive: false, restartInterval, interleaved: true);
        var sos = FindSegment(encoded, Sos);
        var scanStart = sos + 2 + (encoded[sos + 2] << 8 | encoded[sos + 3]);
        var scanEnd = encoded.Length - 2;
        Assert.Multiple(() =>
        {
            Assert.That(encoded[scanEnd..], Is.EqualTo(new byte[] { 0xFF, 0xD9 }), "the variant ends with EOI right after its scan");
            Assert.That(encoded.AsSpan(scanStart, scanEnd - scanStart).IndexOf([(byte)0xFF, (byte)0xD0]), Is.Not.Negative, "the variant has RST markers");
        });

        // Cut inside the scan and close the file with EOI.
        byte[] truncated = [.. encoded.AsSpan(0, scanStart + (scanEnd - scanStart) / 2), 0xFF, 0xD9];

        AssertThrowsInvalidData(truncated);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(7)]
    public void Decode_InvalidHuffmanCode_ThrowsInvalidDataException(int mcus)
    {
        // An MCU is 42 bits here, so the cut leaves 4, 2 or 2 padding 1 bits before EOI; with the zero fill after them
        // they read as an 11xx code, which the sparse DC table does not define.
        var encoded = CraftedJpeg.Build(128, 64, s_crafted420, truncateAfterMcus: mcus);

        AssertThrowsInvalidData(encoded);
    }

    [TestCase(JpegColorType.YCbCrRatio420, 1)]
    [TestCase(JpegColorType.YCbCrRatio420, 2)]
    [TestCase(JpegColorType.YCbCrRatio420, 3)]
    [TestCase(JpegColorType.YCbCrRatio420, 4)]
    [TestCase(JpegColorType.YCbCrRatio422, 3)]
    [TestCase(JpegColorType.YCbCrRatio422, 4)]
    public void Decode_NarrowSubsampledImage_MatchesWindows(JpegColorType colorType, int width)
    {
        const int height = 16;
        var encoded = EncodeRandomJpeg(width, height, colorType, seed: width * 31 + height);

        AssertMatchesWindowsExactly(encoded, $"{colorType} {width}x{height} random pixels");
    }

    [Test]
    public void Decode_VerticallySubsampledImage_MatchesWindows()
    {
        // 4:4:0: luma 1x2, chroma 1x1; Cb alternates by block row so vertical upsampling shows.
        CraftedJpeg.Component[] components =
        [
            new(1, 1, 2, (_, _) => 128),
            new(2, 1, 1, (row, _) => row % 2 == 0 ? 60 : 200),
            new(3, 1, 1, (_, column) => column % 2 == 0 ? 90 : 170),
        ];
        var encoded = CraftedJpeg.Build(VariantWidth, VariantHeight, components);

        AssertMatchesWindowsExactly(encoded, "4:4:0 crafted");
    }

    [Test]
    public void Decode_JfifWithAdobeTransformZero_DecodesAsYCbCr()
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.YCbCrRatio444, progressive: false, restartInterval: 0, interleaved: true);
        var app0 = FindSegment(encoded, App0);
        var afterApp0 = app0 + 2 + (encoded[app0 + 2] << 8 | encoded[app0 + 3]);

        // APP14 "Adobe": version 100, flags 0, 0, transform 0 (which alone would mean RGB).
        byte[] adobe = [0xFF, 0xEE, 0, 14, (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e', 0, 100, 0, 0, 0, 0, 0];
        byte[] withAdobe = [.. encoded.AsSpan(0, afterApp0), .. adobe, .. encoded.AsSpan(afterApp0)];

        AssertMatchesWindowsExactly(withAdobe, "JFIF + Adobe transform 0");
    }

    [Test]
    public void Decode_MissingEoi_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        Assert.That(encoded[^2..], Is.EqualTo(new byte[] { 0xFF, 0xD9 }), "the variant ends with EOI");

        AssertThrowsInvalidData(encoded[..^2]);
    }

    [Test]
    public void Decode_StrayBytesBetweenSegments_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        var dqt = FindSegment(encoded, Dqt);

        // One non-0xFF byte right before the DQT segment's marker.
        byte[] withStrayByte = [.. encoded.AsSpan(0, dqt), 0x42, .. encoded.AsSpan(dqt)];

        AssertThrowsInvalidData(withStrayByte);
    }

    [Test]
    public void Decode_ZeroQuantizationStep_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();

        // The first step of the first table: marker (2), length (2), Pq/Tq (1).
        encoded[FindSegment(encoded, Dqt) + 5] = 0;

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_AllOnesHuffmanCode_ThrowsInvalidDataException()
    {
        // 16 four-bit DC codes: the last is 1111.
        var encoded = CraftedJpeg.Build(16, 16, s_crafted420, dcCodeCount: 16);

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_InvalidSequentialScanParameters_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        var sos = FindSegment(encoded, Sos);
        var count = encoded[sos + 4];
        var se = sos + 5 + 2 * count + 1;
        Assert.That(encoded[se], Is.EqualTo(63), "the baseline scan has Se = 63");

        encoded[se] = 62;

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_DuplicateComponentInScan_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        var sos = FindSegment(encoded, Sos);
        Assert.That(encoded[sos + 4], Is.EqualTo(3), "the baseline scan is interleaved over 3 components");

        // The second scan component takes the first one's id.
        encoded[sos + 7] = encoded[sos + 5];

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_InvalidProgression_ThrowsInvalidDataException()
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.YCbCrRatio444, progressive: true, restartInterval: 0, interleaved: true);
        var sos = FindSegment(encoded, Sos);
        var count = encoded[sos + 4];
        var successiveApproximation = sos + 5 + 2 * count + 2;
        Assert.That(encoded[successiveApproximation] >> 4, Is.Zero, "the first scan is a first scan (Ah = 0)");

        // Ah = 1 on the very first scan: a refinement of coefficients no scan has coded yet.
        encoded[successiveApproximation] = 0x10;

        AssertThrowsInvalidData(encoded);
    }

    [TestCase((byte)0xD0, TestName = "{m}(RST0)")]
    [TestCase((byte)0x01, TestName = "{m}(TEM)")]
    public void Decode_MarkerBetweenSegments_ThrowsInvalidDataException(byte marker)
    {
        var encoded = BaselineVariant();
        var sof = FindSegment(encoded, Sof0);
        var dqt = FindSegment(encoded, Dqt);
        Assert.That(dqt, Is.GreaterThan(sof), "ImageSharp writes the DQT segment after the frame header, before the scan");

        // A marker without a segment between SOF and DQT, outside any scan.
        byte[] withMarker = [.. encoded.AsSpan(0, dqt), 0xFF, marker, .. encoded.AsSpan(dqt)];

        AssertThrowsInvalidData(withMarker);
    }

    [Test]
    public void Decode_ComponentMissingFromScans_ThrowsInvalidDataException()
    {
        var encoded = NonInterleavedSequentialVariant();
        var scans = ScanRanges(encoded);

        // Without the Cr scan (the third).
        byte[] withoutCr = [.. encoded.AsSpan(0, scans[2].Start), .. encoded.AsSpan(scans[2].End)];

        AssertThrowsInvalidData(withoutCr);
    }

    [Test]
    public void Decode_ComponentInTwoSequentialScans_ThrowsInvalidDataException()
    {
        var encoded = NonInterleavedSequentialVariant();
        var scans = ScanRanges(encoded);

        // The Cb scan (the second) twice.
        byte[] cbTwice = [.. encoded.AsSpan(0, scans[1].End), .. encoded.AsSpan(scans[1].Start, scans[1].End - scans[1].Start), .. encoded.AsSpan(scans[1].End)];

        AssertThrowsInvalidData(cbTwice);
    }

    [Test]
    public void Decode_ProgressiveComponentWithoutDcScan_ThrowsInvalidDataException()
    {
        CraftedJpeg.ProgressiveScan[] dcOfAllComponents = [new([0, 1, 2], 0, 0, 0, 0)];
        CraftedJpeg.ProgressiveScan[] dcWithoutCr = [new([0, 1], 0, 0, 0, 0)];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 3, dcOfAllComponents), flipVertically: false), Throws.Nothing,
            "the same file with every component in its DC scan");
        AssertThrowsInvalidData(CraftedJpeg.BuildProgressive(16, 16, 3, dcWithoutCr));
    }

    [Test]
    public void Decode_ScanComponentsOutOfFrameOrder_ThrowsInvalidDataException()
    {
        var encoded = BaselineVariant();
        var sos = FindSegment(encoded, Sos);
        Assert.That(encoded[sos + 4], Is.EqualTo(3), "the baseline scan is interleaved over 3 components");

        // Swap the Cb and Cr selectors (component id and table byte each): Cr is listed before Cb.
        var cb = sos + 7;
        var cr = sos + 9;
        (encoded[cb], encoded[cb + 1], encoded[cr], encoded[cr + 1]) = (encoded[cr], encoded[cr + 1], encoded[cb], encoded[cb + 1]);

        AssertThrowsInvalidData(encoded);
    }

    [Test]
    public void Decode_TooManyBlocksPerMcu_ThrowsInvalidDataException()
    {
        // 3x3 + 1x1 + 1x1 = 11 blocks in each MCU of the interleaved scan.
        CraftedJpeg.Component[] components =
        [
            new(1, 3, 3, (_, _) => 128),
            new(2, 1, 1, (_, _) => 128),
            new(3, 1, 1, (_, _) => 128),
        ];

        AssertThrowsInvalidData(CraftedJpeg.Build(48, 48, components));
    }

    [Test]
    public void Decode_BaselineTableIdAboveOne_ThrowsInvalidDataException()
    {
        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.Build(16, 16, s_crafted420, frameMarker: Sof1, huffmanTableId: 2), flipVertically: false),
            Throws.Nothing, "the same tables in an extended sequential (SOF1) file");
        AssertThrowsInvalidData(CraftedJpeg.Build(16, 16, s_crafted420, huffmanTableId: 2));
    }

    [Test]
    public void Decode_QuantizationTableChangedMidProgression_ThrowsInvalidDataException()
    {
        // A DC first scan at Al = 1 and its refinement, with table 0 (all 8) defined again in between.
        CraftedJpeg.ProgressiveScan[] Script(byte step) =>
        [
            new([0], 0, 0, 0, 1),
            new([0], 0, 0, 1, 0, CraftedJpeg.QuantizationSegment(step)),
        ];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, Script(8)), flipVertically: false), Throws.Nothing,
            "the table defined again with the same values is not altered (T.81 B.2.4)");
        AssertThrowsInvalidData(CraftedJpeg.BuildProgressive(16, 16, 1, Script(2)));
    }

    [Test]
    public void Decode_DcSizeAboveEleven_ThrowsInvalidDataException()
    {
        // DC codes for sizes 0..12; one 8x8 block.
        CraftedJpeg.Component[] gray = [new(1, 1, 1, (_, _) => 200)];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.Build(8, 8, gray, dcCodeCount: 13), flipVertically: false), Throws.Nothing,
            "the table holding size 12 is accepted while the data does not use it");
        AssertThrowsInvalidData(CraftedJpeg.Build(8, 8, gray, dcCodeCount: 13, firstDcSize: 12));
    }

    [Test]
    public void Decode_EobRunInSequentialScan_ThrowsInvalidDataException()
    {
        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.Build(16, 16, s_crafted420), flipVertically: false), Throws.Nothing,
            "the same file ending its blocks with EOB (0x00)");
        AssertThrowsInvalidData(CraftedJpeg.Build(16, 16, s_crafted420, acEndSymbol: 0x10));
    }

    [TestCase(false, TestName = "{m}(sequential)")]
    [TestCase(true, TestName = "{m}(progressive AC first)")]
    public void Decode_AcRunPastLastCoefficient_ThrowsInvalidDataException(bool progressive)
    {
        // Sequential: three ZRLs reach coefficient 49, then a run of 13 lands on 62 (valid) or of 15 on 64 (past 63).
        // Progressive band 1..5: a run of 3 lands on 4 (valid) or of 5 on 6 (past Se).
        byte[] valid = progressive ? [0x31, 0x00] : [0xF0, 0xF0, 0xF0, 0xD1, 0x00];
        byte[] pastEnd = progressive ? [0x51] : [0xF0, 0xF0, 0xF0, 0xF1];

        Assert.That(() => JpegDecoder.Decode(AcSymbolsJpeg(progressive, valid), flipVertically: false), Throws.Nothing,
            "the same file with the run ending inside the band");
        AssertThrowsInvalidData(AcSymbolsJpeg(progressive, pastEnd));
    }

    [TestCase(false, TestName = "{m}(sequential)")]
    [TestCase(true, TestName = "{m}(progressive AC first)")]
    public void Decode_AcSizeAboveTen_ThrowsInvalidDataException(bool progressive)
    {
        Assert.That(() => JpegDecoder.Decode(AcSymbolsJpeg(progressive, [0x0A, 0x00]), flipVertically: false), Throws.Nothing,
            "the same file with an AC coefficient of size 10");
        AssertThrowsInvalidData(AcSymbolsJpeg(progressive, [0x0B, 0x00]));
    }

    [Test]
    public void Decode_RefinementSizeAboveOne_ThrowsInvalidDataException()
    {
        // DC first, the AC band 1..5 first at Al = 1 (all zero), then its refinement where every block gets one new coefficient.
        CraftedJpeg.ProgressiveScan[] Script(byte newCoefficient) =>
        [
            new([0], 0, 0, 0, 0),
            new([0], 1, 5, 0, 1),
            new([0], 1, 5, 1, 0, AcSymbols: [newCoefficient, 0x00]),
        ];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0x01)), flipVertically: false), Throws.Nothing,
            "the same file with the new coefficient's size 1");
        AssertThrowsInvalidData(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0x02)));
    }

    [Test]
    public void Decode_RefinementRunPastBand_ThrowsInvalidDataException()
    {
        // DC first, the AC band 1..5 first at Al = 1 (all zero), then its refinement: a new coefficient after a run of 4
        // zero-history coefficients lands on 5 = Se (valid); after a run of 5 it would land on 6, past the band.
        CraftedJpeg.ProgressiveScan[] Script(byte newCoefficient) =>
        [
            new([0], 0, 0, 0, 0),
            new([0], 1, 5, 0, 1),
            new([0], 1, 5, 1, 0, AcSymbols: [newCoefficient]),
        ];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0x41)), flipVertically: false), Throws.Nothing,
            "the same file with the new coefficient on the band's last coefficient");
        AssertThrowsInvalidData(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0x51)));
    }

    [Test]
    public void Decode_RefinementZrlPastBand_ThrowsInvalidDataException()
    {
        // DC first, the AC band 1..5 first at Al = 1 (all zero), then its refinement: every block ends with EOB0 (valid), or
        // codes a ZRL whose 16 zero-history coefficients do not fit in the 5 of the band.
        CraftedJpeg.ProgressiveScan[] Script(byte symbol) =>
        [
            new([0], 0, 0, 0, 0),
            new([0], 1, 5, 0, 1),
            new([0], 1, 5, 1, 0, AcSymbols: [symbol]),
        ];

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0x00)), flipVertically: false), Throws.Nothing,
            "the same file ending the refinement's blocks with EOB0");
        AssertThrowsInvalidData(CraftedJpeg.BuildProgressive(16, 16, 1, Script(0xF0)));
    }

    [Test]
    public void Decode_TooManyScans_ThrowsNotSupportedException()
    {
        // DC first, then every AC coefficient's first scan at Al = 1, then their refinements: 127 valid scans.
        var script = new List<CraftedJpeg.ProgressiveScan> { new([0], 0, 0, 0, 0) };
        for (var k = 1; k <= 63; k++)
            script.Add(new([0], k, k, 0, 1));

        for (var k = 1; k <= 63; k++)
            script.Add(new([0], k, k, 1, 0));

        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, script[..100]), flipVertically: false), Throws.Nothing,
            "the first 100 scans");
        Assert.That(() => JpegDecoder.Decode(CraftedJpeg.BuildProgressive(16, 16, 1, script[..101]), flipVertically: false),
            Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Decode_SixteenBitQuantizationTable_IsAccepted()
    {
        var encoded = BaselineVariant();
        var sixteenBit = WithSixteenBitQuantizationTables(encoded);
        Assert.That(sixteenBit, Has.Length.GreaterThan(encoded.Length), "the DQT segments were rewritten");

        var expected = JpegDecoder.Decode(encoded, flipVertically: false);
        var actual = JpegDecoder.Decode(sixteenBit, flipVertically: false);

        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void Decode_ZeroPaddingBits_IsAccepted()
    {
        var onePadded = CraftedJpeg.Build(64, 48, s_crafted420, restartInterval: 2);
        var zeroPadded = CraftedJpeg.Build(64, 48, s_crafted420, restartInterval: 2, padWithZeros: true);
        Assert.That(zeroPadded, Is.Not.EqualTo(onePadded), "the entropy-coded segments end inside a byte");

        var expected = JpegDecoder.Decode(onePadded, flipVertically: false);
        var actual = JpegDecoder.Decode(zeroPadded, flipVertically: false);

        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    private static byte[] BaselineVariant() =>
        TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.YCbCrRatio420, progressive: false, restartInterval: 0, interleaved: true);

    /// <summary>
    /// A 16x16 grayscale file whose every block codes <paramref name="acSymbols"/>: sequential (in the one scan), or progressive
    /// (in an AC first scan of the band 1..5 after the DC first scan).
    /// </summary>
    private static byte[] AcSymbolsJpeg(bool progressive, byte[] acSymbols)
    {
        if (progressive)
            return CraftedJpeg.BuildProgressive(16, 16, 1, [new([0], 0, 0, 0, 0), new([0], 1, 5, 0, 0, AcSymbols: acSymbols)]);

        CraftedJpeg.Component[] gray = [new(1, 1, 1, (row, column) => 100 + 20 * row + 10 * column)];
        return CraftedJpeg.Build(16, 16, gray, blockAcSymbols: acSymbols);
    }

    /// <summary>A sequential 4:4:4 variant with one scan per component, in frame order (Y, Cb, Cr).</summary>
    private static byte[] NonInterleavedSequentialVariant()
    {
        var encoded = TestImages.EncodeJpeg(VariantWidth, VariantHeight, JpegColorType.YCbCrRatio444, progressive: false, restartInterval: 0, interleaved: false);
        var sof = FindSegment(encoded, Sof0);
        var scans = ScanRanges(encoded);

        Assert.That(scans, Has.Count.EqualTo(3), "the variant has three scans");
        for (var i = 0; i < scans.Count; i++)
        {
            var start = scans[i].Start;
            Assert.That(encoded[start + 4], Is.EqualTo(1), $"scan {i} codes one component");
            Assert.That(encoded[start + 5], Is.EqualTo(encoded[sof + 10 + 3 * i]), $"scan {i} codes frame component {i}");
        }

        return encoded;
    }

    /// <summary>
    /// Each scan from its SOS marker to the end of its entropy-coded data (the next marker other than RSTn), in file order.
    /// </summary>
    private static List<(int Start, int End)> ScanRanges(byte[] encoded)
    {
        var scans = new List<(int Start, int End)>();
        var offset = 2;
        while (encoded[offset + 1] != 0xD9)
        {
            Assert.That(encoded[offset], Is.EqualTo(0xFF), $"segment marker expected at {offset}");
            var marker = encoded[offset + 1];
            var end = offset + 2 + (encoded[offset + 2] << 8 | encoded[offset + 3]);

            if (marker == Sos)
            {
                while (!(encoded[end] == 0xFF && encoded[end + 1] != 0x00 && encoded[end + 1] is < 0xD0 or > 0xD7))
                    end++;

                scans.Add((offset, end));
            }

            offset = end;
        }

        return scans;
    }

    /// <summary>The file with every DQT table rewritten at 16-bit precision (Pq = 1), the same values.</summary>
    private static byte[] WithSixteenBitQuantizationTables(byte[] encoded)
    {
        var output = new List<byte>(encoded.AsSpan(0, 2).ToArray());
        var offset = 2;
        while (encoded[offset + 1] != Sos)
        {
            var length = 2 + (encoded[offset + 2] << 8 | encoded[offset + 3]);
            if (encoded[offset + 1] != Dqt)
            {
                output.AddRange(encoded.AsSpan(offset, length));
                offset += length;
                continue;
            }

            var payload = new List<byte>();
            for (var table = offset + 4; table < offset + length; table += 65)
            {
                Assert.That(encoded[table] >> 4, Is.Zero, "the variant's tables are 8-bit");
                payload.Add((byte)(0x10 | encoded[table] & 15));
                foreach (var step in encoded.AsSpan(table + 1, 64))
                    payload.AddRange([0, step]);
            }

            output.AddRange([0xFF, Dqt, (byte)((payload.Count + 2) >> 8), (byte)(payload.Count + 2)]);
            output.AddRange(payload);
            offset += length;
        }

        output.AddRange(encoded.AsSpan(offset));
        return output.ToArray();
    }

    private static bool IsSubsampled(JpegColorType colorType) =>
        colorType is JpegColorType.YCbCrRatio422 or JpegColorType.YCbCrRatio420 or JpegColorType.YCbCrRatio411;

    /// <summary>True when the frame's components do not all have the same sampling factors.</summary>
    private static bool IsSubsampled(byte[] encoded)
    {
        var sof = FindSegment(encoded, Sof0, Sof1, Sof2);
        var components = encoded[sof + 9];
        var first = encoded[sof + 11];
        for (var i = 1; i < components; i++)
        {
            if (encoded[sof + 11 + 3 * i] != first)
                return true;
        }

        return false;
    }

    /// <summary>The offset of the <c>0xFF</c> byte of the first segment with one of <paramref name="markers"/>, walking the segments from SOI up to the first scan.</summary>
    private static int FindSegment(byte[] encoded, params byte[] markers)
    {
        var offset = 2;
        while (true)
        {
            Assert.That(encoded[offset], Is.EqualTo(0xFF), $"segment marker expected at {offset}");
            var marker = encoded[offset + 1];
            if (markers.Contains(marker))
                return offset;

            Assert.That(marker, Is.Not.EqualTo(Sos), "segment not found before the first scan");
            offset += 2 + (encoded[offset + 2] << 8 | encoded[offset + 3]);
        }
    }

    /// <summary>A random-pixel image (seeded) encoded by ImageSharp at quality 90.</summary>
    private static byte[] EncodeRandomJpeg(int width, int height, JpegColorType colorType, int seed)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        var random = new Random(seed);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            image[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90, ColorType = colorType });
        return stream.ToArray();
    }

    /// <summary>The strict decoder rejects the file with <see cref="InvalidDataException"/>; the message is logged to show which rule fired.</summary>
    private static void AssertThrowsInvalidData(byte[] encoded)
    {
        var exception = Assert.Throws<InvalidDataException>(() => JpegDecoder.Decode(encoded, flipVertically: false));
        TestContext.Out.WriteLine($"InvalidDataException: {exception?.Message}");
    }

    /// <summary>Ours and Windows (GDI+, libjpeg-based) must agree on every sample.</summary>
    private static void AssertMatchesWindowsExactly(byte[] encoded, string label)
    {
        var actual = JpegDecoder.Decode(encoded, flipVertically: false);
        var expected = ReferenceDecoders.Windows(encoded);

        Assert.That((actual.Width, actual.Height), Is.EqualTo((expected.Width, expected.Height)), label);

        var difference = PixelComparison.Compare(actual.Pixels, expected.Pixels);
        TestContext.Out.WriteLine($"{label} vs Windows: Max={difference.Max} Mean={difference.Mean:F4} Percentile999={difference.Percentile999}");

        Assert.Multiple(() =>
        {
            Assert.That(difference.Max, Is.Zero, $"{label}: max");
            Assert.That(difference.AlphaEqual, Is.True, $"{label}: alpha");
        });
    }

    private static void AssertWithinTolerance(RgbaImage actual, (int Width, int Height, Rgba32[] Pixels) expected, bool subsampled, string label)
    {
        Assert.That((actual.Width, actual.Height), Is.EqualTo((expected.Width, expected.Height)), label);

        var difference = PixelComparison.Compare(actual.Pixels, expected.Pixels);
        TestContext.Out.WriteLine($"{label}: subsampled={subsampled} Max={difference.Max} Mean={difference.Mean:F4} Percentile999={difference.Percentile999}");

        Assert.Multiple(() =>
        {
            Assert.That(actual.Pixels.All(p => p.A == 255), Is.True, $"{label}: alpha must be 255");
            Assert.That(difference.AlphaEqual, Is.True, $"{label}: reference alpha differs");
            if (subsampled)
            {
                Assert.That(difference.Mean, Is.LessThanOrEqualTo(2.5), $"{label}: mean");
                Assert.That(difference.Percentile999, Is.LessThanOrEqualTo(24), $"{label}: 99.9th percentile");
            }
            else
            {
                Assert.That(difference.Max, Is.LessThanOrEqualTo(4), $"{label}: max");
                Assert.That(difference.Mean, Is.LessThanOrEqualTo(1.0), $"{label}: mean");
            }
        });
    }
}
