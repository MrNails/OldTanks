// Follows FreeType 2.13.2 src/truetype/ttinterp.c (the Ins_* functions that measure and move points: ISECT, ALIGNPTS, UTP,
// MDAP, IUP, SHP, SHC, SHZ, SHPIX, IP, MSIRP, ALIGNRP, MIAP, GC, SCFS, MD, DELTAP, DELTAC, FLIPPT, FLIPRGON/OFF, SDPVTL, MDRP,
// MIRP), with the v40 backward-compatibility rules (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

internal sealed partial class Interpreter
{
    private static bool OutOfBounds(long index, int count) => (uint)index >= (uint)count;

    /// <summary>FreeType's <c>Ins_ISECT</c>.</summary>
    private void InsIsect(int a)
    {
        var s = m_stack;
        var point = (ushort)s[a];
        var a0 = (ushort)s[a + 1];
        var a1 = (ushort)s[a + 2];
        var b0 = (ushort)s[a + 3];
        var b1 = (ushort)s[a + 4];

        if (b0 >= m_zp0.PointCount || b1 >= m_zp0.PointCount || a0 >= m_zp1.PointCount || a1 >= m_zp1.PointCount ||
            point >= m_zp2.PointCount)
            return;

        var zp0 = m_zp0;
        var zp1 = m_zp1;
        var dbx = zp0.CurX[b1] - zp0.CurX[b0];
        var dby = zp0.CurY[b1] - zp0.CurY[b0];
        var dax = zp1.CurX[a1] - zp1.CurX[a0];
        var day = zp1.CurY[a1] - zp1.CurY[a0];
        var dx = zp0.CurX[b0] - zp1.CurX[a0];
        var dy = zp0.CurY[b0] - zp1.CurY[a0];

        var discriminant = HintingMath.MulDiv(dax, -dby, 0x40) + HintingMath.MulDiv(day, dbx, 0x40);
        var dotProduct = HintingMath.MulDiv(dax, dbx, 0x40) + HintingMath.MulDiv(day, dby, 0x40);

        // Reject grazing intersections: |tan(angle)| below 1/19, about 3 degrees.
        if (19 * Math.Abs(discriminant) > Math.Abs(dotProduct))
        {
            var val = HintingMath.MulDiv(dx, -dby, 0x40) + HintingMath.MulDiv(dy, dbx, 0x40);
            var rx = HintingMath.MulDiv(val, dax, discriminant);
            var ry = HintingMath.MulDiv(val, day, discriminant);
            m_zp2.CurX[point] = zp1.CurX[a0] + rx;
            m_zp2.CurY[point] = zp1.CurY[a0] + ry;
        }
        else
        {
            // The middle of the middles of A and B.
            m_zp2.CurX[point] = (zp1.CurX[a0] + zp1.CurX[a1] + (zp0.CurX[b0] + zp0.CurX[b1])) / 4;
            m_zp2.CurY[point] = (zp1.CurY[a0] + zp1.CurY[a1] + (zp0.CurY[b0] + zp0.CurY[b1])) / 4;
        }

        m_zp2.Tags[point] |= HintZone.TouchBoth;
    }

    /// <summary>FreeType's <c>Ins_ALIGNPTS</c>.</summary>
    private void InsAlignPts(int a)
    {
        var p1 = (ushort)m_stack[a];
        var p2 = (ushort)m_stack[a + 1];
        if (p1 >= m_zp1.PointCount || p2 >= m_zp0.PointCount)
            return;

        var distance = ProjectCur(m_zp0, p2, m_zp1, p1) / 2;
        Move(m_zp1, p1, distance);
        Move(m_zp0, p2, -distance);
    }

    /// <summary>FreeType's <c>Ins_UTP</c>.</summary>
    private void InsUtp(int a)
    {
        var point = (ushort)m_stack[a];
        if (point >= m_zp0.PointCount)
            return;

        byte mask = 0xFF;
        if (GS.FreeVectorX != 0)
            mask &= unchecked((byte)~HintZone.TouchX);

        if (GS.FreeVectorY != 0)
            mask &= unchecked((byte)~HintZone.TouchY);

        m_zp0.Tags[point] &= mask;
    }

