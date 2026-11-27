using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Woff;
using static CoolEngine.Fonts.Tests.Reference.CraftedFonts;

namespace CoolEngine.Fonts.Tests;

/// <summary>Tests of <see cref="WoffDecoder"/>: the decoded tables against DirectWrite's unpacker and the loaded faces against FreeType.</summary>
[TestFixture]
public sealed class WoffDecoderTests
{
    private const int HeaderLength = 44;

    /// <summary>A decoder that allocates about the file, never the declared sizes: the bound of the bomb tests (64 KiB over the file).</summary>
    private const long AllocationSlack = 64 * 1024;

    private DirectWriteReference m_directWrite = null!;
    private FreeTypeReference m_freeType = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        m_directWrite = new DirectWriteReference();
        m_freeType = new FreeTypeReference();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        m_directWrite.Dispose();
        m_freeType.Dispose();
    }

    private static IEnumerable<TestCaseData> WoffFiles() =>
        TestFonts.WoffFiles().Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    private static IEnumerable<TestCaseData> TrueTypeWoffFiles() =>
        TestFonts.WoffFiles().Where(path => path.EndsWith(".ttf.woff", StringComparison.Ordinal))
            .Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    private static IEnumerable<TestCaseData> CffWoffFiles() =>
        TestFonts.WoffFiles().Where(path => path.EndsWith(".otf.woff", StringComparison.Ordinal))
            .Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    // ---- The references ----

    [TestCaseSource(nameof(WoffFiles))]
    public void Decode_EveryWoff_TablesEqualDirectWriteUnpack(string path)
    {
        var file = FontCache.Bytes(path);

        var ours = SfntTables.Read(WoffDecoder.Decode(file));
        var theirs = SfntTables.Read(m_directWrite.UnpackFontFile(DWriteContainerType.Woff, file));

        Assert.Multiple(() =>
        {
            Assert.That(ours.Keys, Is.EquivalentTo(theirs.Keys), "table tags");
            foreach (var (tag, data) in ours)
                Assert.That(data, Is.EqualTo(theirs[tag]), $"table '{tag}'");
        });
    }

    /// <summary>
    /// The layout is FreeType's (sfwoff.c): the tables in the order of the WOFF file's data, not DirectWrite's (the directory's
    /// tag order, which differs for a file like SourceSans3-Regular.ttf.woff, whose DSIG table comes last in the data). The
    /// image has DirectWrite's length, and its table records are in tag order with every table four-byte aligned and zero padded.
    /// </summary>
    [TestCaseSource(nameof(WoffFiles))]
    public void Decode_EveryWoff_LayoutFollowsTheFileDataOrderAsFreeTypeLaysItOut(string path)
    {
        var file = FontCache.Bytes(path);

        var image = WoffDecoder.Decode(file);

        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(12));
        var dataOrder = Enumerable.Range(0, count).Select(i => (Tag: Encoding.Latin1.GetString(file, 44 + 20 * i, 4), Offset: BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(44 + 20 * i + 4))))
            .OrderBy(entry => entry.Offset).Select(entry => entry.Tag).ToList();
        var records = Enumerable.Range(0, count).Select(i => (Tag: Encoding.Latin1.GetString(image, 12 + 16 * i, 4), Offset: BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(12 + 16 * i + 8)),
            Length: BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(12 + 16 * i + 12)))).ToList();
        var padding = records.All(record => record.Offset % 4 == 0 && image.AsSpan((int)(record.Offset + record.Length), (int)((4 - record.Length % 4) % 4)).ToArray().All(b => b == 0));

        Assert.Multiple(() =>
        {
            Assert.That(records.Select(record => record.Tag), Is.Ordered.Using<string>(StringComparer.Ordinal), "records in tag order");
            Assert.That(records.OrderBy(record => record.Offset).Select(record => record.Tag), Is.EqualTo(dataOrder), "data in file order");
            Assert.That(padding, Is.True, "aligned, zero padded");
            Assert.That(image, Has.Length.EqualTo(m_directWrite.UnpackFontFile(DWriteContainerType.Woff, file).Length), "DirectWrite's length");
        });
    }

    [TestCaseSource(nameof(WoffFiles))]
    public void Decode_EveryWoff_TablesEqualTheOriginalFontsTables(string path)
    {
        var original = FontCache.Bytes(path[..path.LastIndexOf('.')]);

        var decoded = SfntTables.Read(WoffDecoder.Decode(FontCache.Bytes(path)));

        var expected = SfntTables.Read(original);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Keys, Is.EquivalentTo(expected.Keys), "table tags");
            foreach (var (tag, data) in decoded)
                Assert.That(data, Is.EqualTo(expected[tag]), $"table '{tag}'");
        });
    }

    [TestCaseSource(nameof(TrueTypeWoffFiles))]
    public void Load_EveryTrueTypeWoff_EqualsFreeTypeFromSameBytes(string path)
    {
        var file = FontCache.Bytes(path);
        var face = FontLoader.Load(file).Faces[0];
        using var reference = m_freeType.OpenFace(file, 0);
        reference.SetPixelSize(16);
        var font = face.CreateScaledFont(new ScaledFontOptions(16) { Hinting = HintingMode.None });

        var mismatches = new List<string>();
        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
        {
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false);
            var outline = font.GetOutline(glyph);
            if (!outline.Points.Span.SequenceEqual(expected.Outline.Points) || !outline.ContourEndPoints.Span.SequenceEqual(expected.Outline.ContourEnds))
                mismatches.Add($"glyph {glyph}: outline differs");

            if (font.GetMetrics(glyph) != expected.Metrics)
                mismatches.Add($"glyph {glyph}: metrics {font.GetMetrics(glyph)}, FreeType {expected.Metrics}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Take(10)));
            AssertSameFace(face, reference);
            Assert.That(font.LineMetrics, Is.EqualTo(reference.SizeMetrics), "line metrics");
        });
    }

    /// <summary>
    /// The CFF and CFF2 copies (OT-4 WP5, now that CFF outlines exist): every glyph's outline and metrics equal the matching
    /// OTF's loaded here (itself compared with FreeType glyph by glyph) and FreeType's from the same bytes.
    /// </summary>
    [TestCaseSource(nameof(CffWoffFiles))]
    public void Load_EveryCffWoff_OutlinesEqualTheMatchingOtfAndFreeType(string path)
    {
        var file = FontCache.Bytes(path);
        var face = FontLoader.Load(file).Faces[0];
        var otf = FontCache.File(path[..path.LastIndexOf('.')]).Faces[0];
        using var reference = m_freeType.OpenFace(file, 0);
        reference.SetPixelSize(16);
        var options = new ScaledFontOptions(16) { Hinting = HintingMode.None };
        var font = face.CreateScaledFont(options);
        var matching = otf.CreateScaledFont(options);

        var mismatches = new List<string>();
        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
        {
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false);
            var outline = font.GetOutline(glyph);
            if (!outline.Points.Span.SequenceEqual(expected.Outline.Points) || !outline.ContourEndPoints.Span.SequenceEqual(expected.Outline.ContourEnds))
                mismatches.Add($"glyph {glyph}: outline differs from FreeType's");

            if (!outline.Points.Span.SequenceEqual(matching.GetOutline(glyph).Points.Span))
                mismatches.Add($"glyph {glyph}: outline differs from the OTF's");

            if (font.GetMetrics(glyph) != expected.Metrics || font.GetMetrics(glyph) != matching.GetMetrics(glyph))
                mismatches.Add($"glyph {glyph}: metrics {font.GetMetrics(glyph)}, FreeType {expected.Metrics}, OTF {matching.GetMetrics(glyph)}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(mismatches, Is.Empty, string.Join("; ", mismatches.Take(10)));
            Assert.That(face.GlyphCount, Is.EqualTo(otf.GlyphCount));
        });
    }

    [TestCaseSource(nameof(CffWoffFiles))]
    public void Load_EveryCffWoff_PropertiesAndCharacterMapEqualFreeTypeFromSameBytes(string path)
    {
        var file = FontCache.Bytes(path);
        var face = FontLoader.Load(file).Faces[0];
        using var reference = m_freeType.OpenFace(file, 0);

        AssertSameFace(face, reference);
    }

    private static void AssertSameFace(FontFace face, FreeTypeFace reference)
    {
        var characters = new List<string>();
        for (var codePoint = 0; codePoint <= 0xFFFF; codePoint++)
        {
            if (face.GetGlyphIndex(codePoint) != reference.GetCharIndex(codePoint) && characters.Count < 10)
                characters.Add($"U+{codePoint:X4}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(face.FamilyName, Is.EqualTo(reference.FamilyName), "family name");
            Assert.That(face.StyleName, Is.EqualTo(reference.StyleName), "style name");
            Assert.That(face.UnitsPerEm, Is.EqualTo(reference.UnitsPerEm), "units per em");
            Assert.That(face.GlyphCount, Is.EqualTo(reference.NumGlyphs), "glyph count");
            Assert.That(face.Ascender, Is.EqualTo(reference.Ascender), "ascender");
            Assert.That(face.Descender, Is.EqualTo(reference.Descender), "descender");
            Assert.That(face.Height, Is.EqualTo(reference.Height), "height");
            Assert.That(characters, Is.Empty, "code points mapped differently: " + string.Join(", ", characters));
        });
    }

    /// <summary>
    /// The accepted cases of the decoder's remarks: each decodes to the original tables, and DirectWrite unpacks the same tables.
    /// (The cases it refuses, which FreeType accepts, are strict rules since Denys's "Reject, like the spec".)
    /// </summary>
    [TestCaseSource(nameof(AcceptedVariants))]
    public void Decode_AcceptedVariant_DecodesToTheOriginalTablesAndDirectWriteIsAsStated(string name, Func<byte[], byte[]> change, bool directWriteAccepts)
    {
        var tables = ReadTables(Arial());
        var woff = change(WoffBuilder.Build(tables.Select(table => new WoffBuilder.Table(table.Tag, table.Data)).ToList()));

        var ours = SfntTables.Read(WoffDecoder.Decode(woff));

        Assert.Multiple(() =>
        {
            Assert.That(ours.Keys, Is.EquivalentTo(tables.Select(table => table.Tag)), name + ": table tags");
            foreach (var table in tables)
                Assert.That(ours[table.Tag], Is.EqualTo(table.Data), $"{name}: table '{table.Tag}'");

            if (!directWriteAccepts)
            {
                Assert.That(() => m_directWrite.UnpackFontFile(DWriteContainerType.Woff, woff), Throws.InstanceOf<System.Runtime.InteropServices.COMException>(), name + ": DirectWrite");
                return;
            }

            var theirs = SfntTables.Read(m_directWrite.UnpackFontFile(DWriteContainerType.Woff, woff));
            foreach (var (tag, data) in ours)
                Assert.That(data, Is.EqualTo(theirs[tag]), $"{name}: DirectWrite's table '{tag}'");
        });
    }

    private static IEnumerable<TestCaseData> AcceptedVariants()
    {
        yield return Variant("unchanged", woff => woff, true);
        yield return Variant("major and minor version", woff => WoffBuilder.U16(WoffBuilder.U16(woff, 20, 7), 22, 9), true);
        yield return Variant("wrong table checksum", woff => WoffBuilder.U32(woff, HeaderLength + 16, 0x12345678), true);

        static TestCaseData Variant(string name, Func<byte[], byte[]> change, bool directWriteAccepts) =>
            new TestCaseData(name, change, directWriteAccepts).SetArgDisplayNames(name);
    }

    // ---- Strict rules: one test each, asserting its own message ----

    private static byte[] ValidWoff(bool metadata = false, bool privateData = false)
    {
        var tables = ReadTables(Arial()).Select(table => new WoffBuilder.Table(table.Tag, table.Data)).ToList();
        return WoffBuilder.Build(tables, metadata: metadata ? "<metadata/>"u8.ToArray() : null, privateData: privateData ? [1, 2, 3, 4, 5] : null);
    }

    private static void AssertInvalid(byte[] woff, string message) =>
        Assert.That(() => WoffDecoder.Decode(woff), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));

    [Test]
    public void Decode_ValidCraftedWoff_EqualsTheSourceTables()
    {
        var woff = ValidWoff(metadata: true, privateData: true);

        var decoded = SfntTables.Read(WoffDecoder.Decode(woff));

        var expected = ReadTables(Arial());
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Keys, Is.EquivalentTo(expected.Select(table => table.Tag)));
            foreach (var table in expected)
                Assert.That(decoded[table.Tag], Is.EqualTo(table.Data), table.Tag);
        });
    }

    [Test]
    public void Decode_DataShorterThanHeader_Throws() =>
        AssertInvalid(ValidWoff()[..(HeaderLength - 1)], "shorter than its 44-byte header");

    [Test]
    public void Decode_SignatureNotWoff_Throws()
    {
        var woff = ValidWoff();
        "wOF2"u8.CopyTo(woff);

        AssertInvalid(woff, "signature is not 'wOFF'");
    }

    [Test]
    public void Decode_FlavorCollection_Throws() =>
        AssertInvalid(WoffBuilder.U32(ValidWoff(), 4, 0x74746366), "not a collection");

    [Test]
    public void Decode_DeclaredLengthNotFileLength_Throws() =>
        AssertInvalid(WoffBuilder.U32(ValidWoff(), 8, (uint)ValidWoff().Length + 4), "gives the length");

    [Test]
    public void Decode_NoTables_Throws() =>
        AssertInvalid(WoffBuilder.U16(ValidWoff(), 12, 0), "has no tables");

    [Test]
    public void Decode_DirectoryPastEnd_Throws() =>
        AssertInvalid(WoffBuilder.U16(ValidWoff(), 12, 0xFFFF), "table directory");

    [TestCase(1, TestName = "Decode_TagsDescending_Throws")]
    [TestCase(0, TestName = "Decode_TagRepeated_Throws")]
    public void Decode_TagsNotStrictlyAscending_Throws(int mode)
    {
        var woff = ValidWoff();
        var first = WoffBuilder.EntryOf(woff, 0).Tag;
        WoffBuilder.U32(woff, HeaderLength + 20, mode == 1 ? first - 1 : first);

        AssertInvalid(woff, "strictly ascending tag order");
    }

    [Test]
    public void Decode_FirstTableNotRightAfterDirectory_Throws()
    {
        var woff = ValidWoff();

        AssertInvalid(WoffBuilder.U32(woff, HeaderLength + 4, (uint)WoffBuilder.EntryOf(woff, 0).Offset + 4), "where the previous table's data ends");
    }

    [Test]
    public void Decode_TablesOverlap_Throws()
    {
        var woff = ValidWoff();

        AssertInvalid(WoffBuilder.U32(woff, HeaderLength + 20 + 4, (uint)WoffBuilder.EntryOf(woff, 0).Offset), "where the previous table's data ends");
    }

    [Test]
    public void Decode_GapBetweenTables_Throws()
    {
        var woff = ValidWoff();

        AssertInvalid(WoffBuilder.U32(woff, HeaderLength + 20 + 4, (uint)WoffBuilder.EntryOf(woff, 1).Offset + 4), "where the previous table's data ends");
    }

    [Test]
    public void Decode_TableDataPastEnd_Throws()
    {
        var woff = ValidWoff();
        var last = WoffBuilder.LastEntryIndex(woff);
        var at = HeaderLength + 20 * last;
        WoffBuilder.U32(woff, at + 8, (uint)woff.Length + 4);
        WoffBuilder.U32(woff, at + 12, (uint)woff.Length * 4);

        AssertInvalid(woff, "runs past the end of the file");
    }

    [Test]
    public void Decode_CompressedLengthAboveOriginal_Throws()
    {
        var woff = ValidWoff();
        var last = WoffBuilder.LastEntryIndex(woff);
        var entry = WoffBuilder.EntryOf(woff, last);
        WoffBuilder.U32(woff, HeaderLength + 20 * last + 12, (uint)entry.CompLength - 1);

        AssertInvalid(woff, "bytes compressed but");
    }

    [Test]
    public void Decode_OriginalLengthBeyondDeflateRatio_Throws()
    {
        var woff = ValidWoff();
        var index = WoffBuilder.FirstCompressedEntryIndex(woff);
        var entry = WoffBuilder.EntryOf(woff, index);
        WoffBuilder.U32(woff, HeaderLength + 20 * index + 12, (uint)(entry.CompLength * 1032 + 1));

        AssertInvalid(woff, "deflate expands at most 1032 times");
    }

    [Test]
    public void Decode_TotalSfntSizeWrong_Throws()
    {
        var woff = ValidWoff();

        AssertInvalid(WoffBuilder.U32(woff, 16, BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(16)) + 4), "gives the sfnt size");
    }

    [Test]
    public void Decode_ZlibStreamDamaged_Throws()
    {
        var woff = ValidWoff();
        var index = WoffBuilder.FirstCompressedEntryIndex(woff);
        woff[(int)WoffBuilder.EntryOf(woff, index).Offset + 2] = 0x07; // a reserved deflate block type

        AssertInvalid(woff, "is damaged");
    }

    [Test]
    public void Decode_ZlibStreamEndsEarly_Throws()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[5000]) { DeclaredLength = 10000 }]);

        AssertInvalid(woff, "ends after 5000 of 10000 bytes");
    }

    [Test]
    public void Decode_ZlibStreamInflatesToMore_Throws()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[10000]) { DeclaredLength = 5000 }]);

        AssertInvalid(woff, "inflates to more than its 5000 bytes");
    }

    [Test]
    public void Decode_MetadataLengthWithoutOffset_Throws() =>
        AssertInvalid(WoffBuilder.U32(ValidWoff(), 28, 5), "metadata block has a length but no offset");

    [Test]
    public void Decode_MetadataCompressedWithoutOriginalLength_Throws()
    {
        var woff = ValidWoff(metadata: true);

        AssertInvalid(WoffBuilder.U32(woff, 32, 0), "has no original length");
    }

    [Test]
    public void Decode_MetadataNotAtEndOfTables_Throws()
    {
        var woff = ValidWoff(metadata: true);

        AssertInvalid(WoffBuilder.U32(woff, 24, BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(24)) + 4), "metadata block starts at");
    }

    [Test]
    public void Decode_MetadataPastEnd_Throws()
    {
        var woff = ValidWoff(metadata: true);

        AssertInvalid(WoffBuilder.U32(woff, 28, 0x7FFFFFFF), "metadata block runs past the end");
    }

    [Test]
    public void Decode_PrivateLengthWithoutOffset_Throws()
    {
        var woff = ValidWoff();

        AssertInvalid(WoffBuilder.U32(woff, 40, 8), "private data block has a length but no offset");
    }

    [Test]
    public void Decode_PrivateBlockNotAligned_Throws()
    {
        var woff = ValidWoff(metadata: true, privateData: true);

        AssertInvalid(WoffBuilder.U32(woff, 36, BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(36)) + 4), "private data block starts at");
    }

    [Test]
    public void Decode_PrivateBlockPastEnd_Throws()
    {
        var woff = ValidWoff(privateData: true);

        AssertInvalid(WoffBuilder.U32(woff, 40, 0x7FFFFFFF), "private data block runs past the end");
    }

    [Test]
    public void Decode_BytesAfterLastBlock_Throws()
    {
        var woff = ValidWoff();
        var longer = woff.Concat(new byte[8]).ToArray();
        WoffBuilder.U32(longer, 8, (uint)longer.Length);

        AssertInvalid(longer, "its blocks end at");
    }

    // ---- Denys, 2026-11-26: "Reject, like the spec" (cases FreeType accepts, OTS and DirectWrite reject) ----

    [Test]
    public void Decode_ReservedFieldNotZero_Throws()
    {
        var woff = WoffBuilder.U16(ValidWoff(), 14, 0xBEEF);

        Assert.Multiple(() =>
        {
            AssertInvalid(woff, "reserved field is 0xBEEF");
            Assert.That(() => m_directWrite.UnpackFontFile(DWriteContainerType.Woff, woff), Throws.InstanceOf<System.Runtime.InteropServices.COMException>(), "DirectWrite");
        });
    }

    [Test]
    public void Decode_PaddingAfterATableNotZero_Throws()
    {
        var woff = ValidWoff();
        var count = BinaryPrimitives.ReadUInt16BigEndian(woff.AsSpan(12));
        var table = Enumerable.Range(0, count).Select(i => WoffBuilder.EntryOf(woff, i)).First(entry => (entry.Offset + entry.CompLength) % 4 != 0);
        woff[(int)(table.Offset + table.CompLength)] = 0xAA;

        Assert.Multiple(() =>
        {
            AssertInvalid(woff, "is not zero");
            Assert.That(() => m_directWrite.UnpackFontFile(DWriteContainerType.Woff, woff), Throws.InstanceOf<System.Runtime.InteropServices.COMException>(), "DirectWrite");
        });
    }

    [Test]
    public void Decode_PaddingBeforeThePrivateBlockNotZero_Throws()
    {
        var tables = ReadTables(Arial()).Select(table => new WoffBuilder.Table(table.Tag, table.Data)).ToList();
        var metadata = Enumerable.Range(11, 20).Select(length => Enumerable.Range(0, length).Select(i => (byte)(i * 37)).ToArray())
            .First(candidate => WoffBuilder.Deflate(candidate).Length % 4 != 0);
        var woff = WoffBuilder.Build(tables, metadata: metadata, privateData: [1, 2, 3]);
        var privOffset = BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(36));
        woff[(int)privOffset - 1] = 0xAA;

        AssertInvalid(woff, "before the WOFF private data block is not zero");
    }

    [Test]
    public void Decode_BytesAfterAZlibStreamWithinItsCompressedLength_Throws()
    {
        var woff = WoffBuilder.AppendToFirstCompressedTable(ValidWoff(), [0xDE, 0xAD, 0xBE]);

        Assert.Multiple(() =>
        {
            AssertInvalid(woff, "has bytes after its zlib stream");
            Assert.That(() => m_directWrite.UnpackFontFile(DWriteContainerType.Woff, woff), Throws.InstanceOf<System.Runtime.InteropServices.COMException>(), "DirectWrite");
        });
    }

    [Test]
    public void Decode_ZlibStreamWithoutItsAdlerTrailer_IsAcceptedAsAKnownLimit()
    {
        // The stream stops after its last data byte: .NET reads it as complete unless a process-wide opt-in switch is on, and
        // the inflated bytes are bounded by the original length either way (the remarks list it under "Not detected").
        var woff = WoffBuilder.ReplaceFirstCompressedTable(ValidWoff(), compressed => compressed[..^4]);

        var decoded = SfntTables.Read(WoffDecoder.Decode(woff));

        var expected = ReadTables(Arial());
        Assert.Multiple(() =>
        {
            foreach (var table in expected)
                Assert.That(decoded[table.Tag], Is.EqualTo(table.Data), table.Tag);
        });
    }

    /// <summary>The largest compressed table is the only copy of the file's data besides the output (no whole-file copy).</summary>
    [Test]
    public void Decode_RealWoff_AllocatesTheImageAndOneTableBuffer()
    {
        var file = FontCache.Bytes(TestFonts.SourceSans("SourceSans3VF-Upright.ttf.woff"));
        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(12));
        var largest = Enumerable.Range(0, count).Max(i => WoffBuilder.EntryOf(file, i).CompLength);
        WoffDecoder.Decode(file); // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        var image = WoffDecoder.Decode(file);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(image.Length + 2 * largest + 512 * 1024),
            $"bytes allocated for an image of {image.Length} and a largest compressed table of {largest}");
    }

    /// <summary>
    /// The work is linear: the inflaters read the compressed bytes in a number of reads that follows the bytes and the tables,
    /// not the table sizes squared: 200 small tables and one table of the same bytes both take a few reads per table plus one
    /// per 8 KiB (a one-byte-per-read feed of the last chunk would take thousands per table).
    /// </summary>
    [TestCase(200, 8192, TestName = "Decode_ManySmallTables_ReadTheirCompressedBytesInFewReads")]
    [TestCase(1, 200 * 8192, TestName = "Decode_OneLargeTable_ReadsItsCompressedBytesInFewReads")]
    public void Decode_TablesOfCompressibleData_ReadTheirCompressedBytesInFewReads(int tables, int tableLength)
    {
        var random = new Random(5);
        var list = Enumerable.Range(0, tables).Select(i =>
        {
            var data = new byte[tableLength];
            random.NextBytes(data.AsSpan(0, tableLength / 2));
            return new WoffBuilder.Table($"t{i:D3}", data);
        }).ToList();
        var woff = WoffBuilder.Build(list);
        var statistics = new WoffDecoder.Statistics();

        WoffDecoder.Decode(woff, statistics);

        Assert.That(statistics.InputReads, Is.LessThanOrEqualTo(4L * tables + woff.Length / 4096 + 4), $"reads of the compressed bytes for {woff.Length} bytes in {tables} tables");
    }

    // ---- Decompression bombs ----

    /// <summary>
    /// A file of about 130 KB that declares 128 MiB + 1 of original data (the most deflate allows for its size): a library limit,
    /// refused from the directory alone, so the decoder allocates a few KB (the bound is 64 KiB), not 128 MiB.
    /// </summary>
    [Test]
    public void Decode_DeclaredOutputOverMaxFileSize_ThrowsNotSupportedWithoutAllocating()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[FontLoader.MaxFileSize / 1032 + 16], Compress: false) { DeclaredLength = FontLoader.MaxFileSize + 1 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => WoffDecoder.Decode(woff), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    /// <summary>Many tables that each stay under the limit but add up past it: refused before the output is allocated.</summary>
    [Test]
    public void Decode_DeclaredTablesAddUpOverMaxFileSize_ThrowsNotSupportedWithoutAllocating()
    {
        var third = FontLoader.MaxFileSize / 3 + 4;
        var tables = Enumerable.Range(0, 4)
            .Select(i => new WoffBuilder.Table($"t{i:D3}", new byte[third / 1032 + 8], Compress: false) { DeclaredLength = third })
            .ToList();
        var woff = WoffBuilder.Build(tables);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => WoffDecoder.Decode(woff), Throws.TypeOf<NotSupportedException>());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    /// <summary>A small file that declares more than its zlib data could ever inflate to is refused as nonconforming, before allocating.</summary>
    [Test]
    public void Decode_DeclaredOutputBeyondDeflateRatio_ThrowsWithoutAllocating()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[100], Compress: false) { DeclaredLength = 100 * 1032 + 1 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => WoffDecoder.Decode(woff), Throws.TypeOf<InvalidDataException>());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(AllocationSlack), "bytes allocated by the refusal");
    }

    /// <summary>
    /// A stream that inflates to 64 MiB of zeros under a header that declares 80,000 bytes: the decoder stops one byte past the
    /// declaration, allocating the declared output and the file (about 80 KB each), not the 64 MiB the stream produces.
    /// </summary>
    [Test]
    public void Decode_StreamInflatingPastDeclaredLength_ThrowsWithSmallAllocation()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[64 * 1024 * 1024]) { DeclaredLength = 80_000 }]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(() => WoffDecoder.Decode(woff), Throws.TypeOf<InvalidDataException>().With.Message.Contains("inflates to more than"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(2 * woff.Length + 2 * 80_000 + AllocationSlack), "bytes allocated by the refusal");
    }

    // ---- The loader ----

    [Test]
    public void Load_DecodedWoffOverMaxFileSize_ThrowsNotSupported()
    {
        var woff = WoffBuilder.Build([new WoffBuilder.Table("test", new byte[FontLoader.MaxFileSize / 1032 + 16], Compress: false) { DeclaredLength = FontLoader.MaxFileSize + 1 }]);

        Assert.That(() => FontLoader.Load(woff), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"));
    }
}

