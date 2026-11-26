// Follows FreeType 2.13.2 src/smooth/ftgrays.c and the contour walk of src/base/ftoutln.c (FT_Outline_Decompose) (FreeType
// Project License; credit in docs).

namespace CoolEngine.Fonts.Rasterization;

/// <summary>
/// An anti-aliasing scan converter whose output equals FreeType 2.13.2's <c>smooth</c> rasteriser (<c>ftgrays.c</c>) byte for
/// byte: coordinates in 1/256 pixel, each pixel cell accumulating the signed cover and area its edges leave, quadratic arcs
/// flattened by the same forward differences and cubic arcs by the same bisections, non-zero winding.
/// </summary>
/// <remarks>
/// FreeType keeps the cells of a band in sorted lists and renders tall glyphs in bands; a cell's totals do not depend on the
/// banding, so this keeps one dense grid instead. Cells left of the bitmap fold into column -1 (they only carry cover), cells
/// right of it, above it or below it are dropped, as FreeType's clipping does. Reused between glyphs: not thread-safe.
/// <para>
/// Work is counted as it happens, in units: each grid cell a band clears and sweeps, each line segment, each cell a segment
/// steps into, each cubic bisection. Passing the budget given to <see cref="Begin"/> throws <see cref="NotSupportedException"/>
/// at once, so a glyph whose edges are long and many (a zigzag across every band) cannot run for minutes.
/// </para>
/// </remarks>
internal sealed class CoverageRasterizer
{
    private const int PixelBits = 8;
    private const int OnePixel = 1 << PixelBits;

    // FreeType's flattening thresholds and its cubic bisection stack (16 levels of 3 points, plus 1).
    private const int CubicStackLength = 16 * 3 + 1;

    private readonly long[] m_arcX = new long[CubicStackLength];
    private readonly long[] m_arcY = new long[CubicStackLength];

    private int m_width;
    private int m_height;
    private int m_stride;
    private int m_bandStart;
    private int m_bandEnd;
    private int[] m_cover = [];
    private int[] m_area = [];

    // The current cell's index in the grid, or -1 for FreeType's "null cell" outside the clip box.
    private int m_cell = -1;

    // The pen position in 1/256 pixel.
    private long m_x;
    private long m_y;

    private long m_work;
    private long m_budget;
    private bool m_countOnly;

    /// <summary>The work units counted since <see cref="Begin"/>.</summary>
    public long Work => m_work;

    /// <summary>
    /// The rows of one band such that it holds at most this many cells; like FreeType's render pool, it bounds the memory a
    /// tall glyph needs. The cells, and so the output, do not depend on the banding.
    /// </summary>
    public static int BandHeight(int width) => Math.Max(1, MaxBandCells / (width + 1));

    private const int MaxBandCells = 1 << 20;

    /// <summary>Starts a glyph: clears the work count and sets its budget.</summary>
    /// <param name="budget">The most work units allowed.</param>
    /// <param name="countOnly">Only count the work: no grid is allocated, cleared or filled.</param>
    public void Begin(long budget, bool countOnly)
    {
        m_work = 0;
        m_budget = budget;
        m_countOnly = countOnly;
    }

    /// <summary>
    /// Clears the grid for rows <paramref name="bandStart"/> (inclusive) to <paramref name="bandEnd"/> (exclusive) of a bitmap of
    /// this size, in the unit the outline will use (pixels, or subpixels); row 0 is the bottom.
    /// </summary>
    public void Reset(int width, int height, int bandStart, int bandEnd)
    {
        m_width = width;
        m_height = height;
        m_stride = width + 1;
        m_bandStart = bandStart;
        m_bandEnd = bandEnd;
        m_cell = -1;
        m_x = 0;
        m_y = 0;

        var cells = m_stride * (bandEnd - bandStart);
        AddWork(cells);
        if (m_countOnly)
            return;

        if (m_cover.Length < cells)
        {
            m_cover = new int[cells];
            m_area = new int[cells];
        }
        else
        {
            Array.Clear(m_cover, 0, cells);
            Array.Clear(m_area, 0, cells);
        }
    }

