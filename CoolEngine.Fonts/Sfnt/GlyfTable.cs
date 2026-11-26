using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace CoolEngine.Fonts.Sfnt;

/// <summary>One component record of a composite glyph; the transform is 16.16 (the file's 2.14 times 4), as FreeType keeps it.</summary>
internal readonly record struct GlyphComponent(ushort Flags, int GlyphIndex, int Argument1, int Argument2, long Xx, long Xy,
    long Yx, long Yy);

/// <summary>
/// The <c>glyf</c> table with its <c>loca</c> index. <see cref="Parse"/> checks every glyph's structure and the composite graph
/// once, so the glyph loader can read any glyph afterwards without meeting malformed data.
/// </summary>
internal sealed class GlyfTable
{
    // Simple glyph flags.
    public const byte OnCurvePoint = 0x01;
    public const byte XShortVector = 0x02;
    public const byte YShortVector = 0x04;
    public const byte RepeatFlag = 0x08;
    public const byte XIsSameOrPositive = 0x10;
    public const byte YIsSameOrPositive = 0x20;
    public const byte OverlapSimple = 0x40;
    public const byte ReservedSimpleFlag = 0x80;

    // Composite glyph flags.
    public const ushort ArgsAreWords = 0x0001;
    public const ushort ArgsAreXyValues = 0x0002;
    public const ushort RoundXyToGrid = 0x0004;
    public const ushort WeHaveAScale = 0x0008;
    public const ushort MoreComponents = 0x0020;
    public const ushort WeHaveAnXyScale = 0x0040;
    public const ushort WeHaveATwoByTwo = 0x0080;
    public const ushort WeHaveInstructions = 0x0100;
    public const ushort UseMyMetrics = 0x0200;
    public const ushort OverlapCompound = 0x0400;
    public const ushort ScaledComponentOffset = 0x0800;

    public const int HeaderLength = 10;

    // An array's header, as accounted against the parsed-memory budget.
    private const int ArrayOverhead = 24;

    // The validation walk's arrays per glyph: points (4), component index arrays (a reference, 8), state (1), depth (4) and
    // expanded count (4).
    private const int WalkBytesPerGlyph = 21;

    /// <summary>Point numbers are 16-bit, so no glyph, composite or not, may have more points.</summary>
    public const int MaxPoints = 0xFFFF;

    /// <summary>The most component glyphs a composite may load in all, counting every level (a safety limit).</summary>
    public const int MaxExpandedComponents = 0xFFFF;

    /// <summary>The deepest composite nesting loaded (a safety limit: the glyph loader recurses once per level).</summary>
    public const int MaxNesting = 255;

    private readonly ReadOnlyMemory<byte> m_glyf;
    private readonly int[] m_offsets;

    private GlyfTable(ReadOnlyMemory<byte> glyf, int[] offsets)
    {
        m_glyf = glyf;
        m_offsets = offsets;
    }

    /// <param name="loca">The <c>loca</c> table.</param>
    /// <param name="glyf">The <c>glyf</c> table.</param>
    /// <param name="indexToLocFormat"><c>head.indexToLocFormat</c>, already checked to be 0 or 1.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c> (of a version 1.0 <c>maxp</c>).</param>
    /// <param name="parsed">The file's load state, against whose parsed-memory budget the arrays are accounted.</param>
    public static GlyfTable Parse(ReadOnlyMemory<byte> loca, ReadOnlyMemory<byte> glyf, int indexToLocFormat, int glyphCount,
        ParsedTables parsed)
    {
        parsed.Reserve(ArrayOverhead + 4L * (glyphCount + 1), "the 'loca' offsets");
        parsed.Charge((indexToLocFormat == 0 ? 2L : 4L) * (glyphCount + 1), "the 'loca' offsets");
        var offsets = ReadLoca(loca.Span, indexToLocFormat, glyphCount, glyf.Length);
        var table = new GlyfTable(glyf, offsets);
        table.Validate(glyphCount, parsed);

        return table;
    }

    /// <summary>The glyph's bytes; empty for a glyph with no outline.</summary>
    public ReadOnlySpan<byte> GetGlyph(int glyphIndex) =>
        m_glyf.Span[m_offsets[glyphIndex]..m_offsets[glyphIndex + 1]];

