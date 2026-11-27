// Follows FreeType 2.13.2 src/cff/cffobjs.c (cff_face_init: the font matrix normalisation and the concatenation of a
// Font DICT's matrix with the Top DICT's) and src/cff/cffgload.c (cff_slot_load: the Font DICT's matrix and scale, the
// matrix, offset and scale applied to the outline and the advance, the metrics from the control box) and src/sfnt/ttmtx.c
// (tt_face_get_metrics: the HVAR step of the advance, through src/truetype/ttgxvar.c's tt_hvadvance_adjust) (FreeType
// Project License; credit in docs).

using CoolEngine.Fonts.Hinting.Cff;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// Outlines from a <c>CFF </c> or <c>CFF2</c> table (checked at load by <see cref="CffTable"/>), unhinted, as FreeType
/// 2.13.2's CFF driver loads them with <c>FT_LOAD_NO_HINTING</c>: the charstring run by <see cref="Type2Interpreter"/> in
/// whole font units, then the font matrix and its offset (FreeType's, normalised per face), then the size's scale through
/// <c>FT_MulFix</c>; the advance from <c>hmtx</c> through the same matrix, offset and scale; the other metrics from the
/// outline's control box. CFF2 glyphs blend at the size's variation coordinates (the default instance without them), and their
/// advance takes <c>HVAR</c>'s delta, as FreeType's <c>cff_slot_load</c> takes it through <c>tt_face_get_metrics</c>. A glyph whose charstrings pass
/// <see cref="Type2Interpreter.GlyphWorkBudget"/> is <see cref="NotSupportedException"/> (a library limit; FreeType runs on).
/// Immutable; safe on any thread.
/// </summary>
internal sealed class CffOutlineSource : IOutlineSource
{
    private readonly FaceData m_face;
    private readonly CffTable m_table;
    private readonly FontMatrix m_top;
    private readonly FontMatrix[] m_fontDicts;

    /// <param name="face">The face's tables, its CFF or CFF2 table among them (checked at load, <see cref="FaceData.Cff"/>).</param>
    /// <param name="isCff2">The table is <c>CFF2</c> rather than <c>CFF </c>.</param>
    public CffOutlineSource(FaceData face, bool isCff2)
    {
        m_face = face;
        m_table = face.Cff ?? throw new InvalidOperationException("The face has no CFF table.");
        if (m_table.IsCff2 != isCff2)
            throw new InvalidOperationException("The face's CFF table is not of the kind its outline format names.");

        // cff_face_init: without a FontMatrix, the Top DICT's units per em are the head table's (an sfnt-wrapped CFF).
        var top = m_table.Top;
        var topUpm = top.HasFontMatrix ? top.UnitsPerEm : face.Head.UnitsPerEm;
        m_top = Normalize(top.Matrix, top.OffsetX, top.OffsetY, topUpm);

        var fontDicts = m_table.FontDicts;
        m_fontDicts = new FontMatrix[fontDicts.Length];
        for (var i = fontDicts.Length - 1; i >= 0; i--)
        {
            var sub = fontDicts[i];
            CffMatrix matrix;
            long offsetX, offsetY, upm;
            if (sub.HasFontMatrix)
            {
                (matrix, offsetX, offsetY, upm) = (sub.Matrix, sub.OffsetX, sub.OffsetY, sub.UnitsPerEm);

                // A Font DICT's matrix is concatenated with the Top DICT's (already normalised) one; the units per em are
                // FT_ULong values in FreeType, compared unsigned.
                if (top.HasFontMatrix)
                {
                    var scaling = (ulong)m_top.UnitsPerEm > 1 && (ulong)upm > 1 ? (long)Math.Min((ulong)m_top.UnitsPerEm, (ulong)upm) : 1;
                    matrix = CffFixed.MultiplyScaled(m_top.Matrix, matrix, scaling);
                    (offsetX, offsetY) = CffFixed.TransformScaled(offsetX, offsetY, m_top.Matrix, scaling);
                    upm = CffFixed.MulDiv(upm, m_top.UnitsPerEm, scaling);
                }
            }
            else
            {
                // FreeType copies the Top DICT's normalised matrix and offset (the offset already in font units, which the
                // normalisation below shifts once more, as FreeType does).
                (matrix, offsetX, offsetY, upm) = (m_top.Matrix, m_top.OffsetX, m_top.OffsetY, m_top.UnitsPerEm);
            }

            m_fontDicts[i] = Normalize(matrix, offsetX, offsetY, upm);
        }
    }

