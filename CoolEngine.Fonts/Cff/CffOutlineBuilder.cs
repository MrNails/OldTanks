// Follows FreeType 2.13.2 src/psaux/psobjs.c (ps_builder_add_point, ps_builder_add_point1, ps_builder_add_contour,
// ps_builder_start_point, ps_builder_close_contour) and src/psaux/psft.c (cf2_builder_moveTo, cf2_builder_lineTo,
// cf2_builder_cubeTo) (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// Collects a CFF glyph's outline as FreeType's PostScript builder does: points arrive in 16.16 device units of the Adobe
/// engine's unhinted transform (1/64 of a font unit per unit), are stored shifted right by 10 (whole font units, rounded
/// down), and a contour's last point is dropped when it is an on-curve copy of its first. Reused per glyph; not thread-safe.
/// </summary>
internal sealed class CffOutlineBuilder
{
    /// <summary>
    /// The most points one glyph may have while it is built: 65,535, as a TrueType glyph's limit in this library. FreeType
    /// 2.13.2 fails a glyph past 32,767 (<c>FT_OUTLINE_POINTS_MAX</c>); between the two, ours works (Denys's "Ours works").
    /// </summary>
    public const int MaxPoints = 65535;

    public const byte OnCurve = 1;
    public const byte Cubic = 2;

    private int[] m_x = new int[64];
    private int[] m_y = new int[64];
    private byte[] m_tags = new byte[64];
    private int[] m_contours = new int[16];

    public int PointCount { get; private set; }

    public int ContourCount { get; private set; }

    /// <summary>A contour is open: its first point has been added (FreeType's <c>path_begun</c>).</summary>
    private bool m_pathBegun;

    public ReadOnlySpan<int> X => m_x.AsSpan(0, PointCount);

    public ReadOnlySpan<int> Y => m_y.AsSpan(0, PointCount);

    public ReadOnlySpan<byte> Tags => m_tags.AsSpan(0, PointCount);

    public ReadOnlySpan<int> Contours => m_contours.AsSpan(0, ContourCount);

    public void Reset()
    {
        PointCount = 0;
        ContourCount = 0;
        m_pathBegun = false;
    }

    /// <summary><c>cf2_builder_moveTo</c>: closes the contour; the next segment starts a new one.</summary>
    public void MoveTo()
    {
        CloseContour();
        m_pathBegun = false;
    }

    /// <summary><c>cf2_builder_lineTo</c>, in 16.16 device units.</summary>
    public void LineTo(int x0, int y0, int x1, int y1)
    {
        if (!m_pathBegun)
            StartPoint(x0, y0);

        AddPoint1(x1, y1);
    }

    /// <summary><c>cf2_builder_cubeTo</c>, in 16.16 device units.</summary>
    public void CubeTo(int x0, int y0, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        if (!m_pathBegun)
            StartPoint(x0, y0);

        CheckPoints(3);
        AddPoint(x1, y1, Cubic);
        AddPoint(x2, y2, Cubic);
        AddPoint(x3, y3, OnCurve);
    }

    /// <summary>
    /// <c>ps_builder_close_contour</c>: drops a contour with no points, an on-curve last point equal to the first, and then a
    /// contour of one point; records the contour's end.
    /// </summary>
    public void CloseContour()
    {
        var first = ContourCount <= 1 ? 0 : m_contours[ContourCount - 2] + 1;

        // A contour started without points (malformed fonts).
        if (ContourCount > 0 && first == PointCount)
        {
            ContourCount--;
            return;
        }

        // The last point goes when it is an on-curve copy of the first.
        if (PointCount > 1)
        {
            var last = PointCount - 1;
            if (m_x[first] == m_x[last] && m_y[first] == m_y[last] && m_tags[last] == OnCurve)
                PointCount--;
        }

        if (ContourCount > 0)
        {
            // A contour of one point is dropped.
            if (first == PointCount - 1)
            {
                ContourCount--;
                PointCount--;
            }
            else
            {
                m_contours[ContourCount - 1] = PointCount - 1;
            }
        }
    }

    /// <summary><c>ps_builder_start_point</c>.</summary>
    private void StartPoint(int x, int y)
    {
        m_pathBegun = true;
        AddContour();
        AddPoint1(x, y);
    }

    /// <summary><c>ps_builder_add_contour</c>: ends the previous contour at the last point and opens one.</summary>
    private void AddContour()
    {
        if (ContourCount == m_contours.Length)
            Array.Resize(ref m_contours, m_contours.Length * 2);

        if (ContourCount > 0)
            m_contours[ContourCount - 1] = PointCount - 1;

        ContourCount++;
    }

    private void AddPoint1(int x, int y)
    {
        CheckPoints(1);
        AddPoint(x, y, OnCurve);
    }

    /// <summary><c>ps_builder_add_point</c> for the Adobe engine: the 16.16 device value shifted right by 10.</summary>
    private void AddPoint(int x, int y, byte tag)
    {
        m_x[PointCount] = x >> 10;
        m_y[PointCount] = y >> 10;
        m_tags[PointCount] = tag;
        PointCount++;
    }

    private void CheckPoints(int count)
    {
        var needed = PointCount + count;
        if (needed > MaxPoints)
            throw new NotSupportedException(
                $"The glyph's outline has more than {MaxPoints} points, this library's limit for a CFF glyph.");

        if (needed <= m_x.Length)
            return;

        var size = Math.Max(needed, m_x.Length * 2);
        Array.Resize(ref m_x, size);
        Array.Resize(ref m_y, size);
        Array.Resize(ref m_tags, size);
    }
}
