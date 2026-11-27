// Follows FreeType 2.13.2 src/psaux/psft.c (cf2_getScaleAndHintFlag, cf2_checkTransform, CF2_MAX_SIZE, cf2_getUnitsPerEm),
// src/psaux/psfont.c (cf2_font_setup: the blue zones per Font DICT and transform, the Private DICT re-read at a CFF2 face's
// coordinates), src/cff/cffgload.c (cff_slot_load: the Glyph_Too_Big retry unhinted with force_scaling) and src/base/ftobjs.c
// (ft_glyphslot_grid_fit_metrics) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.Cff;

/// <summary>
/// CFF and CFF2 hinting at one size (OT-4 WP6), as FreeType 2.13.2's cff driver runs its Adobe engine with the driver's
/// defaults (<c>hinting-engine</c> adobe, <c>no-stem-darkening</c> true): the engine's transform from the size's scale, its
/// refusal of hinted scales past 2,000 pixels per em (<c>cf2_checkTransform</c>'s <c>Glyph_Too_Big</c>, after which
/// <c>cff_slot_load</c> loads the glyph again unhinted and scales it afterwards), the blue zones of each Font DICT (a CFF2
/// face's at its variation coordinates), and the grid fitting of the metrics <c>FT_Load_Glyph</c> applies to every load
/// without <c>FT_LOAD_NO_HINTING</c>. Not thread-safe (it caches blue zones); one per glyph loader.
/// </summary>
internal sealed class CffHinter
{
    /// <summary><c>CF2_MAX_SIZE</c> (<c>psft.c</c>): the largest ppem the engine hints.</summary>
    public const int MaxHintedPpem = 2000;

    private readonly CffTable m_table;
    private readonly long[]? m_coords;
    private readonly CffBlues?[] m_blues;
    private readonly CffPrivateHints?[] m_hints;

    /// <param name="table">The face's CFF or CFF2 table.</param>
    /// <param name="coords">The normalized coordinates of a CFF2 face's variation; null for the default instance.</param>
    public CffHinter(CffTable table, long[]? coords)
    {
        m_table = table;
        m_coords = coords;
        var count = Math.Max(1, table.FontDicts.Length);
        m_blues = new CffBlues?[count];
        m_hints = new CffPrivateHints?[count];
    }

    /// <summary>
    /// The engine's transform for a glyph (<c>cf2_getScaleAndHintFlag</c> hinted, then <c>cf2_checkTransform</c>): the scales
    /// (the size's, or a CID Font DICT's rescaled ones) plus 32, divided by 64, in 32 bits. False when a scale passes
    /// <see cref="MaxHintedPpem"/> pixels per em (<c>Glyph_Too_Big</c>, the unhinted retry).
    /// </summary>
    /// <param name="xScale">The glyph's horizontal scale (16.16, font units to 26.6).</param>
    /// <param name="yScale">The glyph's vertical scale.</param>
    /// <param name="unitsPerEm">The face's units per em (<c>head</c>), as <c>cf2_getUnitsPerEm</c> reads it.</param>
    /// <param name="scaleX">The engine's horizontal scale (16.16, font units to pixels).</param>
    /// <param name="scaleY">The engine's vertical scale.</param>
    /// <exception cref="InvalidDataException">
    /// A scale is not positive (<c>Invalid_Size_Handle</c>: FreeType fails the glyph), which a CID Font DICT whose matrix
    /// implies a very large em can cause.
    /// </exception>
    public static bool TryGetTransform(long xScale, long yScale, int unitsPerEm, out int scaleX, out int scaleY)
    {
        // ADD_INT32( x_scale, 32 ) / 64: a 32-bit sum, divided with C's truncation.
        scaleX = (int)((uint)xScale + 32u) / 64;
        scaleY = (int)((uint)yScale + 32u) / 64;

        if (scaleX <= 0 || scaleY <= 0)
            throw new InvalidDataException(
                "The glyph's scale for FreeType's CFF engine is not positive (FreeType fails the glyph: Invalid_Size_Handle).");

        if (unitsPerEm > 0x7FFF)
            return false;

        var maxScale = (int)CffFixed.DivFix(MaxHintedPpem << 16, unitsPerEm << 16);
        return scaleX <= maxScale && scaleY <= maxScale;
    }

    /// <summary>
    /// The blue zones of a Font DICT at a vertical scale (<c>cf2_blues_init</c> in <c>cf2_font_setup</c>, which rebuilds them
    /// when the Font DICT or the transform changes), cached per Font DICT.
    /// </summary>
    /// <remarks>
    /// A CFF2 face's Private DICT at its coordinates is re-read once per Font DICT, on the first glyph that needs it; that
    /// glyph is charged <see cref="CffTable.PrivateReparseCost"/> to its work budget before the re-read.
    /// </remarks>
    /// <param name="fontDictIndex">The glyph's Font DICT (0 without an FDArray).</param>
    /// <param name="scaleY">The engine's vertical scale.</param>
    /// <param name="host">The glyph's interpreter: its work budget.</param>
    public CffBlues Blues(int fontDictIndex, int scaleY, ICffHintHost host)
    {
        if (m_blues[fontDictIndex] is { } blues && blues.Scale == scaleY)
            return blues;

        var hints = m_hints[fontDictIndex];
        if (hints is null)
        {
            if (m_table.IsCff2 && m_coords is not null)
            {
                host.Charge(m_table.PrivateReparseCost(fontDictIndex));
                hints = m_table.PrivateHintsAt(fontDictIndex, m_coords);
            }
            else
            {
                hints = (m_table.FontDicts.Length == 0 ? m_table.Top : m_table.FontDicts[fontDictIndex]).Hints;
            }

            m_hints[fontDictIndex] = hints;
        }

        return m_blues[fontDictIndex] = new CffBlues(hints, scaleY);
    }

    /// <summary>
    /// <c>ft_glyphslot_grid_fit_metrics</c> (horizontal): the bearings floored and ceiled to whole pixels with the extent kept
    /// covering the box, the advance rounded.
    /// </summary>
    public static GlyphMetrics GridFit(long width, long height, long bearingX, long bearingY, long advance)
    {
        var right = FixedMath.PixCeil(bearingX + width);
        var bottom = FixedMath.PixFloor(bearingY - height);
        var left = FixedMath.PixFloor(bearingX);
        var top = FixedMath.PixCeil(bearingY);

        return new GlyphMetrics((int)(right - left), (int)(top - bottom), (int)left, (int)top, (int)FixedMath.PixRound(advance));
    }
}
