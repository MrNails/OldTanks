// Follows FreeType 2.13.2 src/truetype/ttinterp.c (Compute_Funcs, Compute_Round, the Round_* functions, SetSuperRound,
// Project, Dual_Project, Direct_Move*, Move_Zp2_Point, Normalize) (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

internal sealed partial class Interpreter
{
    private const int AxisX = 0;
    private const int AxisY = 1;
    private const int AxisAny = 2;

    private HintZone m_zp0;
    private HintZone m_zp1;
    private HintZone m_zp2;

    // Compute_Funcs's choices: which projection and move functions apply, and F_dot_P.
    private int m_projectMode;
    private int m_dualMode;
    private int m_moveMode;
    private long m_fDotP = 0x4000;

    // FreeType's exc->iupx_called and iupy_called (v40 backward compatibility).
    private bool m_iupXCalled;
    private bool m_iupYCalled;

    /// <summary>The v40 rule that blocks y moves once IUP ran on both axes in backward-compatibility mode.</summary>
    private bool PostIupFrozen => IsV40 && BackwardCompatibility && m_iupXCalled && m_iupYCalled;

    /// <summary>FreeType's <c>Compute_Funcs</c>.</summary>
    private void ComputeFuncs()
    {
        if (GS.FreeVectorX == 0x4000)
            m_fDotP = GS.ProjVectorX;
        else if (GS.FreeVectorY == 0x4000)
            m_fDotP = GS.ProjVectorY;
        else
            m_fDotP = ((long)GS.ProjVectorX * GS.FreeVectorX + (long)GS.ProjVectorY * GS.FreeVectorY) >> 14;

        m_projectMode = GS.ProjVectorX == 0x4000 ? AxisX : GS.ProjVectorY == 0x4000 ? AxisY : AxisAny;
        m_dualMode = GS.DualVectorX == 0x4000 ? AxisX : GS.DualVectorY == 0x4000 ? AxisY : AxisAny;

        m_moveMode = AxisAny;
        if (m_fDotP == 0x4000)
        {
            if (GS.FreeVectorX == 0x4000)
                m_moveMode = AxisX;
            else if (GS.FreeVectorY == 0x4000)
                m_moveMode = AxisY;
        }

        // At small sizes F_dot_P can become too small, giving overflows and spikes.
        if (Math.Abs(m_fDotP) < 0x400)
            m_fDotP = 0x4000;
    }

    /// <summary>The projection of (dx, dy) on the projection vector (<c>func_project</c>).</summary>
    private long Project(long dx, long dy) => m_projectMode switch
    {
        AxisX => dx,
        AxisY => dy,
        _ => HintingMath.DotFix14(dx, dy, GS.ProjVectorX, GS.ProjVectorY),
    };

    /// <summary>The projection of (dx, dy) on the dual projection vector (<c>func_dualproj</c>).</summary>
    private long DualProject(long dx, long dy) => m_dualMode switch
    {
        AxisX => dx,
        AxisY => dy,
        _ => HintingMath.DotFix14(dx, dy, GS.DualVectorX, GS.DualVectorY),
    };

    /// <summary>FreeType's <c>PROJECT</c> of two current points.</summary>
    private long ProjectCur(HintZone z1, int p1, HintZone z2, int p2) => Project(z1.CurX[p1] - z2.CurX[p2], z1.CurY[p1] - z2.CurY[p2]);

    /// <summary>FreeType's <c>DUALPROJ</c> of two original points.</summary>
    private long DualProjectOrg(HintZone z1, int p1, HintZone z2, int p2) =>
        DualProject(z1.OrgX[p1] - z2.OrgX[p2], z1.OrgY[p1] - z2.OrgY[p2]);

    /// <summary>The distance of two unscaled points in pixels, as MD, MDRP and IP measure it without the twilight zone.</summary>
    private long DualProjectOrus(HintZone z1, int p1, HintZone z2, int p2)
    {
        if (XScale == YScale)
            return HintingMath.MulFix(DualProject(z1.OrusX[p1] - z2.OrusX[p2], z1.OrusY[p1] - z2.OrusY[p2]), XScale);

        var x = HintingMath.MulFix(z1.OrusX[p1] - z2.OrusX[p2], XScale);
        var y = HintingMath.MulFix(z1.OrusY[p1] - z2.OrusY[p2], YScale);
        return DualProject(x, y);
    }

    /// <summary>FreeType's <c>func_move</c>: <c>Direct_Move</c>, <c>Direct_Move_X</c> or <c>Direct_Move_Y</c>.</summary>
    private void Move(HintZone zone, int point, long distance)
    {
        switch (m_moveMode)
        {
            case AxisX:
                if (!IsV40 || !BackwardCompatibility)
                    zone.CurX[point] += distance;

                zone.Tags[point] |= HintZone.TouchX;
                return;

            case AxisY:
                if (!PostIupFrozen)
                    zone.CurY[point] += distance;

                zone.Tags[point] |= HintZone.TouchY;
                return;
        }

        long v = GS.FreeVectorX;
        if (v != 0)
        {
            // v40 allows the x component of a diagonal move only outside backward compatibility; v35 always.
            if (!IsV40 || !BackwardCompatibility)
                zone.CurX[point] += HintingMath.MulDiv(distance, v, m_fDotP);

            zone.Tags[point] |= HintZone.TouchX;
        }

        v = GS.FreeVectorY;
        if (v != 0)
        {
            if (!PostIupFrozen)
                zone.CurY[point] += HintingMath.MulDiv(distance, v, m_fDotP);

            zone.Tags[point] |= HintZone.TouchY;
        }
    }