    /// <summary>FreeType's <c>Ins_MDAP</c>.</summary>
    private void InsMdap(int a)
    {
        var point = (ushort)m_stack[a];
        if (point >= m_zp0.PointCount)
            return;

        long distance = 0;
        if ((m_opcode & 1) != 0)
        {
            var current = Project(m_zp0.CurX[point], m_zp0.CurY[point]);
            distance = Round(current) - current;
        }

        Move(m_zp0, point, distance);
        GS.Rp0 = point;
        GS.Rp1 = point;
    }

    /// <summary>FreeType's <c>Ins_IUP</c>.</summary>
    private void InsIup()
    {
        // v40 backward compatibility: IUP runs once per axis, then never again for the glyph.
        if (IsV40 && BackwardCompatibility)
        {
            if (m_iupXCalled && m_iupYCalled)
                return;

            if ((m_opcode & 1) != 0)
                m_iupXCalled = true;
            else
                m_iupYCalled = true;
        }

        var pts = Pts;
        if (pts.ContourCount == 0)
            return;

        Charge(pts.PointCount);

        var isX = (m_opcode & 1) != 0;
        var mask = isX ? HintZone.TouchX : HintZone.TouchY;
        var orgs = isX ? pts.OrgX : pts.OrgY;
        var curs = isX ? pts.CurX : pts.CurY;
        var orus = isX ? pts.OrusX : pts.OrusY;
        var maxPoints = (uint)pts.PointCount;

        var contour = 0;
        uint point = 0;
        do
        {
            var endPoint = (uint)(pts.Contours[contour] - pts.FirstPoint);
            var firstPoint = point;

            if (endPoint >= maxPoints)
                endPoint = maxPoints - 1;

            while (point <= endPoint && (pts.Tags[point] & mask) == 0)
                point++;

            if (point <= endPoint)
            {
                var firstTouched = point;
                var curTouched = point;
                point++;

                while (point <= endPoint)
                {
                    if ((pts.Tags[point] & mask) != 0)
                    {
                        IupInterpolate(orgs, curs, orus, maxPoints, curTouched + 1, point - 1, curTouched, point);
                        curTouched = point;
                    }

                    point++;
                }

                if (curTouched == firstTouched)
                {
                    IupShift(orgs, curs, firstPoint, endPoint, curTouched);
                }
                else
                {
                    IupInterpolate(orgs, curs, orus, maxPoints, (ushort)(curTouched + 1), endPoint, curTouched, firstTouched);
                    if (firstTouched > 0)
                        IupInterpolate(orgs, curs, orus, maxPoints, firstPoint, firstTouched - 1, curTouched, firstTouched);
                }
            }

            contour++;
        } while (contour < pts.ContourCount);
    }

    /// <summary>FreeType's <c>iup_worker_shift_</c>.</summary>
    private static void IupShift(long[] orgs, long[] curs, uint p1, uint p2, uint p)
    {
        var dx = curs[p] - orgs[p];
        if (dx == 0)
            return;

        for (var i = p1; i < p; i++)
            curs[i] += dx;

        for (var i = p + 1; i <= p2; i++)
            curs[i] += dx;
    }

