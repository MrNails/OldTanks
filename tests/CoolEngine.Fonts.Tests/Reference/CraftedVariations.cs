namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>One tuple of a tuple variation store (<c>gvar</c> glyph data or <c>cvar</c>).</summary>
/// <param name="Peak">An embedded peak tuple (normalized, one per axis); null to use <paramref name="SharedIndex"/>.</param>
/// <param name="SharedIndex">The shared tuple used when <paramref name="Peak"/> is null.</param>
/// <param name="Start">The intermediate start tuple; null for none (then <paramref name="End"/> is null too).</param>
/// <param name="End">The intermediate end tuple.</param>
/// <param name="Points">Private point numbers; null to use the shared ones; empty for "all points".</param>
/// <param name="X">The x deltas (or the cvt deltas), one per point.</param>
/// <param name="Y">The y deltas; null for <c>cvar</c>.</param>
internal sealed record CraftedTuple(double[]? Peak, int SharedIndex, double[]? Start, double[]? End, int[]? Points, int[] X, int[]? Y);

/// <summary>Builders of variation tables for crafted variable fonts (OT-4 WP5), laid out as OpenType 1.9.1 lays them out.</summary>
internal static class CraftedVariations
{
    /// <summary>An <c>fvar</c> table (version 1.0, 20-byte axes, instances with a PostScript name ID when asked).</summary>
    public static byte[] Fvar((string Tag, double Min, double Default, double Max, int Flags, int NameId)[] axes,
        (int NameId, double[] Coordinates)[] instances, bool postScriptNames = false)
    {
        var instanceSize = 4 + 4 * axes.Length + (postScriptNames ? 2 : 0);
        var w = new BigEndianWriter().U32(0x00010000).U16(16).U16(2).U16(axes.Length).U16(20).U16(instances.Length).U16(instanceSize);
        foreach (var axis in axes)
            w.U32(Tag(axis.Tag)).U32((uint)Fixed(axis.Min)).U32((uint)Fixed(axis.Default)).U32((uint)Fixed(axis.Max)).U16(axis.Flags).U16(axis.NameId);

        foreach (var instance in instances)
        {
            w.U16(instance.NameId).U16(0);
            foreach (var value in instance.Coordinates)
                w.U32((uint)Fixed(value));

            if (postScriptNames)
                w.U16(0xFFFF);
        }

        return w.ToArray();
    }

    /// <summary>An <c>avar</c> table of the given version with one segment map per axis.</summary>
    public static byte[] Avar(uint version, params (double From, double To)[][] maps)
    {
        var w = new BigEndianWriter().U32(version).U16(0).U16(maps.Length);
        foreach (var map in maps)
        {
            w.U16(map.Length);
            foreach (var (from, to) in map)
                w.U16(F2Dot14(from)).U16(F2Dot14(to));
        }

        if (version == 0x00020000)
            w.U32(0).U32(0);

        return w.ToArray();
    }

    /// <summary>The usual avar map of one axis: -1 → -1, 0 → 0, 1 → 1 and the given inner points.</summary>
    public static (double, double)[] AvarMap(params (double From, double To)[] inner) =>
        [(-1, -1), .. inner.Where(pair => pair.From < 0), (0, 0), .. inner.Where(pair => pair.From > 0), (1, 1)];

    /// <summary>A tuple variation store: header, tuple headers, shared point numbers and each tuple's data.</summary>
    public static byte[] TupleStore(int axisCount, CraftedTuple[] tuples, int[]? sharedPoints, int headerPrefix = 0)
    {
        var headers = new BigEndianWriter();
        var data = new BigEndianWriter();
        if (sharedPoints is not null)
            data.Bytes(PackPoints(sharedPoints));

        foreach (var tuple in tuples)
        {
            var serialized = new BigEndianWriter();
            if (tuple.Points is not null)
                serialized.Bytes(PackPoints(tuple.Points));

            serialized.Bytes(PackDeltas(tuple.X));
            if (tuple.Y is not null)
                serialized.Bytes(PackDeltas(tuple.Y));

            var bytes = serialized.ToArray();
            var index = tuple.Peak is null ? tuple.SharedIndex : 0x8000;
            if (tuple.Start is not null)
                index |= 0x4000;

            if (tuple.Points is not null)
                index |= 0x2000;

            headers.U16(bytes.Length).U16(index);
            foreach (var value in tuple.Peak ?? [])
                headers.U16(F2Dot14(value));

            foreach (var value in tuple.Start ?? [])
                headers.U16(F2Dot14(value));

            foreach (var value in tuple.End ?? [])
                headers.U16(F2Dot14(value));

            data.Bytes(bytes);
        }

        var headerBytes = headers.ToArray();
        var count = tuples.Length | (sharedPoints is not null ? 0x8000 : 0);
        return new BigEndianWriter().U16(count).U16(headerPrefix + 4 + headerBytes.Length).Bytes(headerBytes).Bytes(data.ToArray()).ToArray();
    }

