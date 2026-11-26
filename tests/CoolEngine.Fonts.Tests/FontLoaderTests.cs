using System.IO.Compression;
using System.Text;
using CoolEngine.Fonts.Tests.Reference;
using static CoolEngine.Fonts.Tests.Reference.CraftedFonts;

namespace CoolEngine.Fonts.Tests;

[TestFixture]
public sealed class FontLoaderTests
{
    private const int ArgsAreXyValues = 0x0002;
    private const int WeHaveInstructions = 0x0100;

    private FreeTypeReference m_freeType = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => m_freeType = new FreeTypeReference();

    [OneTimeTearDown]
    public void OneTimeTearDown() => m_freeType.Dispose();

    private static IEnumerable<TestCaseData> Signatures()
    {
        yield return new TestCaseData(new byte[] { 0x00, 0x01, 0x00, 0x00, 0, 0 }, FontFormat.TrueType).SetArgDisplayNames("00 01 00 00");
        yield return new TestCaseData("true"u8.ToArray(), FontFormat.TrueType).SetArgDisplayNames("true");
        yield return new TestCaseData("OTTO"u8.ToArray(), FontFormat.OpenTypeCff).SetArgDisplayNames("OTTO");
        yield return new TestCaseData("ttcf"u8.ToArray(), FontFormat.Collection).SetArgDisplayNames("ttcf");
        yield return new TestCaseData("wOFF"u8.ToArray(), FontFormat.Woff).SetArgDisplayNames("wOFF");
        yield return new TestCaseData("wOF2"u8.ToArray(), FontFormat.Woff2).SetArgDisplayNames("wOF2");
        yield return new TestCaseData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, FontFormat.Unknown).SetArgDisplayNames("PNG");
        yield return new TestCaseData(Array.Empty<byte>(), FontFormat.Unknown).SetArgDisplayNames("empty");
    }

    private static IEnumerable<TestCaseData> AllFiles() =>
        TestFonts.AllFiles().Select(path => new TestCaseData(path).SetArgDisplayNames(Path.GetFileName(path)));

    [TestCaseSource(nameof(Signatures))]
    public void DetectFormat_Signature_ReturnsFormat(byte[] data, FontFormat expected)
    {
        Assert.That(FontLoader.DetectFormat(data), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(AllFiles))]
    public void Load_EveryTestFont_FaceCountMatchesFreeType(string path)
    {
        var data = FontCache.Bytes(path);
        using var reference = m_freeType.OpenFace(data, 0);

        var file = FontLoader.Load(data);

        Assert.That(file.Faces, Has.Count.EqualTo(reference.NumFaces));
    }

    [Test]
    public void Load_WoffAndWoff2_ThrowNotSupportedUntilWp3()
    {
        var woff = TestFonts.WoffFiles().First();
        var woff2 = TestFonts.Woff2Files().First();

        Assert.Multiple(() =>
        {
            Assert.That(() => FontLoader.Load(woff), Throws.TypeOf<NotSupportedException>().With.Message.Contains("WP3"));
            Assert.That(() => FontLoader.Load(woff2), Throws.TypeOf<NotSupportedException>().With.Message.Contains("WP3"));
        });
    }

    [Test]
    public async Task LoadAsync_File_EqualsLoad()
    {
        var path = TestFonts.RepositoryArial;

        var loaded = FontLoader.Load(path);
        var loadedAsync = await FontLoader.LoadAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(loadedAsync.Format, Is.EqualTo(loaded.Format));
            Assert.That(loadedAsync.Faces, Has.Count.EqualTo(loaded.Faces.Count));
            Assert.That(loadedAsync.Faces[0].GlyphCount, Is.EqualTo(loaded.Faces[0].GlyphCount));
            Assert.That(loadedAsync.Faces[0].FamilyName, Is.EqualTo(loaded.Faces[0].FamilyName));
        });
    }

    // ---- The strict rules (SfntReader's remarks), one test each; each asserts the message names the broken rule, so the copy
    // cannot pass for another reason. The cases accepted by Denys's safety-strict decision of 2026-11-26 are tested to load
    // and to read as FreeType reads them (AssertLoadsAsFreeType).

    /// <summary>A limit the library imposes on valid data: <see cref="NotSupportedException"/>, as Imaging's MaxPixelCount.</summary>
    [Test]
    public void Load_FileOverMaxFileSize_Throws()
    {
        var data = new byte[FontLoader.MaxFileSize + 1];
        Arial().CopyTo(data, 0);

        Assert.That(() => FontLoader.Load(data), Throws.TypeOf<NotSupportedException>().With.Message.Contains("limit"));
    }

    [Test]
    public void Load_UnknownSfntVersion_ThrowsNotSupported()
    {
        var face = Arial();
        Encoding.ASCII.GetBytes("typ1").CopyTo(face, 0);
        var collection = Collection(Arial(), face);

        Assert.That(() => FontLoader.Load(collection), Throws.TypeOf<NotSupportedException>().With.Message.Contains("sfnt version"));
    }

    /// <summary>A <c>typ1</c> (Apple PostScript-in-sfnt) font on its own is not a format the loader reads.</summary>
    [Test]
    public void Load_Typ1Font_ThrowsNotSupported()
    {
        var font = Arial();
        Encoding.ASCII.GetBytes("typ1").CopyTo(font, 0);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("not a TrueType"));
    }

    /// <summary>An Apple bitmap-only face, with <c>bhed</c> instead of <c>head</c>.</summary>
    [Test]
    public void Load_BhedInsteadOfHead_ThrowsNotSupported()
    {
        var tables = ReadTables(Arial()).Select(table => table.Tag == "head" ? new CraftedTable("bhed", table.Data) : table);
        var font = Build(TrueTypeVersion, tables);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("'bhed'"));
    }

    /// <summary>A face with Apple <c>hvgl</c> outlines and no <c>glyf</c>.</summary>
    [Test]
    public void Load_HvglOutlines_ThrowsNotSupported()
    {
        var font = WithTable(WithoutTable(Arial(), "glyf", "loca", "EBDT", "EBLC", "CBDT", "CBLC", "sbix", "SVG "), "hvgl",
            new BigEndianWriter().U16(1).U16(0).U32(0).ToArray());

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("'hvgl'"));
    }

    [Test]
    public void Load_TableDirectoryPastEnd_Throws()
    {
        var font = Arial()[..100];

        AssertInvalid(font, "directory");
    }

    [Test]
    public void Load_TableRecordPastEnd_Throws()
    {
        var font = Arial();
        var record = RecordOffset(font, "post");
        WriteU32(font, record + 12, (uint)font.Length);

        AssertInvalid(font, "runs past");
    }

    [Test]
    public void Load_TableRecordOverlappingDirectory_Throws()
    {
        var font = Arial();
        var record = RecordOffset(font, "post");
        WriteU32(font, record + 8, 4);
        WriteU32(font, record + 4, Checksum(font.AsSpan(4, (int)U32(font, record + 12))));

        AssertInvalid(font, "overlaps its table directory");
    }

    /// <summary>
    /// Two <c>hhea</c> records, the second with another ascender: the first is used, as FreeType uses it (accepted by Denys's
    /// safety-strict decision; OTS accepts it too and keeps the last).
    /// </summary>
    [Test]
    public void Load_DuplicateTableTag_FirstUsedAsFreeType()
    {
        var tables = ReadTables(Arial());
        var second = Table(Arial(), "hhea");
        WriteU16(second, 4, 3000);
        tables.Add(new CraftedTable("hhea", second));
        var font = Build(TrueTypeVersion, tables);

        var face = AssertLoadsAsFreeType(font);

        Assert.That(face.Ascender, Is.EqualTo(FontLoader.Load(Arial()).Faces[0].Ascender).And.Not.EqualTo(3000));
    }

    /// <summary>Table records in descending tag order load and read as FreeType reads them (Denys's safety-strict decision).</summary>
    [Test]
    public void Load_TableRecordsNotSorted_LoadsAsFreeType()
    {
        var tables = ReadTables(Arial()).OrderByDescending(table => table.Tag, StringComparer.Ordinal);
        var font = Build(TrueTypeVersion, tables, sort: false);

        AssertLoadsAsFreeType(font, 36, 100);
    }

    /// <summary>
    /// Every table checksum wrong: checksums are not verified (nor by FreeType, OTS or DirectWrite; Denys's safety-strict
    /// decision), so the font loads and reads as FreeType reads it.
    /// </summary>
    [Test]
    public void Load_WrongTableChecksums_LoadsAsFreeType()
    {
        var font = Build(TrueTypeVersion, ReadTables(Arial()), checksums: false);

        AssertLoadsAsFreeType(font, 36, 100);
    }

    [Test]
    public void Load_CollectionVersionUnknown_Throws()
    {
        var collection = Collection(Arial());
        WriteU16(collection, 4, 3);

        AssertInvalid(collection, "collection header's version");
    }

    [Test]
    public void Load_CollectionWithoutFaces_Throws()
    {
        var collection = Collection(Arial());
        WriteU32(collection, 8, 0);

        AssertInvalid(collection, "no faces");
    }

    [Test]
    public void Load_CollectionHeaderPastEnd_Throws()
    {
        var collection = Collection(Arial());
        WriteU32(collection, 8, 0x10000000);

        AssertInvalid(collection, "collection header");
    }

    [Test]
    public void Load_CollectionFaceInsideHeader_Throws()
    {
        var collection = Collection(Arial(), Arial());
        WriteU32(collection, 16, 8);

        AssertInvalid(collection, "inside the collection header");
    }

    [Test]
    public void Load_TableOverlappingCollectionHeader_Throws()
    {
        var collection = Collection(Arial());
        var face = (int)U32(collection, 12);
        var record = RecordOffset(collection, "post", face);
        WriteU32(collection, record + 8, 0);
        WriteU32(collection, record + 4, Checksum(collection.AsSpan(0, (int)U32(collection, record + 12))));

        AssertInvalid(collection, "overlaps the collection header");
    }

    [TestCase("head")]
    [TestCase("hhea")]
    [TestCase("maxp")]
    [TestCase("hmtx")]
    [TestCase("cmap")]
    [TestCase("name")]
    [TestCase("post")]
    public void Load_RequiredTableMissing_Throws(string tag)
    {
        AssertInvalid(WithoutTable(Arial(), tag), $"no '{tag}' table");
    }

    [TestCase("loca", "'glyf' table without a 'loca'")]
    [TestCase("glyf", "'loca' table without a 'glyf'")]
    public void Load_LocaOrGlyfWithoutTheOther_Throws(string removed, string message)
    {
        AssertInvalid(WithoutTable(Arial(), removed), message);
    }

    [Test]
    public void Load_NoOutlineOrBitmapTable_Throws()
    {
        AssertInvalid(WithoutTable(Arial(), "glyf", "loca", "EBDT", "EBLC", "CBDT", "CBLC", "sbix", "SVG "), "no outline table");
    }

    [Test]
    public void Load_HeadTooShort_Throws() => AssertInvalid(Truncate(Arial(), "head", 50), "'head' table is 50 bytes");

    [Test]
    public void Load_HeadMagicWrong_Throws() =>
        AssertInvalid(Patch(Arial(), "head", head => WriteU32(head, 12, 0x12345678)), "magic number");

    [TestCase(15)]
    [TestCase(16385)]
    public void Load_UnitsPerEmOutOfRange_Throws(int unitsPerEm) =>
        AssertInvalid(Patch(Arial(), "head", head => WriteU16(head, 18, unitsPerEm)), "unitsPerEm");

    [Test]
    public void Load_IndexToLocFormatInvalid_Throws() =>
        AssertInvalid(Patch(Arial(), "head", head => WriteU16(head, 50, 2)), "indexToLocFormat");

    [Test]
    public void Load_MaxpVersionUnknown_Throws() =>
        AssertInvalid(Patch(Arial(), "maxp", maxp => WriteU32(maxp, 0, 0x00020000)), "'maxp' table's version");

    [Test]
    public void Load_MaxpTooShort_Throws() => AssertInvalid(Truncate(Arial(), "maxp", 20), "'maxp' table is 20 bytes");

    [Test]
    public void Load_MaxpNumGlyphsZero_Throws() =>
        AssertInvalid(Patch(Arial(), "maxp", maxp => WriteU16(maxp, 4, 0)), "numGlyphs is 0");

    [Test]
    public void Load_MaxpVersion05WithGlyf_Throws()
    {
        var maxp = Table(Arial(), "maxp")[..6];
        WriteU32(maxp, 0, 0x00005000);

        AssertInvalid(WithTable(Arial(), "maxp", maxp), "version 0.5 'maxp'");
    }

    [Test]
    public void Load_HheaTooShort_Throws() => AssertInvalid(Truncate(Arial(), "hhea", 34), "'hhea' table is 34 bytes");

    [TestCase(0)]
    [TestCase(1)]
    public void Load_NumberOfHMetricsOutOfRange_Throws(int pastGlyphCount)
    {
        var font = Arial();
        var glyphCount = U16(Table(font, "maxp"), 4);
        var value = pastGlyphCount == 0 ? 0 : glyphCount + 1;

        AssertInvalid(Patch(font, "hhea", hhea => WriteU16(hhea, 34, value)), "numberOfHMetrics");
    }

    [Test]
    public void Load_HmtxTooShort_Throws()
    {
        var hmtx = Table(Arial(), "hmtx");

        AssertInvalid(Truncate(Arial(), "hmtx", hmtx.Length - 2), "'hmtx' table");
    }

    /// <summary>
    /// An <c>OS/2</c> table cut to 80 bytes, shorter than its version claims, with <c>USE_TYPO_METRICS</c> set so its fields
    /// decide the face's metrics: read as FreeType reads it, from the file at the table's offset (Denys's safety-strict decision).
    /// </summary>
    [Test]
    public void Load_Os2ShorterThanVersion_ReadAsFreeType()
    {
        var font = Truncate(TypoMetricsArial(), "OS/2", 80);
        Assert.That(U16(Table(font, "OS/2"), 0), Is.GreaterThanOrEqualTo(1), "the repository Arial's OS/2 version needs more than 80 bytes");

        AssertLoadsAsFreeType(font);
    }

    /// <summary>
    /// A 68-byte Apple version-0 <c>OS/2</c> table, with <c>USE_TYPO_METRICS</c> set: FreeType reads its 78-byte frame from the
    /// file, so the typographic fields come from the bytes after the table; at the end of the file the frame runs past it and
    /// the table is absent. A zero-length record is a missing table to FreeType. Each reads as FreeType reads it.
    /// </summary>
    [TestCase("followed by a table")]
    [TestCase("at the end of the file")]
    [TestCase("zero-length")]
    public void Load_Os2Version0Short_ReadAsFreeType(string layout)
    {
        var os2 = Table(TypoMetricsArial(), "OS/2")[..(layout == "zero-length" ? 0 : 68)];
        if (os2.Length > 0)
            WriteU16(os2, 0, 0);

        var tables = ReadTables(TypoMetricsArial()).Where(table => table.Tag != "OS/2").ToList();
        if (layout == "at the end of the file")
            tables = tables.OrderBy(table => table.Tag, StringComparer.Ordinal).Append(new CraftedTable("OS/2", os2)).ToList();
        else
            tables.Add(new CraftedTable("OS/2", os2));

        var face = AssertLoadsAsFreeType(Build(TrueTypeVersion, tables, sort: layout != "at the end of the file"));

        if (layout != "followed by a table")
            Assert.That(face.Ascender, Is.EqualTo((short)U16(Table(Arial(), "hhea"), 4)), "the table is absent: hhea's ascender");
    }

    /// <summary>
    /// An <c>OS/2</c> version above 5 is read with version 5's layout (Denys's safety-strict decision), as FreeType reads it;
    /// version 0xFFFF makes the table absent, as FreeType treats it.
    /// </summary>
    [TestCase(6)]
    [TestCase(0xFFFF)]
    public void Load_Os2VersionAbove5_ReadAsFreeType(int version)
    {
        var face = AssertLoadsAsFreeType(Patch(TypoMetricsArial(), "OS/2", os2 => WriteU16(os2, 0, version)));

        if (version == 0xFFFF)
            Assert.That(face.Ascender, Is.EqualTo((short)U16(Table(Arial(), "hhea"), 4)), "the table is absent: hhea's ascender");
    }

    /// <summary>A <c>name</c> version above 1 loads and reads as FreeType reads it (Denys's safety-strict decision).</summary>
    [Test]
    public void Load_NameVersionUnknown_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Patch(Arial(), "name", name => WriteU16(name, 0, 2)));

    [Test]
    public void Load_NameRecordsPastTable_Throws() =>
        AssertInvalid(Patch(Arial(), "name", name => WriteU16(name, 2, 0xFFFF)), "'name'");

    /// <summary>
    /// Every family-name record's string moved outside the storage: the records are skipped, as FreeType skips them (Denys's
    /// safety-strict decision), and the names read as FreeType reads them.
    /// </summary>
    [Test]
    public void Load_NameStringOutsideStorage_SkippedAsFreeType()
    {
        var font = Patch(Arial(), "name", name =>
        {
            for (var i = 0; i < U16(name, 2); i++)
            {
                if (U16(name, 6 + 12 * i + 6) == NameFamily)
                    WriteU16(name, 6 + 12 * i + 10, name.Length);
            }
        });

        var face = AssertLoadsAsFreeType(font);

        Assert.That(face.FamilyName, Is.Not.EqualTo(FontLoader.Load(Arial()).Faces[0].FamilyName));
    }

    [Test]
    public void Load_PostTooShort_Throws() => AssertInvalid(Truncate(Arial(), "post", 30), "'post' table is 30 bytes");

    [Test]
    public void Load_PostVersionUnknown_Throws() =>
        AssertInvalid(Patch(Arial(), "post", post => WriteU32(post, 0, 0x00050000)), "'post' table's version");

    /// <summary>Apple's <c>post</c> 4.0: only the header is read, so it loads, as FreeType loads it.</summary>
    [Test]
    public void Load_Post40_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Patch(Arial(), "post", post => WriteU32(post, 0, 0x00040000)));

    /// <summary>
    /// A <c>post</c> 2.0 table whose glyph count runs its name index past the table: the glyph names are never read, so the
    /// font loads (Denys's safety-strict decision) and reads as FreeType reads it.
    /// </summary>
    [Test]
    public void Load_PostGlyphNamesPastTable_LoadsAsFreeType()
    {
        var post = Table(Arial(), "post");
        Assume.That(U32(post, 0), Is.EqualTo(0x00020000u), "Arial's post table has glyph names");

        AssertLoadsAsFreeType(Patch(Arial(), "post", table => WriteU16(table, 32, 0xFFFF)));
    }

    [Test]
    public void Load_LocaTooShort_Throws()
    {
        var loca = Table(Arial(), "loca");

        AssertInvalid(Truncate(Arial(), "loca", loca.Length - 4), "'loca' table is");
    }

    [Test]
    public void Load_LocaNotAscending_Throws()
    {
        var font = WithGlyphs(Arial(), ReadGlyphs(Arial()));

        AssertInvalid(Patch(font, "loca", loca => WriteU32(loca, 8, U32(loca, 4) - 1)), "is smaller than offset");
    }

    [Test]
    public void Load_LocaPastGlyf_Throws()
    {
        var font = WithGlyphs(Arial(), ReadGlyphs(Arial()));

        AssertInvalid(Patch(font, "loca", loca => WriteU32(loca, loca.Length - 4, U32(loca, loca.Length - 4) + 4)), "past the end");
    }

    [Test]
    public void Load_GlyphShorterThanHeader_Throws() => AssertInvalid(WithGlyph(Arial(), 5, [0, 1, 0, 0]), "shorter than the 10-byte header");

    [Test]
    public void Load_ContourEndsNotAscending_Throws()
    {
        var glyph = CraftedTables.SimpleGlyph([3, 7]);
        WriteU16(glyph, 12, 2); // second contour ends before the first

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "not after the previous contour's end");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Load_InstructionsPastGlyphData_Throws(bool composite)
    {
        byte[] glyph;
        if (composite)
        {
            var w = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(10).U16(10);
            w.U16(0x0001 | ArgsAreXyValues | WeHaveInstructions).U16(36).U16(0).U16(0).U16(50);
            glyph = w.ToArray();
        }
        else
        {
            glyph = CraftedTables.SimpleGlyph([2])[..14];
            WriteU16(glyph, 12, 50);
        }

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "bytes of instructions run past");
    }

    [Test]
    public void Load_FlagRepeatPastPointCount_Throws()
    {
        var glyph = new BigEndianWriter().U16(1).U16(0).U16(0).U16(10).U16(10).U16(2).U16(0)
            .U8(0x01 | 0x08).U8(5).Bytes(new byte[8]).ToArray();

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "repeats 5 times past its point count");
    }

    [Test]
    public void Load_FlagsPastGlyphData_Throws()
    {
        var glyph = new BigEndianWriter().U16(1).U16(0).U16(0).U16(10).U16(10).U16(4).U16(0).U8(0x37).U8(0x37).ToArray();

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "point flags run past");
    }

    [Test]
    public void Load_CoordinatesPastGlyphData_Throws()
    {
        var glyph = new BigEndianWriter().U16(1).U16(0).U16(0).U16(10).U16(10).U16(0).U16(0).U8(0x01).U8(0).ToArray();

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "x coordinates run past");
    }

    [Test]
    public void Load_ComponentRecordPastGlyphData_Throws()
    {
        var glyph = CraftedTables.CompositeGlyph((36, ArgsAreXyValues | 0x0020, 0, 0));

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "component record runs past");
    }

    [Test]
    public void Load_ComponentGlyphOutOfRange_Throws()
    {
        var glyphCount = U16(Table(Arial(), "maxp"), 4);
        var glyph = CraftedTables.CompositeGlyph((glyphCount, ArgsAreXyValues, 0, 0));

        AssertInvalid(WithGlyph(Arial(), 5, glyph), $"references glyph {glyphCount}");
    }

    [Test]
    public void Load_ComponentReferencesItself_Throws()
    {
        var glyph = CraftedTables.CompositeGlyph((5, ArgsAreXyValues, 0, 0));

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "references the glyph itself");
    }

    [Test]
    public void Load_ComponentCycle_Throws()
    {
        var glyphs = ReadGlyphs(Arial());
        glyphs[5] = CraftedTables.CompositeGlyph((6, ArgsAreXyValues, 0, 0));
        glyphs[6] = CraftedTables.CompositeGlyph((7, ArgsAreXyValues, 0, 0));
        glyphs[7] = CraftedTables.CompositeGlyph((5, ArgsAreXyValues, 0, 0));

        AssertInvalid(WithGlyphs(Arial(), glyphs), "form a cycle");
    }

    /// <summary>
    /// A chain of composites one level deeper than <c>maxp.maxComponentDepth</c> declares loads (Denys's safety-strict
    /// decision; FreeType raises its own copy of the value) and its outlines equal FreeType's.
    /// </summary>
    [Test]
    public void Load_ComponentsDeeperThanMaxComponentDepth_LoadsAsFreeType()
    {
        var glyphs = ReadGlyphs(Arial());
        var depth = U16(Table(Arial(), "maxp"), 30);
        var simple = FindGlyph(Arial(), contours => contours > 0);

        // A chain one level deeper than maxp allows.
        var first = glyphs.Count - depth - 1;
        for (var level = 0; level <= depth; level++)
        {
            var next = level == depth ? simple : first + level + 1;
            glyphs[first + level] = CraftedTables.CompositeGlyph((next, ArgsAreXyValues, 0, 0));
        }

        AssertLoadsAsFreeType(WithGlyphs(Arial(), glyphs), Enumerable.Range(first, depth + 1).ToArray());
    }

    [TestCase(1000, 0)]
    [TestCase(0, 1000)]
    public void Load_AnchorPointOutOfRange_Throws(int parentPoint, int childPoint)
    {
        var simple = FindGlyph(Arial(), contours => contours > 0);
        var glyph = CraftedTables.CompositeGlyph((simple, ArgsAreXyValues, 0, 0), (simple, 0, parentPoint, childPoint));

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "anchor point");
    }

    [Test]
    public void Load_CompositeOverMaxPoints_Throws()
    {
        var glyphs = ReadGlyphs(Arial());
        glyphs[6] = CraftedTables.SimpleGlyph([249]);
        glyphs[5] = CraftedTables.CompositeGlyph(Enumerable.Repeat((6, ArgsAreXyValues, 0, 0), 263).ToArray());

        AssertInvalid(WithGlyphs(Arial(), glyphs), "points in all, more than 65535");
    }

    [Test]
    public void Load_CompositeOverExpandedLimit_ThrowsNotSupported()
    {
        var glyphs = ReadGlyphs(Arial());
        var empty = Enumerable.Range(0, glyphs.Count).First(i => glyphs[i].Length == 0);
        glyphs[6] = CraftedTables.CompositeGlyph(Enumerable.Repeat((empty, ArgsAreXyValues, 0, 0), 300).ToArray());
        glyphs[5] = CraftedTables.CompositeGlyph(Enumerable.Repeat((6, ArgsAreXyValues, 0, 0), 300).ToArray());
        var font = Patch(WithGlyphs(Arial(), glyphs), "maxp", maxp => WriteU16(maxp, 30, Math.Max((int)U16(maxp, 30), 2)));

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("component glyphs in all"));
    }

    [Test]
    public void Load_CompositeNestedPastLibraryLimit_ThrowsNotSupported()
    {
        const int levels = 256;
        var simple = FindGlyph(Arial(), contours => contours > 0);
        var chain = new byte[levels][];
        var glyphCount = U16(Table(Arial(), "maxp"), 4);
        for (var level = 0; level < levels; level++)
        {
            var next = level == levels - 1 ? simple : glyphCount + level + 1;
            chain[level] = CraftedTables.CompositeGlyph((next, ArgsAreXyValues, 0, 0));
        }

        var (font, _) = AppendGlyphs(Arial(), levels + 1, chain);

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("nest 256 levels"));
    }

    /// <summary>A <c>cmap</c> version above 0 loads and maps as FreeType maps it (Denys's safety-strict decision).</summary>
    [Test]
    public void Load_CmapVersionUnknown_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Patch(Arial(), "cmap", cmap => WriteU16(cmap, 0, 1)));

    [Test]
    public void Load_CmapEncodingRecordsPastTable_Throws() =>
        AssertInvalid(Patch(Arial(), "cmap", cmap => WriteU16(cmap, 2, 0x7FFF)), "'cmap'");

    [Test]
    public void Load_CmapSubtableOffsetPastTable_Throws() =>
        AssertInvalid(Patch(Arial(), "cmap", cmap => WriteU32(cmap, 8, (uint)cmap.Length)), "past the");

    [Test]
    public void Load_CmapSubtableLengthPastTable_Throws()
    {
        var subtable = CraftedTables.SimpleFormat4();
        WriteU16(subtable, 2, subtable.Length + 100);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "declares");
    }

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(10)]
    public void Load_CmapTrimmedDataPastSubtable_Throws(int format)
    {
        var subtable = format switch
        {
            0 => CraftedTables.Format0(100),
            6 => CraftedTables.Format6(32, 50, 10),
            _ => CraftedTables.Format10(32, 50, 10),
        };

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, CraftedTables.SimpleFormat4()), (0, 3, subtable))),
            "'cmap' data is too short");
    }

    [TestCase(3)]
    [TestCase(0)]
    public void Load_CmapFormat4SegCountInvalid_Throws(int segCountX2)
    {
        var subtable = CraftedTables.Format4([('A', 'Z', 1, 0), (0xFFFF, 0xFFFF, 1, 0)], segCountX2: segCountX2);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "segCountX2");
    }

    [Test]
    public void Load_CmapFormat4SegmentStartAfterEnd_Throws()
    {
        var subtable = CraftedTables.Format4([('Z', 'A', 1, 0), (0xFFFF, 0xFFFF, 1, 0)]);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "after its end");
    }

    [Test]
    public void Load_CmapFormat4SegmentsNotAscending_Throws()
    {
        var subtable = CraftedTables.Format4([('a', 'z', 1, 0), ('A', 'Z', 1, 0), (0xFFFF, 0xFFFF, 1, 0)]);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "not after the previous segment");
    }

    [Test]
    public void Load_CmapFormat4LastSegmentNotFFFF_Throws()
    {
        var subtable = CraftedTables.Format4([('A', 'Z', 1, 0)]);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "not U+FFFF");
    }

    [Test]
    public void Load_CmapFormat4GlyphIdsPastSubtable_Throws()
    {
        // Segment 0's idRangeOffset points 2 bytes past itself plus 2 * 26 ids, but only 10 are there.
        var subtable = CraftedTables.Format4([('A', 'Z', 0, 4), (0xFFFF, 0xFFFF, 1, 0)], new ushort[10]);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "glyph ids");
    }

    [TestCase(12)]
    [TestCase(13)]
    public void Load_CmapGroupStartAfterEnd_Throws(int format)
    {
        var subtable = CraftedTables.Groups(format, (0x60, 0x40, 1));

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 10, subtable))), "after its end");
    }

    [TestCase(12)]
    [TestCase(13)]
    public void Load_CmapGroupsNotAscending_Throws(int format)
    {
        var subtable = CraftedTables.Groups(format, (0x40, 0x60, 1), (0x50, 0x70, 1));

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 10, subtable))), "not after the previous group");
    }

    [TestCase("selectors")]
    [TestCase("defaults")]
    [TestCase("non-defaults")]
    public void Load_CmapFormat14NotAscending_Throws(string part)
    {
        var subtable = part switch
        {
            "selectors" => CraftedTables.Format14((0xFE0F, [], [(0x41, 36)]), (0xFE0E, [], [(0x41, 36)])),
            "defaults" => CraftedTables.Format14((0xFE0E, [(0x50, 5), (0x52, 1)], [])),
            _ => CraftedTables.Format14((0xFE0E, [], [(0x42, 36), (0x41, 37)])),
        };

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable), (3, 1, CraftedTables.SimpleFormat4()))), "not after");
    }

    [Test]
    public void Load_CmapFormat14PastSubtable_Throws()
    {
        var subtable = CraftedTables.Format14((0xFE0E, [], [(0x41, 36)]));
        WriteU32(subtable, 6 + 4 + 3 + 4, (uint)subtable.Length);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable), (3, 1, CraftedTables.SimpleFormat4()))),
            "'cmap' data is too short");
    }

    [Test]
    public void Load_CmapOnlyNonUnicode_ThrowsNotSupported()
    {
        var font = WithTable(Arial(), "cmap", CraftedTables.Cmap((1, 0, CraftedTables.Format0(262)), (3, 0, CraftedTables.SimpleFormat4())));

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("no Unicode"));
    }

    [Test]
    public void Load_CmapSelectedFormat8_ThrowsNotSupported()
    {
        var font = WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, CraftedTables.SimpleFormat4()), (3, 10, CraftedTables.Format8())));

        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<NotSupportedException>().With.Message.Contains("format 8"));
    }

    /// <summary>An <c>SVG </c> version above 0 is read with version 0's layout (Denys's safety-strict decision).</summary>
    [Test]
    public void Load_SvgVersionUnknown_ReadAsVersion0()
    {
        var svg = CraftedTables.Svg((36, 36, "<svg/>"u8.ToArray()));
        WriteU16(svg, 0, 1);

        var face = FontLoader.Load(WithTable(Arial(), "SVG ", svg)).Faces[0];

        Assert.That(face.GetSvgDocument(36).ToArray(), Is.EqualTo("<svg/>"u8.ToArray()));
    }

    [Test]
    public void Load_SvgIndexPastTable_Throws()
    {
        var svg = CraftedTables.Svg((36, 36, "<svg/>"u8.ToArray()));
        WriteU16(svg, 10, 100);

        AssertInvalid(WithTable(Arial(), "SVG ", svg), "'SVG '");
    }

    [Test]
    public void Load_SvgRecordStartAfterEnd_Throws()
    {
        var svg = CraftedTables.Svg((40, 36, "<svg/>"u8.ToArray()));

        AssertInvalid(WithTable(Arial(), "SVG ", svg), "after its end");
    }

    [Test]
    public void Load_SvgRecordsNotAscending_Throws()
    {
        var svg = CraftedTables.Svg((40, 41, "<svg/>"u8.ToArray()), (36, 37, "<svg/>"u8.ToArray()));

        AssertInvalid(WithTable(Arial(), "SVG ", svg), "not after the previous record");
    }

    [Test]
    public void Load_SvgDocumentPastTable_Throws()
    {
        var svg = CraftedTables.Svg((36, 36, "<svg/>"u8.ToArray()));
        WriteU32(svg, 10 + 2 + 8, 1000);

        AssertInvalid(WithTable(Arial(), "SVG ", svg), "outside its");
    }

    [Test]
    public void Load_SvgGzipError_Throws()
    {
        var gzip = Compress("<svg/>"u8.ToArray());
        gzip[10] = 0xFF; // a deflate block of the reserved type 3

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip))), "not valid deflate data");
    }

    [Test]
    public void Load_SvgGzipTruncated_Throws()
    {
        var gzip = Compress("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0h10v10z\"/></svg>"u8.ToArray());

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip[..^5]))), "The 'SVG ' document for glyphs 36..36");
    }

    /// <summary>Only the trailer's CRC-32 is wrong: the CRC-32 check alone rejects the document.</summary>
    [Test]
    public void Load_SvgGzipCrcMismatch_Throws()
    {
        var gzip = Compress("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0h10v10z\"/></svg>"u8.ToArray());
        gzip[^8] ^= 0x01; // the trailer's CRC-32

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip))), "fails its gzip CRC-32 check");
    }

    /// <summary>Only the trailer's ISIZE is wrong: the ISIZE check alone rejects the document.</summary>
    [Test]
    public void Load_SvgGzipIsizeMismatch_Throws()
    {
        var gzip = Compress("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0h10v10z\"/></svg>"u8.ToArray());
        gzip[^4] ^= 0x01; // the trailer's ISIZE

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip))), "but its gzip ISIZE records");
    }

    /// <summary>
    /// The inflate budget holds for the file: face 0's document inflates to 1 MiB, face 1's to 127.5 MiB; each face alone is
    /// under 128 MiB, the two together are not. The load stops in face 1's counting pass, before allocating its document
    /// (bound: 8 MiB allocated by the load).
    /// </summary>
    [Test]
    public void Load_CollectionSvgOverFileBudget_ThrowsNotSupported()
    {
        var small = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, CompressZeros(1L * 1024 * 1024))));
        var large = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, CompressZeros(127L * 1024 * 1024 + 512 * 1024))));
        var collection = Collection(small, large);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var exception = Assert.Throws<NotSupportedException>(() => FontLoader.Load(collection));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"{collection.Length} bytes of collection; the load allocated {allocated} bytes before it threw.");

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("file's 134217728-byte budget"));
            Assert.That(allocated, Is.LessThan(8L * 1024 * 1024));
        });
    }

    /// <summary>
    /// 20 faces sharing every table but <c>loca</c> and <c>glyf</c>, each with its own <c>loca</c> of 65,535 empty glyphs and
    /// its own 4-byte <c>glyf</c> (the reviewer's variant, whose <c>loca</c> records overlapped in one run of zeros, is refused
    /// by the overlap rule; one <c>glyf</c> under several <c>loca</c> tables, by the same-inputs rule). Each face's
    /// arrays take about 12.5 times its 128 KiB of <c>loca</c>, so the file's parsed-memory budget (8 bytes per file byte)
    /// stops the load part way; what the load allocates stays within that budget plus 4 MiB.
    /// </summary>
    [Test]
    public void Load_CollectionParsedTablesOverFileBudget_ThrowsNotSupported()
    {
        var collection = MinimalCollection(faces: 20, glyphCount: 65535, ownLocaAndGlyf: true);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var exception = Assert.Throws<NotSupportedException>(() => FontLoader.Load(collection));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine(
            $"{collection.Length} bytes of collection; the load allocated {allocated} bytes in {watch.ElapsedMilliseconds} ms before it threw.");

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("bytes per byte of the file"));
            Assert.That(allocated, Is.LessThan(8L * collection.Length + 4 * 1024 * 1024));
        });
    }

    /// <summary>
    /// The per-face and per-glyph accounting: one more face sharing every table adds its fixed cost and its directory records
    /// (2,048 + 64 per record, plus 64 per record for the overlap check, plus 48 for its entry in the collection's face
    /// list); 1,000 more glyphs add their <c>loca</c> offsets and the check's arrays (4 + 21 bytes each). Without either
    /// reserve the totals change, so this test fails.
    /// </summary>
    [Test]
    public void Load_ParsedMemoryAccounting_CountsFacesAndGlyphArrays()
    {
        var arial = Arial();
        var tables = U16(arial, 4);
        var twoFaces = SfntReaderMeasure(SharedTablesCollection(arial, 2));
        var threeFaces = SfntReaderMeasure(SharedTablesCollection(arial, 3));
        var fewGlyphs = SfntReaderMeasure(MinimalCollection(faces: 1, glyphCount: 100, ownLocaAndGlyf: true));
        var moreGlyphs = SfntReaderMeasure(MinimalCollection(faces: 1, glyphCount: 1100, ownLocaAndGlyf: true));

        Assert.Multiple(() =>
        {
            Assert.That(threeFaces - twoFaces, Is.EqualTo(2048 + 64 * tables + 64 * tables + 48), "one more face");
            Assert.That(moreGlyphs - fewGlyphs, Is.EqualTo(1000 * (4 + 21)), "1,000 more glyphs");
        });
    }

    /// <summary>
    /// Faces sharing Arial's tables, each adding about 4.6 KB of accounted memory for 336 bytes of file: the last count that fits
    /// 8 bytes per file byte loads, one more face throws. Without the per-face reserve both would load.
    /// </summary>
    [Test]
    public void Load_CollectionJustOverParsedBudget_ThrowsNotSupported()
    {
        var arial = Arial();
        var tables = U16(arial, 4);
        var directoryLength = 12 + 16 * tables;
        var perFace = 2048 + 128L * tables + 48;
        var single = SfntReaderMeasure(SharedTablesCollection(arial, 1));
        long Length(int faces) => 12 + 4L * faces + (long)faces * directoryLength + arial.Length - directoryLength;

        var over = 1;
        while (single + (over - 1) * perFace <= ParsedLimit * Length(over))
            over++;

        TestContext.Out.WriteLine($"{over - 1} faces fit, {over} faces do not.");
        Assert.Multiple(() =>
        {
            Assert.That(() => FontLoader.Load(SharedTablesCollection(arial, over - 1)), Throws.Nothing, "just under");
            Assert.That(() => FontLoader.Load(SharedTablesCollection(arial, over)),
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("bytes per byte of the file"), "just over");
        });
    }

    [TestCase("same offset, different length")]
    [TestCase("overlapping ranges")]
    [TestCase("same range, different tag")]
    [TestCase("within one face")]
    public void Load_TableRecordsOverlappingButDifferent_Throws(string overlap)
    {
        byte[] font;
        if (overlap == "within one face")
        {
            font = Arial();
            var name = RecordOffset(font, "name");
            var post = RecordOffset(font, "post");
            WriteU32(font, post + 8, U32(font, name + 8) + 4);
            WriteU32(font, post + 12, 8);
        }
        else
        {
            var arial = Arial();
            font = SharedTablesCollection(arial, 2);
            var record = RecordOffset(font, "post", 12 + 4 * 2 + 12 + 16 * U16(arial, 4));
            switch (overlap)
            {
                case "same offset, different length":
                    WriteU32(font, record + 12, U32(font, record + 12) - 2);
                    break;
                case "overlapping ranges":
                    WriteU32(font, record + 8, U32(font, record + 8) + 2);
                    WriteU32(font, record + 12, U32(font, record + 12) - 2);
                    break;
                default:
                    Encoding.ASCII.GetBytes("posu").CopyTo(font, record);
                    break;
            }
        }

        AssertInvalid(font, "two records may share bytes only as the same record");
    }

    /// <summary>
    /// The reviewer's case in small: 8,000 faces, each with one record of its own over one 4 MiB run of zeros (offsets and
    /// lengths differ by a byte). The overlap check, before any parse, rejects the file: deterministically, within a work
    /// limit of one unit per byte of the file (the time is only reported).
    /// </summary>
    [Test]
    public void Load_CollectionOverlappingRecordsAtScale_ThrowsWithinLinearWork()
    {
        const int faces = 8000;
        const int run = 4 * 1024 * 1024;
        const int directoryLength = 12 + 16;
        var header = 12 + 4 * faces;
        var runStart = header + faces * directoryLength;
        var collection = new byte[runStart + run + faces];
        Encoding.ASCII.GetBytes("ttcf").CopyTo(collection, 0);
        WriteU16(collection, 4, 1);
        WriteU32(collection, 8, faces);
        for (var i = 0; i < faces; i++)
        {
            var at = header + i * directoryLength;
            WriteU32(collection, 12 + 4 * i, (uint)at);
            WriteU32(collection, at, TrueTypeVersion);
            WriteU16(collection, at + 4, 1);
            Encoding.ASCII.GetBytes("glyf").CopyTo(collection, at + 12);
            WriteU32(collection, at + 12 + 8, (uint)(runStart + i));
            WriteU32(collection, at + 12 + 12, run);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var exception = Assert.Throws<InvalidDataException>(
            () => CoolEngine.Fonts.Sfnt.SfntReader.ReadFaces(collection, workLimit: collection.Length));
        watch.Stop();
        TestContext.Out.WriteLine($"{collection.Length} bytes, {faces} faces: rejected in {watch.Elapsed.TotalMilliseconds:F0} ms.");

        Assert.That(exception!.Message, Does.Contain("two records may share bytes only as the same record"));
    }

    /// <summary>
    /// 64 faces whose directories point at one set of tables load, and each table is parsed once, as for one face; also when
    /// each face's records carry their own (unverified) checksums.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Load_CollectionIdenticalRecords_ParsedOnce(bool checksumsDiffer)
    {
        var arial = Arial();
        var collection = SharedTablesCollection(arial, 64);
        if (checksumsDiffer)
        {
            var count = U16(arial, 4);
            for (var face = 0; face < 64; face++)
            {
                var directory = (int)U32(collection, 12 + 4 * face);
                for (var t = 0; t < count; t++)
                    WriteU32(collection, directory + 12 + 16 * t + 4, (uint)(face * 1000 + t));
            }
        }

        var file = FontLoader.Load(collection);
        var single = CoolEngine.Fonts.Sfnt.SfntReader.CountParses(arial);
        var shared = CoolEngine.Fonts.Sfnt.SfntReader.CountParses(collection);

        Assert.Multiple(() =>
        {
            Assert.That(file.Faces, Has.Count.EqualTo(64));
            Assert.That(single, Is.GreaterThan(0));
            Assert.That(shared, Is.EqualTo(single), "one parse per table, not per face");
        });
    }

    /// <summary>
    /// 16 faces share one <c>SVG </c> table whose document inflates to 8 MiB: it is inflated once for the file (bound: 16 MiB
    /// allocated by the load, where one inflation per face would take 128 MiB), and every face returns the same bytes.
    /// </summary>
    [Test]
    public void Load_CollectionSharedSvgDocument_InflatesOnce()
    {
        var font = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, CompressZeros(8L * 1024 * 1024))));
        var collection = SharedTablesCollection(font, 16);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var file = FontLoader.Load(collection);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"16 faces over one 8 MiB document: the load allocated {allocated} bytes.");

        var documents = file.Faces.Select(face => face.GetSvgDocument(36)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(allocated, Is.LessThan(16L * 1024 * 1024));
            Assert.That(documents.All(document => document.Length == 8 * 1024 * 1024), Is.True);
            Assert.That(documents.All(document => document.Equals(documents[0])), Is.True, "one copy, shared");
        });
    }

    [Test]
    public void Load_SvgGzipHeaderMethodNot8_Throws()
    {
        var gzip = Compress("<svg/>"u8.ToArray());
        gzip[2] = 7;

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip))), "compression method 7, not 8");
    }

    [Test]
    public void Load_SvgGzipHeaderReservedFlags_Throws()
    {
        var gzip = Compress("<svg/>"u8.ToArray());
        gzip[3] |= 0x20;

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip))), "reserved gzip header flags");
    }

    [Test]
    public void Load_SvgGzipHeaderRunsIntoTrailer_Throws()
    {
        // FEXTRA with an extra field longer than the whole member.
        var gzip = Compress("<svg/>"u8.ToArray());
        gzip[3] |= 0x04;
        var withExtra = gzip[..10].Concat(new byte[] { 0xFF, 0xFF }).Concat(gzip[10..]).ToArray();

        AssertInvalid(WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, withExtra))), "gzip header runs into its trailer");
    }

    /// <summary>A header CRC (FHCRC) that matches loads; one that does not throws.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Load_SvgGzipHeaderCrcMismatch_Throws(bool matches)
    {
        var svg = "<svg/>"u8.ToArray();
        var gzip = Compress(svg);
        gzip[3] |= 0x02;
        var header = gzip[..10];
        var crc = (ushort)CoolEngine.Fonts.Sfnt.Crc32.Finish(CoolEngine.Fonts.Sfnt.Crc32.Update(CoolEngine.Fonts.Sfnt.Crc32.Initial, header));
        if (!matches)
            crc ^= 0x0101;

        var withCrc = header.Concat(new[] { (byte)crc, (byte)(crc >> 8) }).Concat(gzip[10..]).ToArray();
        var font = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, withCrc)));

        if (matches)
            Assert.That(FontLoader.Load(font).Faces[0].GetSvgDocument(36).ToArray(), Is.EqualTo(svg));
        else
            AssertInvalid(font, "fails its gzip header CRC check");
    }

    private const long ParsedLimit = 8;

    private static long SfntReaderMeasure(byte[] data) => CoolEngine.Fonts.Sfnt.SfntReader.MeasureParsedBytes(data);

    /// <summary>
    /// One list of a format-14 subtable serves selector U+FE00 as default ranges and U+FE01 as non-default mappings, so it is
    /// checked as both kinds. Valid both ways, it maps as FreeType maps it; valid only as default ranges, the load throws.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Load_CmapFormat14ListUsedAsBothKinds_CheckedAsBoth(bool valid)
    {
        // Header (10) and two selector records (22), then the shared list at offset 32.
        var list = valid
            ? new BigEndianWriter().U32(1).U24('A').U16(36).ToArray()             // a range U+0041 + 0 / a mapping U+0041 -> 36
            : new BigEndianWriter().U32(2).U24('A').U8(0).U24('P').U8(0).ToArray(); // two ranges, too short for two mappings
        var subtable = new BigEndianWriter().U16(14).U32((uint)(32 + list.Length)).U32(2)
            .U24(0xFE00).U32(32).U32(0)
            .U24(0xFE01).U32(0).U32(32)
            .Bytes(list).ToArray();
        var font = WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable), (3, 1, CraftedTables.SimpleFormat4())));

        if (!valid)
        {
            AssertInvalid(font, "'cmap' data is too short");
            return;
        }

        var face = FontLoader.Load(font).Faces[0];
        using var reference = m_freeType.OpenFace(font, 0);
        Assert.Multiple(() =>
        {
            Assert.That(face.GetGlyphIndex('A', 0xFE00), Is.EqualTo(reference.GetCharVariantIndex('A', 0xFE00)).And.EqualTo(36));
            Assert.That(face.GetGlyphIndex('A', 0xFE01), Is.EqualTo(reference.GetCharVariantIndex('A', 0xFE01)).And.EqualTo(36));
        });
    }

    /// <summary>
    /// Two (1, 0) format-0 subtables for languages 0 and 2: the pair may repeat; the font loads and maps as FreeType maps it.
    /// </summary>
    [Test]
    public void Load_CmapSamePairTwoLanguages_LoadsAsFreeType()
    {
        var english = CraftedTables.Format0(262);
        var french = CraftedTables.Format0(262);
        WriteU16(french, 4, 2);
        var font = WithTable(Arial(), "cmap",
            CraftedTables.CmapInOrder((1, 0, english), (1, 0, french), (3, 1, CraftedTables.SimpleFormat4())));

        var face = FontLoader.Load(font).Faces[0];
        using var reference = m_freeType.OpenFace(font, 0);

        var differing = Enumerable.Range(0, 0x10000).Count(codePoint => face.GetGlyphIndex(codePoint) != reference.GetCharIndex(codePoint));
        Assert.That(differing, Is.Zero);
    }

    /// <summary>
    /// Two 65 MiB documents (about 64 KiB of gzip each) pass the face's 128 MiB budget together: the load stops in the first
    /// pass, which only counts the inflated bytes, so it stays small in memory (bound: 16 MiB allocated by the load).
    /// </summary>
    [Test]
    public void Load_SvgDocumentsOverInflateBudget_ThrowsNotSupported()
    {
        var document = CompressZeros(65L * 1024 * 1024);
        var font = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, document), (37, 37, document)));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var exception = Assert.Throws<NotSupportedException>(() => FontLoader.Load(font));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"{font.Length} bytes of font; the load allocated {allocated} bytes before it threw.");

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("budget"));
            Assert.That(allocated, Is.LessThan(16L * 1024 * 1024));
        });
    }

    [Test]
    public void Load_CollectionRepeatedFaceOffset_Throws()
    {
        var collection = Collection(Arial(), Arial());
        WriteU32(collection, 16, U32(collection, 12));

        AssertInvalid(collection, "as an earlier face does");
    }

    /// <summary>
    /// 64 directories pointing at one set of tables: the faces share one parsed copy of each table. Bound: the collection's load
    /// may allocate at most twice the single face's load plus 16 KiB per face (a face's own objects: its directory dictionary
    /// of about 20 records, the face, its table data holder, its outline source and its two name strings take a few KiB).
    /// </summary>
    [Test]
    public void Load_CollectionFacesSharingTables_ShareParsedTables()
    {
        const int faces = 64;
        const long perFace = 16 * 1024;
        var arial = Arial();
        var collection = SharedTablesCollection(arial, faces);

        FontLoader.Load(arial);
        FontLoader.Load(collection);
        var single = Allocated(() => FontLoader.Load(arial));
        var shared = Allocated(() => FontLoader.Load(collection));
        var file = FontLoader.Load(collection);
        TestContext.Out.WriteLine($"Single face: {single} bytes; {faces} faces sharing tables: {shared} bytes " +
                                  $"(bound {2 * single + faces * perFace}).");

        Assert.Multiple(() =>
        {
            Assert.That(file.Faces, Has.Count.EqualTo(faces));
            Assert.That(file.Faces.Select(face => face.Data.Glyf).Distinct().Count(), Is.EqualTo(1), "one glyf table");
            Assert.That(file.Faces.Select(face => face.Data.Cmap).Distinct().Count(), Is.EqualTo(1), "one cmap table");
            Assert.That(shared, Is.LessThan(2 * single + faces * perFace));
        });
    }

    [Test]
    public void Load_CmapFormat4RangeOffsetBeforeGlyphIds_Throws()
    {
        // Segment 0's idRangeOffset 2 points into idRangeOffset[1], before the glyph id array.
        var subtable = CraftedTables.Format4([('A', 'B', 0, 2), (0xFFFF, 0xFFFF, 1, 0)], new ushort[2]);

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))), "before the glyph id array");
    }

    [TestCase("selector")]
    [TestCase("default range")]
    [TestCase("non-default mapping")]
    public void Load_CmapFormat14ValueOutsideUnicode_Throws(string part)
    {
        var subtable = part switch
        {
            "selector" => CraftedTables.Format14((0x110000, [], [(0x41, 36)])),
            "default range" => CraftedTables.Format14((0xFE0E, [(0x10FFFF, 1)], [])),
            _ => CraftedTables.Format14((0xFE0E, [], [(0x110000, 36)])),
        };

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable), (3, 1, CraftedTables.SimpleFormat4()))),
            "U+10FFFF");
    }

    [Test]
    public void Load_CmapFormat14SelectorZero_Throws()
    {
        var subtable = CraftedTables.Format14((0, [], [(0x41, 36)]));

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable), (3, 1, CraftedTables.SimpleFormat4()))),
            "variation selector U+0 ");
    }

    [TestCase("0")]
    [TestCase("4, idDelta")]
    [TestCase("4, idRangeOffset")]
    [TestCase("6")]
    [TestCase("10")]
    [TestCase("12")]
    [TestCase("13")]
    [TestCase("14")]
    public void Load_CmapGlyphIdPastGlyphCount_Throws(string format)
    {
        // Arial has 1674 glyphs; format 0 maps bytes, so it is tried on a copy cut to 100 glyphs.
        var font = format == "0" ? SmallArial(100) : Arial();
        var cmap = format switch
        {
            "0" => CraftedTables.Cmap((3, 1, Format0Mapping('A', 200))),
            "4, idDelta" => CraftedTables.Cmap((3, 1, CraftedTables.Format4([('A', 'A', 2000 - 'A', 0), (0xFFFF, 0xFFFF, 1, 0)]))),
            "4, idRangeOffset" => CraftedTables.Cmap((3, 1, CraftedTables.Format4([('A', 'A', 0, 4), (0xFFFF, 0xFFFF, 1, 0)], [2000]))),
            "6" => CraftedTables.Cmap((3, 1, new BigEndianWriter().U16(6).U16(14).U16(0).U16('A').U16(2).U16(36).U16(2000).ToArray())),
            "10" => CraftedTables.Cmap((3, 10, new BigEndianWriter().U16(10).U16(0).U32(24).U32(0).U32('A').U32(2).U16(36).U16(2000).ToArray())),
            "12" => CraftedTables.Cmap((3, 10, CraftedTables.Groups(12, (0x41, 0x5A, 1660)))),
            "13" => CraftedTables.Cmap((3, 10, CraftedTables.Groups(13, (0x41, 0x5A, 2000)))),
            _ => CraftedTables.Cmap((0, 5, CraftedTables.Format14((0xFE0E, [], [(0x41, 2000)]))), (3, 1, CraftedTables.SimpleFormat4())),
        };

        AssertInvalid(WithTable(font, "cmap", cmap), "into glyph ids up to");
    }

    /// <summary>
    /// A version-1 <c>name</c> table with an English Windows family record "AB" and, after it, one for language ID 0x8009
    /// (language tag 9; English to FreeType's choice) "CD". The second is skipped, as FreeType skips it, when tag 9 is past
    /// the list or its string lies outside the storage, and chosen when tag 9 is valid (Denys's safety-strict decision).
    /// </summary>
    [TestCase(1, -1, "AB")]
    [TestCase(10, 9, "AB")]
    [TestCase(10, -1, "CD")]
    public void Load_NameRecordLanguageTag_ReadAsFreeType(int tagCount, int tagOutsideStorage, string family)
    {
        const int records = 2;
        var storage = 6 + 12 * records + 2 + 4 * tagCount;
        var w = new BigEndianWriter().U16(1).U16(records).U16(storage)
            .U16(3).U16(1).U16(0x409).U16(NameFamily).U16(4).U16(0)
            .U16(3).U16(1).U16(0x8009).U16(NameFamily).U16(4).U16(4)
            .U16(tagCount);
        for (var i = 0; i < tagCount; i++)
            w.U16(4).U16(i == tagOutsideStorage ? 100 : 8);

        var name = w.U16('A').U16('B').U16('C').U16('D').U16('e').U16('n').ToArray();

        var face = AssertLoadsAsFreeType(WithTable(Arial(), "name", name));

        Assert.That(face.FamilyName, Is.EqualTo(family));
    }

    /// <summary>
    /// A <c>post</c> 2.5 table whose offset array runs past the table: the glyph names are never read, so the font loads
    /// (Denys's safety-strict decision) and reads as FreeType reads it.
    /// </summary>
    [Test]
    public void Load_Post25IndexPastTable_LoadsAsFreeType()
    {
        var post = Table(Arial(), "post")[..32];
        WriteU32(post, 0, 0x00025000);
        post = post.Concat(new BigEndianWriter().U16(0xFFFF).ToArray()).ToArray();

        AssertLoadsAsFreeType(WithTable(Arial(), "post", post));
    }

    /// <summary>The first two <c>name</c> records swapped load and read as FreeType reads them (Denys's safety-strict decision).</summary>
    [Test]
    public void Load_NameRecordsNotSorted_LoadsAsFreeType()
    {
        var font = Patch(Arial(), "name", name =>
        {
            var first = name[6..18];
            name.AsSpan(18, 12).CopyTo(name.AsSpan(6, 12));
            first.CopyTo(name, 18);
        });

        AssertLoadsAsFreeType(font);
    }

    /// <summary>
    /// Every subfamily record turned into a second family record for its platform, encoding and language, after the first: a
    /// repeated record is accepted (FreeType and OTS accept it; Denys's safety-strict decision), and the family name is chosen
    /// as FreeType chooses it, from the later record.
    /// </summary>
    [Test]
    public void Load_NameRecordRepeated_ChosenAsFreeType()
    {
        const int nameSubfamily = 2;
        var font = Patch(Arial(), "name", name =>
        {
            for (var i = 0; i < U16(name, 2); i++)
            {
                if (U16(name, 6 + 12 * i + 6) == nameSubfamily)
                    WriteU16(name, 6 + 12 * i + 6, NameFamily);
            }
        });

        var face = AssertLoadsAsFreeType(font);

        Assert.That(face.FamilyName, Is.Not.EqualTo(FontLoader.Load(Arial()).Faces[0].FamilyName), "the later record chosen");
    }

    /// <summary>
    /// Encoding records out of order, or (3, 1) twice with different mappings: both are accepted (FreeType and OTS accept them;
    /// Denys's safety-strict decision) and map as FreeType maps them: the last Unicode record in file order is chosen.
    /// </summary>
    [TestCase("unsorted")]
    [TestCase("repeated")]
    public void Load_CmapEncodingRecordsUnsortedOrRepeated_MapAsFreeType(string order)
    {
        var subtable = CraftedTables.SimpleFormat4();
        var other = CraftedTables.Format4([('A', 'Z', 40 - 'A', 0), (0xFFFF, 0xFFFF, 1, 0)]);
        var cmap = order == "unsorted"
            ? CraftedTables.CmapInOrder((3, 1, subtable), (0, 3, other))
            : CraftedTables.CmapInOrder((3, 1, subtable), (3, 1, other));

        var face = AssertLoadsAsFreeType(WithTable(Arial(), "cmap", cmap));

        Assert.That(face.GetGlyphIndex('A'), Is.EqualTo(40));
    }

    /// <summary>
    /// The final 0xFFFF-0xFFFF format-4 segment is exempt from the idRangeOffset and glyph-id rules (Denys's safety-strict
    /// decision): with idRangeOffset 0xFFFF on Arial, and with idDelta 0 (U+FFFF to glyph 65,535) on Arial cut to 100 glyphs,
    /// the font loads and every BMP code point maps as FreeType maps it.
    /// </summary>
    [TestCase("idRangeOffset 0xFFFF")]
    [TestCase("idDelta 0")]
    public void Load_CmapFormat4Terminator_MapsAsFreeType(string terminator)
    {
        var (font, last) = terminator == "idDelta 0"
            ? (SmallArial(100), (0xFFFF, 0xFFFF, 0, 0))
            : (Arial(), (0xFFFF, 0xFFFF, 0, 0xFFFF));
        var cmap = CraftedTables.Cmap((3, 1, CraftedTables.Format4([('A', 'Z', 36 - 'A', 0), last])));

        var face = AssertLoadsAsFreeType(WithTable(font, "cmap", cmap));

        Assert.That(face.GetGlyphIndex(0xFFFF), Is.Zero);
    }

    [Test]
    public void Load_HeadMajorVersionNot1_Throws() =>
        AssertInvalid(Patch(Arial(), "head", head => WriteU16(head, 0, 2)), "'head' table's majorVersion");

    [Test]
    public void Load_HheaMajorVersionNot1_Throws() =>
        AssertInvalid(Patch(Arial(), "hhea", hhea => WriteU16(hhea, 0, 2)), "'hhea' table's majorVersion");

    /// <summary><c>head</c> flags bit 15 set is ignored, as FreeType ignores it (Denys's "Accept it", 2026-11-26).</summary>
    [Test]
    public void Load_HeadFlagsReservedBit_LoadsAsFreeType() =>
        AssertLoadsAsFreeType(Patch(Arial(), "head", head => WriteU16(head, 16, U16(head, 16) | 0x8000)), 36, 100);

    [Test]
    public void Load_SimpleGlyphReservedFlag_Throws()
    {
        var glyph = CraftedTables.SimpleGlyph([2]);
        glyph[10 + 2 + 2] |= 0x80; // the first point's flag

        AssertInvalid(WithGlyph(Arial(), 5, glyph), "bit 7");
    }

    [Test]
    public void Load_TrueTypeFaceWithCffTable_UsesGlyf()
    {
        var face = FontLoader.Load(WithTable(Arial(), "CFF ", [1, 0, 4, 1])).Faces[0];

        Assert.That(face.OutlineFormat, Is.EqualTo(OutlineFormat.TrueType));
    }

    [Test]
    public void Load_TrueTypeFaceWithCffTableAndNoGlyf_IsBitmapOnly()
    {
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca" or "EBDT" or "EBLC")).ToList();
        tables.Add(new CraftedTable("CFF ", [1, 0, 4, 1]));
        tables.Add(new CraftedTable("EBLC", new BigEndianWriter().U32(0x00020000).U32(0).ToArray()));
        tables.Add(new CraftedTable("EBDT", new BigEndianWriter().U32(0x00020000).ToArray()));

        var face = FontLoader.Load(Build(TrueTypeVersion, tables)).Faces[0];

        Assert.That(face.OutlineFormat, Is.EqualTo(OutlineFormat.None));
    }

    [TestCase(true, OutlineFormat.Cff2)]
    [TestCase(false, OutlineFormat.Cff)]
    public void Load_OttoFace_UsesCff2ElseCff(bool withCff2, OutlineFormat expected)
    {
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca")).ToList();
        tables.Add(new CraftedTable("CFF ", [1, 0, 4, 1]));
        if (withCff2)
            tables.Add(new CraftedTable("CFF2", [2, 0, 5, 0, 0]));

        var face = FontLoader.Load(Build(OpenTypeCffVersion, tables)).Faces[0];

        Assert.That(face.OutlineFormat, Is.EqualTo(expected));
    }

    [Test]
    public void Load_OttoFaceWithoutCff_Throws()
    {
        AssertInvalid(Build(OpenTypeCffVersion, ReadTables(Arial())), "without a 'CFF2' or 'CFF ' table");
    }

    // ---- Fix round 5 (Phase B review).

    /// <summary>
    /// Many encoding records over one large format-12 subtable: the subtable is checked once, not once per record. The first
    /// case is the reviewer's shape (65,534 records over a 4.8 MB subtable, which took 34 s): its load work stays under 4 units
    /// per byte of the font (the time is only reported); the second, small enough for FreeType to open (it validates every
    /// record's subtable), also maps every BMP code point as FreeType maps it.
    /// </summary>
    [TestCase(65534, 400_000, false)]
    [TestCase(2_000, 50_000, true)]
    public void Load_CmapManyRecordsOverOneSubtable_ValidatedOnce(int records, int groups, bool compareWithFreeType)
    {
        var subtable = new BigEndianWriter().U16(12).U16(0).U32((uint)(16 + 12 * groups)).U32(0).U32((uint)groups);
        for (var k = 0; k < groups; k++)
            subtable.U32((uint)(2 * k + 0x20)).U32((uint)(2 * k + 0x20)).U32((uint)(1 + k % 1000));

        var subtableOffset = 4 + 8 * records;
        var cmap = new BigEndianWriter().U16(0).U16(records);
        for (var i = 0; i < records; i++)
            cmap.U16(0).U16(4).U32((uint)subtableOffset);

        var font = WithTable(Arial(), "cmap", cmap.Bytes(subtable.ToArray()).ToArray());
        FontLoader.Load(Arial());

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var face = FontLoader.Load(font).Faces[0];
        watch.Stop();
        TestContext.Out.WriteLine(
            $"{records} records over one {16 + 12 * groups}-byte format-12 subtable ({font.Length} bytes of font): loaded in " +
            $"{watch.Elapsed.TotalMilliseconds:F0} ms.");

        var work = CoolEngine.Fonts.Sfnt.SfntReader.MeasureWork(font);
        TestContext.Out.WriteLine($"Load work: {work} units, {(double)work / font.Length:F2} per byte.");

        Assert.Multiple(() =>
        {
            Assert.That(work, Is.LessThan(4L * font.Length), "the subtable checked once");
            Assert.That(face.GetGlyphIndex(0x20 + 2 * 1234), Is.EqualTo(1 + 1234 % 1000));
        });

        if (compareWithFreeType)
            AssertLoadsAsFreeType(font);
    }

    /// <summary>
    /// Thousands of faces sharing every table but <c>maxp</c>, each with its own identical <c>maxp</c> record (the reviewer's
    /// shape: 4,000 faces took 42 s, 2,000 over heavy glyphs 66 s), over Arial with 300 extra glyphs of 20,000 points: every
    /// shared table is parsed once (the same inputs), and the load work stays under 4 units per byte of the file (the time is
    /// only reported); only each face's own <c>maxp</c> adds a parse.
    /// </summary>
    [TestCase(4000)]
    [TestCase(2000)]
    public void Load_CollectionSharedTablesSameInputs_ParsedOnce(int faces)
    {
        var heavy = CraftedTables.SimpleGlyph([19_999]);
        var (font, _) = AppendGlyphs(Arial(), 1, Enumerable.Repeat(heavy, 300).ToArray());
        var shared = ReadTables(font).Where(table => table.Tag != "maxp").ToList();
        var maxp = Table(font, "maxp");
        var collection = CollectionOf(Enumerable.Range(0, faces)
            .Select(_ => shared.Append(new CraftedTable("maxp", (byte[])maxp.Clone())).ToList()).ToList());
        FontLoader.Load(Arial());

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var file = FontLoader.Load(collection);
        watch.Stop();
        TestContext.Out.WriteLine(
            $"{faces} faces ({collection.Length} bytes), each with its own maxp: loaded in {watch.Elapsed.TotalMilliseconds:F0} ms.");
        var single = CoolEngine.Fonts.Sfnt.SfntReader.CountParses(font);
        var parses = CoolEngine.Fonts.Sfnt.SfntReader.CountParses(collection);
        var work = CoolEngine.Fonts.Sfnt.SfntReader.MeasureWork(collection);
        TestContext.Out.WriteLine($"Load work: {work} units, {(double)work / collection.Length:F2} per byte.");

        Assert.Multiple(() =>
        {
            Assert.That(file.Faces, Has.Count.EqualTo(faces));
            Assert.That(parses, Is.EqualTo(single + faces - 1), "each face's own maxp, every shared table once");
            Assert.That(work, Is.LessThan(4L * collection.Length), "each shared table walked once");
        });
    }

    /// <summary>
    /// Two faces sharing one table that they would read with different inputs (Denys's "Same-inputs rule"): face 1 has its
    /// own copy of every other table; for <c>loca</c> (shared with <c>glyf</c>) and <c>cmap</c> its <c>maxp</c> has one glyph
    /// fewer, for <c>hmtx</c> its <c>hhea</c> one long metric fewer.
    /// </summary>
    [TestCase("loca")]
    [TestCase("cmap")]
    [TestCase("hmtx")]
    public void Load_CollectionSharedTableConflictingInputs_Throws(string table)
    {
        var arial = Arial();
        var first = ReadTables(arial);
        var glyphCount = U16(Table(arial, "maxp"), 4);
        var longMetrics = U16(Table(arial, "hhea"), 34);
        var second = first.Select(record =>
        {
            if (record.Tag == table || (table == "loca" && record.Tag == "glyf"))
                return record;

            var copy = (byte[])record.Data.Clone();
            if (record.Tag == "maxp" && table != "hmtx")
                WriteU16(copy, 4, glyphCount - 1);
            else if (record.Tag == "hhea")
                WriteU16(copy, 34, table == "hmtx" ? longMetrics - 1 : Math.Min(longMetrics, glyphCount - 1));

            return new CraftedTable(record.Tag, copy);
        }).ToList();

        AssertInvalid(CollectionOf([first, second]), $"share the '{table}' table");
    }

    /// <summary>
    /// A format-14 subtable whose 4,280 default-range lists start 4 bytes apart in one 4.4 MB run of ranges (the reviewer's
    /// shape: about 2.4e9 range checks): the lists take more bytes in all than the subtable has, which OTS refuses and so does
    /// the load, deterministically within a work limit of two units per byte of the font (the time is only reported).
    /// </summary>
    [Test]
    public void Load_CmapFormat14ListsOverSubtableLength_Throws()
    {
        const int entries = 1_100_000;
        const int selectors = entries / 257;
        var listStart = 10 + 11 * selectors;
        var subtable = new BigEndianWriter().U16(14).U32((uint)(listStart + 4 * entries)).U32(selectors);
        for (var i = 1; i <= selectors; i++)
            subtable.U24(0xE0100 + i).U32((uint)(listStart + 4 * i)).U32(0);

        // Entry j is the range U+j (+0); read as a list's count at entry i, it is 256 * i ranges, all ascending.
        for (var j = 0; j < entries; j++)
            subtable.U24(j).U8(0);

        var font = WithTable(Arial(), "cmap", CraftedTables.Cmap((0, 5, subtable.ToArray()), (3, 1, CraftedTables.SimpleFormat4())));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.That(() => CoolEngine.Fonts.Sfnt.SfntReader.ReadFaces(font, workLimit: 2L * font.Length),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("lists take"));
        watch.Stop();
        TestContext.Out.WriteLine($"{selectors} overlapping format-14 lists: refused in {watch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    /// <summary>
    /// A zero-length table record is a missing table, as FreeType's table lookup treats it (Mentor, by identity and
    /// safety-strict): a zero-length <c>fvar</c> makes no variable font, and a zero-length <c>SVG </c> loads as FreeType loads it.
    /// </summary>
    [Test]
    public void Load_ZeroLengthFvarAndSvg_Absent()
    {
        var font = WithTable(WithTable(Arial(), "fvar", []), "SVG ", []);
        using var reference = m_freeType.OpenFace(font, 0);

        var face = AssertLoadsAsFreeType(font);

        Assert.Multiple(() =>
        {
            Assert.That(face.IsVariable, Is.False.And.EqualTo(reference.HasMultipleMasters));
            Assert.That(face.GetSvgDocument(36).Length, Is.Zero);
        });
    }

    /// <summary>A zero-length <c>hvgl</c> beside bitmap tables is absent: the face loads as bitmap-only, not NotSupported.</summary>
    [Test]
    public void Load_ZeroLengthHvgl_Ignored()
    {
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca")).ToList();
        tables.Add(new CraftedTable("EBLC", new BigEndianWriter().U32(0x00020000).U32(0).ToArray()));
        tables.Add(new CraftedTable("EBDT", new BigEndianWriter().U32(0x00020000).ToArray()));
        tables.Add(new CraftedTable("hvgl", []));

        var face = FontLoader.Load(Build(TrueTypeVersion, tables)).Faces[0];

        Assert.That(face.OutlineFormat, Is.EqualTo(OutlineFormat.None));
    }

    /// <summary>A zero-length <c>EBDT</c> or <c>sbix</c> is absent: with no <c>glyf</c>, the face has no outline or bitmap table.</summary>
    [TestCase("EBDT")]
    [TestCase("sbix")]
    public void Load_ZeroLengthBitmapTableWithoutGlyf_Throws(string tag)
    {
        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca")).ToList();
        tables.Add(new CraftedTable(tag, []));

        AssertInvalid(Build(TrueTypeVersion, tables), "no outline table");
    }

    /// <summary>
    /// A format-4 subtable ending in a pair of 0xFFFF-0xFFFF segments (as Germania.ttf does) loads, as FreeType maps it and OTS
    /// accepts it; U+FFFF maps through the first of the pair, as FreeType's search settles there: with idDelta 1 to glyph 0,
    /// with idDelta 6 to glyph 5. Every BMP code point maps as FreeType maps it.
    /// </summary>
    [TestCase(1, 0)]
    [TestCase(6, 5)]
    public void Load_CmapFormat4DoubleTerminator_MapsAsFreeType(int firstDelta, int expected)
    {
        var subtable = CraftedTables.Format4([('A', 'Z', 36 - 'A', 0), (0xFFFF, 0xFFFF, firstDelta, 0), (0xFFFF, 0xFFFF, 1, 0)]);

        var face = AssertLoadsAsFreeType(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))));

        Assert.That(face.GetGlyphIndex(0xFFFF), Is.EqualTo(expected));
    }

    /// <summary>
    /// A subtable FreeType's choice can never select (not Unicode, not format 14) gets only its bounds check (Mentor,
    /// safety-strict): a (1, 0) format 6 whose glyph array runs past its declared length, or a (3, 4) format 4 with
    /// overlapping segments, beside a sound (3, 1), loads and maps as FreeType maps it.
    /// </summary>
    [TestCase("(1, 0) format 6, array past its length")]
    [TestCase("(3, 4) format 4, overlapping segments")]
    public void Load_CmapUnselectableBrokenSubtable_LoadsAsFreeType(string broken)
    {
        var cmap = broken.StartsWith("(1, 0)", StringComparison.Ordinal)
            ? CraftedTables.Cmap((1, 0, CraftedTables.Format6(32, 50, 10)), (3, 1, CraftedTables.SimpleFormat4()))
            : CraftedTables.Cmap((3, 1, CraftedTables.SimpleFormat4()),
                (3, 4, CraftedTables.Format4([('A', 'Z', 1, 0), ('M', 'Q', 1, 0), (0xFFFF, 0xFFFF, 1, 0)])));

        var face = AssertLoadsAsFreeType(WithTable(Arial(), "cmap", cmap));

        Assert.That(face.GetGlyphIndex('A'), Is.EqualTo(36));
    }

    /// <summary>An <c>SVG </c> document offset of 0xFFFFFFFF, which a 32-bit sum with the list offset would wrap into the table.</summary>
    [Test]
    public void Load_SvgDocumentOffsetWraps_Throws()
    {
        var svg = CraftedTables.Svg((36, 36, "<svg/>"u8.ToArray()));
        WriteU32(svg, 10 + 2 + 4, 0xFFFFFFFF);

        AssertInvalid(WithTable(Arial(), "SVG ", svg), "outside its");
    }

    /// <summary>A simple glyph whose last contour ends at point 65,535 has 65,536 points: point numbers are 16-bit.</summary>
    [Test]
    public void Load_SimpleGlyphOf65536Points_Throws() =>
        AssertInvalid(WithGlyph(Arial(), 5, CraftedTables.SimpleGlyph([0xFFFF])), "65,536 points");

    /// <summary>
    /// A header-only collection declaring 4,000,000 faces (16 MB): the face list is accounted before it is sized, so the load
    /// throws after a small allocation (bound: the load's copy of the file plus 1 MiB), not after sizing the list from the
    /// header's count (the reviewer measured 160 MB for 8,000,000 faces before the fix).
    /// </summary>
    [Test]
    public void Load_CollectionDeclaringMillionsOfFaces_ThrowsAfterSmallAllocation()
    {
        const int faces = 4_000_000;
        var collection = new byte[12 + 4 * faces];
        Encoding.ASCII.GetBytes("ttcf").CopyTo(collection, 0);
        WriteU16(collection, 4, 1);
        WriteU32(collection, 8, faces);

        Exception? exception = null;
        var allocated = Allocated(() => exception = Assert.Throws<NotSupportedException>(() => FontLoader.Load(collection)));
        TestContext.Out.WriteLine($"{faces} declared faces: the load allocated {allocated} bytes before it threw.");

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("face list"));
            Assert.That(allocated, Is.LessThan(collection.Length + 1024 * 1024));
        });
    }

    /// <summary>A <c>cmap</c> encoding record whose subtable offset lies inside the table's header and records (as OTS refuses).</summary>
    [TestCase(0)]
    [TestCase(8)]
    public void Load_CmapSubtableOffsetInsideHeader_Throws(int offset)
    {
        var cmap = CraftedTables.Cmap((0, 3, CraftedTables.SimpleFormat4()), (3, 1, CraftedTables.SimpleFormat4()));
        WriteU32(cmap, 4 + 4, (uint)offset);

        AssertInvalid(WithTable(Arial(), "cmap", cmap), "inside the table's header and encoding records");
    }

    // ---- Fix round 6 (round-5 re-review).

    /// <summary>
    /// The reviewer's shape: Unicode format-12 subtables at offsets 24 bytes apart, each running to the end of one group list,
    /// so each would walk the bytes of all after it (1,000 of them; and 65,534, the reviewer's count, which took 17.4 s).
    /// Subtables checked in full may share bytes only as the same subtable (Denys's "Rules + work budget", as OTS refuses
    /// "excessive overlap"): the load throws before checking any (the time is only reported).
    /// </summary>
    [TestCase(1000)]
    [TestCase(65534)]
    public void Load_CmapSubtablesSharingBytes_Throws(int subtables)
    {
        var groupBytes = 24 * subtables;
        var recordsEnd = 4 + 8 * (subtables + 1);
        var tableLength = recordsEnd + groupBytes + 16;
        var cmap = new byte[tableLength];
        WriteU16(cmap, 2, subtables + 1);
        for (var i = 0; i < subtables; i++)
        {
            var offset = recordsEnd + 24 * i;
            WriteU16(cmap, 4 + 8 * i, 0);
            WriteU16(cmap, 4 + 8 * i + 2, 4);
            WriteU32(cmap, 4 + 8 * i + 4, (uint)offset);
            WriteU16(cmap, offset, 12);
            WriteU32(cmap, offset + 4, (uint)(tableLength - offset));
            WriteU32(cmap, offset + 12, (uint)((tableLength - offset - 16) / 12));
        }

        // A clean (3, 10) last: an empty format-12 subtable after the list.
        var clean = recordsEnd + groupBytes;
        WriteU16(cmap, 4 + 8 * subtables, 3);
        WriteU16(cmap, 4 + 8 * subtables + 2, 10);
        WriteU32(cmap, 4 + 8 * subtables + 4, (uint)clean);
        WriteU16(cmap, clean, 12);
        WriteU32(cmap, clean + 4, 16);

        var font = WithTable(Arial(), "cmap", cmap);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        AssertInvalid(font, "subtables may share bytes only as the same subtable");
        watch.Stop();
        TestContext.Out.WriteLine($"{subtables} overlapping subtables ({font.Length} bytes): refused in {watch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    /// <summary>
    /// The reviewer's shape: <c>SVG </c> records with one offset but different lengths over one gzip member (1,000 over 64 KiB;
    /// and 20,000 over 1 MiB, the reviewer's size, which took 19.7 s). Documents may share bytes only as the same document
    /// (offset and length): the load throws before inflating any (the time is only reported). Records with the same offset and
    /// length still share one document.
    /// </summary>
    [TestCase(1000, 64 * 1024, false)]
    [TestCase(20000, 1024 * 1024, false)]
    [TestCase(1000, 64 * 1024, true)]
    public void Load_SvgDocumentsSharingBytes_ThrowUnlessIdentical(int records, int documentLength, bool identical)
    {
        var noise = new byte[documentLength];
        new Random(1).NextBytes(noise);
        var gzip = Compress(noise);
        var listLength = 2 + 12 * records;
        var svg = new BigEndianWriter().U16(0).U32(10).U32(0).U16(records);
        for (var i = 0; i < records; i++)
            svg.U16(i).U16(i).U32((uint)listLength).U32((uint)(identical ? gzip.Length : gzip.Length - i));

        var font = WithTable(Arial(), "SVG ", svg.Bytes(gzip).ToArray());

        if (!identical)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            AssertInvalid(font, "documents may share bytes only as the same document");
            watch.Stop();
            TestContext.Out.WriteLine($"{records} records over one gzip member ({font.Length} bytes): refused in {watch.Elapsed.TotalMilliseconds:F0} ms.");
            return;
        }

        var face = FontLoader.Load(font).Faces[0];
        Assert.Multiple(() =>
        {
            Assert.That(face.GetSvgDocument(0).ToArray(), Is.EqualTo(noise));
            Assert.That(face.GetSvgDocument(records - 1).Equals(face.GetSvgDocument(0)), Is.True, "one document, shared");
        });
    }

    /// <summary>
    /// The per-file work budget (Denys's "Rules + work budget"): with a limit of half the file's length in work units, loading
    /// Arial passes it and throws <see cref="NotSupportedException"/>; with the file's own limit it loads.
    /// </summary>
    [Test]
    public void Load_WorkOverFileBudget_ThrowsNotSupported()
    {
        var arial = Arial();

        Assert.Multiple(() =>
        {
            Assert.That(() => CoolEngine.Fonts.Sfnt.SfntReader.ReadFaces(arial, workLimit: arial.Length / 2),
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("more work than this library allows"));
            Assert.That(() => FontLoader.Load(arial), Throws.Nothing);
        });
    }

    /// <summary>
    /// The work accounting of the repository's Arial, as measured for <c>ParsedTables.MaxWorkPerFileByte</c>: 418,437 units
    /// for its 367,112 bytes (1.14 per byte). A charge added, dropped or changed anywhere in the load changes the total.
    /// </summary>
    [Test]
    public void Load_WorkAccounting_ArialAsMeasured()
    {
        var arial = Arial();

        var work = CoolEngine.Fonts.Sfnt.SfntReader.MeasureWork(arial);

        Assert.Multiple(() =>
        {
            Assert.That(arial.Length, Is.EqualTo(367_112));
            Assert.That(work, Is.EqualTo(418_437));
        });
    }

    /// <summary>
    /// A zero-length <c>glyf</c> beside a <c>loca</c> whose offsets are all 0: FreeType loads the face with all-empty glyphs,
    /// and so does ours (Mentor, by identity); one non-zero offset lies past the empty <c>glyf</c> and throws.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Load_ZeroLengthGlyfAllZeroLoca_LoadsAsFreeType(bool allZero)
    {
        var glyphCount = U16(Table(Arial(), "maxp"), 4);
        var offsetSize = U16(Table(Arial(), "head"), 50) == 0 ? 2 : 4;
        var loca = new byte[offsetSize * (glyphCount + 1)];
        if (!allZero)
            loca[^1] = 2;

        var tables = ReadTables(Arial()).Where(table => table.Tag is not ("glyf" or "loca")).ToList();
        tables.Add(new CraftedTable("glyf", []));
        tables.Add(new CraftedTable("loca", loca));
        var font = Build(TrueTypeVersion, tables);

        if (!allZero)
        {
            AssertInvalid(font, "past the end of the 0-byte 'glyf' table");
            return;
        }

        var face = AssertLoadsAsFreeType(font, 0, 36);

        Assert.That(face.OutlineFormat, Is.EqualTo(OutlineFormat.TrueType));
    }

    // ---- Fix round 7 (final review).

    /// <summary>
    /// A non-final format-4 segment with idRangeOffset 0xFFFF (FreeType drops such a subtable) throws with its own message, in
    /// a small subtable and in one of 65,534 bytes, the most a format-4 length field declares: no format-4 subtable is long
    /// enough for the generic glyph-id bounds rule to accept the offset, so this rule is what names the cause.
    /// </summary>
    [TestCase(0)]
    [TestCase(32_751)]
    public void Load_CmapFormat4NonFinalRangeOffsetFFFF_Throws(int padding)
    {
        var subtable = CraftedTables.Format4([('A', 'A', 0, 0xFFFF), (0xFFFF, 0xFFFF, 1, 0)], new ushort[padding]);
        Assert.That(subtable.Length, Is.EqualTo(padding == 0 ? 32 : 65_534));

        AssertInvalid(WithTable(Arial(), "cmap", CraftedTables.Cmap((3, 1, subtable))),
            "has idRangeOffset 0xFFFF, which only the final 0xFFFF-0xFFFF segment may have");
    }

    /// <summary>
    /// A valid, highly compressible <c>SVG </c> font: one document of 150,000 short paths that gzip compresses more than 20 to
    /// 1 (about 3.4 MB inflated, well under the inflate budget). Only the compressed bytes are charged to the work budget (the
    /// inflate budget bounds the output), so it loads through <see cref="FontLoader.Load(byte[])"/>.
    /// </summary>
    [Test]
    public void Load_HighlyCompressedSvgDocument_Loads()
    {
        var random = new Random(1);
        var text = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\">");
        for (var i = 0; i < 150_000; i++)
            text.Append($"<path d=\"M{random.Next(4)} {random.Next(4)}h10v10z\"/>");

        var document = Encoding.ASCII.GetBytes(text.Append("</svg>").ToString());
        var gzip = Compress(document);
        var font = WithTable(Arial(), "SVG ", CraftedTables.Svg((36, 36, gzip)));
        TestContext.Out.WriteLine($"{document.Length} bytes in {gzip.Length} of gzip: {(double)document.Length / gzip.Length:F1} to 1.");

        var face = FontLoader.Load(font).Faces[0];

        Assert.Multiple(() =>
        {
            Assert.That((double)document.Length / gzip.Length, Is.GreaterThan(20));
            Assert.That(face.GetSvgDocument(36).ToArray(), Is.EqualTo(document));
        });
    }

    /// <summary>
    /// The reviewer's shape: 200 faces sharing Arial's tables, each with its own (3, 1) format-4 <c>cmap</c> of 8,000
    /// segments (13 MB). The work and memory accounting format their messages only when they throw, so the load allocates
    /// what it keeps and what its parsers need, not a message per glyph or segment (bound: the load's copy of the file plus
    /// 8 MiB; it allocates about 1.1 MB beyond that copy, where a message per charge took 232 MB).
    /// </summary>
    [Test]
    public void Load_ManyChargedWalks_AllocateNoMessagePerCharge()
    {
        const int faces = 200;
        const int segments = 8000;
        var shared = ReadTables(Arial()).Where(table => table.Tag != "cmap").ToList();
        var collection = CollectionOf(Enumerable.Range(0, faces).Select(face =>
        {
            var ranges = Enumerable.Range(0, segments)
                .Select(k => (0x20 + 2 * k, 0x20 + 2 * k, 1 + (k + face) % 1000 - (0x20 + 2 * k), 0))
                .Append((0xFFFF, 0xFFFF, 1, 0)).ToArray();
            return shared.Append(new CraftedTable("cmap", CraftedTables.Cmap((3, 1, CraftedTables.Format4(ranges))))).ToList();
        }).ToList());
        FontLoader.Load(Arial());

        var allocated = Allocated(() => FontLoader.Load(collection));
        TestContext.Out.WriteLine($"{faces} faces, {collection.Length} bytes: the load allocated {allocated} bytes.");

        Assert.That(allocated, Is.LessThan(collection.Length + 8L * 1024 * 1024));
    }

    /// <summary>
    /// A collection whose faces list their tables; each distinct array (by reference) is laid out once, so faces that list the
    /// same array share its record. Checksums are left 0 (they are not verified).
    /// </summary>
    private static byte[] CollectionOf(IReadOnlyList<IReadOnlyList<CraftedTable>> faces)
    {
        var header = 12 + 4 * faces.Count;
        var directories = faces.Select(face => 12 + 16 * face.Count).ToArray();
        var output = new MemoryStream();
        output.Write(new byte[header + directories.Sum()]);
        var offsets = new Dictionary<byte[], uint>(ReferenceEqualityComparer.Instance);
        foreach (var table in faces.SelectMany(face => face))
        {
            if (offsets.ContainsKey(table.Data))
                continue;

            offsets[table.Data] = (uint)output.Length;
            output.Write(table.Data);
            output.Write(new byte[(4 - table.Data.Length % 4) % 4]);
        }

        var result = output.ToArray();
        Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
        WriteU16(result, 4, 1);
        WriteU32(result, 8, (uint)faces.Count);
        var at = header;
        for (var i = 0; i < faces.Count; i++)
        {
            WriteU32(result, 12 + 4 * i, (uint)at);
            WriteU32(result, at, TrueTypeVersion);
            WriteU16(result, at + 4, faces[i].Count);
            var sorted = faces[i].OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
            for (var t = 0; t < sorted.Count; t++)
            {
                var record = at + 12 + 16 * t;
                Encoding.ASCII.GetBytes(sorted[t].Tag).CopyTo(result, record);
                WriteU32(result, record + 8, offsets[sorted[t].Data]);
                WriteU32(result, record + 12, (uint)sorted[t].Data.Length);
            }

            at += directories[i];
        }

        return result;
    }

    /// <summary>
    /// A collection of <paramref name="faces"/> minimal faces with <paramref name="glyphCount"/> empty glyphs each: every table
    /// shared, <c>loca</c> and <c>glyf</c> too unless <paramref name="ownLocaAndGlyf"/> gives each face its own pair (all
    /// offsets 0, every glyph empty; each <c>glyf</c> is 4 zero bytes, as a zero-length table is absent). A <c>glyf</c> shared
    /// with different <c>loca</c> tables would break the same-inputs rule.
    /// </summary>
    private static byte[] MinimalCollection(int faces, int glyphCount, bool ownLocaAndGlyf)
    {
        var locaLength = 2 * (glyphCount + 1);
        var head = new byte[54];
        WriteU16(head, 0, 1);
        WriteU32(head, 12, 0x5F0F3CF5);
        WriteU16(head, 18, 1000);
        var hhea = new byte[36];
        WriteU16(hhea, 0, 1);
        WriteU16(hhea, 34, 1);
        var maxp = new byte[32];
        WriteU32(maxp, 0, 0x00010000);
        WriteU16(maxp, 4, glyphCount);
        var hmtx = new byte[4 + 2 * (glyphCount - 1)];
        var post = new byte[32];
        WriteU32(post, 0, 0x00030000);
        var name = new BigEndianWriter().U16(0).U16(0).U16(6).ToArray();
        var cmap = CraftedTables.Cmap((3, 1, CraftedTables.Format4([(0xFFFF, 0xFFFF, 1, 0)])));

        // Shared tables, then one run of zeros holding every face's loca and glyf.
        string[] tags = ["cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "name", "post"];
        var shared = new Dictionary<string, byte[]> { ["cmap"] = cmap, ["head"] = head, ["hhea"] = hhea, ["hmtx"] = hmtx,
            ["maxp"] = maxp, ["name"] = name, ["post"] = post };
        var directoryLength = 12 + 16 * tags.Length;
        var header = 12 + 4 * faces;
        var tablesStart = header + faces * directoryLength;
        var output = new MemoryStream();
        output.Write(new byte[tablesStart]);
        var offsets = new Dictionary<string, uint>();
        foreach (var (tag, data) in shared)
        {
            offsets[tag] = (uint)output.Length;
            output.Write(data);
            output.Write(new byte[(4 - data.Length % 4) % 4]);
        }

        var locaRun = (uint)output.Length;
        var pairLength = (locaLength + 3) / 4 * 4 + 4;
        output.Write(new byte[ownLocaAndGlyf ? (long)pairLength * faces : pairLength]);
        var result = output.ToArray();

        Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
        WriteU16(result, 4, 1);
        WriteU32(result, 8, (uint)faces);
        for (var i = 0; i < faces; i++)
        {
            var at = header + i * directoryLength;
            WriteU32(result, 12 + 4 * i, (uint)at);
            WriteU32(result, at, TrueTypeVersion);
            WriteU16(result, at + 4, tags.Length);
            for (var t = 0; t < tags.Length; t++)
            {
                var record = at + 12 + 16 * t;
                var tag = tags[t];
                var pair = locaRun + (ownLocaAndGlyf ? (uint)(i * pairLength) : 0u);
                var (offset, length) = tag switch
                {
                    "loca" => (pair, locaLength),
                    "glyf" => (pair + (uint)(pairLength - 4), 4),
                    _ => (offsets[tag], shared[tag].Length),
                };
                Encoding.ASCII.GetBytes(tag).CopyTo(result, record);
                WriteU32(result, record + 4, Checksum(result.AsSpan((int)offset, length)));
                WriteU32(result, record + 8, offset);
                WriteU32(result, record + 12, (uint)length);
            }
        }

        return result;
    }

    /// <summary>gzip of <paramref name="count"/> zero bytes, written a mebibyte at a time.</summary>
    private static byte[] CompressZeros(long count)
    {
        var zeros = new byte[1024 * 1024];
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            for (long written = 0; written < count; written += zeros.Length)
                gzip.Write(zeros, 0, (int)Math.Min(zeros.Length, count - written));
        }

        return output.ToArray();
    }

    private static long Allocated(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>A collection of <paramref name="faces"/> directories, all pointing at the one copy of the font's tables.</summary>
    private static byte[] SharedTablesCollection(byte[] font, int faces)
    {
        var count = U16(font, 4);
        var directoryLength = 12 + 16 * count;
        var header = 12 + 4 * faces;
        var tablesStart = header + faces * directoryLength;
        var result = new byte[tablesStart + font.Length - directoryLength];

        Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
        WriteU16(result, 4, 1);
        WriteU32(result, 8, (uint)faces);
        for (var i = 0; i < faces; i++)
        {
            var at = header + i * directoryLength;
            WriteU32(result, 12 + 4 * i, (uint)at);
            font.AsSpan(0, directoryLength).CopyTo(result.AsSpan(at));
            for (var t = 0; t < count; t++)
            {
                var record = at + 12 + 16 * t + 8;
                WriteU32(result, record, U32(result, record) - (uint)directoryLength + (uint)tablesStart);
            }
        }

        font.AsSpan(directoryLength).CopyTo(result.AsSpan(tablesStart));
        return result;
    }

    /// <summary>A format 0 subtable mapping one byte code to a glyph and the rest to 0.</summary>
    private static byte[] Format0Mapping(int code, int glyph)
    {
        var subtable = CraftedTables.Format0(262);
        subtable.AsSpan(6).Clear();
        subtable[6 + code] = (byte)glyph;
        return subtable;
    }

    /// <summary>
    /// Arial cut to its first <paramref name="glyphCount"/> glyphs: composites reaching past them become empty, and maxp, hhea
    /// and hmtx follow.
    /// </summary>
    private static byte[] SmallArial(int glyphCount)
    {
        var font = Arial();
        var glyphs = ReadGlyphs(font).Take(glyphCount).ToList();
        for (var g = 0; g < glyphs.Count; g++)
        {
            if (glyphs[g].Length >= 10 && (short)U16(glyphs[g], 0) < 0)
                glyphs[g] = [];
        }

        var longMetrics = Math.Min((int)U16(Table(font, "hhea"), 34), glyphCount);
        var hmtx = Table(font, "hmtx")[..(4 * longMetrics + 2 * (glyphCount - longMetrics))];
        font = WithGlyphs(font, ReadGlyphs(font).Select((glyph, g) => g < glyphCount ? glyphs[g] : glyph).ToList());
        var tables = ReadTables(font).Where(table => table.Tag is not ("glyf" or "loca" or "maxp" or "hhea" or "hmtx")).ToList();
        var maxp = Table(font, "maxp");
        WriteU16(maxp, 4, glyphCount);
        var hhea = Table(font, "hhea");
        WriteU16(hhea, 34, longMetrics);
        var glyf = new MemoryStream();
        var loca = new byte[4 * (glyphCount + 1)];
        for (var g = 0; g < glyphCount; g++)
        {
            WriteU32(loca, 4 * g, (uint)glyf.Length);
            glyf.Write(glyphs[g]);
        }

        WriteU32(loca, 4 * glyphCount, (uint)glyf.Length);
        tables.AddRange([new("maxp", maxp), new("hhea", hhea), new("hmtx", hmtx), new("glyf", glyf.ToArray()), new("loca", loca)]);
        return Build(TrueTypeVersion, tables);
    }

    internal static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(data);

        return output.ToArray();
    }

    private static void AssertInvalid(byte[] font, string message) =>
        Assert.That(() => FontLoader.Load(font), Throws.TypeOf<InvalidDataException>().With.Message.Contains(message));

    /// <summary>
    /// Loads the font and checks that face 0 reads as FreeType reads it: names, units per em, glyph count, ascender, descender
    /// and height, the glyph of every BMP code point, the line metrics at 32 px and, for <paramref name="glyphs"/>, the
    /// unhinted outline and metrics at 32 px.
    /// </summary>
    private FontFace AssertLoadsAsFreeType(byte[] font, params int[] glyphs)
    {
        var face = FontLoader.Load(font).Faces[0];
        using var reference = m_freeType.OpenFace(font, 0);
        reference.SetPixelSize(32);
        var scaled = face.CreateScaledFont(new ScaledFontOptions(32) { Hinting = HintingMode.None });

        var differences = new List<string>();
        for (var codePoint = 0; codePoint < 0x10000 && differences.Count < 5; codePoint++)
        {
            var ours = face.GetGlyphIndex(codePoint);
            var expected = reference.GetCharIndex(codePoint);
            if (ours != expected)
                differences.Add($"U+{codePoint:X4}: glyph {ours}, FreeType {expected}");
        }

        foreach (var glyph in glyphs)
        {
            var expected = reference.LoadGlyph(glyph, hinting: false, render: false);
            if (ScaledFontTests.Compare(scaled.GetOutline(glyph), expected.Outline) is { } outline)
                differences.Add($"glyph {glyph} outline: {outline}");

            if (scaled.GetMetrics(glyph) != expected.Metrics)
                differences.Add($"glyph {glyph} metrics: {scaled.GetMetrics(glyph)}, FreeType {expected.Metrics}");
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
            Assert.That(scaled.LineMetrics, Is.EqualTo(reference.SizeMetrics), "line metrics at 32 px");
            Assert.That(differences, Is.Empty);
        });

        return face;
    }

    private const int NameFamily = 1;

    /// <summary>Arial with <c>OS/2</c> <c>fsSelection</c> bit 7 (USE_TYPO_METRICS) set, so the face's metrics come from OS/2.</summary>
    private static byte[] TypoMetricsArial() => Patch(Arial(), "OS/2", os2 => WriteU16(os2, 62, U16(os2, 62) | 0x80));

    /// <summary>Where a table's record starts in a face's directory.</summary>
    private static int RecordOffset(byte[] font, string tag, int faceOffset = 0)
    {
        var count = U16(font, faceOffset + 4);
        for (var i = 0; i < count; i++)
        {
            var at = faceOffset + 12 + 16 * i;
            if (Encoding.ASCII.GetString(font, at, 4) == tag)
                return at;
        }

        throw new InvalidOperationException($"No '{tag}' record.");
    }
}
