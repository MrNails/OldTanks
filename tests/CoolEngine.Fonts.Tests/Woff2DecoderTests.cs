using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Woff;
using static CoolEngine.Fonts.Tests.Reference.CraftedFonts;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// Tests of <see cref="Woff2Decoder"/>: the untransformed tables against DirectWrite's unpacker, the rebuilt <c>glyf</c>,
/// <c>loca</c> and <c>hmtx</c> through outlines and metrics against the matching TrueType font, and one test per strict rule
/// (crafted files, each asserting its own message).
/// </summary>
[TestFixture]
public sealed class Woff2DecoderTests
{
    /// <summary>The bound of the bomb tests: a refusal allocates no more than this above what the file itself needs.</summary>
    private const long AllocationSlack = 64 * 1024;

    private DirectWriteReference m_directWrite = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => m_directWrite = new DirectWriteReference();

    [OneTimeTearDown]
    public void OneTimeTearDown() => m_directWrite.Dispose();

    private static IEnumerable<TestCaseData> Woff2Files() =>
        TestFonts.Woff2Files().Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    private static IEnumerable<TestCaseData> TrueTypeWoff2Files() =>
        TestFonts.Woff2Files().Where(path => path.EndsWith(".ttf.woff2", StringComparison.Ordinal))
            .Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    private static IEnumerable<TestCaseData> CffWoff2Files() =>
        TestFonts.Woff2Files().Where(path => path.EndsWith(".otf.woff2", StringComparison.Ordinal))
            .Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    private static string MatchingFont(string woff2Path) => woff2Path[..woff2Path.LastIndexOf('.')];

    private static byte[] RegularTtfWoff2() => FontCache.Bytes(TestFonts.SourceSans("SourceSans3-Regular.ttf.woff2"));

    // ---- The references ----

    /// <summary>
    /// Every table but <c>glyf</c> and <c>loca</c> (rebuilt: compared through outlines below), byte for byte. <c>head</c> is
    /// compared without <c>checkSumAdjustment</c>, which covers the whole image and so the rebuilt <c>glyf</c> and the layout;
    /// ours is checked to make the image sum to 0xB1B0AFBA instead.
    /// </summary>
    [TestCaseSource(nameof(Woff2Files))]
    public void Decode_EveryWoff2_UntransformedTablesEqualDirectWriteUnpack(string path)
    {
        var file = FontCache.Bytes(path);

        var image = Woff2Decoder.Decode(file);
        var ours = SfntTables.Read(image);
        var theirs = SfntTables.Read(m_directWrite.UnpackFontFile(DWriteContainerType.Woff2, file));

        Assert.Multiple(() =>
        {
            Assert.That(ours.Keys, Is.EquivalentTo(theirs.Keys), "table tags");
            foreach (var (tag, data) in ours.Where(pair => pair.Key is not ("glyf" or "loca")))
                Assert.That(WithoutChecksumAdjustment(tag, data), Is.EqualTo(WithoutChecksumAdjustment(tag, theirs[tag])), $"table '{tag}'");

            Assert.That(Sum(image), Is.EqualTo(0xB1B0AFBAu), "the image's checksum");
        });
    }

    /// <summary>The whole image, directory, layout, padding, rebuilt <c>glyf</c>, <c>loca</c> and <c>hmtx</c> and <c>checkSumAdjustment</c> included.</summary>
    [TestCaseSource(nameof(Woff2Files))]
    public void Decode_EveryWoff2_ImageEqualsDirectWriteUnpack(string path)
    {
        var file = FontCache.Bytes(path);

        var ours = Woff2Decoder.Decode(file);
        var theirs = m_directWrite.UnpackFontFile(DWriteContainerType.Woff2, file);

        Assert.That(ours, Is.EqualTo(theirs));
    }

    [TestCaseSource(nameof(Woff2Files))]
    public void Decode_EveryWoff2_RecordChecksumsAndLayoutAreCorrect(string path)
    {
        var image = Woff2Decoder.Decode(FontCache.Bytes(path));

        var count = BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(4));
        var wrong = new List<string>();
        var previousTag = "";
        for (var i = 0; i < count; i++)
        {
            var record = image.AsSpan(12 + 16 * i);
            var tag = Encoding.Latin1.GetString(record[..4]);
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            var data = image.AsSpan(offset, (length + 3) & ~3).ToArray();
            if (tag == "head")
                data.AsSpan(8, 4).Clear();

            if (BinaryPrimitives.ReadUInt32BigEndian(record[4..]) != Sum(data))
                wrong.Add($"{tag}: checksum");

            if (offset % 4 != 0 || data.AsSpan(length).ToArray().Any(b => b != 0))
                wrong.Add($"{tag}: alignment or padding");

            if (string.CompareOrdinal(previousTag, tag) >= 0)
                wrong.Add($"{tag}: record not after {previousTag}");

            previousTag = tag;
        }

