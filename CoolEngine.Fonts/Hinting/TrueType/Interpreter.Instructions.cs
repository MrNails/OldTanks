// Follows FreeType 2.13.2 src/truetype/ttinterp.c (the opcode switch of TT_RunIns and the Ins_* functions for the stack,
// arithmetic, control flow, definitions, the cvt, the storage and the graphics state) (FreeType Project License; credit in
// docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

internal sealed partial class Interpreter
{
    /// <summary>The opcode switch of <c>TT_RunIns</c>; <paramref name="a"/> is the index of the first argument on the stack.</summary>
    private void Execute(int a)
    {
        var s = m_stack;
        var opcode = m_opcode;
        switch (opcode)
        {
            case 0x00: case 0x01: case 0x02: case 0x03: case 0x04: case 0x05:
                SetVectorsToAxis(opcode);
                break;

            case 0x06: case 0x07:   // SPvTL
                if (SetVectorToLine((ushort)s[a + 1], (ushort)s[a], ref GS.ProjVectorX, ref GS.ProjVectorY))
                {
                    GS.DualVectorX = GS.ProjVectorX;
                    GS.DualVectorY = GS.ProjVectorY;
                    ComputeFuncs();
                }
                break;

            case 0x08: case 0x09:   // SFvTL
                if (SetVectorToLine((ushort)s[a + 1], (ushort)s[a], ref GS.FreeVectorX, ref GS.FreeVectorY))
                    ComputeFuncs();
                break;

            case 0x0A:              // SPvFS
                Normalize((short)s[a], (short)s[a + 1], ref GS.ProjVectorX, ref GS.ProjVectorY);
                GS.DualVectorX = GS.ProjVectorX;
                GS.DualVectorY = GS.ProjVectorY;
                ComputeFuncs();
                break;

            case 0x0B:              // SFvFS
                Normalize((short)s[a], (short)s[a + 1], ref GS.FreeVectorX, ref GS.FreeVectorY);
                ComputeFuncs();
                break;

            case 0x0C:              // GPv
                s[a] = GS.ProjVectorX;
                s[a + 1] = GS.ProjVectorY;
                break;

            case 0x0D:              // GFv
                s[a] = GS.FreeVectorX;
                s[a + 1] = GS.FreeVectorY;
                break;

            case 0x0E:              // SFvTPv
                GS.FreeVectorX = GS.ProjVectorX;
                GS.FreeVectorY = GS.ProjVectorY;
                ComputeFuncs();
                break;

            case 0x0F:
                InsIsect(a);
                break;

            case 0x10: GS.Rp0 = (ushort)s[a]; break;
            case 0x11: GS.Rp1 = (ushort)s[a]; break;
            case 0x12: GS.Rp2 = (ushort)s[a]; break;

            case 0x13: case 0x14: case 0x15: case 0x16:
                InsSetZonePointer(opcode, s[a]);
                break;

            case 0x17:              // SLOOP
                if (s[a] < 0)
                    m_error = InterpreterError.BadArgument;
                else
                    GS.Loop = s[a] > 0xFFFF ? 0xFFFF : s[a];
                break;

            case 0x18: GS.RoundState = RoundMode.ToGrid; break;
            case 0x19: GS.RoundState = RoundMode.ToHalfGrid; break;
            case 0x1A: GS.MinimumDistance = s[a]; break;
            case 0x1B: InsElse(); break;
            case 0x1C: InsJmpr(a); break;
            case 0x1D: GS.ControlValueCutIn = s[a]; break;
            case 0x1E: GS.SingleWidthCutIn = s[a]; break;
            case 0x1F: GS.SingleWidthValue = HintingMath.MulFix(s[a], Scale); break;

            case 0x20: s[a + 1] = s[a]; break;   // DUP
            case 0x21: break;                     // POP
            case 0x22: m_newTop = 0; break;       // CLEAR
            case 0x23: (s[a], s[a + 1]) = (s[a + 1], s[a]); break;   // SWAP
            case 0x24: s[a] = m_top; break;       // DEPTH

            case 0x25:              // CINDEX
            {
                var l = s[a];
                s[a] = l <= 0 || l > m_args ? 0 : s[m_args - l];
                break;
            }

            case 0x26:              // MINDEX
            {
                var l = s[a];
                if (l > 0 && l <= m_args)
                {
                    Charge(l);
                    var k = s[m_args - l];
                    Array.Copy(s, m_args - l + 1, s, m_args - l, l - 1);
                    s[m_args - 1] = k;
                }
                break;
            }

            case 0x27: InsAlignPts(a); break;
            case 0x29: InsUtp(a); break;
            case 0x2A: InsLoopCall(a); break;
            case 0x2B: InsCall(a); break;
            case 0x2C: InsFdef(a); break;
            case 0x2D: InsEndf(); break;
            case 0x2E: case 0x2F: InsMdap(a); break;
            case 0x30: case 0x31: InsIup(); break;
            case 0x32: case 0x33: InsShp(); break;
            case 0x34: case 0x35: InsShc(a); break;
            case 0x36: case 0x37: InsShz(a); break;
            case 0x38: InsShpix(a); break;
            case 0x39: InsIp(); break;
            case 0x3A: case 0x3B: InsMsirp(a); break;
            case 0x3C: InsAlignRp(); break;
            case 0x3D: GS.RoundState = RoundMode.ToDoubleGrid; break;
            case 0x3E: case 0x3F: InsMiap(a); break;

            case 0x40: InsNpushb(a); break;
            case 0x41: InsNpushw(a); break;

            case 0x42:              // WS
            {
                var i = (ulong)s[a];
                if (i < (ulong)m_storage.Length)
                {
                    // Glyph programs write to a copy of the storage that lasts for one glyph load.
                    if (m_iniRange == CodeRange.Glyph && m_storage != m_glyfStorage)
                    {
                        Charge(m_storageBase.Length);
                        if (m_glyfStorage is null || m_glyfStorage.Length != m_storageBase.Length)
                            m_glyfStorage = new long[m_storageBase.Length];

                        Array.Copy(m_storageBase, m_glyfStorage, m_storageBase.Length);
                        m_storage = m_glyfStorage;
                    }

                    m_storage[i] = s[a + 1];
                }
                break;
            }

            case 0x43:              // RS
            {
                var i = (ulong)s[a];
                s[a] = i < (ulong)m_storage.Length ? m_storage[i] : 0;
                break;
            }

            case 0x44:              // WCvtP
            {
                var i = (ulong)s[a];
                if (i < (ulong)m_cvt.Length)
                    WriteCvt((int)i, s[a + 1]);
                break;
            }

            case 0x45:              // RCvt
            {
                var i = (ulong)s[a];
                s[a] = i < (ulong)m_cvt.Length ? m_cvt[i] : 0;
                break;
            }

            case 0x46: case 0x47: InsGc(a); break;
            case 0x48: InsScfs(a); break;
            case 0x49: case 0x4A: InsMd(a); break;
            case 0x4B: s[a] = Ppem; break;                               // MPPEM
            case 0x4C: s[a] = IsV40 ? PointSize : Ppem; break;           // MPS
            case 0x4D: GS.AutoFlip = true; break;
            case 0x4E: GS.AutoFlip = false; break;
            case 0x4F: m_error = InterpreterError.DebugOpcode; break;   // DEBUG

            case 0x50: s[a] = s[a] < s[a + 1] ? 1 : 0; break;
            case 0x51: s[a] = s[a] <= s[a + 1] ? 1 : 0; break;
            case 0x52: s[a] = s[a] > s[a + 1] ? 1 : 0; break;
            case 0x53: s[a] = s[a] >= s[a + 1] ? 1 : 0; break;
            case 0x54: s[a] = s[a] == s[a + 1] ? 1 : 0; break;
            case 0x55: s[a] = s[a] != s[a + 1] ? 1 : 0; break;
            case 0x56: s[a] = (Round(s[a]) & 127) == 64 ? 1 : 0; break;  // ODD
            case 0x57: s[a] = (Round(s[a]) & 127) == 0 ? 1 : 0; break;   // EVEN
            case 0x58: InsIf(a); break;
            case 0x59: break;                                              // EIF
            case 0x5A: s[a] = s[a] != 0 && s[a + 1] != 0 ? 1 : 0; break;
            case 0x5B: s[a] = s[a] != 0 || s[a + 1] != 0 ? 1 : 0; break;
            case 0x5C: s[a] = s[a] == 0 ? 1 : 0; break;
            case 0x5D: case 0x71: case 0x72: InsDeltaP(a); break;
            case 0x5E: GS.DeltaBase = (ushort)s[a]; break;                // SDB

            case 0x5F:              // SDS
                if ((ulong)s[a] > 6)
                    m_error = InterpreterError.BadArgument;
                else
                    GS.DeltaShift = (ushort)s[a];
                break;

            case 0x60: s[a] += s[a + 1]; break;
            case 0x61: s[a] -= s[a + 1]; break;

            case 0x62:              // DIV
                if (s[a + 1] == 0)
                    m_error = InterpreterError.DivideByZero;
                else
                    s[a] = HintingMath.MulDivNoRound(s[a], 64, s[a + 1]);
                break;

            case 0x63: s[a] = HintingMath.MulDiv(s[a], s[a + 1], 64); break;
            case 0x64: s[a] = s[a] < 0 ? -s[a] : s[a]; break;
            case 0x65: s[a] = -s[a]; break;
            case 0x66: s[a] &= ~63L; break;                       // FLOOR
            case 0x67: s[a] = (s[a] + 63) & ~63L; break;          // CEILING
            case 0x68: case 0x69: case 0x6A: case 0x6B: s[a] = Round(s[a]); break;
            case 0x6C: case 0x6D: case 0x6E: case 0x6F: break;   // NROUND: Round_None with no compensation

            case 0x70:              // WCvtF
            {
                // FreeType writes the cvt in use: the size's own, unless a WCVTP or DELTAC of this glyph load made the copy.
                var i = (ulong)s[a];
                if (i < (ulong)m_cvt.Length)
                    m_cvt[i] = HintingMath.MulFix(s[a + 1], Scale);
                break;
            }

            case 0x73: case 0x74: case 0x75: InsDeltaC(a); break;

            case 0x76:              // SROUND
                SetSuperRound(0x4000, s[a]);
                GS.RoundState = RoundMode.Super;
                break;

            case 0x77:              // S45ROUND
                SetSuperRound(0x2D41, s[a]);
                GS.RoundState = RoundMode.Super45;
                break;

            case 0x78:              // JROT
                if (s[a + 1] != 0)
                    InsJmpr(a);
                break;

            case 0x79:              // JROF
                if (s[a + 1] == 0)
                    InsJmpr(a);
                break;

            case 0x7A: GS.RoundState = RoundMode.Off; break;
            case 0x7C: GS.RoundState = RoundMode.UpToGrid; break;
            case 0x7D: GS.RoundState = RoundMode.DownToGrid; break;
            case 0x7E: case 0x7F: break;                          // SANGW, AA
            case 0x80: InsFlipPt(); break;
            case 0x81: case 0x82: InsFlipRange(a, opcode == 0x81); break;
            case 0x85: InsScanCtrl(s[a]); break;
            case 0x86: case 0x87: InsSdpvtl(a); break;
            case 0x88: s[a] = GetInfo(s[a]); break;
            case 0x89: InsIdef(a); break;

            case 0x8A:              // ROLL
            {
                var x = s[a + 2];
                var y = s[a + 1];
                var z = s[a];
                s[a + 2] = z;
                s[a + 1] = x;
                s[a] = y;
                break;
            }

            case 0x8B: s[a] = Math.Max(s[a], s[a + 1]); break;
            case 0x8C: s[a] = Math.Min(s[a], s[a + 1]); break;

            case 0x8D:              // SCANTYPE
                if (s[a] >= 0)
                    GS.ScanType = (int)s[a] & 0xFFFF;
                break;

            case 0x8E: InsInstCtrl(a); break;

            case 0x91:              // GETVARIATION: the normalised coordinates, 16.16 to 2.14
                if (HasBlend)
                {
                    Charge(AxisCount);
                    for (var i = 0; i < AxisCount; i++)
                        s[a + i] = i < VariationCoordinates.Length ? VariationCoordinates[i] >> 2 : 0;
                }
                else
                {
                    InsUnknown();
                }
                break;

            case 0x92:              // GETDATA
                if (HasBlend)
                    s[a] = 17;
                else
                    InsUnknown();
                break;

            default:
                if (opcode >= 0xE0)
                    InsMirp(a);
                else if (opcode >= 0xC0)
                    InsMdrp(a);
                else if (opcode >= 0xB8)
                    InsPushw(a);
                else if (opcode >= 0xB0)
                    InsPushb(a);
                else
                    InsUnknown();
                break;
        }
    }