    /// <summary>
    /// Reads a simple glyph (<c>numberOfContours</c> &gt; 0). With <paramref name="buffer"/> null it only checks the structure.
    /// </summary>
    /// <returns>The number of points.</returns>
    public static int ReadSimple(ReadOnlySpan<byte> data, int glyphIndex, SimpleGlyph? buffer)
    {
        var r = new BigEndianReader(data, "glyf");
        var contourCount = r.I16(0);
        var offset = HeaderLength;

        r.Check(offset, 2L * contourCount + 2);
        var last = -1;
        for (var i = 0; i < contourCount; i++)
        {
            var end = r.U16(offset + 2 * i);
            if (end <= last)
                throw new InvalidDataException(
                    $"Glyph {glyphIndex}: contour {i} ends at point {end}, not after the previous contour's end {last}.");

            // Point numbers are 16-bit: at most MaxPoints points (0..0xFFFE), as for a composite.
            if (end >= MaxPoints)
                throw new InvalidDataException(
                    $"Glyph {glyphIndex}: contour {i} ends at point 65535, so the glyph has 65,536 points, more than 65535.");

            last = end;
        }

        var pointCount = last + 1;
        offset += 2 * contourCount;

        var instructionLength = r.U16(offset);
        offset += 2;
        if (!r.Contains(offset, instructionLength))
            throw new InvalidDataException(
                $"Glyph {glyphIndex}: its {instructionLength} bytes of instructions run past its {data.Length} bytes of data.");

        var instructionOffset = offset;
        offset += instructionLength;

        buffer?.Reset(pointCount, contourCount);
        if (buffer is not null)
        {
            for (var i = 0; i < contourCount; i++)
                buffer.ContourEnds[i] = r.U16(HeaderLength + 2 * i);

            buffer.InstructionOffset = instructionOffset;
            buffer.InstructionLength = instructionLength;
        }

        // Flags, with repeats; a repeat may not run past the point count.
        // A large glyph's flags go to a pooled array, so checking every glyph allocates nothing per glyph.
        var pooled = pointCount <= 1024 ? null : ArrayPool<byte>.Shared.Rent(pointCount);
        Span<byte> flags = pooled is null ? stackalloc byte[pointCount] : pooled.AsSpan(0, pointCount);
        for (var n = 0; n < pointCount;)
        {
            if (!r.Contains(offset, 1))
                throw FlagsTruncated(glyphIndex, data.Length);

            var flag = r.U8(offset++);
            if ((flag & ReservedSimpleFlag) != 0)
                throw new InvalidDataException($"Glyph {glyphIndex}: a point flag sets bit 7, which is reserved and must be 0.");

            flags[n++] = flag;
            if ((flag & RepeatFlag) == 0)
                continue;

            if (!r.Contains(offset, 1))
                throw FlagsTruncated(glyphIndex, data.Length);

            var repeat = r.U8(offset++);
            if (repeat > pointCount - n)
                throw new InvalidDataException(
                    $"Glyph {glyphIndex}: a flag repeats {repeat} times past its point count {pointCount}.");

            flags.Slice(n, repeat).Fill(flag);
            n += repeat;
        }

        offset = ReadCoordinates(r, offset, flags, XShortVector, XIsSameOrPositive, buffer?.X, glyphIndex, 'x');
        ReadCoordinates(r, offset, flags, YShortVector, YIsSameOrPositive, buffer?.Y, glyphIndex, 'y');

        if (buffer is not null)
            flags.CopyTo(buffer.Flags);

        if (pooled is not null)
            ArrayPool<byte>.Shared.Return(pooled);

        return pointCount;
    }

    /// <summary>
    /// Reads a composite glyph's component records, checking that each fits the data and names a valid other glyph.
    /// </summary>
    /// <param name="data">The glyph.</param>
    /// <param name="glyphIndex">Its index, for messages and the self-reference check.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    /// <param name="components">Receives the records; null to check only.</param>
    /// <returns>The offset and length of the composite's instructions (length 0 without <see cref="WeHaveInstructions"/>).</returns>
    public static (int Offset, int Length) ReadComposite(ReadOnlySpan<byte> data, int glyphIndex, int glyphCount,
        List<GlyphComponent>? components) =>
        ReadComposite(data, glyphIndex, glyphCount, components, out _);