    /// <summary>
    /// Scan-converts the outline's contours as FreeType's <c>FT_Outline_Decompose</c> walks them (a contour may start on a
    /// control point; two quadratic control points imply the on-curve point midway). Coordinates are 26.6 in the grid's space.
    /// </summary>
    /// <exception cref="InvalidDataException">A contour starts on a cubic control point or has a lone cubic control point.</exception>
    public void AddOutline(ReadOnlySpan<long> xs, ReadOnlySpan<long> ys, ReadOnlySpan<OutlinePointKind> kinds,
        ReadOnlySpan<int> contourEnds)
    {
        var last = -1;
        foreach (var contourEnd in contourEnds)
        {
            var first = last + 1;
            last = contourEnd;
            if (last < first)
                throw new InvalidDataException("An outline contour ends before it starts.");

            var limit = last;
            long startX = xs[first], startY = ys[first];
            long lastX = xs[last], lastY = ys[last];
            var point = first;

            if (kinds[first] == OutlinePointKind.Cubic)
                throw new InvalidDataException("An outline contour starts on a cubic control point.");

            if (kinds[first] == OutlinePointKind.Quadratic)
            {
                if (kinds[last] == OutlinePointKind.OnCurve)
                {
                    // Start at the last point if it is on the curve.
                    startX = lastX;
                    startY = lastY;
                    limit--;
                }
                else
                {
                    // Both ends are control points: start midway between them.
                    startX = (startX + lastX) / 2;
                    startY = (startY + lastY) / 2;
                }

                point--;
            }

            MoveTo(startX, startY);

            var closed = false;
            while (point < limit)
            {
                point++;
                switch (kinds[point])
                {
                    case OutlinePointKind.OnCurve:
                        LineTo(xs[point], ys[point]);
                        continue;

                    case OutlinePointKind.Quadratic:
                    {
                        long controlX = xs[point], controlY = ys[point];
                        while (true)
                        {
                            if (point >= limit)
                            {
                                ConicTo(controlX, controlY, startX, startY);
                                closed = true;
                                break;
                            }

                            point++;
                            long x = xs[point], y = ys[point];
                            if (kinds[point] == OutlinePointKind.OnCurve)
                            {
                                ConicTo(controlX, controlY, x, y);
                                break;
                            }

                            if (kinds[point] != OutlinePointKind.Quadratic)
                                throw new InvalidDataException("An outline has a cubic control point after a quadratic one.");

                            ConicTo(controlX, controlY, (controlX + x) / 2, (controlY + y) / 2);
                            controlX = x;
                            controlY = y;
                        }

                        break;
                    }

                    default:
                    {
                        if (point + 1 > limit || kinds[point + 1] != OutlinePointKind.Cubic)
                            throw new InvalidDataException("An outline has a cubic control point without its pair.");

                        point += 2;
                        if (point <= limit)
                        {
                            CubicTo(xs[point - 2], ys[point - 2], xs[point - 1], ys[point - 1], xs[point], ys[point]);
                            continue;
                        }

                        CubicTo(xs[point - 2], ys[point - 2], xs[point - 1], ys[point - 1], startX, startY);
                        closed = true;
                        break;
                    }
                }

                if (closed)
                    break;
            }

            if (!closed)
                LineTo(startX, startY);
        }
    }

    /// <summary>
    /// FreeType's <c>gray_sweep</c>: turns the band's cells into coverage bytes, row 0 of <paramref name="pixels"/> at the top
    /// of the grid.
    /// </summary>
    public void Sweep(byte[] pixels)
    {
        for (var y = m_bandStart; y < m_bandEnd; y++)
        {
            var row = (m_height - 1 - y) * m_width;
            var cells = (y - m_bandStart) * m_stride;
            var cover = m_cover[cells] * (OnePixel * 2);
            for (var x = 0; x < m_width; x++)
            {
                var cell = cells + x + 1;
                cover = unchecked(cover + m_cover[cell] * (OnePixel * 2));
                pixels[row + x] = Coverage(unchecked(cover - m_area[cell]));
            }
        }
    }

