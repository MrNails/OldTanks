// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (ft_var_readpackedpoints, ft_var_readpackeddeltas, ft_var_apply_tuple and the
// tuple loops of TT_Vary_Apply_Glyph_Deltas and tt_face_vary_cvt) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>A tuple variation store, named in messages only (nothing is formatted unless a rule fails).</summary>
/// <param name="Table">The table: <c>gvar</c> or <c>cvar</c>.</param>
/// <param name="Glyph">The glyph whose data it is; -1 for <c>cvar</c>.</param>
internal readonly record struct StoreName(string Table, int Glyph)
{
    public override string ToString() => Glyph < 0 ? $"'{Table}' table" : $"'{Table}' data of glyph {Glyph}";
}

/// <summary>One tuple variation header, as the tuple loop reads it.</summary>
/// <param name="TupleIndex">The tupleIndex field with its flags.</param>
/// <param name="PeakAt">Where the embedded peak tuple starts in the data; -1 for a shared tuple.</param>
/// <param name="IntermediateAt">Where the intermediate start and end tuples start; -1 without them.</param>
/// <param name="DataAt">Where the tuple's serialized data starts.</param>
/// <param name="DataSize">The serialized data's length.</param>
internal readonly record struct TupleHeader(int TupleIndex, int PeakAt, int IntermediateAt, int DataAt, int DataSize)
{
    public bool HasPrivatePoints => (TupleIndex & TupleVariations.PrivatePointNumbers) != 0;

    public bool IsIntermediate => (TupleIndex & TupleVariations.IntermediateRegion) != 0;

    public int SharedIndex => TupleIndex & TupleVariations.TupleIndexMask;
}

/// <summary>
/// The tuple variation store of <c>gvar</c> (one per glyph) and <c>cvar</c>: headers, packed point numbers and packed deltas,
/// checked (<see cref="Validate"/>, at load) and read (<see cref="ReadHeaders"/>, <see cref="ReadPoints"/>,
/// <see cref="ReadDeltas"/>, at glyph load or size set-up) as FreeType reads them. Stateless.
/// </summary>
internal static class TupleVariations
{
    public const int SharedPointNumbers = 0x8000;
    public const int TupleCountMask = 0x0FFF;
    public const int EmbeddedPeakTuple = 0x8000;
    public const int IntermediateRegion = 0x4000;
    public const int PrivatePointNumbers = 0x2000;
    public const int TupleIndexMask = 0x0FFF;

    /// <summary>
    /// Checks a tuple variation store: the header, the tuple headers and every tuple's serialized data (point numbers and
    /// deltas) inside the data, each run of points or deltas inside its count, each shared tuple index inside the shared tuples.
    /// </summary>
    /// <param name="data">The store: a glyph's variation data, or the <c>cvar</c> table.</param>
    /// <param name="headerAt">Where the tuple count lies (0 for a glyph, 4 for <c>cvar</c>).</param>
    /// <param name="axisCount">The axes each tuple has.</param>
    /// <param name="sharedTupleCount"><c>gvar</c>'s shared tuples.</param>
    /// <param name="allPoints">The deltas a tuple without point numbers has (the glyph's points and its phantom points; the cvt's entries).</param>
    /// <param name="dimensions">Delta sets per tuple: 2 (x and y) for <c>gvar</c>, 1 for <c>cvar</c>.</param>
    /// <param name="what">The store, for messages.</param>
    /// <param name="parsed">The file's load state: the walk is charged.</param>
    public static void Validate(ReadOnlySpan<byte> data, int headerAt, int axisCount, int sharedTupleCount, int allPoints,
        int dimensions, StoreName what, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, what.Table);
        if (!r.Contains(headerAt, 4))
            throw new InvalidDataException($"The {what} has its header past its {data.Length} bytes.");

        var tupleCount = r.U16(headerAt) & TupleCountMask;
        var dataOffset = r.U16(headerAt + 2);
        if (dataOffset > data.Length)
            throw new InvalidDataException($"The {what} has its serialized data at {dataOffset}, past its {data.Length} bytes.");

        parsed.Charge(4L + 4L * tupleCount, "the tuple variation headers of", tupleCount);
        var pos = (int)dataOffset;
        var hasShared = (r.U16(headerAt) & SharedPointNumbers) != 0;
        var sharedPoints = hasShared ? ValidatePoints(r, ref pos, data.Length, what, -1, parsed) : 0;