/// <summary>The tables of an sfnt image by tag, each as its record's length says.</summary>
internal static class SfntTables
{
    public static Dictionary<string, byte[]> Read(byte[] sfnt)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4));
        var tables = new Dictionary<string, byte[]>();
        for (var i = 0; i < count; i++)
        {
            var at = 12 + 16 * i;
            var tag = Encoding.Latin1.GetString(sfnt, at, 4);
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(at + 8));
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(at + 12));
            tables.Add(tag, sfnt.AsSpan(offset, length).ToArray());
        }

        return tables;
    }
}

/// <summary>Builds WOFF 1.0 files from tables, with the knobs the strict-rule tests turn.</summary>
internal static class WoffBuilder
{
    public sealed record Table(string Tag, byte[] Data, bool Compress = true)
    {
        /// <summary>The <c>origLength</c> the directory declares, when it should not be the data's length.</summary>
        public long? DeclaredLength { get; init; }
    }

    public readonly record struct Entry(uint Tag, long Offset, long CompLength, long OrigLength);

    public static byte[] Build(IReadOnlyList<Table> tables, uint flavor = 0x00010000, byte[]? metadata = null, byte[]? privateData = null)
    {
        var sorted = tables.OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
        var blobs = sorted.Select(table => table.Compress ? Deflate(table.Data) : table.Data).ToList();
        var directoryEnd = HeaderLength + 20 * sorted.Count;

        using var output = new MemoryStream();
        output.SetLength(directoryEnd);
        var offsets = new List<long>();
        for (var i = 0; i < sorted.Count; i++)
        {
            offsets.Add(output.Length);
            var blob = blobs[i].Length < sorted[i].Data.Length ? blobs[i] : sorted[i].Data;
            blobs[i] = blob;
            output.Position = output.Length;
            output.Write(blob);
            Pad(output);
        }

        long sfntSize = 12 + 16 * sorted.Count;
        var directory = new byte[20 * sorted.Count];
        for (var i = 0; i < sorted.Count; i++)
        {
            var original = sorted[i].DeclaredLength ?? sorted[i].Data.Length;
            sfntSize += (original + 3) & ~3L;
            Write32(directory, 20 * i, BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(sorted[i].Tag)));
            Write32(directory, 20 * i + 4, (uint)offsets[i]);
            Write32(directory, 20 * i + 8, (uint)blobs[i].Length);
            Write32(directory, 20 * i + 12, (uint)original);
            Write32(directory, 20 * i + 16, 0xC0FFEE00u + (uint)i);
        }