    /// <summary>FreeType's <c>Ins_SxyTCA</c>: SVTCA, SPvTCA, SFvTCA.</summary>
    private void SetVectorsToAxis(byte opcode)
    {
        var aa = (short)((opcode & 1) << 14);
        var bb = (short)(aa ^ 0x4000);

        if (opcode < 4)
        {
            GS.ProjVectorX = aa;
            GS.ProjVectorY = bb;
            GS.DualVectorX = aa;
            GS.DualVectorY = bb;
        }

        if ((opcode & 2) == 0)
        {
            GS.FreeVectorX = aa;
            GS.FreeVectorY = bb;
        }

        ComputeFuncs();
    }

    /// <summary>FreeType's <c>Ins_SxVTL</c>.</summary>
    private bool SetVectorToLine(ushort index1, ushort index2, ref short vx, ref short vy)
    {
        if (index1 >= m_zp2.PointCount || index2 >= m_zp1.PointCount)
            return false;

        var opcode = m_opcode;
        var a = m_zp1.CurX[index2] - m_zp2.CurX[index1];
        var b = m_zp1.CurY[index2] - m_zp2.CurY[index1];

        // If the points coincide, SPvTL and SFvTL act as SPvTCA[x] and SFvTCA[x].
        if (a == 0 && b == 0)
        {
            a = 0x4000;
            opcode = 0;
        }

        if ((opcode & 1) != 0)
        {
            var c = b;
            b = a;
            a = -c;
        }

        Normalize(a, b, ref vx, ref vy);
        return true;
    }