    /// <summary>FreeType's <c>iup_worker_interpolate_</c>.</summary>
    private static void IupInterpolate(long[] orgs, long[] curs, long[] orus, uint maxPoints, uint p1, uint p2, uint ref1,
        uint ref2)
    {
        if (p1 > p2)
            return;

        if (ref1 >= maxPoints || ref2 >= maxPoints)
            return;

        var orus1 = orus[ref1];
        var orus2 = orus[ref2];
        if (orus1 > orus2)
        {
            (orus1, orus2) = (orus2, orus1);
            (ref1, ref2) = (ref2, ref1);
        }

        var org1 = orgs[ref1];
        var org2 = orgs[ref2];
        var cur1 = curs[ref1];
        var cur2 = curs[ref2];
        var delta1 = cur1 - org1;
        var delta2 = cur2 - org2;

        if (cur1 == cur2 || orus1 == orus2)
        {
            // A trivial snap or shift of untouched points.
            for (var i = p1; i <= p2; i++)
            {
                var x = orgs[i];
                if (x <= org1)
                    x += delta1;
                else if (x >= org2)
                    x += delta2;
                else
                    x = cur1;

                curs[i] = x;
            }

            return;
        }

        long scale = 0;
        var scaleValid = false;
        for (var i = p1; i <= p2; i++)
        {
            var x = orgs[i];
            if (x <= org1)
            {
                x += delta1;
            }
            else if (x >= org2)
            {
                x += delta2;
            }
            else
            {
                if (!scaleValid)
                {
                    scaleValid = true;
                    scale = HintingMath.DivFix(cur2 - cur1, orus2 - orus1);
                }

                x = cur1 + HintingMath.MulFix(orus[i] - orus1, scale);
            }

            curs[i] = x;
        }
    }

    /// <summary>
    /// FreeType's <c>Compute_Point_Displacement</c>: the reference point's move projected onto the freedom vector; false when
    /// the reference point is out of range.
    /// </summary>
    private bool ComputePointDisplacement(out long dx, out long dy, out HintZone zone, out int refPoint)
    {
        HintZone zp;
        int p;
        if ((m_opcode & 1) != 0)
        {
            zp = m_zp0;
            p = GS.Rp1;
        }
        else
        {
            zp = m_zp1;
            p = GS.Rp2;
        }

        if (p >= zp.PointCount)
        {
            dx = dy = 0;
            zone = zp;
            refPoint = 0;
            return false;
        }

        zone = zp;
        refPoint = p;
        var d = Project(zp.CurX[p] - zp.OrgX[p], zp.CurY[p] - zp.OrgY[p]);
        dx = HintingMath.MulDiv(d, GS.FreeVectorX, m_fDotP);
        dy = HintingMath.MulDiv(d, GS.FreeVectorY, m_fDotP);
        return true;
    }