        long metaOffset = 0, metaLength = 0, metaOrig = 0, privOffset = 0, privLength = 0;
        if (metadata is not null)
        {
            metaOffset = output.Length;
            var compressed = Deflate(metadata);
            metaLength = compressed.Length;
            metaOrig = metadata.Length;
            output.Write(compressed);
        }

        if (privateData is not null)
        {
            Pad(output);
            privOffset = output.Length;
            privLength = privateData.Length;
            output.Write(privateData);
        }

        var woff = output.ToArray();
        directory.CopyTo(woff, HeaderLength);
        "wOFF"u8.CopyTo(woff);
        Write32(woff, 4, flavor);
        Write32(woff, 8, (uint)woff.Length);
        U16(woff, 12, (ushort)sorted.Count);
        Write32(woff, 16, (uint)sfntSize);
        Write32(woff, 20, 0x00010002);
        Write32(woff, 24, (uint)metaOffset);
        Write32(woff, 28, (uint)metaLength);
        Write32(woff, 32, (uint)metaOrig);
        Write32(woff, 36, (uint)privOffset);
        Write32(woff, 40, (uint)privLength);
        return woff;
    }

    private const int HeaderLength = 44;

    public static Entry EntryOf(byte[] woff, int index)
    {
        var at = HeaderLength + 20 * index;
        return new Entry(
            BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(at)),
            BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(at + 4)),
            BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(at + 8)),
            BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(at + 12)));
    }

    /// <summary>The directory index of the table whose data comes last in the file.</summary>
    public static int LastEntryIndex(byte[] woff)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(woff.AsSpan(12));
        return Enumerable.Range(0, count).MaxBy(i => EntryOf(woff, i).Offset);
    }

    public static int FirstCompressedEntryIndex(byte[] woff)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(woff.AsSpan(12));
        return Enumerable.Range(0, count).First(i => EntryOf(woff, i).CompLength < EntryOf(woff, i).OrigLength);
    }

    /// <summary>Appends bytes to the first compressed table's data inside its compressed length (and shifts everything after it).</summary>
    public static byte[] AppendToFirstCompressedTable(byte[] woff, byte[] extra) =>
        ReplaceFirstCompressedTable(woff, compressed => [.. compressed, .. extra]);

    /// <summary>Replaces the first compressed table's compressed bytes (padding and every later offset follow).</summary>
    public static byte[] ReplaceFirstCompressedTable(byte[] woff, Func<byte[], byte[]> change)
    {
        var index = FirstCompressedEntryIndex(woff);
        var entry = EntryOf(woff, index);
        var oldPadded = (entry.CompLength + 3) & ~3L;
        var replacement = change(woff.AsSpan((int)entry.Offset, (int)entry.CompLength).ToArray());
        var newCompLength = replacement.Length;
        var newPadded = (newCompLength + 3) & ~3L;
        var shift = (int)(newPadded - oldPadded);

        var result = new byte[woff.Length + shift];
        woff.AsSpan(0, (int)entry.Offset).CopyTo(result);
        replacement.CopyTo(result, entry.Offset);
        woff.AsSpan((int)(entry.Offset + oldPadded)).CopyTo(result.AsSpan((int)(entry.Offset + newPadded)));

        var count = BinaryPrimitives.ReadUInt16BigEndian(woff.AsSpan(12));
        for (var i = 0; i < count; i++)
        {
            var at = HeaderLength + 20 * i;
            if (i == index)
                Write32(result, at + 8, (uint)newCompLength);
            else if (EntryOf(woff, i).Offset > entry.Offset)
                Write32(result, at + 4, (uint)(EntryOf(woff, i).Offset + shift));
        }

        foreach (var field in new[] { 24, 36 })
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(field)) != 0)
                Write32(result, field, (uint)(BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(field)) + shift));
        }

        Write32(result, 8, (uint)result.Length);
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

    private static void Write32(byte[] data, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), value);

    public static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);

        return output.ToArray();
    }

    private static void Pad(MemoryStream stream)
    {
        while (stream.Length % 4 != 0)
            stream.WriteByte(0);
    }
}