    /// <summary>FreeType's <c>Ins_SZP0</c>, <c>Ins_SZP1</c>, <c>Ins_SZP2</c>, <c>Ins_SZPS</c>.</summary>
    private void InsSetZonePointer(byte opcode, long value)
    {
        HintZone zone;
        switch ((int)value)
        {
            case 0:
                zone = Twilight;
                break;
            case 1:
                zone = Pts;
                break;
            default:
                return;
        }

        var gep = (ushort)value;
        switch (opcode)
        {
            case 0x13:
                m_zp0 = zone;
                GS.Gep0 = gep;
                break;
            case 0x14:
                m_zp1 = zone;
                GS.Gep1 = gep;
                break;
            case 0x15:
                m_zp2 = zone;
                GS.Gep2 = gep;
                break;
            default:
                m_zp0 = m_zp1 = m_zp2 = zone;
                GS.Gep0 = GS.Gep1 = GS.Gep2 = gep;
                break;
        }
    }

    /// <summary>FreeType's <c>Ins_IF</c>.</summary>
    private void InsIf(int a)
    {
        if (m_stack[a] != 0)
            return;

        var nIfs = 1;
        var done = false;
        do
        {
            if (!SkipCode())
                return;

            switch (m_opcode)
            {
                case 0x58:
                    nIfs++;
                    break;
                case 0x1B:
                    done = nIfs == 1;
                    break;
                case 0x59:
                    nIfs--;
                    done = nIfs == 0;
                    break;
            }
        } while (!done);
    }