        Assert.That(wrong, Is.Empty, string.Join("; ", wrong));
    }

    [TestCaseSource(nameof(CffWoff2Files))]
    public void Decode_EveryCffWoff2_CffTablesEqualTheMatchingOtfs(string path)
    {
        var decoded = SfntTables.Read(Woff2Decoder.Decode(FontCache.Bytes(path)));
        var expected = SfntTables.Read(FontCache.Bytes(MatchingFont(path)));

        var tag = expected.ContainsKey("CFF2") ? "CFF2" : "CFF ";
        Assert.That(decoded[tag], Is.EqualTo(expected[tag]), $"table '{tag}'");
    }

    [TestCaseSource(nameof(TrueTypeWoff2Files))]
    public void Load_EveryTrueTypeWoff2_OutlinesEqualTheMatchingTtf(string path)
    {
        var face = FontLoader.Load(FontCache.Bytes(path)).Faces[0];
        var expected = FontCache.File(MatchingFont(path)).Faces[0];

        AssertSameOutlines(face, expected);
    }

    /// <summary>
    /// The CFF and CFF2 copies (OT-4 WP5, now that CFF outlines exist): every glyph's outline and metrics equal the matching
    /// OTF's, and a variable one's at its last named instance too.
    /// </summary>
    [TestCaseSource(nameof(CffWoff2Files))]
    public void Load_EveryCffWoff2_OutlinesEqualTheMatchingOtf(string path)
    {
        var face = FontLoader.Load(FontCache.Bytes(path)).Faces[0];
        var expected = FontCache.File(MatchingFont(path)).Faces[0];

        AssertSameOutlines(face, expected);
        if (expected.IsVariable)
        {
            var instance = expected.NamedInstances[^1];
            AssertSameOutlines(face, expected, expected.Axes.Select((axis, i) => new VariationCoordinate(axis.Tag, instance.Coordinates[i])).ToArray());
        }
    }

    private static void AssertSameOutlines(FontFace face, FontFace expected, VariationCoordinate[]? variation = null)
    {
        var options = new ScaledFontOptions(16) { Hinting = HintingMode.None, Variation = variation ?? [] };
        var font = face.CreateScaledFont(options);
        var reference = expected.CreateScaledFont(options);

        var mismatches = new List<string>();
        for (var glyph = 0; glyph < expected.GlyphCount; glyph++)
        {
            var ours = font.GetOutline(glyph);
            var theirs = reference.GetOutline(glyph);
            if (!ours.Points.Span.SequenceEqual(theirs.Points.Span) || !ours.ContourEndPoints.Span.SequenceEqual(theirs.ContourEndPoints.Span))
                mismatches.Add($"glyph {glyph}: outline differs");

            if (font.GetMetrics(glyph) != reference.GetMetrics(glyph))
                mismatches.Add($"glyph {glyph}: metrics {font.GetMetrics(glyph)}, TTF {reference.GetMetrics(glyph)}");

            if (face.GetAdvanceWidth(glyph) != expected.GetAdvanceWidth(glyph))
                mismatches.Add($"glyph {glyph}: advance {face.GetAdvanceWidth(glyph)}, TTF {expected.GetAdvanceWidth(glyph)}");
        }

        var characters = Enumerable.Range(0, 0x10000).Count(codePoint => face.GetGlyphIndex(codePoint) != expected.GetGlyphIndex(codePoint));

        Assert.Multiple(() =>
        {
            Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Take(10)));
            Assert.That(face.GlyphCount, Is.EqualTo(expected.GlyphCount), "glyph count");
            Assert.That(face.FamilyName, Is.EqualTo(expected.FamilyName), "family name");
            Assert.That(face.StyleName, Is.EqualTo(expected.StyleName), "style name");
            Assert.That(characters, Is.Zero, "code points mapped differently");
            Assert.That(font.LineMetrics, Is.EqualTo(reference.LineMetrics), "line metrics");
        });
    }

    [TestCaseSource(nameof(CffWoff2Files))]
    public void Load_EveryCffWoff2_PropertiesEqualTheMatchingOtf(string path)
    {
        var face = FontLoader.Load(FontCache.Bytes(path)).Faces[0];
        var expected = FontCache.File(MatchingFont(path)).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That(face.GlyphCount, Is.EqualTo(expected.GlyphCount), "glyph count");
            Assert.That(face.OutlineFormat, Is.EqualTo(expected.OutlineFormat), "outline format");
            Assert.That(face.FamilyName, Is.EqualTo(expected.FamilyName), "family name");
            Assert.That(Enumerable.Range(0, 0x10000).Count(codePoint => face.GetGlyphIndex(codePoint) != expected.GetGlyphIndex(codePoint)), Is.Zero, "code points mapped differently");
            Assert.That(Enumerable.Range(0, face.GlyphCount).Count(glyph => face.GetAdvanceWidth(glyph) != expected.GetAdvanceWidth(glyph)), Is.Zero, "advances");
        });
    }

    private static byte[] WithoutChecksumAdjustment(string tag, byte[] table)
    {
        if (tag != "head" || table.Length < 12)
            return table;

        var copy = (byte[])table.Clone();
        copy.AsSpan(8, 4).Clear();
        return copy;
    }

    private static uint Sum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i + 4 <= data.Length; i += 4)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i));

        return sum;
    }

    // ---- The accepted cases: each decodes, and DirectWrite unpacks it to the same tables ----

    private static IEnumerable<TestCaseData> AcceptedVariants()
    {
        yield return Variant("unchanged", woff => woff, true);
        yield return Variant("non-zero header reserved", woff => Woff2Builder.U16(woff, 14, 0xBEEF), true);
        yield return Variant("totalSfntSize below the image's size", woff => Woff2Builder.U32(woff, 16, 1000), false);
        yield return Variant("totalSfntSize above the image's size", woff => Woff2Builder.U32(woff, 16, 0x12345678), false);
        yield return Variant("major and minor version", woff => Woff2Builder.U16(Woff2Builder.U16(woff, 24, 7), 26, 9), true);
        yield return Variant("glyf header reserved and option bits", woff => Woff2Builder.Edit(woff, tables =>
        {
            var glyf = tables.Single(table => table.Tag == "glyf");
            glyf.Data[1] = 7;
            glyf.Data[2] = 0x80;
        }), true);
        yield return Variant("unused bytes at the end of glyf", woff => Woff2Builder.Edit(woff, tables =>
        {
            var index = tables.FindIndex(table => table.Tag == "glyf");
            tables[index] = tables[index] with { Data = [.. tables[index].Data, 1, 2, 3] };
        }), true);
        yield return Variant("loca before glyf", woff => Woff2Builder.Edit(woff, tables =>
        {
            var glyf = tables.FindIndex(table => table.Tag == "glyf");
            var loca = tables.FindIndex(table => table.Tag == "loca");
            (tables[glyf], tables[loca]) = (tables[loca], tables[glyf]);
        }), true);
        yield return Variant("original length of a transformed table", woff => Woff2Builder.Edit(woff, tables =>
        {
            var index = tables.FindIndex(table => table.Tag == "glyf");
            tables[index] = tables[index] with { OrigLength = 1 };
        }), true);
        yield return Variant("metadata that is not Brotli data", woff => Woff2Builder.AppendBlocks(woff, metadata: [1, 2, 3, 4, 5, 6]), true);
        yield return Variant("private data", woff => Woff2Builder.AppendBlocks(woff, privateData: [1, 2, 3, 4, 5]), true);
        yield return Variant("bytes after the Brotli stream", woff => Woff2Builder.Edit(woff, _ => { }, brotliTail: [0, 0, 0]), true);

        static TestCaseData Variant(string name, Func<byte[], byte[]> change, bool directWriteAccepts) =>
            new TestCaseData(name, change, directWriteAccepts).SetArgDisplayNames(name);
    }

    /// <summary>
    /// The accepted cases of the decoder's remarks: each decodes to the original font's tables. DirectWrite accepts the cases
    /// flagged so and unpacks the same tables; for the others it refuses the file (asserted too, so the difference is on
    /// record): the W3C text forbids rejecting a wrong <c>totalSfntSize</c> and FreeType ignores it, DirectWrite refuses one.
    /// </summary>
    [TestCaseSource(nameof(AcceptedVariants))]
    public void Decode_AcceptedVariant_DecodesAndDirectWriteIsAsStated(string name, Func<byte[], byte[]> change, bool directWriteAccepts)
    {
        var woff2 = change(Woff2Builder.Edit(RegularTtfWoff2(), _ => { }));

        var ours = SfntTables.Read(Woff2Decoder.Decode(woff2));

        Assert.Multiple(() =>
        {
            var expected = SfntTables.Read(Woff2Decoder.Decode(RegularTtfWoff2()));
            Assert.That(ours.Keys, Is.EquivalentTo(expected.Keys), name + ": table tags");
            foreach (var (tag, data) in ours)
                Assert.That(WithoutChecksumAdjustment(tag, data), Is.EqualTo(WithoutChecksumAdjustment(tag, expected[tag])), $"{name}: table '{tag}' as decoded without the change");
            if (!directWriteAccepts)
            {
                Assert.That(() => m_directWrite.UnpackFontFile(DWriteContainerType.Woff2, woff2), Throws.InstanceOf<System.Runtime.InteropServices.COMException>(), name + ": DirectWrite");
                return;
            }

            var theirs = SfntTables.Read(m_directWrite.UnpackFontFile(DWriteContainerType.Woff2, woff2));
            Assert.That(ours.Keys, Is.EquivalentTo(theirs.Keys), name + ": table tags");
            foreach (var (tag, data) in ours.Where(pair => pair.Key is not ("glyf" or "loca")))
                Assert.That(WithoutChecksumAdjustment(tag, data), Is.EqualTo(WithoutChecksumAdjustment(tag, theirs[tag])), $"{name}: table '{tag}'");
        });
    }

    // ---- Strict rules: header, directory, collection, blocks, Brotli ----

    private static byte[] Valid() => Woff2Builder.Edit(RegularTtfWoff2(), _ => { });

    private static void AssertInvalid(byte[] woff2, string message) =>
        Assert.That(() => Woff2Decoder.Decode(woff2), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));

    [Test]
    public void Decode_RebuiltValidWoff2_EqualsTheOriginalDecode()
    {
        var original = Woff2Decoder.Decode(RegularTtfWoff2());

        Assert.That(Woff2Decoder.Decode(Valid()), Is.EqualTo(original));
    }

    [Test]
    public void Decode_DataShorterThanHeader_Throws() =>
        AssertInvalid(Valid()[..47], "shorter than its 48-byte header");

    [Test]
    public void Decode_SignatureNotWoff2_Throws()
    {
        var woff2 = Valid();
        "wOFF"u8.CopyTo(woff2);

        AssertInvalid(woff2, "signature is not 'wOF2'");
    }

    [Test]
    public void Decode_DeclaredLengthNotFileLength_Throws() =>
        AssertInvalid(Woff2Builder.U32(Valid(), 8, 100), "gives the length");

    [Test]
    public void Decode_NoTables_Throws() =>
        AssertInvalid(Woff2Builder.U16(Valid(), 12, 0), "has no tables");

    [Test]
    public void Decode_MoreTablesThanTheFileHoldsBytesFor_Throws() =>
        AssertInvalid(Woff2Builder.U16(Valid(), 12, 0xFFFF), "entries) runs past the end of the file");

    [Test]
    public void Decode_DirectoryEndsInsideAnEntry_Throws()
    {
        var woff2 = Woff2Builder.Build([], 0x00010000, tableCount: 3, directory: [0x01, 0x05, 0x01, 0x05, 0x01, 0x85])[..54];
        Woff2Builder.U32(woff2, 8, 54);

        AssertInvalid(woff2, "table directory is read past its end");
    }

    [Test]
    public void Decode_Base128WithLeadingZero_Throws() =>
        AssertInvalid(Woff2Builder.Build([], 0x00010000, tableCount: 1, directory: [0x00, 0x80, 0x01]), "starts with a zero byte");

    [Test]
    public void Decode_Base128LongerThanFiveBytes_Throws() =>
        AssertInvalid(Woff2Builder.Build([], 0x00010000, tableCount: 1, directory: [0x00, 0x81, 0x81, 0x81, 0x81, 0x81, 0x01]), "longer than five bytes");

    [Test]
    public void Decode_Base128AboveUInt32_Throws() =>
        AssertInvalid(Woff2Builder.Build([], 0x00010000, tableCount: 1, directory: [0x00, 0x90, 0x80, 0x80, 0x80, 0x00]), "above 2^32 - 1");

    [Test]
    public void Decode_Base128OfFiveBytesAtTheLimit_IsRead()
    {
        // 0xFFFFFFFF in five bytes as an original length: read, and then refused for its size (a library limit), not as malformed.
        var woff2 = Woff2Builder.Build([], 0x00010000, tableCount: 1, directory: [0x00, 0x8F, 0xFF, 0xFF, 0xFF, 0x7F]);

        Assert.That(() => Woff2Decoder.Decode(woff2), Throws.TypeOf<NotSupportedException>());
    }

    [TestCase("glyf", 1, TestName = "Decode_GlyfTransformVersion1_Throws")]
    [TestCase("loca", 2, TestName = "Decode_LocaTransformVersion2_Throws")]
    [TestCase("hmtx", 2, TestName = "Decode_HmtxTransformVersion2_Throws")]
    [TestCase("cmap", 1, TestName = "Decode_CmapTransformVersion1_Throws")]
    public void Decode_UndefinedTransformVersion_Throws(string tag, int version)
    {
        var woff2 = Woff2Builder.Build([new Woff2Builder.Table(tag, [1, 2, 3, 4], version)]);

        AssertInvalid(woff2, "does not define for it");
    }

    [Test]
    public void Decode_GlyfTransformedButLocaNot_Throws()
    {
        var woff2 = Woff2Builder.Build([new("glyf", GlyfStreams.OneEmptyGlyph(), 0), new("loca", new byte[8], 3)]);

        AssertInvalid(woff2, "transforms only one of");
    }

    [Test]
    public void Decode_LocaTransformedWithLength_Throws()
    {
        var woff2 = Woff2Builder.Build([new("glyf", GlyfStreams.OneEmptyGlyph(), 0), new("loca", new byte[4], 0) { OrigLength = 8 }]);

        AssertInvalid(woff2, "its transform length is 4, not 0");
    }

    [Test]
    public void Decode_TagTwiceInOneFont_Throws()
    {
        var woff2 = Woff2Builder.Build([new("name", [1, 2, 3, 4]), new("name", [5, 6, 7, 8])]);

        AssertInvalid(woff2, "lists the table 'name' twice");
    }

    private static byte[] CollectionFile(uint version = 0x00010000, Action<List<Woff2Builder.Table>, List<(uint Flavor, int[] Tables)>>? change = null)
    {
        var tables = new List<Woff2Builder.Table>
        {
            new("head", Head(1)),
            new("glyf", GlyfStreams.OneEmptyGlyph(), 0),
            new("loca", [], 0) { OrigLength = 8 },
            new("head", Head(1)),
        };
        var fonts = new List<(uint Flavor, int[] Tables)> { (0x00010000, [0, 1, 2]), (0x00010000, [3, 1, 2]) };
        change?.Invoke(tables, fonts);
        return Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(version, fonts));
    }

    [Test]
    public void Decode_Collection_WritesOneDirectoryPerFontAndSharedTablesOnce()
    {
        var image = Woff2Decoder.Decode(CollectionFile(version: 0x00020000));

        var fontCount = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(8));
        var first = (int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(12));
        var second = (int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(16));
        var firstTables = SfntRecords(image, first);
        var secondTables = SfntRecords(image, second);

        Assert.Multiple(() =>
        {
            Assert.That(BinaryPrimitives.ReadUInt32BigEndian(image), Is.EqualTo(0x74746366u), "ttcf");
            Assert.That(BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(4)), Is.EqualTo(0x00010000u), "version 1.0 (no DSIG fields)");
            Assert.That(fontCount, Is.EqualTo(2u));
            Assert.That(firstTables.Keys, Is.EqualTo(new[] { "glyf", "head", "loca" }));
            Assert.That(secondTables.Keys, Is.EqualTo(new[] { "glyf", "head", "loca" }));
            Assert.That(firstTables["glyf"], Is.EqualTo(secondTables["glyf"]), "glyf is shared");
            Assert.That(firstTables["loca"], Is.EqualTo(secondTables["loca"]), "loca is shared");
            foreach (var font in new[] { first, second })
            {
                var records = SfntRecords(image, font);
                var directory = image.AsSpan(font, 12 + 16 * records.Count).ToArray();
                var sum = Sum(directory) + records.Aggregate(0u, (total, pair) => total + Sum(WithoutChecksumAdjustment(pair.Key,
                    image.AsSpan(pair.Value.Offset, (pair.Value.Length + 3) & ~3).ToArray())));
                var adjustment = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(records["head"].Offset + 8));
                Assert.That(sum + adjustment, Is.EqualTo(0xB1B0AFBAu), "each font's checksum");
            }
        });
    }

    [Test]
    public void Load_CraftedCollectionOfARealFont_LoadsEveryFaceAsTheOriginal()
    {
        var tables = ReadTables(FontCache.Bytes(TestFonts.SourceSans("SourceSans3-Regular.ttf")))
            .Select(table => new Woff2Builder.Table(table.Tag, table.Data)).ToList();
        var loca = tables.Single(table => table.Tag == "loca");
        tables.Remove(loca);
        tables.Insert(tables.FindIndex(table => table.Tag == "glyf") + 1, loca);
        var all = Enumerable.Range(0, tables.Count).ToArray();
        var woff2 = Woff2Builder.Build(tables, 0x74746366,
            new Woff2Builder.Collection(0x00010000, [(0x00010000, all), (0x00010000, all)]));

        var file = FontLoader.Load(woff2);

        Assert.Multiple(() =>
        {
            Assert.That(file.Format, Is.EqualTo(FontFormat.Woff2));
            Assert.That(file.Faces, Has.Count.EqualTo(2));
            foreach (var face in file.Faces)
                AssertSameOutlines(face, FontCache.Face(new TestFace("", TestFonts.SourceSans("SourceSans3-Regular.ttf"), 0)));
        });
    }

    [Test]
    public void Decode_CollectionVersion_Throws() =>
        AssertInvalid(CollectionFile(version: 0x00030000), "collection version is 0x00030000");

    [Test]
    public void Decode_CollectionWithoutFonts_Throws() =>
        AssertInvalid(CollectionFile(change: (_, fonts) => fonts.Clear()), "collection has no fonts");

    [Test]
    public void Decode_CollectionFontWithoutTables_Throws() =>
        AssertInvalid(CollectionFile(change: (_, fonts) => fonts[1] = (0x00010000, [])), "collection has no tables");

    [Test]
    public void Decode_CollectionTableIndexPastDirectory_Throws() =>
        AssertInvalid(CollectionFile(change: (_, fonts) => fonts[1] = (0x00010000, [3, 1, 9])), "lists table 9, but the directory has 4");

    [Test]
    public void Decode_CollectionFontWithGlyfButNoLoca_Throws() =>
        AssertInvalid(CollectionFile(change: (_, fonts) => fonts[1] = (0x00010000, [3, 1])), "has 'glyf' without 'loca'");

    [Test]
    public void Decode_CollectionLocaNotDirectlyAfterGlyf_Throws() =>
        AssertInvalid(CollectionFile(change: (tables, fonts) =>
        {
            tables.Insert(2, new("name", [1, 2, 3, 4]));
            fonts[0] = (0x00010000, [0, 1, 2, 3]);
            fonts[1] = (0x00010000, [4, 1, 2, 3]);
        }), "does not directly follow");

    [Test]
    public void Decode_CollectionWithMoreFontsThanTheFileHoldsBytesFor_Throws() =>
        AssertInvalid(Woff2Builder.Build([], 0x74746366, tableCount: 1, directory: [0x01, 0x36, 0x00, 0x01, 0x00, 0x00, 253, 0xEA, 0x60]),
            "(60000 fonts) runs past the end of the file");

    [Test]
    public void Decode_CollectionFontWithMoreTablesThanTheFileHoldsBytesFor_Throws() =>
        AssertInvalid(Woff2Builder.Build([], 0x74746366, tableCount: 1, directory: [0x01, 0x36, 0x00, 0x01, 0x00, 0x00, 1, 200, 0x00, 0x01, 0x00, 0x00]),
            "table list of font 0 of the WOFF2 collection runs past");

    [Test]
    public void Decode_CompressedDataPastEnd_Throws() =>
        AssertInvalid(Woff2Builder.U32(Valid(), 20, 0x7FFFFFFF), "compressed font data runs past the end");

    [Test]
    public void Decode_MetadataLengthWithoutOffset_Throws() =>
        AssertInvalid(Woff2Builder.U32(Valid(), 32, 5), "metadata block has a length but no offset");

    [Test]
    public void Decode_MetadataCompressedWithoutOriginalLength_Throws() =>
        AssertInvalid(Woff2Builder.U32(Woff2Builder.AppendBlocks(Valid(), metadata: [1, 2, 3, 4]), 36, 0), "has no original length");

    [Test]
    public void Decode_MetadataNotAtAlignedEndOfData_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), metadata: [1, 2, 3, 4]);

        AssertInvalid(Woff2Builder.U32(woff2, 28, BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(28)) + 4), "metadata block starts at");
    }

    [Test]
    public void Decode_MetadataPastEnd_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), metadata: [1, 2, 3, 4]);

        AssertInvalid(Woff2Builder.U32(woff2, 32, 0x7FFFFFFF), "metadata block runs past the end");
    }

    [Test]
    public void Decode_PrivateLengthWithoutOffset_Throws() =>
        AssertInvalid(Woff2Builder.U32(Valid(), 44, 5), "private data block has a length but no offset");

    [Test]
    public void Decode_PrivateBlockNotAtAlignedEndOfData_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), privateData: [1, 2, 3, 4]);

        AssertInvalid(Woff2Builder.U32(woff2, 40, BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(40)) + 4), "private data block starts at");
    }

    [Test]
    public void Decode_PrivateBlockPastEnd_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), privateData: [1, 2, 3, 4]);

        AssertInvalid(Woff2Builder.U32(woff2, 44, 0x7FFFFFFF), "private data block runs past the end");
    }

    [Test]
    public void Decode_BytesAfterLastBlock_Throws()
    {
        var woff2 = Valid().Concat(new byte[8]).ToArray();
        Woff2Builder.U32(woff2, 8, (uint)woff2.Length);

        AssertInvalid(woff2, "its blocks end at");
    }

    [Test]
    public void Decode_BrotliDataDamaged_Throws()
    {
        var woff2 = Valid();
        var start = Woff2Builder.CompressedStart(woff2);
        woff2.AsSpan(start, 64).Fill(0xFF);

        AssertInvalid(woff2, "Brotli data is damaged");
    }

    [Test]
    public void Decode_BrotliDataCutShort_Throws()
    {
        var woff2 = Woff2Builder.Edit(Valid(), _ => { }, brotliCut: 8);

        AssertInvalid(woff2, "Brotli data ends after");
    }

    [Test]
    public void Decode_BrotliOutputShorterThanTableLengths_Throws()
    {
        var woff2 = Woff2Builder.Build([new("name", new byte[100]) { OrigLength = 120 }]);

        AssertInvalid(woff2, "decodes to 100 bytes, but the table lengths add up to 120");
    }

    [Test]
    public void Decode_BrotliOutputLongerThanTableLengths_Throws()
    {
        var woff2 = Woff2Builder.Build([new("name", new byte[120]) { OrigLength = 100 }]);

        AssertInvalid(woff2, "decodes to more than the 100 bytes");
    }

    // ---- Denys, 2026-11-26: "Reject, like the spec" ----

    [Test]
    public void Decode_PaddingAfterTheCompressedDataNotZero_Throws()
    {
        var woff2 = Valid();
        var compressedEnd = Woff2Builder.CompressedEnd(woff2);
        Assert.That(compressedEnd % 4, Is.Not.Zero, "the test file has padding to change");
        woff2[compressedEnd] = 0xAA;

        AssertInvalid(woff2, "at the end of the WOFF2 file is not zero");
    }

    [Test]
    public void Decode_PaddingBeforeTheMetadataNotZero_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), metadata: [1, 2, 3, 4]);
        var compressedEnd = Woff2Builder.CompressedEnd(woff2);
        Assert.That(compressedEnd % 4, Is.Not.Zero, "the test file has padding to change");
        woff2[compressedEnd] = 0xAA;

        AssertInvalid(woff2, "before the WOFF2 metadata block is not zero");
    }

    [Test]
    public void Decode_PaddingBeforeThePrivateBlockNotZero_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), metadata: [1, 2, 3, 4, 5], privateData: [1, 2, 3]);
        var metaEnd = (int)(BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(28)) + BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(32)));
        woff2[metaEnd] = 0xAA;

        AssertInvalid(woff2, "before the WOFF2 private data block is not zero");
    }

    [Test]
    public void Decode_BytesAfterThePrivateBlock_Throws()
    {
        var woff2 = Woff2Builder.AppendBlocks(Valid(), privateData: [1, 2, 3, 4, 5]).Concat(new byte[3]).ToArray();
        Woff2Builder.U32(woff2, 8, (uint)woff2.Length);

        AssertInvalid(woff2, "3 bytes after its private data block");
    }

    // ---- The memory and work bounds ----

    /// <summary>A collection of <paramref name="fonts"/> fonts over one glyf, loca and hhea, each with its own transformed hmtx of three bytes.</summary>
    private static byte[] HmtxCollection(int fonts, int glyphs)
    {
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 1);
        var empty = new GlyfStreams();
        for (var i = 0; i < glyphs; i++)
            empty.AddEmpty();

        var tables = new List<Woff2Builder.Table>
        {
            new("head", Head(1)),
            new("glyf", empty.Build(), 0),
            new("loca", [], 0) { OrigLength = (glyphs + 1) * 4 },
            new("hhea", hhea),
        };
        var list = new List<(uint Flavor, int[] Tables)>();
        for (var i = 0; i < fonts; i++)
        {
            tables.Add(new Woff2Builder.Table("hmtx", [0x03, 0x00, 0x64], 1) { OrigLength = 4 });
            list.Add((0x00010000, [0, 1, 2, 3, 4 + i]));
        }

        return Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(0x00010000, list));
    }

    [Test]
    public void Decode_CollectionOfTransformedHmtxTables_RebuildsEach()
    {
        var image = Woff2Decoder.Decode(HmtxCollection(3, 100));

        var second = (int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(16));
        var hmtx = SfntRecords(image, second)["hmtx"];
        var table = image.AsSpan(hmtx.Offset, hmtx.Length).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(table, Has.Length.EqualTo(2 * 1 + 2 * 100));
            Assert.That(table[..4], Is.EqualTo(new byte[] { 0, 100, 0, 0 }), "advance, then lsb = xMin of the empty glyph");
            Assert.That(table[4..], Is.All.Zero);
        });
    }

    /// <summary>
    /// A collection of 4,000 fonts, each with a transformed hmtx of 3 bytes that rebuilds to 10,002: 40 MB of tables from
    /// about 50 KB of file (the reviewer's file, scaled to a small limit). With a 4 MiB limit the charges stop it at about 400
    /// fonts; the decoder allocates its limit and a fixed scratch, not the 40 MB.
    /// </summary>
    [Test]
    public void Decode_ManyTransformedHmtxTablesOverTheLimit_ThrowsNotSupportedWithBoundedAllocation()
    {
        var woff2 = HmtxCollection(4000, 5000);
        const long limit = 4 * 1024 * 1024;

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => Woff2Decoder.Decode(woff2, limit, null), Throws.TypeOf<NotSupportedException>().With.Message.Contains("hmtx"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(2 * limit), $"bytes allocated for a {woff2.Length}-byte file");
    }

    /// <summary>The same shape over a plain glyf: its glyph boxes are read once, not once per hmtx.</summary>
    [Test]
    public void Decode_ManyTransformedHmtxTablesOverAPlainGlyf_ReadsTheGlyphBoxesOnce()
    {
        const int glyphs = 300;
        var head = Head(1);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), glyphs);
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 1);
        var tables = new List<Woff2Builder.Table> { new("head", head), new("maxp", maxp), new("hhea", hhea), new("glyf", new byte[4]), new("loca", new byte[(glyphs + 1) * 4]) };
        var list = new List<(uint Flavor, int[] Tables)>();
        for (var i = 0; i < 50; i++)
        {
            tables.Add(new Woff2Builder.Table("hmtx", [0x03, 0x00, 0x64], 1) { OrigLength = 4 });
            list.Add((0x00010000, [0, 1, 2, 3, 4, 5 + i]));
        }

        var statistics = new Woff2Decoder.Statistics();
        Woff2Decoder.Decode(Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(0x00010000, list)), FontLoader.MaxFileSize, statistics);

        Assert.That(statistics.GlyphBoxReads, Is.EqualTo(glyphs), "glyph boxes read for 50 hmtx tables");
    }

    /// <summary>One table shared by 200 fonts is checksummed once, when it is written (not once per font).</summary>
    [Test]
    public void Decode_TableSharedByManyFonts_IsChecksummedOnce()
    {
        var tables = new List<Woff2Builder.Table> { new("name", new byte[1024 * 1024]), new("head", Head(1)) };
        var fonts = Enumerable.Range(0, 200).Select(_ => (Flavor: 0x00010000u, Tables: new[] { 0, 1 })).ToList();
        var statistics = new Woff2Decoder.Statistics();

        Woff2Decoder.Decode(Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(0x00010000, fonts)), FontLoader.MaxFileSize, statistics);

        Assert.That(statistics.ChecksummedBytes, Is.EqualTo(1024 * 1024 + 54), "bytes checksummed for 200 fonts over 2 tables");
    }

    /// <summary>The glyf scratch arrays (700 KiB) are allocated once per file, not once per glyf table.</summary>
    [Test]
    public void Decode_ManyGlyfTables_AllocateTheScratchOnce()
    {
        var tables = new List<Woff2Builder.Table> { new("head", Head(1)) };
        var fonts = new List<(uint Flavor, int[] Tables)>();
        for (var i = 0; i < 300; i++)
        {
            tables.Add(new Woff2Builder.Table("glyf", GlyfStreams.OneEmptyGlyph(), 0));
            tables.Add(new Woff2Builder.Table("loca", [], 0) { OrigLength = 8 });
            fonts.Add((0x00010000, [0, 1 + 2 * i, 2 + 2 * i]));
        }

        var woff2 = Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(0x00010000, fonts));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Woff2Decoder.Decode(woff2);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(8 * 1024 * 1024), "bytes allocated for 300 glyf tables (once per table: 190 MB)");
    }

    [Test]
    public void Decode_CollectionOfSingleTableFonts_Decodes()
    {
        // 100 fonts of one table each take 6 bytes of collection directory apiece, the minimum.
        var tables = new List<Woff2Builder.Table> { new("head", Head(1)) };
        var fonts = Enumerable.Range(0, 100).Select(_ => (Flavor: 0x00010000u, Tables: new[] { 0 })).ToList();

        var image = Woff2Decoder.Decode(Woff2Builder.Build(tables, 0x74746366, new Woff2Builder.Collection(0x00010000, fonts)));

        Assert.That(BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(8)), Is.EqualTo(100u));
    }

    /// <summary>The Source Sans 3 files decode with allocations of a few times the image: Brotli buffer, rebuilt tables, image.</summary>
    [TestCaseSource(nameof(Woff2Files))]
    public void Decode_EveryWoff2_AllocatesAFewTimesTheImage(string path)
    {
        var file = FontCache.Bytes(path);
        Woff2Decoder.Decode(file); // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        var image = Woff2Decoder.Decode(file);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.Out.WriteLine($"{Path.GetFileName(path)}: {allocated} bytes for an image of {image.Length} ({(double)allocated / image.Length:F2}x)");
        Assert.That(allocated, Is.LessThan(4L * image.Length + 2 * 1024 * 1024));
    }

    // ---- The two limits and the glyf sink, with a small limit ----

    [Test]
    public void Decode_ImageOverTheLimit_ThrowsNotSupported()
    {
        var woff2 = Woff2Builder.Build([new("name", new byte[100]), new("post", new byte[100])]);
        var length = Woff2Decoder.Decode(woff2).Length;

        Assert.Multiple(() =>
        {
            Assert.That(Woff2Decoder.Decode(woff2, length, null), Has.Length.EqualTo(length), "at the limit");
            Assert.That(() => Woff2Decoder.Decode(woff2, length - 1, null), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"), "one byte over");
        });
    }

    [Test]
    public void Decode_RebuiltLocaOverTheLimit_ThrowsNotSupported()
    {
        // 1,000 empty glyphs take about 2.2 KB of stream and rebuild to a 4,004-byte loca; the limit is the stream's length.
        var glyphs = new GlyfStreams();
        for (var i = 0; i < 1000; i++)
            glyphs.AddEmpty();

        var data = glyphs.Build();
        var woff2 = Woff2Builder.Build([new("head", Head(1)), new("glyf", data, 0), new("loca", [], 0) { OrigLength = 4004 }]);

        Assert.That(() => Woff2Decoder.Decode(woff2, 54 + data.Length, null), Throws.TypeOf<NotSupportedException>().With.Message.Contains("loca and glyph boxes"));
    }

    [Test]
    public void Decode_RebuiltGlyfOverWhatIsLeft_ThrowsNotSupported()
    {
        var points = new (int X, int Y, bool On)[] { (1000, 9000, true), (9000, 1000, true), (1000, 9000, true) };
        var glyphs = new GlyfStreams();
        for (var i = 0; i < 10; i++)
            glyphs.AddSimple([points]);

        var data = glyphs.Build();
        var woff2 = Woff2Builder.Build([new("head", Head(1)), new("glyf", data, 0), new("loca", [], 0) { OrigLength = 44 }]);

        Assert.Multiple(() =>
        {
            Assert.That(Woff2Decoder.Decode(woff2, FontLoader.MaxFileSize, null), Is.Not.Empty, "within the real limit");
            Assert.That(() => Woff2Decoder.Decode(woff2, 54 + data.Length, null), Throws.TypeOf<NotSupportedException>().With.Message.Contains("'glyf' table"));
        });
    }

    // ---- The layout of a single font (FreeType's order) ----

    [Test]
    public void Decode_DirectoryNotInTagOrder_LaysDataOutInTagOrderWithLocaAfterGlyf()
    {
        var woff2 = Woff2Builder.Build(
        [
            new("post", [1, 2, 3, 4]),
            new("loca", [], 0) { OrigLength = 8 },
            new("name", [5, 6, 7, 8]),
            new("head", Head(1)),
            new("glyf", GlyfStreams.OneEmptyGlyph(), 0),
            new("cmap", [9, 9, 9, 9]),
        ]);

        var image = Woff2Decoder.Decode(woff2);

        var records = SfntRecords(image, 0);
        Assert.Multiple(() =>
        {
            Assert.That(records.Keys, Is.EqualTo(new[] { "cmap", "glyf", "head", "loca", "name", "post" }), "records in tag order");
            Assert.That(records.OrderBy(pair => pair.Value.Offset).Select(pair => pair.Key), Is.EqualTo(new[] { "cmap", "glyf", "loca", "head", "name", "post" }), "data: tag order, loca right after glyf");
        });
    }

    // ---- Decompression bombs ----

    /// <summary>Table lengths that add up past 128 MiB: a library limit, refused from the directory (the bound is 64 KiB of allocation).</summary>
    [Test]
    public void Decode_DeclaredLengthsOverMaxFileSize_ThrowsNotSupportedWithoutAllocating()
    {
        var woff2 = Woff2Builder.Build([new("name", [1, 2, 3, 4]) { OrigLength = FontLoader.MaxFileSize / 2 + 4 }, new("post", [1, 2, 3, 4]) { OrigLength = FontLoader.MaxFileSize / 2 + 4 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => Woff2Decoder.Decode(woff2), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    /// <summary>A header that declares 100 MiB (within the limit) over a stream of 10 bytes: the buffer follows the output, not the declaration.</summary>
    [Test]
    public void Decode_DeclaredLengthFarAboveTheStream_ThrowsWithSmallAllocation()
    {
        var woff2 = Woff2Builder.Build([new("name", new byte[10]) { OrigLength = 100 * 1024 * 1024 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => Woff2Decoder.Decode(woff2), Throws.TypeOf<InvalidDataException>().With.Message.Contains("decodes to 10 bytes"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    /// <summary>A Brotli stream of 64 MiB of zeros under a header that declares 1,000 bytes: decoding stops one byte past the declaration.</summary>
    [Test]
    public void Decode_StreamProducingFarMoreThanDeclared_ThrowsWithSmallAllocation()
    {
        var woff2 = Woff2Builder.Build([new("name", new byte[64 * 1024 * 1024]) { OrigLength = 1000 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => Woff2Decoder.Decode(woff2), Throws.TypeOf<InvalidDataException>().With.Message.Contains("decodes to more than the 1000 bytes"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    [Test]
    public void Load_DecodedWoff2OverMaxFileSize_ThrowsNotSupported()
    {
        var woff2 = Woff2Builder.Build([new("name", [1, 2, 3, 4]) { OrigLength = FontLoader.MaxFileSize + 4 }]);

        Assert.That(() => FontLoader.Load(woff2), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"));
    }

    // ---- The glyf transform ----

    private static byte[] Head(int indexToLocFormat)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), (short)indexToLocFormat);
        return head;
    }

    /// <summary>A WOFF2 holding <c>head</c>, a transformed <c>glyf</c> (<paramref name="glyf"/>) and its <c>loca</c>.</summary>
    private static byte[] GlyfFile(GlyfStreams glyf, int headFormat = 1, int? locaOrigLength = null)
    {
        var data = glyf.Build();
        return Woff2Builder.Build(
        [
            new("head", Head(headFormat)),
            new("glyf", data, 0),
            new("loca", [], 0) { OrigLength = locaOrigLength ?? (glyf.GlyphCount + 1) * (glyf.IndexFormat == 0 ? 2 : 4) },
        ]);
    }

    private static (byte[] Glyf, byte[] Loca) DecodeGlyf(byte[] woff2)
    {
        var tables = SfntTables.Read(Woff2Decoder.Decode(woff2));
        return (tables["glyf"], tables["loca"]);
    }

    private static readonly (int X, int Y, bool On)[] Triangle = [(0, 0, true), (100, 0, true), (50, 80, false)];

    [Test]
    public void Decode_SimpleGlyph_RebuildsGlyphAndLoca()
    {
        var glyphs = new GlyfStreams().AddEmpty().AddSimple([Triangle], instructions: [0xB0, 0x01]).AddEmpty();

        var (glyf, loca) = DecodeGlyf(GlyfFile(glyphs));

        var offsets = Enumerable.Range(0, 4).Select(i => (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4 * i))).ToArray();
        var glyph = TrueTypeGlyph.Parse(glyf.AsSpan(offsets[1], offsets[2] - offsets[1]));
        Assert.Multiple(() =>
        {
            Assert.That(offsets[1], Is.Zero);
            Assert.That(offsets[2] % 4, Is.Zero, "padded to four bytes");
            Assert.That(offsets[3], Is.EqualTo(offsets[2]), "empty glyph");
            Assert.That(glyf, Has.Length.EqualTo(offsets[3]));
            Assert.That(glyph.Contours, Is.EqualTo(1));
            Assert.That(glyph.Box, Is.EqualTo((0, 0, 100, 80)), "computed box");
            Assert.That(glyph.EndPoints, Is.EqualTo(new[] { 2 }));
            Assert.That(glyph.Instructions, Is.EqualTo(new byte[] { 0xB0, 0x01 }));
            Assert.That(glyph.Points, Is.EqualTo(Triangle));
        });
    }

    [Test]
    public void Decode_ShortIndexFormat_WritesHalfOffsets()
    {
        var glyphs = new GlyfStreams { IndexFormat = 0 }.AddSimple([Triangle]).AddSimple([Triangle]);

        var (glyf, loca) = DecodeGlyf(GlyfFile(glyphs, headFormat: 0));

        Assert.Multiple(() =>
        {
            Assert.That(loca, Has.Length.EqualTo(6));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(loca.AsSpan(4)) * 2, Is.EqualTo(glyf.Length));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(loca.AsSpan(2)) * 2, Is.EqualTo(glyf.Length / 2));
        });
    }

    [Test]
    public void Decode_SeveralContoursAndExplicitBox_KeepsTheBoxAndEndPoints()
    {
        var square = new (int X, int Y, bool On)[] { (10, 10, true), (10, 60, true), (60, 60, true), (60, 10, true) };
        var glyphs = new GlyfStreams().AddSimple([square, Triangle], box: (-5, -6, 700, 800));

        var (glyf, _) = DecodeGlyf(GlyfFile(glyphs));

        var glyph = TrueTypeGlyph.Parse(glyf);
        Assert.Multiple(() =>
        {
            Assert.That(glyph.Box, Is.EqualTo((-5, -6, 700, 800)));
            Assert.That(glyph.EndPoints, Is.EqualTo(new[] { 3, 6 }));
            Assert.That(glyph.Points, Is.EqualTo(square.Concat(Triangle)));
        });
    }

    [Test]
    public void Decode_OverlapBitmap_SetsTheFlagOnTheFirstPointOfMarkedSimpleGlyphs()
    {
        var glyphs = new GlyfStreams().AddSimple([Triangle], overlap: true).AddSimple([Triangle], overlap: false).AddSimple([Triangle], overlap: true);

        var (glyf, loca) = DecodeGlyf(GlyfFile(glyphs));

        var flags = Enumerable.Range(0, 3).Select(i =>
        {
            var start = (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4 * i));
            return TrueTypeGlyph.Parse(glyf.AsSpan(start)).FirstFlag;
        }).ToArray();
        Assert.That(flags.Select(flag => (flag & 0x40) != 0), Is.EqualTo(new[] { true, false, true }));
    }

    [Test]
    public void Decode_NoOverlapBitmap_LeavesTheFlagClear()
    {
        var glyphs = new GlyfStreams().AddSimple([Triangle]);

        var (glyf, _) = DecodeGlyf(GlyfFile(glyphs));

        Assert.That(TrueTypeGlyph.Parse(glyf).FirstFlag & 0x40, Is.Zero);
    }

    [TestCase(253, 700, TestName = "Decode_PointCountAs253Form_ReadsTheValue")]
    [TestCase(254, 600, TestName = "Decode_PointCountAs254Form_ReadsTheValue")]
    [TestCase(255, 300, TestName = "Decode_PointCountAs255Form_ReadsTheValue")]
    public void Decode_PointCountEncodings_AllReadTheirValue(int form, int count)
    {
        var points = Enumerable.Range(0, count).Select(i => (X: i * 3, Y: (i % 7) * 5, On: i % 2 == 0)).ToArray();
        var glyphs = new GlyfStreams { Form255 = form }.AddSimple([points]);

        var (glyf, _) = DecodeGlyf(GlyfFile(glyphs));

        Assert.That(TrueTypeGlyph.Parse(glyf).Points, Is.EqualTo(points));
    }

    [Test]
    public void Decode_EveryTripletFlag_DecodesAsTheFormatsTable()
    {
        var failures = new List<string>();
        var patterns = new[] { new byte[] { 0x00, 0x00, 0x00, 0x00 }, [0x7F, 0x55, 0x2A, 0x7E], [0x13, 0xA7, 0x4C, 0x01] };
        for (var index = 0; index < 128; index++)
        {
            foreach (var onCurve in new[] { true, false })
            {
                foreach (var pattern in patterns)
                {
                    var flag = (byte)(index | (onCurve ? 0 : 0x80));
                    var glyphs = new GlyfStreams().AddRawPoint(flag, pattern.Take(SpecTriplet.ByteCount(index)).ToArray());
                    var (glyf, _) = DecodeGlyf(GlyfFile(glyphs));
                    var point = TrueTypeGlyph.Parse(glyf).Points.Single();

                    var (dx, dy) = SpecTriplet.Decode(index, pattern);
                    if (point != (dx, dy, onCurve))
                        failures.Add($"flag {index} bytes {Convert.ToHexString(pattern)}: {point}, expected ({dx}, {dy}, {onCurve})");
                }
            }
        }

        Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(10)));
    }

    [Test]
    public void Decode_CompositeGlyph_CopiesComponentsBoxAndInstructions()
    {
        // Two components: glyph 1 with word arguments and a scale, then glyph 2 with byte arguments; instructions follow.
        byte[] components =
        [
            0x01, 0x2B, 0x00, 0x01, 0x00, 0x0A, 0xFF, 0xF6, 0x40, 0x00, // MORE | ARGS_WORDS | XY | SCALE | HAVE_INSTRUCTIONS (0x012B)
            0x01, 0x00, 0x00, 0x02, 0x05, 0x06,                            // last component: byte arguments, HAVE_INSTRUCTIONS
        ];
        var glyphs = new GlyfStreams().AddSimple([Triangle]).AddSimple([Triangle]).AddComposite(components, [0xB1, 0x02, 0x03], (1, 2, 3, 4));

        var (glyf, loca) = DecodeGlyf(GlyfFile(glyphs));

        var start = (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(8));
        var composite = glyf.AsSpan(start).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(BinaryPrimitives.ReadInt16BigEndian(composite), Is.EqualTo(-1));
            Assert.That(composite[2..10], Is.EqualTo(new byte[] { 0, 1, 0, 2, 0, 3, 0, 4 }), "box");
            Assert.That(composite[10..(10 + components.Length)], Is.EqualTo(components));
            Assert.That(composite[(10 + components.Length)..(10 + components.Length + 5)], Is.EqualTo(new byte[] { 0, 3, 0xB1, 0x02, 0x03 }), "instructions with their length");
        });
    }

    [Test]
    public void Decode_CompositeWithoutInstructions_WritesNoInstructionBlock()
    {
        byte[] components = [0x00, 0x02, 0x00, 0x01, 0x00, 0x00];
        var glyphs = new GlyfStreams().AddSimple([Triangle]).AddComposite(components, null, (0, 0, 9, 9));

        var (_, loca) = DecodeGlyf(GlyfFile(glyphs));

        var start = (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4));
        var end = (int)BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(8));
        Assert.That(end - start, Is.EqualTo(16), "the 10-byte header and the 6-byte record, with no instruction block");
    }

    // ---- glyf strict rules ----

    private static void AssertGlyfInvalid(GlyfStreams glyphs, string message, int headFormat = 1) =>
        AssertInvalid(GlyfFile(glyphs, headFormat), message);

    [Test]
    public void Decode_GlyfShorterThanHeader_Throws() =>
        AssertInvalid(Woff2Builder.Build([new("glyf", new byte[35], 0), new("loca", [], 0) { OrigLength = 4 }]), "shorter than its 36-byte header");

    [TestCase(0, "nContourStream", TestName = "Decode_GlyfNContourStreamPastEnd_Throws")]
    [TestCase(6, "instructionStream", TestName = "Decode_GlyfInstructionStreamPastEnd_Throws")]
    public void Decode_GlyfStreamSizePastEnd_Throws(int stream, string name)
    {
        var data = new GlyfStreams().AddSimple([Triangle]).Build();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8 + 4 * stream), 0x00FFFFFF);

        AssertInvalid(Woff2Builder.Build([new("glyf", data, 0), new("loca", [], 0) { OrigLength = 8 }]), $"{name} (16777215 bytes) runs past the end");
    }

    [Test]
    public void Decode_GlyfIndexFormatAboveOne_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { IndexFormat = 2 }.AddEmpty(), "index format 2, not 0 or 1");

    [Test]
    public void Decode_GlyfIndexFormatDiffersFromHead_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { IndexFormat = 1 }.AddEmpty(), "but 'head' says 0", headFormat: 0);

    [Test]
    public void Decode_GlyfNContourStreamShorterThanGlyphCount_Throws()
    {
        var data = new GlyfStreams().AddEmpty().AddEmpty().Build();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 2); // nContourStreamSize: one glyph's worth for two glyphs

        AssertInvalid(Woff2Builder.Build([new("glyf", data, 0), new("loca", [], 0) { OrigLength = 12 }]), "nContourStream has 2 bytes, fewer than the 4");
    }

    [Test]
    public void Decode_GlyfGlyphWithTwoNegativeContours_Throws() =>
        AssertGlyfInvalid(new GlyfStreams().AddRawContourCount(-2), "only -1 marks a composite");

    [Test]
    public void Decode_GlyfEmptyGlyphWithExplicitBox_Throws() =>
        AssertGlyfInvalid(new GlyfStreams().AddEmpty(explicitBox: true), "is empty but has an explicit bounding box");

    [Test]
    public void Decode_GlyfCompositeWithoutBox_Throws() =>
        AssertGlyfInvalid(new GlyfStreams().AddComposite([0x00, 0x00, 0x00, 0x00, 0x00, 0x00], null, box: null), "has no explicit bounding box");

    [Test]
    public void Decode_GlyfContourWithoutPoints_Throws() =>
        AssertGlyfInvalid(new GlyfStreams().AddSimple([[]]), "has no points");

    [Test]
    public void Decode_GlyfMoreThan65535Points_Throws()
    {
        var first = Enumerable.Range(0, 40_000).Select(i => (X: i % 100, Y: i % 50, On: true)).ToArray();
        var second = Enumerable.Range(0, 25_536).Select(i => (X: i % 100, Y: i % 50, On: true)).ToArray();

        AssertGlyfInvalid(new GlyfStreams().AddSimple([first, second]), "more than 65535 points");
    }

    [Test]
    public void Decode_GlyfMorePointsThanFlags_Throws()
    {
        var data = new GlyfStreams().AddSimple([Triangle]).Build();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), 2); // flagStreamSize

        AssertInvalid(Woff2Builder.Build([new("glyf", data, 0), new("loca", [], 0) { OrigLength = 8 }]), "has 3 points, but the flagStream has 2 flags left");
    }

    [Test]
    public void Decode_GlyfPointCountsPastTheirStream_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { DropFrom = GlyfStreams.Stream.Points }.AddSimple([Triangle]), "nPointsStream is read past its end");

    [Test]
    public void Decode_GlyfCoordinatesPastTheirStream_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { DropFrom = GlyfStreams.Stream.Glyph }.AddSimple([Triangle]), "glyphStream is read past its end");

    [Test]
    public void Decode_GlyfInstructionsPastTheirStream_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { DropFrom = GlyfStreams.Stream.Instructions }.AddSimple([Triangle], instructions: [1, 2, 3]), "instructionStream is read past its end");

    [Test]
    public void Decode_GlyfCompositeRecordPastItsStream_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { DropFrom = GlyfStreams.Stream.Composite }.AddComposite([0x00, 0x03, 0x00, 0x01, 0x00, 0x00], null, (0, 0, 1, 1)), "compositeStream ends inside");

    [Test]
    public void Decode_GlyfBoxPastItsStream_Throws() =>
        AssertGlyfInvalid(new GlyfStreams { DropFrom = GlyfStreams.Stream.Box }.AddSimple([Triangle], box: (0, 0, 1, 1)), "bboxStream is read past its end");

    [Test]
    public void Decode_GlyfBoxStreamShorterThanItsBitmap_Throws()
    {
        var data = new GlyfStreams().AddEmpty().Build();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(28), 2); // bboxStreamSize

        AssertInvalid(Woff2Builder.Build([new("glyf", data, 0), new("loca", [], 0) { OrigLength = 8 }]), "bboxStream (2 bytes) is shorter than its 4-byte bitmap");
    }

    [Test]
    public void Decode_GlyfOverlapBitmapPastEnd_Throws()
    {
        var data = new GlyfStreams().AddSimple([Triangle], overlap: true).Build();

        AssertInvalid(Woff2Builder.Build([new("glyf", data[..^1], 0), new("loca", [], 0) { OrigLength = 8 }]), "overlap bitmap runs past the end");
    }

    [TestCase(40_000, 0, TestName = "Decode_GlyfDeltaAbove16Bit_Throws")]
    [TestCase(20_000, 20_000, TestName = "Decode_GlyfCoordinateAbove16Bit_Throws")]
    public void Decode_GlyfCoordinateOutside16BitRange_Throws(int firstX, int secondX)
    {
        var points = secondX == 0
            ? new (int X, int Y, bool On)[] { (firstX, 0, true) }
            : [(firstX, 0, true), (firstX + secondX, 0, true)];

        AssertGlyfInvalid(new GlyfStreams().AddSimple([points]), "outside the 16-bit range");
    }

    [Test]
    public void Decode_GlyfTooLongForShortLoca_Throws()
    {
        var points = Enumerable.Range(0, 20_000).Select(i => (X: i % 2 == 0 ? 1000 : 9000, Y: i % 2 == 0 ? 9000 : 1000, On: true)).ToArray();
        var glyphs = new GlyfStreams { IndexFormat = 0 }.AddSimple([points]).AddSimple([points]).AddSimple([points]);

        AssertGlyfInvalid(glyphs, "too long for the short 'loca' format", headFormat: 0);
    }

    [Test]
    public void Decode_GlyfLocaOriginalLengthWrong_Throws() =>
        AssertInvalid(GlyfFile(new GlyfStreams().AddEmpty(), locaOrigLength: 12), "'loca' table's original length is 12, but 1 glyphs need 8");

    // ---- The hmtx transform ----

    private static (byte[] Ttf, List<CraftedTable> Tables, int GlyphCount, int MetricCount, short[] XMin) RealTtf()
    {
        var ttf = FontCache.Bytes(TestFonts.SourceSans("SourceSans3-Regular.ttf"));
        var tables = ReadTables(ttf);
        var glyphCount = BinaryPrimitives.ReadUInt16BigEndian(tables.Single(table => table.Tag == "maxp").Data.AsSpan(4));
        var metricCount = BinaryPrimitives.ReadUInt16BigEndian(tables.Single(table => table.Tag == "hhea").Data.AsSpan(34));
        var loca = tables.Single(table => table.Tag == "loca").Data;
        var glyf = tables.Single(table => table.Tag == "glyf").Data;
        var longOffsets = BinaryPrimitives.ReadInt16BigEndian(tables.Single(table => table.Tag == "head").Data.AsSpan(50)) != 0;
        long At(int i) => longOffsets ? BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4 * i)) : 2L * BinaryPrimitives.ReadUInt16BigEndian(loca.AsSpan(2 * i));
        var xMin = Enumerable.Range(0, glyphCount).Select(i => At(i + 1) > At(i) ? BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan((int)At(i) + 2)) : (short)0).ToArray();
        return (ttf, tables, glyphCount, metricCount, xMin);
    }

    /// <summary>The transformed <c>hmtx</c> of a real TrueType font: <paramref name="flags"/> say which lsb arrays are left out.</summary>
    private static byte[] TransformedHmtx(byte flags, List<CraftedTable> tables, int glyphCount, int metricCount, out byte[] expected, short[] xMin)
    {
        var hmtx = tables.Single(table => table.Tag == "hmtx").Data;
        var output = new List<byte> { flags };
        for (var i = 0; i < metricCount; i++)
            output.AddRange(hmtx.AsSpan(4 * i, 2).ToArray());

        var rebuilt = (byte[])hmtx.Clone();
        if ((flags & 1) == 0)
        {
            for (var i = 0; i < metricCount; i++)
                output.AddRange(hmtx.AsSpan(4 * i + 2, 2).ToArray());
        }
        else
        {
            for (var i = 0; i < metricCount; i++)
                BinaryPrimitives.WriteInt16BigEndian(rebuilt.AsSpan(4 * i + 2), xMin[i]);
        }

        for (var i = metricCount; i < glyphCount; i++)
        {
            var at = 4 * metricCount + 2 * (i - metricCount);
            if ((flags & 2) == 0)
                output.AddRange(hmtx.AsSpan(at, 2).ToArray());
            else
                BinaryPrimitives.WriteInt16BigEndian(rebuilt.AsSpan(at), xMin[i]);
        }

        expected = rebuilt;
        return output.ToArray();
    }

    private static byte[] HmtxFile(byte[] hmtx, List<CraftedTable> tables, Func<CraftedTable, bool>? keep = null) =>
        Woff2Builder.Build(tables.Where(table => table.Tag != "hmtx" && (keep?.Invoke(table) ?? true))
            .Select(table => new Woff2Builder.Table(table.Tag, table.Data))
            .Append(new Woff2Builder.Table("hmtx", hmtx, 1) { OrigLength = 4 }).ToList());

    [TestCase(0x01, TestName = "Decode_HmtxProportionalLsbOmitted_RebuildsFromGlyphBoxes")]
    [TestCase(0x02, TestName = "Decode_HmtxMonospacedLsbOmitted_RebuildsFromGlyphBoxes")]
    [TestCase(0x03, TestName = "Decode_HmtxBothLsbArraysOmitted_RebuildsFromGlyphBoxes")]
    public void Decode_HmtxTransformFlags_RebuildLeftSideBearings(int flags)
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        Assert.That(metricCount, Is.LessThan(glyphCount), "the test font has monospaced entries");
        var transformed = TransformedHmtx((byte)flags, tables, glyphCount, metricCount, out var expected, xMin);

        var rebuilt = SfntTables.Read(Woff2Decoder.Decode(HmtxFile(transformed, tables)))["hmtx"];

        Assert.That(rebuilt, Is.EqualTo(expected));
    }

    [Test]
    public void Load_TransformedHmtxOverAPlainGlyf_LoadsAsTheOriginal()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);

        var face = FontLoader.Load(HmtxFile(transformed, tables)).Faces[0];

        AssertSameOutlines(face, FontCache.Face(new TestFace("", TestFonts.SourceSans("SourceSans3-Regular.ttf"), 0)));
    }

    [Test]
    public void Decode_HmtxLongerThanItsFlagsSay_DecodesTheSame()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out var expected, xMin);

        var rebuilt = SfntTables.Read(Woff2Decoder.Decode(HmtxFile([.. transformed, 9, 9], tables)))["hmtx"];

        Assert.That(rebuilt, Is.EqualTo(expected));
    }

    [Test]
    public void Decode_HmtxTransformWithoutGlyf_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);

        AssertInvalid(HmtxFile(transformed, tables, table => table.Tag is not ("glyf" or "loca")), "needs 'glyf' and 'hhea'");
    }

    [Test]
    public void Decode_HmtxTransformWithoutHhea_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);

        AssertInvalid(HmtxFile(transformed, tables, table => table.Tag != "hhea"), "needs 'glyf' and 'hhea'");
    }

    [Test]
    public void Decode_HmtxReservedFlagBits_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        transformed[0] = 0x07;

        AssertInvalid(HmtxFile(transformed, tables), "reserved flag bits set");
    }

    [Test]
    public void Decode_HmtxBothLsbArraysStored_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x00, tables, glyphCount, metricCount, out _, xMin);

        AssertInvalid(HmtxFile(transformed, tables), "has flags 0");
    }

    [Test]
    public void Decode_HmtxEmpty_Throws()
    {
        var (_, tables, _, _, _) = RealTtf();

        AssertInvalid(HmtxFile([], tables), "'hmtx' table is empty");
    }

    [Test]
    public void Decode_HmtxWithHheaTooShortForTheMetricCount_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        var changed = tables.Select(table => table.Tag == "hhea" ? table with { Data = table.Data[..34] } : table).ToList();

        AssertInvalid(HmtxFile(transformed, changed), "'hhea' table is too short");
    }

    [Test]
    public void Decode_HmtxOverPlainGlyfWithoutMaxp_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);

        AssertInvalid(HmtxFile(transformed, tables, table => table.Tag != "maxp"), "needs 'head' and 'maxp'");
    }

    [Test]
    public void Decode_HmtxOverPlainGlyfWithShortLoca_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        var changed = tables.Select(table => table.Tag == "loca" ? table with { Data = table.Data[..16] } : table).ToList();

        AssertInvalid(HmtxFile(transformed, changed), "'loca' table is too short for the glyph count");
    }

    [Test]
    public void Decode_HmtxOverPlainGlyfWithGlyphPastTheTable_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        var changed = tables.Select(table => table.Tag == "glyf" ? table with { Data = table.Data[..100] } : table).ToList();

        AssertInvalid(HmtxFile(transformed, changed), "is past the table's end");
    }

    [Test]
    public void Decode_HmtxMetricCountAboveGlyphCount_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        var hhea = tables.Single(table => table.Tag == "hhea").Data.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), (ushort)(glyphCount + 1));
        var changed = tables.Select(table => table.Tag == "hhea" ? table with { Data = hhea } : table).ToList();

        AssertInvalid(HmtxFile(transformed, changed), "metrics, but the font has");
    }

    [Test]
    public void Decode_HmtxMetricCountZero_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);
        var hhea = tables.Single(table => table.Tag == "hhea").Data.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 0);
        var changed = tables.Select(table => table.Tag == "hhea" ? table with { Data = hhea } : table).ToList();

        AssertInvalid(HmtxFile(transformed, changed), "0 metrics, but the font has");
    }

    [Test]
    public void Decode_HmtxShorterThanItsFlagsSay_Throws()
    {
        var (_, tables, glyphCount, metricCount, xMin) = RealTtf();
        var transformed = TransformedHmtx(0x03, tables, glyphCount, metricCount, out _, xMin);

        AssertInvalid(HmtxFile(transformed[..^2], tables), "but its flags and");
    }

    // ---- Helpers for reading output ----

    private static SortedDictionary<string, (int Offset, int Length)> SfntRecords(byte[] image, int fontOffset)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(fontOffset + 4));
        var records = new SortedDictionary<string, (int, int)>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var at = fontOffset + 12 + 16 * i;
            records.Add(Encoding.Latin1.GetString(image, at, 4),
                ((int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(at + 8)), (int)BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(at + 12))));
        }

        return records;
    }
}

