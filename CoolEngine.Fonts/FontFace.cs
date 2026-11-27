// ReadNames and ReadLineMetrics follow FreeType 2.13.2 src/sfnt/sfobjs.c (sfnt_load_face) (FreeType Project License;
// credit in docs).

using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts;

/// <summary>
/// One face of a font file: its names, design metrics, character map and glyphs. Immutable and safe on any thread; sized
/// glyphs come from a <see cref="ScaledFont"/>, which <see cref="CreateScaledFont"/> makes.
/// </summary>
public sealed class FontFace
{
    private const int MaxCodePoint = 0x10FFFF;

    private readonly IOutlineSource m_outlines;

    internal FontFace(FaceData data)
    {
        Data = data;
        m_outlines = data.OutlineFormat switch
        {
            OutlineFormat.TrueType => new GlyfOutlineSource(data),
            OutlineFormat.Cff => new CffOutlineSource(data, isCff2: false),
            OutlineFormat.Cff2 => new CffOutlineSource(data, isCff2: true),
            _ => new EmptyOutlineSource(data),
        };

        (FamilyName, StyleName) = ReadNames(data);
        (Ascender, Descender, Height) = ReadLineMetrics(data);
        MaxAdvanceWidth = (short)data.Hhea.AdvanceWidthMax;
    }

    /// <summary>The face's index in its file (0 unless the file is a collection).</summary>
    public int FaceIndex => Data.FaceIndex;

    /// <summary>
    /// The family name as FreeType's <c>family_name</c>: the WWS, typographic or font family name chosen as FreeType chooses
    /// them, in ASCII with <c>?</c> for other characters; empty when the face has none.
    /// </summary>
    public string FamilyName { get; }

    /// <summary>The style name as FreeType's <c>style_name</c>, chosen and converted like <see cref="FamilyName"/>.</summary>
    public string StyleName { get; }

    /// <summary>Font units per em, from <c>head</c>.</summary>
    public int UnitsPerEm => Data.Head.UnitsPerEm;

    /// <summary>The number of glyphs, from <c>maxp</c>.</summary>
    public int GlyphCount => Data.GlyphCount;

    /// <summary>The outline table the glyphs come from; <see cref="Fonts.OutlineFormat.None"/> for a bitmap-only face.</summary>
    public OutlineFormat OutlineFormat => Data.OutlineFormat;

    /// <summary>
    /// The ascender in font units as FreeType's <c>FT_FaceRec</c>: <c>OS/2</c>'s typographic one when <c>USE_TYPO_METRICS</c>
    /// is set, else <c>hhea</c>'s, falling back to <c>OS/2</c> when <c>hhea</c>'s ascender and descender are both 0.
    /// </summary>
    public short Ascender { get; }

    /// <summary>The descender in font units (negative below the baseline), chosen like <see cref="Ascender"/>.</summary>
    public short Descender { get; }

    /// <summary>The line height in font units: ascender minus descender plus the line gap, chosen like <see cref="Ascender"/>.</summary>
    public short Height { get; }

    /// <summary>The face has an <c>fvar</c> table.</summary>
    public bool IsVariable => Data.Tables.Contains(Tags.Fvar);

    /// <summary>
    /// FreeType 2.13.2's <c>FT_Load_Glyph</c> with default flags would hand this face to its auto-hinter: a TrueType face
    /// with <c>glyf</c> outlines that has no instructions at all (<c>maxp.maxSizeOfInstructions</c> 0, no <c>fpgm</c>, no
    /// <c>prep</c>) and is not one of FreeType's tricky fonts (<c>FT_Load_Glyph</c> in <c>src/base/ftobjs.c</c>). This library
    /// has no auto-hinter: such a face renders unhinted, with <see cref="HintingMode.Native"/> too (there is nothing to run).
    /// </summary>
    public bool WouldBeAutoHintedByFreeType =>
        OutlineFormat == OutlineFormat.TrueType
        && Data.Maxp.MaxSizeOfInstructions == 0
        && Data.Tables.Get(0x6670676D).Length == 0   // fpgm
        && Data.Tables.Get(0x70726570).Length == 0   // prep
        && !Hinting.TrueType.TrickyFonts.IsTricky(Data);

    /// <summary>The variation axes; empty for a face that is not variable.</summary>
    /// <exception cref="NotSupportedException">The face is variable: the axes come with OT-4 WP5.</exception>
    public IReadOnlyList<VariationAxis> Axes =>
        IsVariable ? throw new NotSupportedException("Variations: OT-4 WP5") : [];

    /// <summary>The named instances; empty for a face that is not variable.</summary>
    /// <exception cref="NotSupportedException">The face is variable: the instances come with OT-4 WP5.</exception>
    public IReadOnlyList<NamedInstance> NamedInstances =>
        IsVariable ? throw new NotSupportedException("Variations: OT-4 WP5") : [];

    internal FaceData Data { get; }

    /// <summary><c>hhea.advanceWidthMax</c> as FreeType's <c>max_advance_width</c> (a signed 16-bit value).</summary>
    internal short MaxAdvanceWidth { get; }

    /// <summary>
    /// The glyph for a Unicode code point through the subtable FreeType selects; 0 when it is unmapped, mapped past the glyph
    /// count, or outside 0..0x10FFFF.
    /// </summary>
    public int GetGlyphIndex(int codePoint) => Data.Cmap.Lookup(codePoint);