    /// <summary>FreeType's <c>Ins_ELSE</c>.</summary>
    private void InsElse()
    {
        var nIfs = 1;
        do
        {
            if (!SkipCode())
                return;

            switch (m_opcode)
            {
                case 0x58:
                    nIfs++;
                    break;
                case 0x59:
                    nIfs--;
                    break;
            }
        } while (nIfs != 0);
    }

    /// <summary>FreeType's <c>Ins_JMPR</c> (also the taken branch of JROT and JROF).</summary>
    private void InsJmpr(int a)
    {
        var offset = m_stack[a];
        if (offset == 0 && m_args == 0)
        {
            m_error = InterpreterError.BadArgument;
            return;
        }

        m_ip += offset;
        if (m_ip < 0 || (m_callTop > 0 && m_ip > CurrentDefEnd()))
        {
            m_error = InterpreterError.BadArgument;
            return;
        }

        m_stepIns = false;

        if (offset < 0 && ++m_negJumpCounter > m_negJumpCounterMax)
            m_error = InterpreterError.ExecutionTooLong;
    }

    /// <summary>The end of the function or instruction definition the innermost call frame runs.</summary>
    private long CurrentDefEnd()
    {
        var frame = m_callStack[m_callTop - 1];
        return frame.IsInstruction ? m_instructionDefs[frame.Def].End : m_functionDefs[frame.Def].End;
    }