/// <summary>Reads one simple or composite TrueType glyph's bytes (absolute coordinates; flags and repeats decoded).</summary>
internal sealed record TrueTypeGlyph(int Contours, (int, int, int, int) Box, int[] EndPoints, byte[] Instructions,
    (int X, int Y, bool On)[] Points, byte FirstFlag)
{
    public static TrueTypeGlyph Parse(ReadOnlySpan<byte> glyph)
    {
        var contours = BinaryPrimitives.ReadInt16BigEndian(glyph);
        var box = (
            (int)BinaryPrimitives.ReadInt16BigEndian(glyph[2..]), BinaryPrimitives.ReadInt16BigEndian(glyph[4..]),
            BinaryPrimitives.ReadInt16BigEndian(glyph[6..]), BinaryPrimitives.ReadInt16BigEndian(glyph[8..]));
        var at = 10;
        var ends = new int[contours];
        for (var i = 0; i < contours; i++, at += 2)
            ends[i] = BinaryPrimitives.ReadUInt16BigEndian(glyph[at..]);

        var instructionLength = BinaryPrimitives.ReadUInt16BigEndian(glyph[at..]);
        var instructions = glyph.Slice(at + 2, instructionLength).ToArray();
        at += 2 + instructionLength;

        var count = ends[^1] + 1;
        var flags = new byte[count];
        for (var i = 0; i < count;)
        {
            var flag = glyph[at++];
            flags[i++] = flag;
            if ((flag & 0x08) == 0)
                continue;

            for (var repeat = glyph[at++]; repeat > 0; repeat--)
                flags[i++] = flag;
        }

        var xs = Coordinates(glyph, ref at, flags, 0x02, 0x10);
        var ys = Coordinates(glyph, ref at, flags, 0x04, 0x20);
        var points = Enumerable.Range(0, count).Select(i => (xs[i], ys[i], (flags[i] & 1) != 0)).ToArray();
        return new TrueTypeGlyph(contours, box, ends, instructions, points, flags[0]);
    }

    private static int[] Coordinates(ReadOnlySpan<byte> glyph, ref int at, byte[] flags, int shortFlag, int sameFlag)
    {
        var values = new int[flags.Length];
        var value = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            if ((flags[i] & shortFlag) != 0)
            {
                var delta = glyph[at++];
                value += (flags[i] & sameFlag) != 0 ? delta : -delta;
            }
            else if ((flags[i] & sameFlag) == 0)
            {
                value += BinaryPrimitives.ReadInt16BigEndian(glyph[at..]);
                at += 2;
            }

            values[i] = value;
        }

        return values;
    }
}