    public IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale, Variations.VariationInstance? variation)
    {
        if (options.Hinting == HintingMode.Native)
            CffHinterFactory.Create(m_face, scale);

        return new CffGlyphLoader(this, scale, variation);
    }

    /// <summary>
    /// <c>cff_face_init</c>'s normalisation, in FreeType's 64-bit <c>FT_Fixed</c>: the matrix divided by |yy| (|yx| when yy is
    /// 0) so yy is 1, the units per em and the 16.16 offset likewise, then the offset in whole font units.
    /// </summary>
    private static FontMatrix Normalize(CffMatrix matrix, long offsetX, long offsetY, long upm)
    {
        var temp = matrix.Yy != 0 ? CffFixed.Abs(matrix.Yy) : CffFixed.Abs(matrix.Yx);
        if (temp != CffFixed.One)
        {
            upm = CffFixed.DivFix(upm, temp);
            matrix = new CffMatrix(CffFixed.DivFix(matrix.Xx, temp), CffFixed.DivFix(matrix.Xy, temp),
                CffFixed.DivFix(matrix.Yx, temp), CffFixed.DivFix(matrix.Yy, temp));
            offsetX = CffFixed.DivFix(offsetX, temp);
            offsetY = CffFixed.DivFix(offsetY, temp);
        }

        return new FontMatrix(matrix, offsetX >> 16, offsetY >> 16, upm);
    }

    /// <summary>A font matrix as FreeType applies it: normalised, its offset in whole font units.</summary>
    private readonly record struct FontMatrix(CffMatrix Matrix, long OffsetX, long OffsetY, long UnitsPerEm);

    /// <summary>Loads glyphs at one size (FreeType's <c>cff_slot_load</c>, unhinted); reuses its buffers, so not thread-safe.</summary>
    private sealed class CffGlyphLoader : IGlyphLoader
    {
        private readonly CffOutlineSource m_source;
        private readonly FontScale m_scale;
        private readonly CffOutlineBuilder m_builder = new();
        private readonly Type2Interpreter m_interpreter;
        private readonly Variations.VariationInstance? m_variation;

        public CffGlyphLoader(CffOutlineSource source, FontScale scale, Variations.VariationInstance? variation)
        {
            m_source = source;
            m_scale = scale;
            m_variation = variation;
            m_interpreter = new Type2Interpreter(source.m_table, m_builder, variation?.Normalized);
        }

        /// <summary>
        /// The glyph in FreeType's 64-bit <c>FT_Pos</c> arithmetic; the outline's points and the metrics are then given in the
        /// 32 bits of <see cref="OutlinePoint"/> and <see cref="GlyphMetrics"/> (their low 32 bits, should a crafted matrix take
        /// them past that range).
        /// </summary>
        public LoadedGlyph Load(int glyphIndex)
        {
            var table = m_source.m_table;
            var xScale = m_scale.XScale;
            var yScale = m_scale.YScale;

            // The Font DICT's matrix; its own units per em rescale the size (cff_slot_load).
            var matrix = m_source.m_top;
            if (table.FontDicts.Length > 0)
            {
                var sub = m_source.m_fontDicts[table.FontDictIndexOf(glyphIndex)];
                if (m_source.m_top.UnitsPerEm != sub.UnitsPerEm)
                {
                    xScale = CffFixed.MulDiv(xScale, m_source.m_top.UnitsPerEm, sub.UnitsPerEm);
                    yScale = CffFixed.MulDiv(yScale, m_source.m_top.UnitsPerEm, sub.UnitsPerEm);
                }

                matrix = sub;
            }

            m_interpreter.Run(glyphIndex);

            var b = m_builder;
            var count = b.PointCount;
            var xs = b.X;
            var ys = b.Y;
            var tags = b.Tags;
            var points = new OutlinePoint[count];
            var transform = !matrix.Matrix.IsIdentity;
            var translate = matrix.OffsetX != 0 || matrix.OffsetY != 0;
            var m = matrix.Matrix;
            long xMin = 0, yMin = 0, xMax = 0, yMax = 0;
            for (var i = 0; i < count; i++)
            {
                long x = xs[i], y = ys[i];
                if (transform)
                    (x, y) = (CffFixed.MulFix(x, m.Xx) + CffFixed.MulFix(y, m.Xy), CffFixed.MulFix(x, m.Yx) + CffFixed.MulFix(y, m.Yy));

                if (translate)
                {
                    x += matrix.OffsetX;
                    y += matrix.OffsetY;
                }

                x = CffFixed.MulFix(x, xScale);
                y = CffFixed.MulFix(y, yScale);
                points[i] = new OutlinePoint((int)x, (int)y,
                    tags[i] == CffOutlineBuilder.OnCurve ? OutlinePointKind.OnCurve : OutlinePointKind.Cubic);

                if (i == 0)
                {
                    xMin = xMax = x;
                    yMin = yMax = y;
                    continue;
                }

                xMin = Math.Min(xMin, x);
                xMax = Math.Max(xMax, x);
                yMin = Math.Min(yMin, y);
                yMax = Math.Max(yMax, y);
            }

            // The advance: hmtx's (adjusted by HVAR with variation coordinates, in 16 bits as tt_face_get_metrics keeps it),
            // through the matrix (xx), the offset and the scale.
            long advance = m_source.m_face.Hmtx.Get(glyphIndex).Advance;
            if (m_variation?.Tables.Hvar is { } hvar)
                advance = (ushort)(advance + hvar.AdvanceDelta(glyphIndex, m_variation.Normalized, new GlyphWork(m_interpreter)));
            if (transform)
                advance = CffFixed.MulFix(advance, m.Xx);

            advance += matrix.OffsetX;
            advance = CffFixed.MulFix(advance, xScale);

            var outline = count == 0 ? GlyphOutline.Empty : new GlyphOutline(points, b.Contours.ToArray());
            var metrics = new GlyphMetrics((int)(xMax - xMin), (int)(yMax - yMin), (int)xMin, (int)yMax, (int)advance);
            return new LoadedGlyph(outline, metrics, false);
        }

        /// <summary>Charges the advance's <c>HVAR</c> work to the glyph's charstring budget.</summary>
        private sealed class GlyphWork(Type2Interpreter interpreter) : Variations.IWorkCounter
        {
            public void Charge(long units) => interpreter.ChargeWork(units);
        }
    }
}