    /// <inheritdoc cref="ReadComposite(ReadOnlySpan{byte}, int, int, List{GlyphComponent})"/>
    /// <param name="count">The number of component records.</param>
    public static (int Offset, int Length) ReadComposite(ReadOnlySpan<byte> data, int glyphIndex, int glyphCount,
        List<GlyphComponent>? components, out int count)
    {
        var r = new BigEndianReader(data, "glyf");
        var offset = HeaderLength;
        ushort flags;
        count = 0;

        do
        {
            if (!r.Contains(offset, 4))
                throw ComponentTruncated(glyphIndex, data.Length);

            flags = r.U16(offset);
            var index = r.U16(offset + 2);
            offset += 4;

            if (index >= glyphCount)
                throw new InvalidDataException($"Glyph {glyphIndex}: a component references glyph {index}; the font has {glyphCount}.");

            if (index == glyphIndex)
                throw new InvalidDataException($"Glyph {glyphIndex}: a component references the glyph itself.");

            var argumentLength = (flags & ArgsAreWords) != 0 ? 4 : 2;
            var transformLength = (flags & WeHaveAScale) != 0 ? 2
                : (flags & WeHaveAnXyScale) != 0 ? 4
                : (flags & WeHaveATwoByTwo) != 0 ? 8
                : 0;

            if (!r.Contains(offset, argumentLength + transformLength))
                throw ComponentTruncated(glyphIndex, data.Length);

            int argument1, argument2;
            if ((flags & ArgsAreXyValues) != 0)
            {
                (argument1, argument2) = (flags & ArgsAreWords) != 0
                    ? (r.I16(offset), r.I16(offset + 2))
                    : (r.I8(offset), r.I8(offset + 1));
            }
            else
            {
                (argument1, argument2) = (flags & ArgsAreWords) != 0
                    ? (r.U16(offset), r.U16(offset + 2))
                    : (r.U8(offset), r.U8(offset + 1));
            }

            offset += argumentLength;

            long xx = 0x10000, xy = 0, yx = 0, yy = 0x10000;
            if ((flags & WeHaveAScale) != 0)
            {
                xx = yy = r.I16(offset) * 4L;
            }
            else if ((flags & WeHaveAnXyScale) != 0)
            {
                xx = r.I16(offset) * 4L;
                yy = r.I16(offset + 2) * 4L;
            }
            else if ((flags & WeHaveATwoByTwo) != 0)
            {
                // The file order is xscale, scale01, scale10, yscale: x' = xx*x + xy*y, y' = yx*x + yy*y.
                xx = r.I16(offset) * 4L;
                yx = r.I16(offset + 2) * 4L;
                xy = r.I16(offset + 4) * 4L;
                yy = r.I16(offset + 6) * 4L;
            }

            offset += transformLength;
            components?.Add(new GlyphComponent(flags, index, argument1, argument2, xx, xy, yx, yy));
            count++;
        } while ((flags & MoreComponents) != 0);

        if ((flags & WeHaveInstructions) == 0)
            return (offset, 0);

        if (!r.Contains(offset, 2))
            throw ComponentTruncated(glyphIndex, data.Length);

        var length = r.U16(offset);
        if (!r.Contains(offset + 2, length))
            throw new InvalidDataException(
                $"Glyph {glyphIndex}: its {length} bytes of instructions run past its {data.Length} bytes of data.");

        return (offset + 2, length);
    }

    private static int[] ReadLoca(ReadOnlySpan<byte> loca, int indexToLocFormat, int glyphCount, int glyfLength)
    {
        var r = new BigEndianReader(loca, "loca");
        var entrySize = indexToLocFormat == 0 ? 2 : 4;
        var needed = (long)entrySize * (glyphCount + 1);
        if (r.Length < needed)
            throw new InvalidDataException($"The 'loca' table is {r.Length} bytes long; {glyphCount + 1} offsets need {needed}.");

        var offsets = new int[glyphCount + 1];
        long previous = 0;
        for (var i = 0; i <= glyphCount; i++)
        {
            long value = entrySize == 2 ? r.U16(2 * i) * 2L : r.U32(4 * i);
            if (value < previous)
                throw new InvalidDataException($"The 'loca' table's offset {i} ({value}) is smaller than offset {i - 1} ({previous}).");

            if (value > glyfLength)
                throw new InvalidDataException(
                    $"The 'loca' table's offset {i} ({value}) lies past the end of the {glyfLength}-byte 'glyf' table.");

            offsets[i] = (int)value;
            previous = value;
        }

        return offsets;
    }