    /// <summary>FreeType's <c>Ins_SHP</c>.</summary>
    private void InsShp()
    {
        if (m_top < GS.Loop)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        // On an out-of-range reference point FreeType returns at once: the loop and the points stay.
        if (!ComputePointDisplacement(out var dx, out var dy, out _, out _))
            return;

        Charge(GS.Loop);
        while (GS.Loop > 0)
        {
            m_args--;
            var point = (ushort)m_stack[m_args];
            if (point < m_zp2.PointCount)
                MoveZp2Point(point, dx, dy, true);

            GS.Loop--;
        }

        GS.Loop = 1;
        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_SHC</c>.</summary>
    private void InsShc(int a)
    {
        var contour = (short)m_stack[a];
        var bounds = GS.Gep2 == 0 ? 1 : m_zp2.ContourCount;
        if ((uint)contour >= (uint)bounds)
            return;

        if (!ComputePointDisplacement(out var dx, out var dy, out var zone, out var refPoint))
            return;

        var start = contour == 0 ? 0 : (ushort)(m_zp2.Contours[contour - 1] + 1 - m_zp2.FirstPoint);

        // The twilight zone has no contours: its whole point count is used.
        var limit = GS.Gep2 == 0 ? m_zp2.PointCount : (ushort)(m_zp2.Contours[contour] - m_zp2.FirstPoint + 1);
        Charge(Math.Max(0, limit - start));

        for (var i = start; i < limit; i++)
        {
            if (zone != m_zp2 || refPoint != i)
                MoveZp2Point(i, dx, dy, true);
        }
    }

    /// <summary>
    /// FreeType's <c>Ins_SHZ</c>. In a glyph zone the limit is the last contour's end plus one, as FreeType 2.13.2 computes it
    /// (without subtracting the zone's first point; only a nested composite's own program has a first point above 0); points
    /// past the zone, which FreeType 2.13.2 would write outside it, are not moved.
    /// </summary>
    private void InsShz(int a)
    {
        if ((ulong)m_stack[a] >= 2)
            return;

        if (!ComputePointDisplacement(out var dx, out var dy, out var zone, out var refPoint))
            return;

        int limit;
        if (GS.Gep2 == 0)
            limit = m_zp2.PointCount;
        else if (GS.Gep2 == 1 && m_zp2.ContourCount > 0)
            limit = (ushort)(m_zp2.Contours[m_zp2.ContourCount - 1] + 1);
        else
            limit = 0;

        limit = Math.Min(limit, m_zp2.PointCount);
        Charge(limit);

        // SHZ does not touch the points.
        for (var i = 0; i < limit; i++)
        {
            if (zone != m_zp2 || refPoint != i)
                MoveZp2Point(i, dx, dy, false);
        }
    }

    /// <summary>FreeType's <c>Ins_SHPIX</c>.</summary>
    private void InsShpix(int a)
    {
        var inTwilight = GS.Gep0 == 0 || GS.Gep1 == 0 || GS.Gep2 == 0;

        if (m_top < GS.Loop + 1)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        var dx = HintingMath.MulFix14(m_stack[a], GS.FreeVectorX);
        var dy = HintingMath.MulFix14(m_stack[a], GS.FreeVectorY);

        Charge(GS.Loop);
        while (GS.Loop > 0)
        {
            m_args--;
            var point = (ushort)m_stack[m_args];
            if (point < m_zp2.PointCount)
            {
                if (IsV40 && BackwardCompatibility)
                {
                    // SHPIX may move twilight points; otherwise it acts as DELTAP does in backward compatibility.
                    if (inTwilight ||
                        (!(m_iupXCalled && m_iupYCalled) &&
                         ((IsComposite && GS.FreeVectorY != 0) || (m_zp2.Tags[point] & HintZone.TouchY) != 0)))
                        MoveZp2Point(point, 0, dy, true);
                }
                else
                {
                    MoveZp2Point(point, dx, dy, true);
                }
            }

            GS.Loop--;
        }

        GS.Loop = 1;
        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_IP</c>.</summary>
    private void InsIp()
    {
        if (m_top < GS.Loop)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        // In the twilight zone the original points stand in for the unscaled ones (which are all 0 there).
        var twilight = GS.Gep0 == 0 || GS.Gep1 == 0 || GS.Gep2 == 0;

        if (GS.Rp1 >= m_zp0.PointCount)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        var zp0 = m_zp0;
        int rp1 = GS.Rp1;
        var baseX = twilight ? zp0.OrgX[rp1] : zp0.OrusX[rp1];
        var baseY = twilight ? zp0.OrgY[rp1] : zp0.OrusY[rp1];
        var curBaseX = zp0.CurX[rp1];
        var curBaseY = zp0.CurY[rp1];

        long oldRange, curRange;
        if (GS.Rp2 >= m_zp1.PointCount)
        {
            oldRange = 0;
            curRange = 0;
        }
        else
        {
            int rp2 = GS.Rp2;
            if (twilight)
                oldRange = DualProject(m_zp1.OrgX[rp2] - baseX, m_zp1.OrgY[rp2] - baseY);
            else if (XScale == YScale)
                oldRange = DualProject(m_zp1.OrusX[rp2] - baseX, m_zp1.OrusY[rp2] - baseY);
            else
                oldRange = DualProject(HintingMath.MulFix(m_zp1.OrusX[rp2] - baseX, XScale),
                    HintingMath.MulFix(m_zp1.OrusY[rp2] - baseY, YScale));

            curRange = Project(m_zp1.CurX[rp2] - curBaseX, m_zp1.CurY[rp2] - curBaseY);
        }

        Charge(GS.Loop);
        for (; GS.Loop > 0; GS.Loop--)
        {
            var point = (uint)m_stack[--m_args];
            if (point >= (uint)m_zp2.PointCount)
                continue;

            var zp2 = m_zp2;
            long orgDist;
            if (twilight)
                orgDist = DualProject(zp2.OrgX[point] - baseX, zp2.OrgY[point] - baseY);
            else if (XScale == YScale)
                orgDist = DualProject(zp2.OrusX[point] - baseX, zp2.OrusY[point] - baseY);
            else
                orgDist = DualProject(HintingMath.MulFix(zp2.OrusX[point] - baseX, XScale),
                    HintingMath.MulFix(zp2.OrusY[point] - baseY, YScale));

            var curDist = Project(zp2.CurX[point] - curBaseX, zp2.CurY[point] - curBaseY);

            long newDist;
            if (orgDist != 0)
                newDist = oldRange != 0 ? HintingMath.MulDiv(orgDist, curRange, oldRange) : orgDist;
            else
                newDist = 0;

            Move(zp2, (ushort)point, newDist - curDist);
        }

        GS.Loop = 1;
        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_MSIRP</c>.</summary>
    private void InsMsirp(int a)
    {
        var point = (ushort)m_stack[a];
        if (point >= m_zp1.PointCount || GS.Rp0 >= m_zp0.PointCount)
            return;

        // In the twilight zone the original point is set too (as the Microsoft rasteriser does).
        if (GS.Gep1 == 0)
        {
            m_zp1.OrgX[point] = m_zp0.OrgX[GS.Rp0];
            m_zp1.OrgY[point] = m_zp0.OrgY[GS.Rp0];
            MoveOrig(m_zp1, point, m_stack[a + 1]);
            m_zp1.CurX[point] = m_zp1.OrgX[point];
            m_zp1.CurY[point] = m_zp1.OrgY[point];
        }

        var distance = ProjectCur(m_zp1, point, m_zp0, GS.Rp0);
        Move(m_zp1, point, m_stack[a + 1] - distance);

        GS.Rp1 = GS.Rp0;
        GS.Rp2 = point;
        if ((m_opcode & 1) != 0)
            GS.Rp0 = point;
    }

    /// <summary>FreeType's <c>Ins_ALIGNRP</c>.</summary>
    private void InsAlignRp()
    {
        if (m_top < GS.Loop || GS.Rp0 >= m_zp0.PointCount)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        Charge(GS.Loop);
        while (GS.Loop > 0)
        {
            m_args--;
            var point = (ushort)m_stack[m_args];
            if (point < m_zp1.PointCount)
            {
                var distance = ProjectCur(m_zp1, point, m_zp0, GS.Rp0);
                Move(m_zp1, point, -distance);
            }

            GS.Loop--;
        }

        GS.Loop = 1;
        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_MIAP</c>.</summary>
    private void InsMiap(int a)
    {
        var cvtEntry = (ulong)m_stack[a + 1];
        var point = (ushort)m_stack[a];

        if (point >= m_zp0.PointCount || cvtEntry >= (ulong)m_cvt.Length)
        {
            GS.Rp0 = point;
            GS.Rp1 = point;
            return;
        }

        var distance = m_cvt[cvtEntry];

        // In the twilight zone the original point is set to the cvt distance along the freedom vector, unrounded.
        if (GS.Gep0 == 0)
        {
            m_zp0.OrgX[point] = HintingMath.MulFix14(distance, GS.FreeVectorX);
            m_zp0.OrgY[point] = HintingMath.MulFix14(distance, GS.FreeVectorY);
            m_zp0.CurX[point] = m_zp0.OrgX[point];
            m_zp0.CurY[point] = m_zp0.OrgY[point];
        }

        var orgDist = Project(m_zp0.CurX[point], m_zp0.CurY[point]);

        if ((m_opcode & 1) != 0)
        {
            var delta = distance - orgDist;
            if (delta < 0)
                delta = -delta;

            if (delta > GS.ControlValueCutIn)
                distance = orgDist;

            distance = Round(distance);
        }

        Move(m_zp0, point, distance - orgDist);

        GS.Rp0 = point;
        GS.Rp1 = point;
    }

    /// <summary>FreeType's <c>Ins_GC</c>.</summary>
    private void InsGc(int a)
    {
        var l = (ulong)m_stack[a];
        long r = 0;
        if (l < (ulong)m_zp2.PointCount)
        {
            r = (m_opcode & 1) != 0
                ? DualProject(m_zp2.OrgX[l], m_zp2.OrgY[l])
                : Project(m_zp2.CurX[l], m_zp2.CurY[l]);
        }

        m_stack[a] = r;
    }

    /// <summary>FreeType's <c>Ins_SCFS</c>.</summary>
    private void InsScfs(int a)
    {
        var l = (ushort)m_stack[a];
        if (l >= m_zp2.PointCount)
            return;

        var k = Project(m_zp2.CurX[l], m_zp2.CurY[l]);
        Move(m_zp2, l, m_stack[a + 1] - k);

        // In the twilight zone the original point follows (as the Microsoft rasteriser does).
        if (GS.Gep2 == 0)
        {
            m_zp2.OrgX[l] = m_zp2.CurX[l];
            m_zp2.OrgY[l] = m_zp2.CurY[l];
        }
    }

    /// <summary>FreeType's <c>Ins_MD</c>.</summary>
    private void InsMd(int a)
    {
        var k = (ushort)m_stack[a + 1];
        var l = (ushort)m_stack[a];
        long d;

        if (l >= m_zp0.PointCount || k >= m_zp1.PointCount)
            d = 0;
        else if ((m_opcode & 1) != 0)
            d = ProjectCur(m_zp0, l, m_zp1, k);
        else if (GS.Gep0 == 0 || GS.Gep1 == 0)
            d = DualProjectOrg(m_zp0, l, m_zp1, k);
        else
            d = DualProjectOrus(m_zp0, l, m_zp1, k);

        m_stack[a] = d;
    }

    /// <summary>FreeType's <c>Ins_DELTAP</c>: DELTAP1, DELTAP2, DELTAP3.</summary>
    private void InsDeltaP(int a)
    {
        var ppem = (ulong)Ppem;
        var count = (ulong)m_stack[a];

        for (ulong k = 1; k <= count; k++)
        {
            if (m_args < 2)
            {
                m_args = 0;
                break;
            }

            Charge(1);

            m_args -= 2;
            var point = (ushort)m_stack[m_args + 1];
            var b = m_stack[m_args];

            // Out-of-range points are ignored: some popular fonts have invalid DELTAP references.
            if (point >= m_zp0.PointCount)
                continue;

            var c = ((ulong)b & 0xF0) >> 4;
            c += m_opcode switch
            {
                0x71 => 16UL,
                0x72 => 32UL,
                _ => 0UL,
            };

            c += GS.DeltaBase;
            if (ppem != c)
                continue;

            b = (long)(((ulong)b & 0xF) - 8);
            if (b >= 0)
                b++;

            b *= 1L << (6 - GS.DeltaShift);

            if (IsV40 && BackwardCompatibility)
            {
                if (!(m_iupXCalled && m_iupYCalled) &&
                    ((IsComposite && GS.FreeVectorY != 0) || (m_zp0.Tags[point] & HintZone.TouchY) != 0))
                    Move(m_zp0, point, b);
            }
            else
            {
                Move(m_zp0, point, b);
            }
        }

        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_DELTAC</c>: DELTAC1, DELTAC2, DELTAC3.</summary>
    private void InsDeltaC(int a)
    {
        var ppem = (ulong)Ppem;
        var count = (ulong)m_stack[a];

        for (ulong k = 1; k <= count; k++)
        {
            if (m_args < 2)
            {
                m_args = 0;
                break;
            }

            Charge(1);

            m_args -= 2;
            var index = (ulong)m_stack[m_args + 1];
            var b = m_stack[m_args];

            if (index >= (ulong)m_cvt.Length)
                continue;

            var c = ((ulong)b & 0xF0) >> 4;
            c += m_opcode switch
            {
                0x74 => 16UL,
                0x75 => 32UL,
                _ => 0UL,
            };

            c += GS.DeltaBase;
            if (ppem != c)
                continue;

            b = (long)(((ulong)b & 0xF) - 8);
            if (b >= 0)
                b++;

            b *= 1L << (6 - GS.DeltaShift);
            MoveCvt((int)index, b);
        }

        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_FLIPPT</c>.</summary>
    private void InsFlipPt()
    {
        if (PostIupFrozen || m_top < GS.Loop)
        {
            GS.Loop = 1;
            m_newTop = m_args;
            return;
        }

        Charge(GS.Loop);
        while (GS.Loop > 0)
        {
            m_args--;
            var point = (ushort)m_stack[m_args];
            if (point < Pts.PointCount)
                Pts.Tags[point] ^= HintZone.TagOn;

            GS.Loop--;
        }

        GS.Loop = 1;
        m_newTop = m_args;
    }

    /// <summary>FreeType's <c>Ins_FLIPRGON</c> and <c>Ins_FLIPRGOFF</c>.</summary>
    private void InsFlipRange(int a, bool on)
    {
        if (PostIupFrozen)
            return;

        var k = (ushort)m_stack[a + 1];
        var l = (ushort)m_stack[a];
        if (k >= Pts.PointCount || l >= Pts.PointCount)
            return;

        Charge(Math.Max(0, k - l + 1));

        for (int i = l; i <= k; i++)
        {
            if (on)
                Pts.Tags[i] |= HintZone.TagOn;
            else
                Pts.Tags[i] &= unchecked((byte)~HintZone.TagOn);
        }
    }

    /// <summary>FreeType's <c>Ins_SDPVTL</c>.</summary>
    private void InsSdpvtl(int a)
    {
        var p1 = (ushort)m_stack[a + 1];
        var p2 = (ushort)m_stack[a];
        if (p2 >= m_zp1.PointCount || p1 >= m_zp2.PointCount)
            return;

        var opcode = m_opcode;
        var x = m_zp1.OrgX[p2] - m_zp2.OrgX[p1];
        var y = m_zp1.OrgY[p2] - m_zp2.OrgY[p1];

        // Coinciding points act as SVTCA[x].
        if (x == 0 && y == 0)
        {
            x = 0x4000;
            opcode = 0;
        }

        if ((opcode & 1) != 0)
            (x, y) = (-y, x);

        Normalize(x, y, ref GS.DualVectorX, ref GS.DualVectorY);

        x = m_zp1.CurX[p2] - m_zp2.CurX[p1];
        y = m_zp1.CurY[p2] - m_zp2.CurY[p1];
        if (x == 0 && y == 0)
        {
            x = 0x4000;
            opcode = 0;
        }

        if ((opcode & 1) != 0)
            (x, y) = (-y, x);

        Normalize(x, y, ref GS.ProjVectorX, ref GS.ProjVectorY);
        ComputeFuncs();
    }

    /// <summary>FreeType's <c>Ins_MDRP</c>.</summary>
    private void InsMdrp(int a)
    {
        var point = (ushort)m_stack[a];

        if (point < m_zp1.PointCount && GS.Rp0 < m_zp0.PointCount)
        {
            // The twilight zone measures original points; the glyph zone unscaled ones, scaled.
            var orgDist = GS.Gep0 == 0 || GS.Gep1 == 0
                ? DualProjectOrg(m_zp1, point, m_zp0, GS.Rp0)
                : DualProjectOrus(m_zp1, point, m_zp0, GS.Rp0);

            // Single width cut-in: |orgDist - single_width_value| < single_width_cutin.
            if (GS.SingleWidthCutIn > 0 &&
                orgDist < GS.SingleWidthValue + GS.SingleWidthCutIn &&
                orgDist > GS.SingleWidthValue - GS.SingleWidthCutIn)
                orgDist = orgDist >= 0 ? GS.SingleWidthValue : -GS.SingleWidthValue;

            var distance = (m_opcode & 4) != 0 ? Round(orgDist) : orgDist;

            if ((m_opcode & 8) != 0)
            {
                var minimum = GS.MinimumDistance;
                if (orgDist >= 0)
                {
                    if (distance < minimum)
                        distance = minimum;
                }
                else if (distance > -minimum)
                {
                    distance = -minimum;
                }
            }

            var current = ProjectCur(m_zp1, point, m_zp0, GS.Rp0);
            Move(m_zp1, point, distance - current);
        }

        GS.Rp1 = GS.Rp0;
        GS.Rp2 = point;
        if ((m_opcode & 16) != 0)
            GS.Rp0 = point;
    }

    /// <summary>FreeType's <c>Ins_MIRP</c>.</summary>
    private void InsMirp(int a)
    {
        var point = (ushort)m_stack[a];

        // cvt[-1] reads as 0.
        var cvtEntry = (ulong)(m_stack[a + 1] + 1);

        if (point < m_zp1.PointCount && cvtEntry < (ulong)m_cvt.Length + 1 && GS.Rp0 < m_zp0.PointCount)
            MirpMove(point, cvtEntry);

        GS.Rp1 = GS.Rp0;
        if ((m_opcode & 16) != 0)
            GS.Rp0 = point;

        GS.Rp2 = point;
    }

    private void MirpMove(ushort point, ulong cvtEntry)
    {
        var cvtDist = cvtEntry == 0 ? 0 : m_cvt[cvtEntry - 1];

        // Single width test.
        var delta = cvtDist - GS.SingleWidthValue;
        if (delta < 0)
            delta = -delta;

        if (delta < GS.SingleWidthCutIn)
            cvtDist = cvtDist >= 0 ? GS.SingleWidthValue : -GS.SingleWidthValue;

        // In the twilight zone the original point is placed from rp0 (as the Microsoft rasteriser does).
        if (GS.Gep1 == 0)
        {
            m_zp1.OrgX[point] = m_zp0.OrgX[GS.Rp0] + HintingMath.MulFix14(cvtDist, GS.FreeVectorX);
            m_zp1.OrgY[point] = m_zp0.OrgY[GS.Rp0] + HintingMath.MulFix14(cvtDist, GS.FreeVectorY);
            m_zp1.CurX[point] = m_zp1.OrgX[point];
            m_zp1.CurY[point] = m_zp1.OrgY[point];
        }

        var orgDist = DualProjectOrg(m_zp1, point, m_zp0, GS.Rp0);
        var curDist = ProjectCur(m_zp1, point, m_zp0, GS.Rp0);

        // Auto-flip.
        if (GS.AutoFlip && (orgDist ^ cvtDist) < 0)
            cvtDist = -cvtDist;

        long distance;
        if ((m_opcode & 4) != 0)
        {
            // The control value cut-in applies only when both points are in the same zone.
            if (GS.Gep0 == GS.Gep1)
            {
                delta = cvtDist - orgDist;
                if (delta < 0)
                    delta = -delta;

                if (delta > GS.ControlValueCutIn)
                    cvtDist = orgDist;
            }

            distance = Round(cvtDist);
        }
        else
        {
            distance = cvtDist;
        }

        if ((m_opcode & 8) != 0)
        {
            var minimum = GS.MinimumDistance;
            if (orgDist >= 0)
            {
                if (distance < minimum)
                    distance = minimum;
            }
            else if (distance > -minimum)
            {
                distance = -minimum;
            }
        }

        Move(m_zp1, point, distance - curDist);
    }
}