/// <summary>The triplet encoding of the WOFF2 format (section 5.2), written from the format's table, independently of the decoder.</summary>
internal static class SpecTriplet
{
    public static int ByteCount(int index) => index < 84 ? 1 : index < 120 ? 2 : index < 124 ? 3 : 4;

    public static (int Dx, int Dy) Decode(int index, byte[] bytes)
    {
        int dx, dy;
        var xPositive = index % 4 is 1 or 3;
        var yPositive = index % 4 is 2 or 3;
        if (index < 10)
        {
            dx = 0;
            dy = (256 * (index / 2) + bytes[0]) * (index % 2 == 1 ? 1 : -1);
        }
        else if (index < 20)
        {
            var i = index - 10;
            dx = (256 * (i / 2) + bytes[0]) * (i % 2 == 1 ? 1 : -1);
            dy = 0;
        }
        else if (index < 84)
        {
            var i = index - 20;
            dx = (1 + 16 * (i / 16) + (bytes[0] >> 4)) * (xPositive ? 1 : -1);
            dy = (1 + 16 * (i / 4 % 4) + (bytes[0] & 0x0F)) * (yPositive ? 1 : -1);
        }
        else if (index < 120)
        {
            var i = index - 84;
            dx = (1 + 256 * (i / 12) + bytes[0]) * (xPositive ? 1 : -1);
            dy = (1 + 256 * (i % 12 / 4) + bytes[1]) * (yPositive ? 1 : -1);
        }
        else if (index < 124)
        {
            dx = ((bytes[0] << 4) | (bytes[1] >> 4)) * (xPositive ? 1 : -1);
            dy = (((bytes[1] & 0x0F) << 8) | bytes[2]) * (yPositive ? 1 : -1);
        }
        else
        {
            dx = ((bytes[0] << 8) | bytes[1]) * (xPositive ? 1 : -1);
            dy = ((bytes[2] << 8) | bytes[3]) * (yPositive ? 1 : -1);
        }

        return (dx, dy);
    }
}