    /// <summary>A <c>gvar</c> table (long offsets) of <paramref name="glyphCount"/> glyphs, those in <paramref name="glyphs"/> with data.</summary>
    public static byte[] Gvar(int axisCount, double[][] sharedTuples, int glyphCount, IReadOnlyDictionary<int, byte[]> glyphs)
    {
        var sharedAt = 20 + 4 * (glyphCount + 1);
        var dataAt = sharedAt + 2 * axisCount * sharedTuples.Length;
        var w = new BigEndianWriter().U32(0x00010000).U16(axisCount).U16(sharedTuples.Length).U32((uint)sharedAt)
            .U16(glyphCount).U16(1).U32((uint)dataAt);
        var offset = 0;
        for (var g = 0; g <= glyphCount; g++)
        {
            w.U32((uint)offset);
            if (g < glyphCount && glyphs.TryGetValue(g, out var bytes))
                offset += bytes.Length;
        }

        foreach (var tuple in sharedTuples)
        {
            foreach (var value in tuple)
                w.U16(F2Dot14(value));
        }

        for (var g = 0; g < glyphCount; g++)
        {
            if (glyphs.TryGetValue(g, out var bytes))
                w.Bytes(bytes);
        }

        return w.ToArray();
    }

    /// <summary>A <c>cvar</c> table (version 1.0).</summary>
    public static byte[] Cvar(int axisCount, CraftedTuple[] tuples, int[]? sharedPoints) =>
        new BigEndianWriter().U32(0x00010000).Bytes(TupleStore(axisCount, tuples, sharedPoints, headerPrefix: 4)).ToArray();

    /// <summary>
    /// An item variation store: regions (start, peak, end per axis) and item variation data (region indices and one delta set
    /// per item, 16-bit words for every region when <c>Words</c>, else bytes).
    /// </summary>
    public static byte[] Store(int axisCount, (double Start, double Peak, double End)[][] regions,
        params (int[] Regions, int[][] Items, bool Words)[] data)
    {
        var regionList = new BigEndianWriter().U16(axisCount).U16(regions.Length);
        foreach (var region in regions)
        {
            foreach (var (start, peak, end) in region)
                regionList.U16(F2Dot14(start)).U16(F2Dot14(peak)).U16(F2Dot14(end));
        }

        var regionBytes = regionList.ToArray();
        var entries = data.Select(entry =>
        {
            var e = new BigEndianWriter().U16(entry.Items.Length).U16(entry.Words ? entry.Regions.Length : 0).U16(entry.Regions.Length);
            foreach (var index in entry.Regions)
                e.U16(index);

            foreach (var item in entry.Items)
            {
                foreach (var delta in item)
                {
                    if (entry.Words)
                        e.U16(delta & 0xFFFF);
                    else
                        e.U8(delta & 0xFF);
                }
            }

            return e.ToArray();
        }).ToArray();

        var header = 8 + 4 * data.Length;
        var w = new BigEndianWriter().U16(1).U32((uint)header).U16(data.Length);
        var at = header + regionBytes.Length;
        foreach (var entry in entries)
        {
            w.U32((uint)at);
            at += entry.Length;
        }

        w.Bytes(regionBytes);
        foreach (var entry in entries)
            w.Bytes(entry);

        return w.ToArray();
    }

    /// <summary>A delta-set index map (format 0, 16-bit inner indices, 4-byte entries) of (outer, inner) pairs.</summary>
    public static byte[] IndexMap(params (int Outer, int Inner)[] entries)
    {
        var w = new BigEndianWriter().U8(0).U8(0x3F).U16(entries.Length);
        foreach (var (outer, inner) in entries)
            w.U32((uint)(outer << 16 | inner));

        return w.ToArray();
    }

