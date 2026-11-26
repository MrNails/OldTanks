// Follows FreeType 2.13.2 src/truetype/ttgload.c (load_truetype_glyph, TT_Process_Simple_Glyph,
// TT_Process_Composite_Component, tt_loader_set_pp, compute_glyph_metrics) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Hinting.TrueType;

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// TrueType outlines from <c>glyf</c>, scaled to 26.6 exactly as FreeType 2.13.2's <c>ttgload.c</c> scales them: each point and
/// each phantom point through <c>FT_MulFix</c>, composite components transformed with <c>FT_MulFix</c> on 16.16 matrices and
/// offset in pixels, the outline shifted so the first phantom point (x minimum minus the left side bearing) is the origin.
/// </summary>
internal sealed class GlyfOutlineSource : IOutlineSource
{
    private readonly FaceData m_face;

    public GlyfOutlineSource(FaceData face) => m_face = face;

    public IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale)
    {
        var hinter = options.Hinting == HintingMode.Native
            ? TrueTypeHinterFactory.Create(m_face, scale, options.Interpreter)
            : null;

        return new GlyfGlyphLoader(m_face, scale, hinter);
    }
}

/// <summary>Loads glyphs of one face at one size; reuses its buffers, so not thread-safe.</summary>
internal sealed class GlyfGlyphLoader : IGlyphLoader
{
    private const int PhantomCount = 4;

    private readonly FaceData m_face;
    private readonly GlyfTable m_glyf;
    private readonly FontScale m_scale;
    private readonly ITrueTypeHinter? m_hinter;
    private readonly SimpleGlyph m_simple = new();

    // The outline being built (FreeType's gloader base): 26.6 points, on-curve tags and contour ends.
    private long[] m_x = new long[64];
    private long[] m_y = new long[64];
    private byte[] m_tags = new byte[64];
    private int[] m_contours = new int[16];
    private int m_pointCount;
    private int m_contourCount;

    // The phantom points of the glyph being loaded: pp1, pp2 (horizontal) and pp3, pp4 (vertical).
    private Phantoms m_phantoms;
    private bool m_overlap;

    public GlyfGlyphLoader(FaceData face, FontScale scale, ITrueTypeHinter? hinter)
    {
        m_face = face;
        m_glyf = face.Glyf!;
        m_scale = scale;
        m_hinter = hinter;
    }