        var at = headerAt + 4;
        for (var i = 0; i < tupleCount; i++)
        {
            var header = ReadHeader(r, ref at, axisCount, what, i);
            if ((header.TupleIndex & EmbeddedPeakTuple) == 0 && header.SharedIndex >= sharedTupleCount)
                throw new InvalidDataException(
                    $"The {what}'s tuple {i} names shared tuple {header.SharedIndex}, but 'gvar' has {sharedTupleCount}.");

            if (!r.Contains(pos, header.DataSize))
                throw new InvalidDataException(
                    $"The {what}'s tuple {i} has {header.DataSize} bytes of data at {pos}, past its {data.Length} bytes.");

            // FreeType ignores such a tuple (it has no point numbers to read).
            if (!header.HasPrivatePoints && !hasShared)
                throw new InvalidDataException(
                    $"The {what}'s tuple {i} uses the shared point numbers, but the {what} has none.");

            var end = pos + header.DataSize;
            var p = pos;
            var points = header.HasPrivatePoints ? ValidatePoints(r, ref p, end, what, i, parsed) : sharedPoints;
            var count = points == 0 ? allPoints : points;

            for (var d = 0; d < dimensions; d++)
                ValidateDeltas(r, ref p, end, count, what, i, parsed);

            // Bytes left in the tuple's data are ignored, as FreeType ignores them.
            pos = end;
        }
    }

    /// <summary>Reads the tuple headers (checked at load) into <paramref name="headers"/>.</summary>
    /// <returns>The shared point numbers' position (-1 without them).</returns>
    public static int ReadHeaders(ReadOnlySpan<byte> data, int headerAt, int axisCount, List<TupleHeader> headers, out int dataStart)
    {
        var r = new BigEndianReader(data, "tuple variations");
        var tupleCount = r.U16(headerAt) & TupleCountMask;
        var shared = (r.U16(headerAt) & SharedPointNumbers) != 0;
        dataStart = r.U16(headerAt + 2);
        headers.Clear();

        var at = headerAt + 4;
        var pos = dataStart;
        var sharedAt = -1;
        if (shared)
        {
            sharedAt = pos;
            SkipPoints(r, ref pos);
        }

        for (var i = 0; i < tupleCount; i++)
        {
            var header = ReadHeader(r, ref at, axisCount, default, i);
            headers.Add(header with { DataAt = pos });
            pos += header.DataSize;
        }

        return sharedAt;
    }

    /// <summary>
    /// <c>ft_var_apply_tuple</c>: the scalar of one tuple at the normalized coordinates (16.16), 0 when it does not apply.
    /// </summary>
    public static long Scalar(ReadOnlySpan<byte> data, TupleHeader header, ReadOnlySpan<long> sharedTuples, ReadOnlySpan<long> coords)
    {
        var axisCount = coords.Length;
        long apply = 0x10000;
        for (var i = 0; i < axisCount; i++)
        {
            var peak = header.PeakAt >= 0 ? F2Dot14(data, header.PeakAt + 2 * i) : sharedTuples[header.SharedIndex * axisCount + i];
            if (peak == 0)
                continue;

            var coord = coords[i];
            if (coord == 0)
                return 0;

            if (coord == peak)
                continue;

            if (!header.IsIntermediate)
            {
                if (coord < Math.Min(0, peak) || coord > Math.Max(0, peak))
                    return 0;

                apply = FixedMath.MulDiv(apply, coord, peak);
            }
            else
            {
                var start = F2Dot14(data, header.IntermediateAt + 2 * i);
                var end = F2Dot14(data, header.IntermediateAt + 2 * (axisCount + i));
                if (coord <= start || coord >= end)
                    return 0;

                apply = coord < peak
                    ? FixedMath.MulDiv(apply, coord - start, peak - start)
                    : FixedMath.MulDiv(apply, end - coord, end - peak);
            }
        }

        return apply;
    }

    /// <summary>
    /// <c>ft_var_readpackedpoints</c> on checked data: the point numbers at <paramref name="pos"/> into <paramref name="points"/>.
    /// </summary>
    /// <returns>The number of points; 0 for "all points".</returns>
    public static int ReadPoints(ReadOnlySpan<byte> data, ref int pos, ref ushort[] points)
    {
        var r = new BigEndianReader(data, "tuple variations");
        int n = r.U8(pos++);
        if (n == 0)
            return 0;

        if ((n & 0x80) != 0)
            n = (n & 0x7F) << 8 | r.U8(pos++);

        if (points.Length < n)
            points = new ushort[n];

        ushort first = 0;
        var i = 0;
        while (i < n)
        {
            int run = r.U8(pos++);
            var words = (run & 0x80) != 0;
            run = (run & 0x7F) + 1;
            for (var j = 0; j < run; j++)
            {
                first += words ? r.U16(pos) : r.U8(pos);
                pos += words ? 2 : 1;
                points[i++] = first;
            }
        }

        return n;
    }

    /// <summary><c>ft_var_readpackeddeltas</c> on checked data: <paramref name="count"/> deltas into <paramref name="deltas"/>, 16.16.</summary>
    public static void ReadDeltas(ReadOnlySpan<byte> data, ref int pos, int count, long[] deltas)
    {
        var r = new BigEndianReader(data, "tuple variations");
        var i = 0;
        while (i < count)
        {
            int run = r.U8(pos++);
            var n = (run & 0x3F) + 1;
            for (var j = 0; j < n; j++)
            {
                long value = 0;
                if ((run & 0x80) == 0)
                {
                    value = (run & 0x40) != 0 ? r.I16(pos) : r.I8(pos);
                    pos += (run & 0x40) != 0 ? 2 : 1;
                }

                deltas[i++] = value << 16;
            }
        }
    }

    private static long F2Dot14(ReadOnlySpan<byte> data, int at) => (short)(data[at] << 8 | data[at + 1]) * 4L;

    private static string Which(int tuple) => tuple < 0 ? "shared" : $"tuple {tuple}'s";

    private static TupleHeader ReadHeader(BigEndianReader r, ref int at, int axisCount, StoreName what, int i)
    {
        if (!r.Contains(at, 4))
            throw new InvalidDataException($"The {what}'s tuple {i} header at {at} runs past its {r.Length} bytes.");

        int size = r.U16(at);
        int index = r.U16(at + 2);
        at += 4;
        var peakAt = -1;
        if ((index & EmbeddedPeakTuple) != 0)
        {
            if (!r.Contains(at, 2L * axisCount))
                throw new InvalidDataException($"The {what}'s tuple {i} has its peak tuple past its {r.Length} bytes.");

            peakAt = at;
            at += 2 * axisCount;
        }

        var intermediateAt = -1;
        if ((index & IntermediateRegion) != 0)
        {
            if (!r.Contains(at, 4L * axisCount))
                throw new InvalidDataException($"The {what}'s tuple {i} has its intermediate tuples past its {r.Length} bytes.");

            intermediateAt = at;
            at += 4 * axisCount;
        }

        return new TupleHeader(index, peakAt, intermediateAt, 0, size);
    }

    private static void SkipPoints(BigEndianReader r, ref int pos)
    {
        int n = r.U8(pos++);
        if (n == 0)
            return;

        if ((n & 0x80) != 0)
            n = (n & 0x7F) << 8 | r.U8(pos++);

        var i = 0;
        while (i < n)
        {
            int run = r.U8(pos++);
            var length = (run & 0x7F) + 1;
            pos += length * ((run & 0x80) != 0 ? 2 : 1);
            i += length;
        }
    }

    /// <returns>The number of points; 0 for "all points".</returns>
    private static int ValidatePoints(BigEndianReader r, ref int pos, int end, StoreName what, int tuple, ParsedTables parsed)
    {
        if (pos >= end)
            throw new InvalidDataException($"The {what}'s {Which(tuple)} point numbers at {pos} run past their data.");

        int n = r.U8(pos++);
        if (n == 0)
            return 0;

        if ((n & 0x80) != 0)
        {
            if (pos >= end)
                throw new InvalidDataException($"The {what}'s {Which(tuple)} point count at {pos - 1} runs past its data.");

            n = (n & 0x7F) << 8 | r.U8(pos++);
        }

        var i = 0;
        while (i < n)
        {
            if (pos >= end)
                throw new InvalidDataException($"The {what}'s {Which(tuple)} point numbers run past their data.");

            int run = r.U8(pos++);
            var length = (run & 0x7F) + 1;
            if (length > n - i)
                throw new InvalidDataException(
                    $"The {what}'s {Which(tuple)} point numbers have a run of {length} points where {n - i} of {n} remain.");

            var bytes = length * ((run & 0x80) != 0 ? 2 : 1);
            if (end - pos < bytes)
                throw new InvalidDataException($"The {what}'s {Which(tuple)} point numbers run past their data.");

            parsed.Charge(1 + bytes, "packed point numbers at", pos);
            pos += bytes;
            i += length;
        }

        return n;
    }

    private static void ValidateDeltas(BigEndianReader r, ref int pos, int end, int count, StoreName what, int tuple, ParsedTables parsed)
    {
        var i = 0;
        while (i < count)
        {
            if (pos >= end)
                throw new InvalidDataException($"The {what}'s tuple {tuple} has its deltas past its data ({i} of {count} read).");

            int run = r.U8(pos++);
            var n = (run & 0x3F) + 1;
            if (n > count - i)
                throw new InvalidDataException(
                    $"The {what}'s tuple {tuple} has a run of {n} deltas where {count - i} of {count} remain.");

            var bytes = (run & 0x80) != 0 ? 0 : n * ((run & 0x40) != 0 ? 2 : 1);
            if (end - pos < bytes)
                throw new InvalidDataException($"The {what}'s tuple {tuple} has its deltas past its data.");

            parsed.Charge(1 + bytes, "packed deltas at", pos);
            pos += bytes;
            i += n;
        }
    }
}