    /// <summary>An <c>HVAR</c> (or <c>VVAR</c>) table: the store, then the advance map if any.</summary>
    public static byte[] Hvar(byte[] store, byte[]? advanceMap = null)
    {
        var w = new BigEndianWriter().U16(1).U16(0).U32(20).U32(advanceMap is null ? 0 : (uint)(20 + store.Length)).U32(0).U32(0);
        w.Bytes(store);
        if (advanceMap is not null)
            w.Bytes(advanceMap);

        return w.ToArray();
    }

    /// <summary>An <c>MVAR</c> table: 8-byte value records, then the store.</summary>
    public static byte[] Mvar(byte[] store, params (string Tag, int Outer, int Inner)[] records) => MvarSized(store, 8, records);

    /// <summary>An <c>MVAR</c> table whose value records are <paramref name="recordSize"/> bytes (padded with 0x5A past 8), then the store.</summary>
    public static byte[] MvarSized(byte[] store, int recordSize, params (string Tag, int Outer, int Inner)[] records)
    {
        var storeAt = 12 + recordSize * records.Length;
        var w = new BigEndianWriter().U16(1).U16(0).U16(0).U16(recordSize).U16(records.Length).U16(records.Length == 0 ? 0 : storeAt);
        foreach (var (tag, outer, inner) in records)
        {
            w.U32(Tag(tag)).U16(outer).U16(inner);
            for (var i = 8; i < recordSize; i++)
                w.U8(0x5A);
        }

        return w.Bytes(store).ToArray();
    }

    /// <summary>A copy of the font with the table moved to the end of the file (so a read past it is a read past the file).</summary>
    public static byte[] TableLast(byte[] font, string tag)
    {
        var tables = CraftedFonts.ReadTables(font).OrderBy(table => table.Tag, StringComparer.Ordinal).ToList();
        var last = tables.Single(table => table.Tag == tag);
        tables.Remove(last);
        tables.Add(last);
        return CraftedFonts.Build(CraftedFonts.Version(font), tables, sort: false);
    }

    /// <summary>Packed point numbers; an empty list is "all points".</summary>
    public static byte[] PackPoints(int[] points)
    {
        var w = new BigEndianWriter();
        if (points.Length < 128)
            w.U8(points.Length);
        else
            w.U8(0x80 | points.Length >> 8).U8(points.Length & 0xFF);

        var previous = 0;
        for (var i = 0; i < points.Length;)
        {
            var run = Math.Min(128, points.Length - i);
            var words = Enumerable.Range(i, run).Any(j => points[j] - (j == 0 ? 0 : points[j - 1]) > 255);
            w.U8((words ? 0x80 : 0) | (run - 1));
            for (var j = i; j < i + run; j++)
            {
                var delta = points[j] - previous;
                previous = points[j];
                if (words)
                    w.U16(delta);
                else
                    w.U8(delta);
            }

            i += run;
        }

        return w.ToArray();
    }

    /// <summary>Packed deltas: runs of up to 64 zeros, bytes or words.</summary>
    public static byte[] PackDeltas(int[] deltas)
    {
        var w = new BigEndianWriter();
        for (var i = 0; i < deltas.Length;)
        {
            var run = Math.Min(64, deltas.Length - i);
            var values = deltas.AsSpan(i, run).ToArray();
            if (values.All(value => value == 0))
            {
                w.U8(0x80 | (run - 1));
            }
            else if (values.All(value => value is >= -128 and <= 127))
            {
                w.U8(run - 1);
                foreach (var value in values)
                    w.U8(value & 0xFF);
            }
            else
            {
                w.U8(0x40 | (run - 1));
                foreach (var value in values)
                    w.U16(value & 0xFFFF);
            }

            i += run;
        }

        return w.ToArray();
    }

    /// <summary>Arial with an <c>fvar</c> of the given axes (no instances) and the given extra tables.</summary>
    public static byte[] VariableArial((string Tag, double Min, double Default, double Max)[] axes, params CraftedTable[] tables)
    {
        var font = CraftedFonts.WithTable(CraftedFonts.Arial(), "fvar",
            Fvar(axes.Select(axis => (axis.Tag, axis.Min, axis.Default, axis.Max, 0, 256)).ToArray(), []));
        foreach (var table in tables)
            font = CraftedFonts.WithTable(font, table.Tag, table.Data);

        return font;
    }

    public static int F2Dot14(double value) => (int)Math.Round(value * 16384) & 0xFFFF;

    public static int Fixed(double value) => (int)Math.Round(value * 65536);

    public static uint Tag(string tag) => (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
}
