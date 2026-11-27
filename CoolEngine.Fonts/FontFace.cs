// ReadNames and ReadLineMetrics follow FreeType 2.13.2 src/sfnt/sfobjs.c (sfnt_load_face); Axes and NamedInstances are
// src/truetype/ttgxvar.c's TT_Get_MM_Var, as Variations/FvarTable.cs reads it (FreeType Project License; credit in docs).

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
        (Axes, NamedInstances) = ReadVariations(data);
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

    /// <summary>
    /// The face is variable: it has an <c>fvar</c> table whose header FreeType accepts (FreeType's
    /// <c>FT_FACE_FLAG_MULTIPLE_MASTERS</c>).
    /// </summary>
    public bool IsVariable => Data.Variations is not null;

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

    /// <summary>
    /// The variation axes from <c>fvar</c>, in its order (FreeType's <c>FT_MM_Var</c> axes: an axis whose minimum, default and
    /// maximum are out of order has its default as all three); empty for a face that is not variable. The name is chosen and
    /// converted as <see cref="FamilyName"/> is (empty when the <c>name</c> table has none).
    /// </summary>
    public IReadOnlyList<VariationAxis> Axes { get; }

    /// <summary>
    /// The named instances from <c>fvar</c>, in its order; empty for a face that is not variable. Unlike FreeType, which adds
    /// a synthesized default instance when no named instance lies at the defaults, only the table's instances are listed. The
    /// name is chosen and converted as <see cref="FamilyName"/> is (empty when the <c>name</c> table has none). For a face with
    /// CFF (not CFF2) outlines, these are listed though FreeType exposes none (it resets the count, and then fails
    /// <c>FT_Get_MM_Var</c> and <c>FT_Set_Var_Design_Coordinates</c> for such a face).
    /// </summary>
    public IReadOnlyList<NamedInstance> NamedInstances { get; }

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
    /// <remarks>
    /// With <see cref="ScaledFontOptions.Variation"/> empty the face is at its default instance without a blend, as a FreeType
    /// face on which no variation function was called. With coordinates (even the defaults) it behaves as FreeType's face after
    /// <c>FT_Set_Var_Design_Coordinates</c> with every axis given (each axis not listed at its default) and then
    /// <c>FT_Set_Pixel_Sizes</c>: see <see cref="Variations.VariationInstance"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The pixel size is outside 1..4096, the hinting mode or interpreter version is undefined, or a variation coordinate names
    /// an axis the face does not have, names one twice or has a value that is not finite.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <see cref="HintingMode.Native"/> is asked of a CFF or CFF2 face (OT-4 WP6); variation coordinates are given for a variable
    /// face with CFF (not CFF2) outlines, a library limit (FreeType varies only such a face's <c>HVAR</c> advances and
    /// <c>MVAR</c> metrics, its outlines staying the default's, and refuses the coordinates when its <c>fvar</c> has named
    /// instances); a work budget of the library is passed.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The face's <c>avar</c> maps the coordinates outside -1..1, which FreeType refuses (<c>FT_Set_Var_Design_Coordinates</c>
    /// fails).
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
        Variations.VariationInstance? variation = null;
        if (options.Variation.Count > 0)
        {
            if (Data.Variations is not { } tables)
                throw new ArgumentOutOfRangeException(nameof(options), options.Variation[0].Tag,
                    "The face is not variable, so it has no variation axes.");

            variation = Variations.VariationInstance.Create(this, tables, options.Variation);
            if (OutlineFormat == OutlineFormat.Cff)
                throw new NotSupportedException(
                    "Variation coordinates for a face with CFF (not CFF2) outlines are not supported: FreeType varies only its " +
                    "HVAR advances and MVAR metrics, and refuses the coordinates when its 'fvar' has named instances.");
        }

        var scale = FontScale.FromPixelSize(options.PixelSize, UnitsPerEm);

        return new ScaledFont(this, options, scale, m_outlines.CreateLoader(options, scale, variation), variation);
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

    /// <summary>
    /// The axes and named instances of a variable face's <c>fvar</c>, as FreeType's <c>TT_Get_MM_Var</c> reads them: the
    /// read-only lists the file's load built once for the face's (<c>fvar</c>, <c>name</c>) records, shared by every face
    /// reading the same pair.
    /// </summary>
    private static (IReadOnlyList<VariationAxis>, IReadOnlyList<NamedInstance>) ReadVariations(FaceData data) =>
        data.Variations is { } variations ? (variations.Names.Axes, variations.Names.Instances) : ([], []);

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