    /// <summary>FreeType's <c>Ins_NPUSHB</c>.</summary>
    private void InsNpushb(int a)
    {
        var count = (ushort)m_code[m_ip + 1];
        if ((uint)count >= (uint)(m_stack.Length + 1 - m_top))
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        for (var k = 1; k <= count; k++)
            m_stack[a + k - 1] = m_code[m_ip + k + 1];

        m_newTop += count;
    }

    /// <summary>FreeType's <c>Ins_NPUSHW</c>.</summary>
    private void InsNpushw(int a)
    {
        var count = (ushort)m_code[m_ip + 1];
        if ((uint)count >= (uint)(m_stack.Length + 1 - m_top))
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        m_ip += 2;
        for (var k = 0; k < count; k++)
            m_stack[a + k] = GetShortIns();

        m_stepIns = false;
        m_newTop += count;
    }

    /// <summary>FreeType's <c>Ins_PUSHB</c>.</summary>
    private void InsPushb(int a)
    {
        var count = m_opcode - 0xB0 + 1;
        if ((uint)count >= (uint)(m_stack.Length + 1 - m_top))
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        for (var k = 1; k <= count; k++)
            m_stack[a + k - 1] = m_code[m_ip + k];
    }

    /// <summary>FreeType's <c>Ins_PUSHW</c>.</summary>
    private void InsPushw(int a)
    {
        var count = m_opcode - 0xB8 + 1;
        if ((uint)count >= (uint)(m_stack.Length + 1 - m_top))
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        m_ip++;
        for (var k = 0; k < count; k++)
            m_stack[a + k] = GetShortIns();

        m_stepIns = false;
    }

    /// <summary>FreeType's <c>Write_CVT</c>: glyph programs write to a copy of the cvt that lasts for one glyph load.</summary>
    private void WriteCvt(int index, long value)
    {
        ModifyCvtCheck();
        m_cvt[index] = value;
    }

    /// <summary>FreeType's <c>Move_CVT</c>.</summary>
    private void MoveCvt(int index, long value)
    {
        ModifyCvtCheck();
        m_cvt[index] += value;
    }

    /// <summary>FreeType's <c>Modify_CVT_Check</c>.</summary>
    private void ModifyCvtCheck()
    {
        if (m_iniRange != CodeRange.Glyph || m_cvt == m_glyfCvt)
            return;

        Charge(m_cvtBase.Length);
        if (m_glyfCvt is null || m_glyfCvt.Length != m_cvtBase.Length)
            m_glyfCvt = new long[m_cvtBase.Length];

        Array.Copy(m_cvtBase, m_glyfCvt, m_cvtBase.Length);
        m_cvt = m_glyfCvt;
    }

    /// <summary>FreeType's <c>Ins_FDEF</c>.</summary>
    private void InsFdef(int a)
    {
        if (m_iniRange == CodeRange.Glyph)
        {
            m_error = InterpreterError.DefInGlyfBytecode;
            return;
        }

        var n = (ulong)m_stack[a];
        var index = n <= 0xFFFF && m_functionIndex.TryGetValue((int)n, out var found) ? found : m_numFunctionDefs;

        if (index == m_numFunctionDefs)
        {
            if (m_numFunctionDefs >= m_functionDefs.Length)
            {
                m_error = InterpreterError.TooManyFunctionDefs;
                return;
            }

            m_numFunctionDefs++;
        }

        // FDEF takes a 32-bit number, but function numbers must fit 16 bits.
        if (n > 0xFFFF)
        {
            m_error = InterpreterError.TooManyFunctionDefs;
            return;
        }

        ref var rec = ref m_functionDefs[index];
        rec.Range = m_curRange;
        rec.Opc = (long)n;
        rec.Start = m_ip + 1;
        rec.Active = true;
        m_functionIndex[(int)n] = index;

        if ((int)n > m_maxFunc)
            m_maxFunc = (int)n;

        // Skip the definition; FDEF and IDEF may not nest.
        while (SkipCode())
        {
            switch (m_opcode)
            {
                case 0x89:
                case 0x2C:
                    m_error = InterpreterError.NestedDefs;
                    return;
                case 0x2D:
                    rec.End = m_ip;
                    return;
            }
        }
    }