    /// <summary>
    /// The glyph for a variation sequence (<c>cmap</c> format 14), as FreeType's <c>FT_Face_GetCharVariantIndex</c>; 0 when the
    /// face has no format-14 subtable or the sequence has no glyph.
    /// </summary>
    public int GetGlyphIndex(int codePoint, int variationSelector)
    {
        if ((uint)codePoint > MaxCodePoint || (uint)variationSelector > MaxCodePoint)
            return 0;

        return Data.Cmap.LookupVariant(codePoint, variationSelector);
    }

    /// <summary>The glyph's advance width in font units from <c>hmtx</c> (the default instance of a variable face).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or not below <see cref="GlyphCount"/>.</exception>
    public int GetAdvanceWidth(int glyphIndex)
    {
        CheckGlyphIndex(glyphIndex);
        return Data.Hmtx.Get(glyphIndex).Advance;
    }

    /// <summary>The glyph's SVG document from the <c>SVG </c> table with any gzip removed; empty when it has none.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or not below <see cref="GlyphCount"/>.</exception>
    public ReadOnlyMemory<byte> GetSvgDocument(int glyphIndex)
    {
        CheckGlyphIndex(glyphIndex);
        return Data.Svg?.GetDocument(glyphIndex) ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>Makes a sized font. Each <see cref="ScaledFont"/> holds hinting state: use one per thread.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The pixel size is outside 1..4096, the hinting mode or interpreter version is undefined, or a variation coordinate names
    /// an axis the face does not have.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The face is variable and coordinates are given (OT-4 WP5), or <see cref="HintingMode.Native"/> is asked of a TrueType
    /// face (OT-4 WP4).
    /// </exception>
    public ScaledFont CreateScaledFont(ScaledFontOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PixelSize is < ScaledFontOptions.MinPixelSize or > ScaledFontOptions.MaxPixelSize)
            throw new ArgumentOutOfRangeException(nameof(options), options.PixelSize,
                $"The pixel size must be {ScaledFontOptions.MinPixelSize}..{ScaledFontOptions.MaxPixelSize}.");

        if (!Enum.IsDefined(options.Hinting))
            throw new ArgumentOutOfRangeException(nameof(options), options.Hinting, "Unknown hinting mode.");

        if (!Enum.IsDefined(options.Interpreter))
            throw new ArgumentOutOfRangeException(nameof(options), options.Interpreter, "Unknown interpreter version.");

        ArgumentNullException.ThrowIfNull(options.Variation, nameof(options.Variation));
        if (options.Variation.Count > 0)
        {
            if (!IsVariable)
                throw new ArgumentOutOfRangeException(nameof(options), options.Variation[0].Tag,
                    "The face is not variable, so it has no variation axes.");

            throw new NotSupportedException("Variations: OT-4 WP5");
        }

        var scale = FontScale.FromPixelSize(options.PixelSize, UnitsPerEm);

        return new ScaledFont(this, options, scale, m_outlines.CreateLoader(options, scale));
    }

    internal void CheckGlyphIndex(int glyphIndex)
    {
        if ((uint)glyphIndex >= (uint)GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, $"The face has {GlyphCount} glyphs.");
    }

    /// <summary>FreeType's <c>sfnt_load_face</c> choice of family and style names.</summary>
    internal static (string Family, string Style) ReadNames(FaceData data)
    {
        var name = data.Name;
        string? family, style;

        if (data.Os2 is { } os2 && (os2.FsSelection & Os2Table.WeightWidthSlope) != 0)
        {
            family = name.Get(NameTable.TypographicFamily) ?? name.Get(NameTable.FontFamily);
            style = name.Get(NameTable.TypographicSubfamily) ?? name.Get(NameTable.FontSubfamily);
        }
        else
        {
            family = name.Get(NameTable.WwsFamily) ?? name.Get(NameTable.TypographicFamily) ?? name.Get(NameTable.FontFamily);
            style = name.Get(NameTable.WwsSubfamily) ?? name.Get(NameTable.TypographicSubfamily) ??
                    name.Get(NameTable.FontSubfamily);
        }

        return (family ?? "", style ?? "");
    }

    /// <summary>FreeType's <c>sfnt_load_face</c> choice of ascender, descender and height (each a signed 16-bit value).</summary>
    private static (short Ascender, short Descender, short Height) ReadLineMetrics(FaceData data)
    {
        var os2 = data.Os2;
        var hhea = data.Hhea;

        if (os2 is not null && (os2.FsSelection & Os2Table.UseTypoMetrics) != 0)
            return (os2.TypoAscender, os2.TypoDescender, (short)(os2.TypoAscender - os2.TypoDescender + os2.TypoLineGap));

        if (hhea.Ascender != 0 || hhea.Descender != 0 || os2 is null)
            return (hhea.Ascender, hhea.Descender, (short)(hhea.Ascender - hhea.Descender + hhea.LineGap));

        if (os2.TypoAscender != 0 || os2.TypoDescender != 0)
            return (os2.TypoAscender, os2.TypoDescender, (short)(os2.TypoAscender - os2.TypoDescender + os2.TypoLineGap));

        var ascender = (short)os2.WinAscent;
        var descender = (short)-(short)os2.WinDescent;
        return (ascender, descender, (short)(ascender - descender));
    }
}