    public LoadedGlyph Load(int glyphIndex)
    {
        m_pointCount = 0;
        m_contourCount = 0;
        m_overlap = false;

        LoadGlyph(glyphIndex, isTopLevel: true);

        // Translate so that pp1 is the origin (FreeType's TT_Load_Glyph), whatever head.flags bit 1 says.
        var originX = m_phantoms.X1;
        var points = new OutlinePoint[m_pointCount];
        long xMin = 0, yMin = 0, xMax = 0, yMax = 0;
        for (var i = 0; i < m_pointCount; i++)
        {
            var x = m_x[i] - originX;
            var y = m_y[i];
            points[i] = new OutlinePoint((int)x, (int)y,
                (m_tags[i] & GlyfTable.OnCurvePoint) != 0 ? OutlinePointKind.OnCurve : OutlinePointKind.Quadratic);

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

        var outline = m_pointCount == 0 ? GlyphOutline.Empty : new GlyphOutline(points, m_contours[..m_contourCount]);

        // FreeType's compute_glyph_metrics: the control box of the final outline, the advance from the phantom points.
        var metrics = new GlyphMetrics((int)(xMax - xMin), (int)(yMax - yMin), (int)xMin, (int)yMax,
            (int)(m_phantoms.X2 - m_phantoms.X1));

        if (m_hinter is not null)
            metrics = m_hinter.FitMetrics(glyphIndex, metrics);

        return new LoadedGlyph(outline, metrics, m_overlap);
    }

    /// <summary>FreeType's <c>load_truetype_glyph</c>: appends the glyph's points and leaves its phantom points in <see cref="m_phantoms"/>.</summary>
    private void LoadGlyph(int glyphIndex, bool isTopLevel)
    {
        var data = m_glyf.GetGlyph(glyphIndex);
        var r = new BigEndianReader(data, "glyf");
        var contourCount = data.Length == 0 ? 0 : r.I16(0);
        var empty = contourCount == 0;

        int xMin = 0, yMax = 0;
        if (!empty)
        {
            xMin = r.I16(2);
            yMax = r.I16(8);
        }

        SetPhantoms(glyphIndex, xMin, yMax);

        if (empty)
        {
            m_phantoms = ScalePhantoms(m_phantoms);
            return;
        }

        if (contourCount > 0)
            LoadSimple(data, glyphIndex);
        else
            LoadComposite(data, glyphIndex, isTopLevel);
    }

    /// <summary>FreeType's <c>tt_loader_set_pp</c>, in font units, with the vertical metrics emulated from <c>OS/2</c> or <c>hhea</c>.</summary>
    private void SetPhantoms(int glyphIndex, int xMin, int yMax)
    {
        var (advance, bearing) = m_face.Hmtx.Get(glyphIndex);

        // TT_Get_VMetrics without a vmtx table: the typographic (or hhea) ascender and descender.
        short ascender, descender;
        if (m_face.Os2 is { } os2)
            (ascender, descender) = (os2.TypoAscender, os2.TypoDescender);
        else
            (ascender, descender) = (m_face.Hhea.Ascender, m_face.Hhea.Descender);

        var topBearing = (short)(ascender - yMax);
        var advanceHeight = (ushort)Math.Abs(ascender - descender);

        long x1 = xMin - bearing;
        long x3 = 0;

        // FreeType's v40 grayscale hinting puts the vertical phantom points at half the advance.
        if (m_hinter is { Version: TrueTypeInterpreterVersion.V40 })
            x3 = advance / 2;

        long y3 = yMax + topBearing;
        m_phantoms = new Phantoms(x1, x1 + advance, x3, y3, x3, y3 - advanceHeight);
    }

    private Phantoms ScalePhantoms(Phantoms p) => new(
        FixedMath.MulFix(p.X1, m_scale.XScale),
        FixedMath.MulFix(p.X2, m_scale.XScale),
        FixedMath.MulFix(p.X3, m_scale.XScale),
        FixedMath.MulFix(p.Y3, m_scale.YScale),
        FixedMath.MulFix(p.X4, m_scale.XScale),
        FixedMath.MulFix(p.Y4, m_scale.YScale));

    /// <summary>FreeType's <c>TT_Load_Simple_Glyph</c> and <c>TT_Process_Simple_Glyph</c>.</summary>
    private void LoadSimple(ReadOnlySpan<byte> data, int glyphIndex)
    {
        var glyph = m_simple;
        GlyfTable.ReadSimple(data, glyphIndex, glyph);
        var n = glyph.PointCount;
        var total = n + PhantomCount;

        // Scale every point and the phantom points (pp1.y and pp2.y are 0).
        var x = new long[total];
        var y = new long[total];
        long[]? unscaledX = m_hinter is null ? null : new long[total];
        long[]? unscaledY = m_hinter is null ? null : new long[total];
        for (var i = 0; i < n; i++)
        {
            x[i] = glyph.X[i];
            y[i] = glyph.Y[i];
        }

        var p = m_phantoms;
        (x[n], y[n]) = (p.X1, 0);
        (x[n + 1], y[n + 1]) = (p.X2, 0);
        (x[n + 2], y[n + 2]) = (p.X3, p.Y3);
        (x[n + 3], y[n + 3]) = (p.X4, p.Y4);

        if (unscaledX is not null)
        {
            Array.Copy(x, unscaledX, total);
            Array.Copy(y, unscaledY!, total);
        }

        for (var i = 0; i < total; i++)
        {
            x[i] = FixedMath.MulFix(x[i], m_scale.XScale);
            y[i] = FixedMath.MulFix(y[i], m_scale.YScale);
        }

        var tags = new byte[total];
        for (var i = 0; i < n; i++)
            tags[i] = (byte)(glyph.Flags[i] & GlyfTable.OnCurvePoint);

        var scaled = new Phantoms(x[n], x[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
        m_phantoms = scaled;

        if (m_hinter is not null)
        {
            var zone = new GlyphZone(x, y, unscaledX, unscaledY, tags, glyph.ContourEnds[..glyph.ContourCount], total,
                glyph.ContourCount);
            var instructions = data.Slice(glyph.InstructionOffset, glyph.InstructionLength);
            if (m_hinter.HintGlyph(zone, instructions, isComposite: false))
                m_phantoms = new Phantoms(x[n], x[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
        }

        if ((glyph.Flags[0] & GlyfTable.OverlapSimple) != 0)
            m_overlap = true;

        // Append to the outline (FT_GlyphLoader_Add).
        EnsurePoints(m_pointCount + n);
        EnsureContours(m_contourCount + glyph.ContourCount);
        Array.Copy(x, 0, m_x, m_pointCount, n);
        Array.Copy(y, 0, m_y, m_pointCount, n);
        Array.Copy(tags, 0, m_tags, m_pointCount, n);
        for (var c = 0; c < glyph.ContourCount; c++)
            m_contours[m_contourCount + c] = glyph.ContourEnds[c] + m_pointCount;

        m_pointCount += n;
        m_contourCount += glyph.ContourCount;
    }

    /// <summary>The composite branch of FreeType's <c>load_truetype_glyph</c>.</summary>
    private void LoadComposite(ReadOnlySpan<byte> data, int glyphIndex, bool isTopLevel)
    {
        var components = new List<GlyphComponent>();
        var instructions = GlyfTable.ReadComposite(data, glyphIndex, m_face.GlyphCount, components);
        var instructionBytes = data.Slice(instructions.Offset, instructions.Length).ToArray();

        // FT_OUTLINE_OVERLAP comes from the first component of the glyph asked for.
        if (isTopLevel && (components[0].Flags & GlyfTable.OverlapCompound) != 0)
            m_overlap = true;

        m_phantoms = ScalePhantoms(m_phantoms);

        var startPoint = m_pointCount;
        var startContour = m_contourCount;

        foreach (var component in components)
        {
            var saved = m_phantoms;
            var basePoints = m_pointCount;

            LoadGlyph(component.GlyphIndex, isTopLevel: false);

            if ((component.Flags & GlyfTable.UseMyMetrics) == 0)
                m_phantoms = saved;

            if (m_pointCount == basePoints)
                continue;

            PlaceComponent(component, startPoint, basePoints);
        }

        // FreeType hints a composite only when its last component announces instructions and there are some.
        var last = components[^1];
        if (m_hinter is not null && (last.Flags & GlyfTable.WeHaveInstructions) != 0 && instructionBytes.Length > 0 &&
            m_pointCount > startPoint)
            HintComposite(startPoint, startContour, instructionBytes);
    }

    /// <summary>FreeType's <c>TT_Process_Composite_Component</c>: transform, then offset, the points of the component just loaded.</summary>
    private void PlaceComponent(GlyphComponent component, int startPoint, int basePoints)
    {
        var flags = component.Flags;
        var haveScale = (flags & (GlyfTable.WeHaveAScale | GlyfTable.WeHaveAnXyScale | GlyfTable.WeHaveATwoByTwo)) != 0;

        if (haveScale)
        {
            for (var i = basePoints; i < m_pointCount; i++)
            {
                var px = m_x[i];
                var py = m_y[i];
                m_x[i] = FixedMath.MulFix(px, component.Xx) + FixedMath.MulFix(py, component.Xy);
                m_y[i] = FixedMath.MulFix(px, component.Yx) + FixedMath.MulFix(py, component.Yy);
            }
        }

        long dx, dy;
        if ((flags & GlyfTable.ArgsAreXyValues) == 0)
        {
            // Anchor points, both checked at load: point Argument1 of the composite so far meets point Argument2 of this one.
            var k = startPoint + component.Argument1;
            var l = basePoints + component.Argument2;
            dx = m_x[k] - m_x[l];
            dy = m_y[k] - m_y[l];
        }
        else
        {
            dx = component.Argument1;
            dy = component.Argument2;
            if (dx == 0 && dy == 0)
                return;

            // Without TT_CONFIG_OPTION_COMPONENT_OFFSET_SCALED (FreeType's default), only SCALED_COMPONENT_OFFSET scales.
            if (haveScale && (flags & GlyfTable.ScaledComponentOffset) != 0)
            {
                dx = FixedMath.MulFix(dx, FixedMath.Hypot(component.Xx, component.Xy));
                dy = FixedMath.MulFix(dy, FixedMath.Hypot(component.Yy, component.Yx));
            }

            dx = FixedMath.MulFix(dx, m_scale.XScale);
            dy = FixedMath.MulFix(dy, m_scale.YScale);

            if ((flags & GlyfTable.RoundXyToGrid) != 0 && m_hinter is not null)
            {
                // The x offset is rounded only in v35, where advance widths are hinted too.
                if (m_hinter.Version == TrueTypeInterpreterVersion.V35)
                    dx = FixedMath.PixRound(dx);

                dy = FixedMath.PixRound(dy);
            }
        }

        if (dx == 0 && dy == 0)
            return;

        for (var i = basePoints; i < m_pointCount; i++)
        {
            m_x[i] += dx;
            m_y[i] += dy;
        }
    }

    /// <summary>FreeType's <c>TT_Process_Composite_Glyph</c>: the composite's own program over its placed components.</summary>
    private void HintComposite(int startPoint, int startContour, byte[] instructions)
    {
        var n = m_pointCount - startPoint;
        var total = n + PhantomCount;
        var x = new long[total];
        var y = new long[total];
        var tags = new byte[total];
        Array.Copy(m_x, startPoint, x, 0, n);
        Array.Copy(m_y, startPoint, y, 0, n);
        Array.Copy(m_tags, startPoint, tags, 0, n);

        var p = m_phantoms;
        (x[n], y[n]) = (p.X1, 0);
        (x[n + 1], y[n + 1]) = (p.X2, 0);
        (x[n + 2], y[n + 2]) = (p.X3, p.Y3);
        (x[n + 3], y[n + 3]) = (p.X4, p.Y4);

        var contourCount = m_contourCount - startContour;
        var contours = new int[contourCount];
        for (var c = 0; c < contourCount; c++)
            contours[c] = m_contours[startContour + c] - startPoint;

        var zone = new GlyphZone(x, y, null, null, tags, contours, total, contourCount);
        var takePhantoms = m_hinter!.HintGlyph(zone, instructions, isComposite: true);

        Array.Copy(x, 0, m_x, startPoint, n);
        Array.Copy(y, 0, m_y, startPoint, n);
        if (takePhantoms)
            m_phantoms = new Phantoms(x[n], x[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
    }

    private void EnsurePoints(int count)
    {
        if (count <= m_x.Length)
            return;

        var size = Math.Max(count, m_x.Length * 2);
        Array.Resize(ref m_x, size);
        Array.Resize(ref m_y, size);
        Array.Resize(ref m_tags, size);
    }

    private void EnsureContours(int count)
    {
        if (count > m_contours.Length)
            Array.Resize(ref m_contours, Math.Max(count, m_contours.Length * 2));
    }

    /// <summary>pp1 = (X1, 0), pp2 = (X2, 0), pp3 = (X3, Y3), pp4 = (X4, Y4).</summary>
    private readonly record struct Phantoms(long X1, long X2, long X3, long Y3, long X4, long Y4);
}