    /// <summary>FreeType's <c>Ins_IDEF</c>.</summary>
    private void InsIdef(int a)
    {
        if (m_iniRange == CodeRange.Glyph)
        {
            m_error = InterpreterError.DefInGlyfBytecode;
            return;
        }

        var opc = m_stack[a];
        var index = opc is >= 0 and <= 0xFF && m_instructionIndex[opc] >= 0 ? m_instructionIndex[opc] : m_numInstructionDefs;

        if (index == m_numInstructionDefs)
        {
            if (m_numInstructionDefs >= m_instructionDefs.Length)
            {
                m_error = InterpreterError.TooManyInstructionDefs;
                return;
            }

            m_numInstructionDefs++;
        }

        // The opcode must be an unsigned 8-bit integer.
        if (opc is < 0 or > 0xFF)
        {
            m_error = InterpreterError.TooManyInstructionDefs;
            return;
        }

        ref var def = ref m_instructionDefs[index];
        def.Opc = (byte)opc;
        def.Start = m_ip + 1;
        def.Range = m_curRange;
        def.Active = true;
        m_instructionIndex[opc] = index;

        if (opc > m_maxIns)
            m_maxIns = (int)opc;

        while (SkipCode())
        {
            switch (m_opcode)
            {
                case 0x89:
                case 0x2C:
                    m_error = InterpreterError.NestedDefs;
                    return;
                case 0x2D:
                    def.End = m_ip;
                    return;
            }
        }
    }

    /// <summary>The function definition CALL and LOOPCALL use, found as FreeType finds it; -1 when undefined.</summary>
    private int FindFunction(ulong f)
    {
        if (f >= (ulong)(m_maxFunc + 1))
            return -1;

        // Normally function n is entry n; otherwise FreeType searches the table (here, the index finds the same entry).
        var index = (int)f;
        if ((m_maxFunc + 1 != m_numFunctionDefs || (ulong)m_functionDefs[index].Opc != f) &&
            !m_functionIndex.TryGetValue((int)f, out index))
            return -1;

        return m_functionDefs[index].Active ? index : -1;
    }

    /// <summary>FreeType's <c>Ins_CALL</c>: an undefined function is <see cref="InterpreterError.InvalidReference"/>.</summary>
    private void InsCall(int a)
    {
        var index = FindFunction((ulong)m_stack[a]);
        if (index < 0)
        {
            m_error = InterpreterError.InvalidReference;
            return;
        }

        if (m_callTop >= CallStackSize)
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        m_callStack[m_callTop++] = new CallRecord
        {
            CallerRange = m_curRange,
            CallerIp = m_ip + 1,
            CurCount = 1,
            Def = index,
        };

        ref var def = ref m_functionDefs[index];
        GotoCodeRange(def.Range, def.Start);
        m_stepIns = false;
    }

    /// <summary>FreeType's <c>Ins_LOOPCALL</c>.</summary>
    private void InsLoopCall(int a)
    {
        var index = FindFunction((ulong)m_stack[a + 1]);
        if (index < 0)
        {
            m_error = InterpreterError.InvalidReference;
            return;
        }

        if (m_callTop >= CallStackSize)
        {
            m_error = InterpreterError.StackOverflow;
            return;
        }

        var count = m_stack[a];
        if (count <= 0)
            return;

        m_callStack[m_callTop++] = new CallRecord
        {
            CallerRange = m_curRange,
            CallerIp = m_ip + 1,
            CurCount = (int)count,
            Def = index,
        };

        ref var def = ref m_functionDefs[index];
        GotoCodeRange(def.Range, def.Start);
        m_stepIns = false;

        m_loopcallCounter += (ulong)count;
        if (m_loopcallCounter > m_loopcallCounterMax)
            m_error = InterpreterError.ExecutionTooLong;
    }