/// <summary>Builds the transformed <c>glyf</c> table of a WOFF2 file from glyph descriptions, with knobs to break it.</summary>
internal sealed class GlyfStreams
{
    public enum Stream { Contours, Points, Flags, Glyph, Composite, Box, Instructions }

    private readonly List<byte> m_contours = [];
    private readonly List<byte> m_points = [];
    private readonly List<byte> m_flags = [];
    private readonly List<byte> m_glyph = [];
    private readonly List<byte> m_composite = [];
    private readonly List<byte> m_boxes = [];
    private readonly List<byte> m_instructions = [];
    private readonly List<bool> m_hasBox = [];
    private readonly List<bool> m_overlap = [];

    public int GlyphCount => m_hasBox.Count;

    public int IndexFormat { get; init; } = 1;

    /// <summary>How 255UInt16 values of 253 and above are written: 253 (word), 254 (+506) or 255 (+253).</summary>
    public int Form255 { get; init; } = 255;

    /// <summary>When set, the named stream is written empty (its size 0), so the decoder reads past it.</summary>
    public Stream? DropFrom { get; init; }

    public static byte[] OneEmptyGlyph() => new GlyfStreams().AddEmpty().Build();

    public GlyfStreams AddEmpty(bool explicitBox = false)
    {
        S16(m_contours, 0);
        m_hasBox.Add(explicitBox);
        m_overlap.Add(false);
        if (explicitBox)
            m_boxes.AddRange(new byte[8]);

        return this;
    }