    /// <summary>FreeType's <c>func_move_orig</c>: moves an original point.</summary>
    private void MoveOrig(HintZone zone, int point, long distance)
    {
        switch (m_moveMode)
        {
            case AxisX:
                zone.OrgX[point] += distance;
                return;

            case AxisY:
                zone.OrgY[point] += distance;
                return;
        }

        long v = GS.FreeVectorX;
        if (v != 0)
            zone.OrgX[point] += HintingMath.MulDiv(distance, v, m_fDotP);

        v = GS.FreeVectorY;
        if (v != 0)
            zone.OrgY[point] += HintingMath.MulDiv(distance, v, m_fDotP);
    }

    /// <summary>FreeType's <c>Move_Zp2_Point</c>.</summary>
    private void MoveZp2Point(int point, long dx, long dy, bool touch)
    {
        if (GS.FreeVectorX != 0)
        {
            if (!(IsV40 && BackwardCompatibility))
                m_zp2.CurX[point] += dx;

            if (touch)
                m_zp2.Tags[point] |= HintZone.TouchX;
        }

        if (GS.FreeVectorY != 0)
        {
            if (!PostIupFrozen)
                m_zp2.CurY[point] += dy;

            if (touch)
                m_zp2.Tags[point] |= HintZone.TouchY;
        }
    }

    /// <summary>The current rounding function (<c>func_round</c>) on a distance; FreeType's engine compensation is 0.</summary>
    private long Round(long distance) => GS.RoundState switch
    {
        RoundMode.ToHalfGrid => RoundToHalfGrid(distance),
        RoundMode.ToGrid => RoundToGrid(distance),
        RoundMode.ToDoubleGrid => RoundToDoubleGrid(distance),
        RoundMode.DownToGrid => RoundDownToGrid(distance),
        RoundMode.UpToGrid => RoundUpToGrid(distance),
        RoundMode.Super => RoundSuper(distance),
        RoundMode.Super45 => RoundSuper45(distance),
        _ => distance,
    };

    private static long RoundToGrid(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d + 32) & ~63L;
            if (val < 0)
                val = 0;
        }
        else
        {
            val = -((-d + 32) & ~63L);
            if (val > 0)
                val = 0;
        }

        return val;
    }

    private static long RoundToHalfGrid(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d & ~63L) + 32;
            if (val < 0)
                val = 32;
        }
        else
        {
            val = -((-d & ~63L) + 32);
            if (val > 0)
                val = -32;
        }

        return val;
    }

    private static long RoundDownToGrid(long d)
    {
        long val;
        if (d >= 0)
        {
            val = d & ~63L;
            if (val < 0)
                val = 0;
        }
        else
        {
            val = -(-d & ~63L);
            if (val > 0)
                val = 0;
        }

        return val;
    }

    private static long RoundUpToGrid(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d + 63) & ~63L;
            if (val < 0)
                val = 0;
        }
        else
        {
            val = -((-d + 63) & ~63L);
            if (val > 0)
                val = 0;
        }

        return val;
    }

    private static long RoundToDoubleGrid(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d + 16) & ~31L;
            if (val < 0)
                val = 0;
        }
        else
        {
            val = -((-d + 16) & ~31L);
            if (val > 0)
                val = 0;
        }

        return val;
    }

    private long RoundSuper(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d + (m_threshold - m_phase)) & -m_period;
            val += m_phase;
            if (val < 0)
                val = m_phase;
        }
        else
        {
            val = -(((m_threshold - m_phase) - d) & -m_period);
            val -= m_phase;
            if (val > 0)
                val = -m_phase;
        }

        return val;
    }

    private long RoundSuper45(long d)
    {
        long val;
        if (d >= 0)
        {
            val = (d + (m_threshold - m_phase)) / m_period * m_period;
            val += m_phase;
            if (val < 0)
                val = m_phase;
        }
        else
        {
            val = -(((m_threshold - m_phase) - d) / m_period * m_period);
            val -= m_phase;
            if (val > 0)
                val = -m_phase;
        }

        return val;
    }

    /// <summary>FreeType's <c>SetSuperRound</c>.</summary>
    private void SetSuperRound(long gridPeriod, long selector)
    {
        switch ((int)(selector & 0xC0))
        {
            case 0:
                m_period = gridPeriod / 2;
                break;
            case 0x40:
                m_period = gridPeriod;
                break;
            case 0x80:
                m_period = gridPeriod * 2;
                break;
            case 0xC0:
                m_period = gridPeriod;
                break;
        }

        switch ((int)(selector & 0x30))
        {
            case 0:
                m_phase = 0;
                break;
            case 0x10:
                m_phase = m_period / 4;
                break;
            case 0x20:
                m_phase = m_period / 2;
                break;
            case 0x30:
                m_phase = m_period * 3 / 4;
                break;
        }

        if ((selector & 0x0F) == 0)
            m_threshold = m_period - 1;
        else
            m_threshold = ((int)(selector & 0x0F) - 4) * m_period / 8;

        m_period >>= 8;
        m_phase >>= 8;
        m_threshold >>= 8;
    }

    /// <summary>FreeType's <c>Normalize</c>: (vx, vy) as a 2.14 unit vector; (0, 0) leaves the vector unchanged.</summary>
    private static void Normalize(long vx, long vy, ref short rx, ref short ry)
    {
        if (vx == 0 && vy == 0)
            return;

        HintingMath.NormLen(ref vx, ref vy);
        rx = (short)(vx / 4);
        ry = (short)(vy / 4);
    }
}