    /// <summary>
    /// FreeType's <c>ft_smooth_overlap_spans</c> over a grid four times finer than <paramref name="pixels"/>: each pixel adds
    /// the rounded sixteenth of its 16 subpixels' coverage, a full pixel clamped to 255.
    /// </summary>
    public void SweepOversampled(byte[] pixels, int width, int height, int factor)
    {
        var square = factor * factor;
        for (var y = m_bandStart; y < m_bandEnd; y++)
        {
            var row = (height - 1 - y / factor) * width;
            var cells = (y - m_bandStart) * m_stride;
            var cover = m_cover[cells] * (OnePixel * 2);
            for (var x = 0; x < m_width; x++)
            {
                var cell = cells + x + 1;
                cover = unchecked(cover + m_cover[cell] * (OnePixel * 2));
                var subpixel = (uint)((Coverage(unchecked(cover - m_area[cell])) + square / 2) / square);
                var sum = pixels[row + x / factor] + subpixel;
                pixels[row + x / factor] = (byte)(sum - (sum >> 8));
            }
        }
    }

    /// <summary>FreeType's <c>FT_FILL_RULE</c> for the non-zero rule: area to 0..255.</summary>
    private static byte Coverage(int area)
    {
        var coverage = area >> (PixelBits * 2 + 1 - 8);
        if (coverage < 0)
            coverage = ~coverage;

        return coverage > 255 ? (byte)255 : (byte)coverage;
    }

    private static int Trunc(long x) => (int)(x >> PixelBits);

    private static int Fract(long x) => (int)(x & (OnePixel - 1));

    private static long Upscale(long x) => x * (OnePixel >> 6);

    private static long LeftShift(long a, int b) => (long)((ulong)a << b);

    /// <summary><c>FT_UDIV</c>: a / b through the precomputed reciprocal <c>0xFFFFFFFF / b</c>.</summary>
    private static int UDiv(long a, long reciprocal) => (int)(((ulong)a * (ulong)reciprocal) >> 32);

    /// <summary><c>gray_set_cell</c>.</summary>
    private void SetCell(int ex, int ey)
    {
        AddWork(1);
        if (ey < m_bandStart || ey >= m_bandEnd || ex >= m_width)
        {
            m_cell = -1;
            return;
        }

        m_cell = (ey - m_bandStart) * m_stride + Math.Max(ex, -1) + 1;
    }

    private void AddWork(long units)
    {
        m_work += units;
        if (m_work > m_budget)
            throw new NotSupportedException(
                $"Rasterising the glyph takes more than {m_budget} work units, this library's limit.");
    }

    /// <summary><c>FT_INTEGRATE</c>.</summary>
    private void Integrate(int cover, int area)
    {
        if (m_cell < 0 || m_countOnly)
            return;

        unchecked
        {
            m_cover[m_cell] += cover;
            m_area[m_cell] += cover * area;
        }
    }

    private void MoveTo(long x, long y)
    {
        x = Upscale(x);
        y = Upscale(y);
        SetCell(Trunc(x), Trunc(y));
        m_x = x;
        m_y = y;
    }

    private void LineTo(long x, long y) => RenderLine(Upscale(x), Upscale(y));