    public GlyfStreams AddRawContourCount(short count)
    {
        S16(m_contours, count);
        m_hasBox.Add(false);
        m_overlap.Add(false);
        return this;
    }

    public GlyfStreams AddSimple((int X, int Y, bool On)[][] contours, byte[]? instructions = null, (int, int, int, int)? box = null, bool overlap = false)
    {
        S16(m_contours, (short)contours.Length);
        int previousX = 0, previousY = 0;
        foreach (var contour in contours)
        {
            Write255(m_points, contour.Length);
            foreach (var (x, y, on) in contour)
            {
                var dx = x - previousX;
                var dy = y - previousY;
                previousX = x;
                previousY = y;
                m_flags.Add((byte)((on ? 0 : 0x80) | (124 + (dx >= 0 ? 1 : 0) + (dy >= 0 ? 2 : 0))));
                U16(m_glyph, (ushort)Math.Abs(dx));
                U16(m_glyph, (ushort)Math.Abs(dy));
            }
        }

        Write255(m_glyph, instructions?.Length ?? 0);
        m_instructions.AddRange(instructions ?? []);
        AddBox(box);
        m_overlap.Add(overlap);
        return this;
    }

    /// <summary>One simple glyph of one contour and one point, with the given flag byte and glyph-stream bytes.</summary>
    public GlyfStreams AddRawPoint(byte flag, byte[] glyphBytes)
    {
        S16(m_contours, 1);
        Write255(m_points, 1);
        m_flags.Add(flag);
        m_glyph.AddRange(glyphBytes);
        Write255(m_glyph, 0);
        AddBox(null);
        m_overlap.Add(false);
        return this;
    }