    private static int ReadCoordinates(BigEndianReader r, int offset, ReadOnlySpan<byte> flags, byte shortFlag, byte sameFlag,
        int[]? output, int glyphIndex, char axis)
    {
        var value = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i];
            if ((flag & shortFlag) != 0)
            {
                if (!r.Contains(offset, 1))
                    throw CoordinatesTruncated(glyphIndex, axis, r.Length);

                var delta = r.U8(offset++);
                value += (flag & sameFlag) != 0 ? delta : -delta;
            }
            else if ((flag & sameFlag) == 0)
            {
                if (!r.Contains(offset, 2))
                    throw CoordinatesTruncated(glyphIndex, axis, r.Length);

                value += r.I16(offset);
                offset += 2;
            }

            if (output is not null)
                output[i] = value;
        }

        return offset;
    }

    private static InvalidDataException FlagsTruncated(int glyphIndex, int length) =>
        new($"Glyph {glyphIndex}: its point flags run past its {length} bytes of data.");

    private static InvalidDataException CoordinatesTruncated(int glyphIndex, char axis, int length) =>
        new($"Glyph {glyphIndex}: its {axis} coordinates run past its {length} bytes of data.");

    private static InvalidDataException ComponentTruncated(int glyphIndex, int length) =>
        new($"Glyph {glyphIndex}: a component record runs past its {length} bytes of data.");

    /// <summary>
    /// Checks every glyph, then walks the composite graph without recursion: no cycles, no anchor point out of range, at most
    /// <see cref="MaxPoints"/> points, nesting no deeper than <see cref="MaxNesting"/>. Nesting deeper than
    /// <c>maxp.maxComponentDepth</c> declares is accepted, as FreeType and OTS accept it (Denys's safety-strict decision of
    /// 2026-11-26): the cycle check and the two limits bound the work.
    /// </summary>
    private void Validate(int glyphCount, ParsedTables parsed)
    {
        // Only the component glyph indices are kept for the walk; the records are read again when a composite is finished.
        parsed.Reserve(5 * ArrayOverhead + (long)WalkBytesPerGlyph * glyphCount, "the 'glyf' check");
        var points = new int[glyphCount];
        var components = new ushort[glyphCount][];
        var list = new List<GlyphComponent>();

        for (var g = 0; g < glyphCount; g++)
        {
            var data = GetGlyph(g);
            if (data.Length == 0)
                continue;

            if (data.Length < HeaderLength)
                throw new InvalidDataException($"Glyph {g}: its {data.Length} bytes are shorter than the {HeaderLength}-byte header.");

            var contourCount = new BigEndianReader(data, "glyf").I16(0);
            if (contourCount > 0)
            {
                // Its bytes, and its points: repeated flags and repeated coordinates take no bytes.
                var endsLength = HeaderLength + 2 * contourCount;
                var declaredPoints = data.Length >= endsLength ? BinaryPrimitives.ReadUInt16BigEndian(data[(endsLength - 2)..]) + 1 : 0;
                parsed.Charge(data.Length + 2L * declaredPoints, "checking glyph", g);
                points[g] = ReadSimple(data, g, null);
            }
            else if (contourCount < 0)
            {
                // Its records are read three times: counted, kept, and read again when the composite is finished.
                parsed.Charge(3L * data.Length, "checking glyph", g);
                // Count first, so the record list grows only after its real size is accounted.
                ReadComposite(data, g, glyphCount, null, out var count);
                if (count > list.Capacity)
                {
                    parsed.Reserve(ArrayOverhead + (long)Unsafe.SizeOf<GlyphComponent>() * count, "the component records of glyph", g);
                    list.Capacity = count;
                }

                list.Clear();
                ReadComposite(data, g, glyphCount, list);
                parsed.Reserve(ArrayOverhead + 2L * list.Count, "the components of glyph", g);
                var indices = new ushort[list.Count];
                for (var i = 0; i < indices.Length; i++)
                    indices[i] = (ushort)list[i].GlyphIndex;

                components[g] = indices;
            }
        }

        // Depth-first over the composites. State: 0 unvisited, 1 on the current path, 2 done.
        var state = new byte[glyphCount];
        var depth = new int[glyphCount];
        var expanded = new int[glyphCount];
        var stack = new Stack<(int Glyph, int Next)>();

        for (var root = 0; root < glyphCount; root++)
        {
            if (state[root] != 0)
                continue;

            if (components[root] is null)
            {
                state[root] = 2;
                expanded[root] = 1;
                continue;
            }

            state[root] = 1;
            stack.Push((root, 0));
            while (stack.Count > 0)
            {
                var (glyph, next) = stack.Pop();
                var parts = components[glyph];
                if (next < parts.Length)
                {
                    stack.Push((glyph, next + 1));
                    var child = parts[next];
                    if (state[child] == 1)
                        throw new InvalidDataException($"Glyph {glyph}: its components form a cycle through glyph {child}.");

                    if (state[child] == 0)
                    {
                        if (components[child] is null)
                        {
                            state[child] = 2;
                            expanded[child] = 1;
                        }
                        else
                        {
                            state[child] = 1;
                            stack.Push((child, 0));
                        }
                    }

                    continue;
                }

                list.Clear();
                ReadComposite(GetGlyph(glyph), glyph, glyphCount, list);
                FinishComposite(glyph, list, points, depth, expanded);
                state[glyph] = 2;
            }
        }
    }

    private static void FinishComposite(int glyph, List<GlyphComponent> parts, int[] points, int[] depth, int[] expanded)
    {
        long total = 0;
        long count = 1;
        var level = 0;

        foreach (var part in parts)
        {
            var childPoints = points[part.GlyphIndex];

            // Anchor points: point Argument1 of the components loaded so far meets point Argument2 of this one. FreeType skips
            // the check (and the component) when the component has no points.
            if ((part.Flags & ArgsAreXyValues) == 0 && childPoints > 0)
            {
                if (part.Argument1 >= total)
                    throw new InvalidDataException(
                        $"Glyph {glyph}: anchor point {part.Argument1} is past the {total} points of the components before it.");

                if (part.Argument2 >= childPoints)
                    throw new InvalidDataException(
                        $"Glyph {glyph}: anchor point {part.Argument2} is past the {childPoints} points of component glyph {part.GlyphIndex}.");
            }

            total += childPoints;
            count += expanded[part.GlyphIndex];
            level = Math.Max(level, depth[part.GlyphIndex]);
        }

        if (total > MaxPoints)
            throw new InvalidDataException($"Glyph {glyph}: its components have {total} points in all, more than {MaxPoints}.");

        depth[glyph] = level + 1;
        if (depth[glyph] > MaxNesting)
            throw new NotSupportedException(
                $"Glyph {glyph}: its components nest {depth[glyph]} levels deep, more than this library's limit of {MaxNesting}.");

        if (count > MaxExpandedComponents)
            throw new NotSupportedException(
                $"Glyph {glyph}: it loads {count} component glyphs in all, more than this library's limit of {MaxExpandedComponents}.");

        points[glyph] = (int)total;
        expanded[glyph] = (int)count;
    }
}

/// <summary>A simple glyph read by <see cref="GlyfTable.ReadSimple"/>, in font units; reused between glyphs.</summary>
internal sealed class SimpleGlyph
{
    public int[] X { get; private set; } = [];

    public int[] Y { get; private set; } = [];

    public byte[] Flags { get; private set; } = [];

    public int[] ContourEnds { get; private set; } = [];

    public int PointCount { get; private set; }

    public int ContourCount { get; private set; }

    public int InstructionOffset { get; set; }

    public int InstructionLength { get; set; }

    public void Reset(int pointCount, int contourCount)
    {
        PointCount = pointCount;
        ContourCount = contourCount;
        if (X.Length < pointCount)
        {
            X = new int[pointCount];
            Y = new int[pointCount];
            Flags = new byte[pointCount];
        }

        if (ContourEnds.Length < contourCount)
            ContourEnds = new int[contourCount];
    }
}