    /// <summary><c>gray_render_line</c> (the 64-bit version): walks the cells the segment crosses.</summary>
    private void RenderLine(long toX, long toY)
    {
        AddWork(1);
        var ey1 = Trunc(m_y);
        var ey2 = Trunc(toY);

        // Vertical clipping.
        if ((ey1 >= m_bandEnd && ey2 >= m_bandEnd) || (ey1 < m_bandStart && ey2 < m_bandStart))
        {
            m_x = toX;
            m_y = toY;
            return;
        }

        var ex1 = Trunc(m_x);
        var ex2 = Trunc(toX);
        var fx1 = Fract(m_x);
        var fy1 = Fract(m_y);
        int fx2, fy2;

        var dx = toX - m_x;
        var dy = toY - m_y;

        if (ex1 == ex2 && ey1 == ey2)
        {
            // Inside one cell.
        }
        else if (dy == 0)
        {
            // Horizontal: no cover, only the cell changes.
            SetCell(ex2, ey2);
            m_x = toX;
            m_y = toY;
            return;
        }
        else if (dx == 0)
        {
            if (dy > 0)
            {
                do
                {
                    fy2 = OnePixel;
                    Integrate(fy2 - fy1, fx1 * 2);
                    fy1 = 0;
                    ey1++;
                    SetCell(ex1, ey1);
                } while (ey1 != ey2);
            }
            else
            {
                do
                {
                    fy2 = 0;
                    Integrate(fy2 - fy1, fx1 * 2);
                    fy1 = OnePixel;
                    ey1--;
                    SetCell(ex1, ey1);
                } while (ey1 != ey2);
            }
        }
        else
        {
            // `prod` tells which side of the current cell the line leaves through, and where.
            var prod = dx * fy1 - dy * fx1;
            var dxReciprocal = ex1 != ex2 ? 0xFFFFFFFFL / dx : 0;
            var dyReciprocal = ey1 != ey2 ? 0xFFFFFFFFL / dy : 0;

            do
            {
                if (prod - dx * OnePixel > 0 && prod <= 0)
                {
                    // Left.
                    fx2 = 0;
                    fy2 = UDiv(-prod, -dxReciprocal);
                    prod -= dy * OnePixel;
                    Integrate(fy2 - fy1, fx1 + fx2);
                    fx1 = OnePixel;
                    fy1 = fy2;
                    ex1--;
                }
                else if (prod - dx * OnePixel + dy * OnePixel > 0 && prod - dx * OnePixel <= 0)
                {
                    // Up.
                    prod -= dx * OnePixel;
                    fx2 = UDiv(-prod, dyReciprocal);
                    fy2 = OnePixel;
                    Integrate(fy2 - fy1, fx1 + fx2);
                    fx1 = fx2;
                    fy1 = 0;
                    ey1++;
                }
                else if (prod + dy * OnePixel >= 0 && prod - dx * OnePixel + dy * OnePixel <= 0)
                {
                    // Right.
                    prod += dy * OnePixel;
                    fx2 = OnePixel;
                    fy2 = UDiv(prod, dxReciprocal);
                    Integrate(fy2 - fy1, fx1 + fx2);
                    fx1 = 0;
                    fy1 = fy2;
                    ex1++;
                }
                else
                {
                    // Down.
                    fx2 = UDiv(prod, -dyReciprocal);
                    fy2 = 0;
                    prod += dx * OnePixel;
                    Integrate(fy2 - fy1, fx1 + fx2);
                    fx1 = fx2;
                    fy1 = OnePixel;
                    ey1--;
                }

                SetCell(ex1, ey1);
            } while (ex1 != ex2 || ey1 != ey2);
        }

        fx2 = Fract(toX);
        fy2 = Fract(toY);
        Integrate(fy2 - fy1, fx1 + fx2);

        m_x = toX;
        m_y = toY;
    }