    public GlyfStreams AddComposite(byte[] components, byte[]? instructions, (int, int, int, int)? box)
    {
        S16(m_contours, -1);
        m_composite.AddRange(components);
        if (instructions is not null)
        {
            Write255(m_glyph, instructions.Length);
            m_instructions.AddRange(instructions);
        }

        AddBox(box);
        m_overlap.Add(false);
        return this;
    }

    public byte[] Build()
    {
        var count = GlyphCount;
        var bitmap = new byte[4 * ((count + 31) / 32)];
        for (var i = 0; i < count; i++)
        {
            if (m_hasBox[i])
                bitmap[i >> 3] |= (byte)(0x80 >> (i & 7));
        }

        var overlapBitmap = new byte[(count + 7) / 8];
        for (var i = 0; i < count; i++)
        {
            if (m_overlap[i])
                overlapBitmap[i >> 3] |= (byte)(0x80 >> (i & 7));
        }

        var hasOverlap = m_overlap.Any(bit => bit);
        var streams = new byte[][]
        {
            Take(Stream.Contours, m_contours), Take(Stream.Points, m_points), Take(Stream.Flags, m_flags), Take(Stream.Glyph, m_glyph),
            Take(Stream.Composite, m_composite), [.. bitmap, .. Take(Stream.Box, m_boxes)], Take(Stream.Instructions, m_instructions),
        };

        var output = new List<byte>();
        U16(output, 0);
        U16(output, (ushort)(hasOverlap ? 1 : 0));
        U16(output, (ushort)count);
        U16(output, (ushort)IndexFormat);
        foreach (var stream in streams)
            output.AddRange(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness((uint)stream.Length)));