    /// <summary>FreeType's <c>Ins_ENDF</c>.</summary>
    private void InsEndf()
    {
        if (m_callTop <= 0)
        {
            m_error = InterpreterError.EndfInExecStream;
            return;
        }

        m_callTop--;
        ref var frame = ref m_callStack[m_callTop];
        frame.CurCount--;
        m_stepIns = false;

        if (frame.CurCount > 0)
        {
            m_callTop++;
            m_ip = frame.IsInstruction ? m_instructionDefs[frame.Def].Start : m_functionDefs[frame.Def].Start;
        }
        else
        {
            // Returning past the end of the caller's range is valid (a CALL as its last instruction); the result is not tested.
            GotoCodeRange(frame.CallerRange, frame.CallerIp);
        }
    }

    /// <summary>FreeType's <c>Ins_UNKNOWN</c>: an instruction definition for the opcode, else an invalid opcode.</summary>
    private void InsUnknown()
    {
        var index = m_instructionIndex[m_opcode];
        if (index >= 0 && m_instructionDefs[index].Active)
        {
            ref var def = ref m_instructionDefs[index];

            if (m_callTop >= CallStackSize)
            {
                m_error = InterpreterError.StackOverflow;
                return;
            }

            m_callStack[m_callTop++] = new CallRecord
            {
                CallerRange = m_curRange,
                CallerIp = m_ip + 1,
                CurCount = 1,
                IsInstruction = true,
                Def = index,
            };

            GotoCodeRange(def.Range, def.Start);
            m_stepIns = false;
            return;
        }

        m_error = InterpreterError.InvalidOpcode;
    }

    /// <summary>FreeType's <c>Ins_INSTCTRL</c>.</summary>
    private void InsInstCtrl(int a)
    {
        var k = (ulong)m_stack[a + 1];
        var l = (ulong)m_stack[a];

        // Selectors are indices 1..3, not flags.
        if (k is < 1 or > 3)
            return;

        var kf = 1UL << (int)(k - 1);
        if (l != 0 && l != kf)
            return;

        if (m_iniRange == CodeRange.Cvt)
        {
            GS.InstructControl &= (byte)~kf;
            GS.InstructControl |= (byte)l;
        }
        else if (m_iniRange == CodeRange.Glyph && k == 3)
        {
            // Selector 3 also works in a glyph program: it switches v40's backward compatibility for the glyph.
            if (IsV40)
                BackwardCompatibility = l != 4;
        }
    }

    /// <summary>FreeType's <c>Ins_SCANCTRL</c>; the scan control affects only monochrome rendering.</summary>
    private void InsScanCtrl(long value)
    {
        var threshold = (int)(value & 0xFF);
        if (threshold == 0xFF)
        {
            GS.ScanControl = true;
            return;
        }

        if (threshold == 0)
        {
            GS.ScanControl = false;
            return;
        }

        if ((value & 0x100) != 0 && Ppem <= threshold)
            GS.ScanControl = true;

        if ((value & 0x800) != 0 && Ppem > threshold)
            GS.ScanControl = false;
    }

    /// <summary>FreeType's <c>Ins_GETINFO</c>, with the answers of a grayscale rendering at <c>FT_LOAD_TARGET_NORMAL</c>.</summary>
    private long GetInfo(long selector)
    {
        long k = 0;

        // Rasteriser version.
        if ((selector & 1) != 0)
            k = m_version;

        // Glyph rotated (bit 8) and stretched (bit 9): never.

        // Variation glyph.
        if ((selector & 8) != 0 && HasBlend)
            k |= 1 << 10;

        // Grayscale rendering.
        if ((selector & 32) != 0 && Grayscale)
            k |= 1 << 12;

        if (IsV40 && SubpixelHintingLean)
        {
            // Hinting for subpixel rendering.
            if ((selector & 64) != 0)
                k |= 1 << 13;

            // Vertical LCD subpixels (bit 15): never at FT_LOAD_TARGET_NORMAL.

            // Subpixel positioned.
            if ((selector & 1024) != 0)
                k |= 1 << 17;

            // Symmetrical smoothing.
            if ((selector & 2048) != 0)
                k |= 1 << 18;

            // ClearType hinting and grayscale rendering.
            if ((selector & 4096) != 0 && GrayscaleCleartype)
                k |= 1 << 19;
        }

        return k;
    }
}