    /// <summary>
    /// <c>gray_render_conic</c> with <c>BEZIER_USE_DDA</c> (64-bit and SSE2 hosts): 2^n forward-difference steps, n chosen so
    /// the deviation falls to a quarter pixel.
    /// </summary>
    private void ConicTo(long controlX, long controlY, long toX, long toY)
    {
        long p0X = m_x, p0Y = m_y;
        long p1X = Upscale(controlX), p1Y = Upscale(controlY);
        long p2X = Upscale(toX), p2Y = Upscale(toY);

        // Skip an arc wholly above or below the bitmap.
        if ((Trunc(p0Y) >= m_bandEnd && Trunc(p1Y) >= m_bandEnd && Trunc(p2Y) >= m_bandEnd) ||
            (Trunc(p0Y) < m_bandStart && Trunc(p1Y) < m_bandStart && Trunc(p2Y) < m_bandStart))
        {
            m_x = p2X;
            m_y = p2Y;
            return;
        }

        var bx = p1X - p0X;
        var by = p1Y - p0Y;
        var ax = p2X - p1X - bx;
        var ay = p2Y - p1Y - by;

        var d = Math.Max(Math.Abs(ax), Math.Abs(ay));
        if (d <= OnePixel / 4)
        {
            RenderLine(p2X, p2Y);
            return;
        }

        var shift = 0;
        do
        {
            d >>= 2;
            shift++;
        } while (d > OnePixel / 4);

        var rx = LeftShift(ax, 33 - 2 * shift);
        var ry = LeftShift(ay, 33 - 2 * shift);
        var qx = LeftShift(bx, 33 - shift) + LeftShift(ax, 32 - 2 * shift);
        var qy = LeftShift(by, 33 - shift) + LeftShift(ay, 32 - 2 * shift);
        var px = LeftShift(p0X, 32);
        var py = LeftShift(p0Y, 32);

        for (var count = 1u << shift; count > 0; count--)
        {
            px = unchecked(px + qx);
            py = unchecked(py + qy);
            qx = unchecked(qx + rx);
            qy = unchecked(qy + ry);
            RenderLine(px >> 32, py >> 32);
        }
    }

    /// <summary><c>gray_render_cubic</c>: bisects until each piece's control points lie near its chord's trisection points.</summary>
    private void CubicTo(long control1X, long control1Y, long control2X, long control2Y, long toX, long toY)
    {
        var ax = m_arcX;
        var ay = m_arcY;
        ax[0] = Upscale(toX);
        ay[0] = Upscale(toY);
        ax[1] = Upscale(control2X);
        ay[1] = Upscale(control2Y);
        ax[2] = Upscale(control1X);
        ay[2] = Upscale(control1Y);
        ax[3] = m_x;
        ay[3] = m_y;

        if ((Trunc(ay[0]) >= m_bandEnd && Trunc(ay[1]) >= m_bandEnd && Trunc(ay[2]) >= m_bandEnd && Trunc(ay[3]) >= m_bandEnd) ||
            (Trunc(ay[0]) < m_bandStart && Trunc(ay[1]) < m_bandStart && Trunc(ay[2]) < m_bandStart && Trunc(ay[3]) < m_bandStart))
        {
            m_x = ax[0];
            m_y = ay[0];
            return;
        }

        var arc = 0;
        while (true)
        {
            if (Math.Abs(2 * ax[arc] - 3 * ax[arc + 1] + ax[arc + 3]) > OnePixel / 2 ||
                Math.Abs(2 * ay[arc] - 3 * ay[arc + 1] + ay[arc + 3]) > OnePixel / 2 ||
                Math.Abs(ax[arc] - 3 * ax[arc + 2] + 2 * ax[arc + 3]) > OnePixel / 2 ||
                Math.Abs(ay[arc] - 3 * ay[arc + 2] + 2 * ay[arc + 3]) > OnePixel / 2)
            {
                if (arc + 6 >= CubicStackLength)
                    throw new InvalidDataException("A cubic arc needs more bisections than the rasteriser allows.");

                AddWork(1);
                SplitCubic(ax, arc);
                SplitCubic(ay, arc);
                arc += 3;
                continue;
            }

            RenderLine(ax[arc], ay[arc]);
            if (arc == 0)
                return;

            arc -= 3;
        }
    }

    /// <summary><c>gray_split_cubic</c> on one coordinate: the arc at <paramref name="b"/> becomes two arcs, the second on top.</summary>
    private static void SplitCubic(long[] v, int b)
    {
        v[b + 6] = v[b + 3];
        var a = v[b] + v[b + 1];
        var bb = v[b + 1] + v[b + 2];
        var c = v[b + 2] + v[b + 3];
        v[b + 5] = c >> 1;
        c += bb;
        v[b + 4] = c >> 2;
        v[b + 1] = a >> 1;
        a += bb;
        v[b + 2] = a >> 2;
        v[b + 3] = (a + c) >> 3;
    }
}