        foreach (var stream in streams)
            output.AddRange(stream);

        if (hasOverlap)
            output.AddRange(overlapBitmap);

        return output.ToArray();
    }

    private byte[] Take(Stream stream, List<byte> bytes) => DropFrom == stream ? [] : bytes.ToArray();

    private void AddBox((int X0, int Y0, int X1, int Y1)? box)
    {
        m_hasBox.Add(box is not null);
        if (box is not { } value)
            return;

        S16(m_boxes, (short)value.X0);
        S16(m_boxes, (short)value.Y0);
        S16(m_boxes, (short)value.X1);
        S16(m_boxes, (short)value.Y1);
    }

    private void Write255(List<byte> output, int value)
    {
        if (value < 253)
        {
            output.Add((byte)value);
        }
        else if (Form255 == 253 || (Form255 == 254 && value is < 506 or >= 762) || (Form255 == 255 && value >= 509))
        {
            output.Add(253);
            U16(output, (ushort)value);
        }
        else if (Form255 == 254)
        {
            output.Add(254);
            output.Add((byte)(value - 506));
        }
        else
        {
            output.Add(255);
            output.Add((byte)(value - 253));
        }
    }

    private static void U16(List<byte> output, ushort value)
    {
        output.Add((byte)(value >> 8));
        output.Add((byte)value);
    }

    private static void S16(List<byte> output, short value) => U16(output, (ushort)value);
}

/// <summary>Builds WOFF2 files from tables, with the knobs the strict-rule tests turn, and takes real ones apart to change them.</summary>
internal static class Woff2Builder
{
    private static readonly string[] s_knownTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG",
        "EBDT", "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea", "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC",
        "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar", "bdat", "bloc", "bsln", "cvar", "fdsc",
        "feat", "fmtx", "fvar", "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop", "trak", "Zapf", "Silf", "Glat",
        "Gloc", "Feat", "Sill",
    ];

    /// <summary>
    /// A table as the file holds it: <see cref="Data"/> is its bytes in the Brotli stream (the transformed bytes of a transformed
    /// table). <see cref="Version"/> is the directory's transform version, or -1 for the table's null transform.
    /// </summary>
    public sealed record Table(string Tag, byte[] Data, int Version = -1)
    {
        /// <summary>The original length the directory declares; the data's length when not set.</summary>
        public long? OrigLength { get; init; }

        public int EffectiveVersion => Version >= 0 ? Version : Tag is "glyf" or "loca" ? 3 : 0;

        public bool Transformed => Tag switch { "glyf" or "loca" => EffectiveVersion == 0, "hmtx" => EffectiveVersion == 1, _ => false };
    }

    public sealed record Collection(uint Version, IReadOnlyList<(uint Flavor, int[] Tables)> Fonts);

    public static byte[] Build(IReadOnlyList<Table> tables, uint flavor = 0x00010000, Collection? collection = null,
        int? tableCount = null, byte[]? directory = null, byte[]? brotliTail = null, int brotliCut = 0)
    {
        var header = new List<byte>();
        var entries = new List<byte>();
        foreach (var table in tables)
        {
            var index = Array.IndexOf(s_knownTags, table.Tag);
            entries.Add((byte)((index < 0 ? 63 : index) | (table.EffectiveVersion << 6)));
            if (index < 0)
                entries.AddRange(Encoding.ASCII.GetBytes(table.Tag));

            Base128(entries, table.OrigLength ?? table.Data.Length);
            if (table.Transformed)
                Base128(entries, table.Data.Length);
        }

        if (collection is not null)
        {
            entries.AddRange(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(collection.Version)));
            Write255(entries, collection.Fonts.Count);
            foreach (var (fontFlavor, indices) in collection.Fonts)
            {
                Write255(entries, indices.Length);
                entries.AddRange(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(fontFlavor)));
                foreach (var index in indices)
                    Write255(entries, index);
            }
        }

        var directoryBytes = directory ?? entries.ToArray();
        var stream = new byte[tables.Sum(table => table.Data.Length)];
        var streamAt = 0;
        foreach (var table in tables)
        {
            table.Data.CopyTo(stream, streamAt);
            streamAt += table.Data.Length;
        }

        var compressed = Brotli(stream);
        compressed = [.. compressed[..^brotliCut], .. brotliTail ?? []];

        var file = new List<byte>();
        file.AddRange(new byte[48]);
        file.AddRange(directoryBytes);
        file.AddRange(compressed);
        while (file.Count % 4 != 0)
            file.Add(0);

        var result = file.ToArray();
        "wOF2"u8.CopyTo(result);
        U32(result, 4, flavor);
        U32(result, 8, (uint)result.Length);
        U16(result, 12, (ushort)(tableCount ?? tables.Count));
        U32(result, 16, (uint)(12 + 16 * tables.Count + tables.Sum(table => (table.OrigLength ?? table.Data.Length + 3) & ~3L)));
        U32(result, 20, (uint)compressed.Length);
        U16(result, 24, 1);
        U16(result, 26, 0);
        return result;
    }

    /// <summary>The tables of a real WOFF2 file, with their stream bytes and transform versions.</summary>
    public static (uint Flavor, List<Table> Tables) Parse(byte[] woff2)
    {
        var flavor = BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(4));
        var count = BinaryPrimitives.ReadUInt16BigEndian(woff2.AsSpan(12));
        var at = 48;
        var entries = new List<(string Tag, int Version, long Orig, long Length)>();
        for (var i = 0; i < count; i++)
        {
            var flags = woff2[at++];
            var tag = (flags & 0x3F) == 63 ? Encoding.ASCII.GetString(woff2, (at += 4) - 4, 4) : s_knownTags[flags & 0x3F];
            var version = flags >> 6;
            var orig = ReadBase128(woff2, ref at);
            var transformed = tag switch { "glyf" or "loca" => version == 0, "hmtx" => version == 1, _ => false };
            var length = transformed ? ReadBase128(woff2, ref at) : orig;
            entries.Add((tag, version, orig, length));
        }

        var compressedSize = (int)BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(20));
        var stream = Woff2Decoder.BrotliInflate(woff2.AsSpan(at, compressedSize), entries.Sum(entry => entry.Length));
        var tables = new List<Table>();
        var offset = 0;
        foreach (var (tag, version, orig, length) in entries)
        {
            tables.Add(new Table(tag, stream.AsSpan(offset, (int)length).ToArray(), version) { OrigLength = orig });
            offset += (int)length;
        }

        return (flavor, tables);
    }

    /// <summary>Rebuilds a real file after <paramref name="change"/> has changed its table list (and the stream's Brotli tail).</summary>
    public static byte[] Edit(byte[] woff2, Action<List<Table>> change, byte[]? brotliTail = null, int brotliCut = 0)
    {
        var (flavor, tables) = Parse(woff2);
        change(tables);
        var result = Build(tables, flavor, brotliTail: brotliTail, brotliCut: brotliCut);
        woff2.AsSpan(16, 4).CopyTo(result.AsSpan(16)); // DirectWrite refuses a totalSfntSize below the size it needs
        return result;
    }

    public static int CompressedStart(byte[] woff2)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(woff2.AsSpan(12));
        var at = 48;
        for (var i = 0; i < count; i++)
        {
            var flags = woff2[at++];
            var tag = (flags & 0x3F) == 63 ? Encoding.ASCII.GetString(woff2, (at += 4) - 4, 4) : s_knownTags[flags & 0x3F];
            var version = flags >> 6;
            ReadBase128(woff2, ref at);
            if (tag switch { "glyf" or "loca" => version == 0, "hmtx" => version == 1, _ => false })
                ReadBase128(woff2, ref at);
        }

        return at;
    }

    public static int CompressedEnd(byte[] woff2) => CompressedStart(woff2) + (int)BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(20));

    /// <summary>Appends a metadata and/or private block (raw bytes) after the compressed data, with the header fields that describe them.</summary>
    public static byte[] AppendBlocks(byte[] woff2, byte[]? metadata = null, byte[]? privateData = null)
    {
        var file = woff2[..CompressedEnd(woff2)].ToList();
        long metaOffset = 0, privOffset = 0;
        if (metadata is not null)
        {
            while (file.Count % 4 != 0)
                file.Add(0);

            metaOffset = file.Count;
            file.AddRange(metadata);
        }

        if (privateData is not null)
        {
            while (file.Count % 4 != 0)
                file.Add(0);

            privOffset = file.Count;
            file.AddRange(privateData);
        }

        var result = file.ToArray();
        woff2.AsSpan(0, 48).CopyTo(result);
        U32(result, 8, (uint)result.Length);
        U32(result, 28, (uint)metaOffset);
        U32(result, 32, (uint)(metadata?.Length ?? 0));
        U32(result, 36, (uint)(metadata?.Length ?? 0));
        U32(result, 40, (uint)privOffset);
        U32(result, 44, (uint)(privateData?.Length ?? 0));
        return result;
    }

    public static byte[] U16(byte[] data, int at, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value);
        return data;
    }

    public static byte[] U32(byte[] data, int at, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), value);
        return data;
    }

    private static void Base128(List<byte> output, long value)
    {
        var groups = new List<byte>();
        do
        {
            groups.Add((byte)(value & 0x7F));
            value >>= 7;
        }
        while (value != 0);

        for (var i = groups.Count - 1; i >= 0; i--)
            output.Add((byte)(groups[i] | (i > 0 ? 0x80 : 0)));
    }

    private static long ReadBase128(byte[] data, ref int at)
    {
        long value = 0;
        for (var i = 0; i < 5; i++)
        {
            var b = data[at++];
            value = (value << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0)
                break;
        }

        return value;
    }

    private static void Write255(List<byte> output, int value)
    {
        if (value < 253)
        {
            output.Add((byte)value);
        }
        else
        {
            output.Add(253);
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }
    }

    private static byte[] Brotli(byte[] data)
    {
        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        if (!BrotliEncoder.TryCompress(data, buffer, out var written, 5, 22))
            throw new InvalidOperationException("Brotli compression failed.");

        return buffer[..written];
    }
}
