namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>Builds <c>cmap</c> subtables, <c>cmap</c> tables, glyphs and <c>SVG </c> tables for crafted fonts.</summary>
internal static class CraftedTables
{
    /// <summary>
    /// A <c>cmap</c> table with these encoding records sorted by platform and encoding (as the format requires), each pointing
    /// to its own copy of the subtable.
    /// </summary>
    public static byte[] Cmap(params (int Platform, int Encoding, byte[] Subtable)[] records) =>
        CmapInOrder(records.OrderBy(record => (record.Platform, record.Encoding)).ToArray());

    /// <summary>A <c>cmap</c> table with these encoding records in the order given, sorted or not.</summary>
    public static byte[] CmapInOrder(params (int Platform, int Encoding, byte[] Subtable)[] records)
    {
        var w = new BigEndianWriter().U16(0).U16(records.Length);
        var offset = 4 + 8 * records.Length;
        foreach (var (platform, encoding, subtable) in records)
        {
            w.U16(platform).U16(encoding).U32((uint)offset);
            offset += subtable.Length;
        }

        foreach (var record in records)
            w.Bytes(record.Subtable);

        return w.ToArray();
    }

    /// <summary>A format 4 subtable; <paramref name="glyphIds"/> follows the four arrays.</summary>
    public static byte[] Format4((int Start, int End, int Delta, int RangeOffset)[] segments, ushort[]? glyphIds = null,
        int? segCountX2 = null)
    {
        glyphIds ??= [];
        var length = 16 + 8 * segments.Length + 2 * glyphIds.Length;
        var w = new BigEndianWriter().U16(4).U16(length).U16(0).U16(segCountX2 ?? 2 * segments.Length).U16(0).U16(0).U16(0);
        foreach (var s in segments)
            w.U16(s.End);

        w.U16(0);
        foreach (var s in segments)
            w.U16(s.Start);

        foreach (var s in segments)
            w.U16(s.Delta);

        foreach (var s in segments)
            w.U16(s.RangeOffset);

        foreach (var id in glyphIds)
            w.U16(id);

        return w.ToArray();
    }

    /// <summary>A valid format 4 subtable mapping 'A'..'Z' to glyphs 36..61 and ending with the U+FFFF segment.</summary>
    public static byte[] SimpleFormat4() => Format4([('A', 'Z', 36 - 'A', 0), (0xFFFF, 0xFFFF, 1, 0)]);

    /// <summary>A format 12 or 13 subtable.</summary>
    public static byte[] Groups(int format, params (uint Start, uint End, uint Glyph)[] groups)
    {
        var w = new BigEndianWriter().U16(format).U16(0).U32((uint)(16 + 12 * groups.Length)).U32(0).U32((uint)groups.Length);
        foreach (var g in groups)
            w.U32(g.Start).U32(g.End).U32(g.Glyph);

        return w.ToArray();
    }

    /// <summary>A format 14 subtable: per selector, its default ranges and non-default mappings.</summary>
    public static byte[] Format14(params (int Selector, (int Start, int Count)[] Defaults, (int Code, int Glyph)[] NonDefaults)[] records)
    {
        var header = 10 + 11 * records.Length;
        var body = new BigEndianWriter();
        var offsets = new List<(int Default, int NonDefault)>();
        foreach (var record in records)
        {
            var defaultOffset = 0;
            if (record.Defaults.Length > 0)
            {
                defaultOffset = header + body.Position;
                body.U32((uint)record.Defaults.Length);
                foreach (var (start, count) in record.Defaults)
                    body.U24(start).U8(count);
            }

            var nonDefaultOffset = 0;
            if (record.NonDefaults.Length > 0)
            {
                nonDefaultOffset = header + body.Position;
                body.U32((uint)record.NonDefaults.Length);
                foreach (var (code, glyph) in record.NonDefaults)
                    body.U24(code).U16(glyph);
            }

            offsets.Add((defaultOffset, nonDefaultOffset));
        }

        var bodyBytes = body.ToArray();
        var w = new BigEndianWriter().U16(14).U32((uint)(header + bodyBytes.Length)).U32((uint)records.Length);
        for (var i = 0; i < records.Length; i++)
            w.U24(records[i].Selector).U32((uint)offsets[i].Default).U32((uint)offsets[i].NonDefault);

        return w.Bytes(bodyBytes).ToArray();
    }

    /// <summary>A format 6 subtable with <paramref name="count"/> entries declared but only <paramref name="present"/> written.</summary>
    public static byte[] Format6(int first, int count, int present)
    {
        var w = new BigEndianWriter().U16(6).U16(10 + 2 * present).U16(0).U16(first).U16(count);
        for (var i = 0; i < present; i++)
            w.U16(1);

        return w.ToArray();
    }

    /// <summary>A format 10 subtable with <paramref name="count"/> entries declared but only <paramref name="present"/> written.</summary>
    public static byte[] Format10(int first, int count, int present)
    {
        var w = new BigEndianWriter().U16(10).U16(0).U32((uint)(20 + 2 * present)).U32(0).U32((uint)first).U32((uint)count);
        for (var i = 0; i < present; i++)
            w.U16(1);

        return w.ToArray();
    }

    /// <summary>A format 0 subtable cut to <paramref name="length"/> bytes (its declared length too).</summary>
    public static byte[] Format0(int length)
    {
        var w = new BigEndianWriter().U16(0).U16(length).U16(0);
        for (var i = 6; i < length; i++)
            w.U8(i < 6 + 256 ? i & 0x7F : 0);

        return w.ToArray();
    }

    /// <summary>A format 8 subtable header with a declared length that fits (format 8 is not read, only located).</summary>
    public static byte[] Format8() => new BigEndianWriter().U16(8).U16(0).U32(16).U32(0).U32(0).ToArray();

    /// <summary>A simple glyph: one contour per entry of <paramref name="contourEnds"/>, every point on the curve at short positive deltas.</summary>
    public static byte[] SimpleGlyph(int[] contourEnds, int instructionLength = 0, byte[]? instructions = null)
    {
        var points = contourEnds.Length == 0 ? 0 : contourEnds[^1] + 1;
        var w = new BigEndianWriter().U16(contourEnds.Length).U16(0).U16(0).U16(10).U16(10);
        foreach (var end in contourEnds)
            w.U16(end);

        w.U16(instructionLength);
        w.Bytes(instructions ?? new byte[instructionLength]);
        for (var i = 0; i < points; i++)
            w.U8(0x01 | 0x02 | 0x04 | 0x10 | 0x20);

        for (var i = 0; i < 2 * points; i++)
            w.U8(1);

        return w.ToArray();
    }

    /// <summary>A simple glyph from absolute points, every one on the curve, written with 16-bit deltas.</summary>
    public static byte[] SimpleGlyphFromPoints(params (int X, int Y)[][] contours)
    {
        var points = contours.SelectMany(contour => contour).ToArray();
        var w = new BigEndianWriter().U16(contours.Length)
            .U16(points.Min(p => p.X) & 0xFFFF).U16(points.Min(p => p.Y) & 0xFFFF)
            .U16(points.Max(p => p.X) & 0xFFFF).U16(points.Max(p => p.Y) & 0xFFFF);
        var end = -1;
        foreach (var contour in contours)
        {
            end += contour.Length;
            w.U16(end);
        }

        w.U16(0);
        foreach (var _ in points)
            w.U8(0x01);

        var previous = 0;
        foreach (var point in points)
        {
            w.U16((point.X - previous) & 0xFFFF);
            previous = point.X;
        }

        previous = 0;
        foreach (var point in points)
        {
            w.U16((point.Y - previous) & 0xFFFF);
            previous = point.Y;
        }

        return w.ToArray();
    }

    /// <summary>
    /// A composite glyph: components (glyph, flags, arguments) with 16-bit arguments; <c>MORE_COMPONENTS</c> is set between them.
    /// </summary>
    public static byte[] CompositeGlyph(params (int Glyph, int Flags, int Argument1, int Argument2)[] components)
    {
        var w = new BigEndianWriter().U16(0xFFFF).U16(0).U16(0).U16(10).U16(10);
        for (var i = 0; i < components.Length; i++)
        {
            var (glyph, flags, argument1, argument2) = components[i];
            flags |= 0x0001; // ARG_1_AND_2_ARE_WORDS
            if (i < components.Length - 1)
                flags |= 0x0020; // MORE_COMPONENTS

            w.U16(flags).U16(glyph).U16(argument1).U16(argument2);
        }

        return w.ToArray();
    }

    /// <summary>An <c>SVG </c> table: version 0, the document list right after the header, documents after the records.</summary>
    public static byte[] Svg(params (int Start, int End, byte[] Document)[] records)
    {
        const int listOffset = 10;
        var w = new BigEndianWriter().U16(0).U32(listOffset).U32(0).U16(records.Length);
        var offset = 2 + 12 * records.Length;
        foreach (var (start, end, document) in records)
        {
            w.U16(start).U16(end).U32((uint)offset).U32((uint)document.Length);
            offset += document.Length;
        }

        foreach (var record in records)
            w.Bytes(record.Document);

        return w.ToArray();
    }

    /// <summary>
    /// An <c>EBLC</c> (<paramref name="major"/> 2) or <c>CBLC</c> (3) table with one strike of <paramref name="ppem"/> pixels
    /// and no index subtables: a strike FreeType counts, with no bitmaps in it.
    /// </summary>
    public static byte[] BitmapLocations(int major, int ppem)
    {
        var w = new BigEndianWriter().U16(major).U16(0).U32(1);
        w.U32(8 + 48).U32(0).U32(0).U32(0);
        for (var direction = 0; direction < 2; direction++)
        {
            // SbitLineMetrics: ascender, descender (signed), widthMax, then nine bytes left 0.
            w.U8(ppem * 4 / 5).U8(-(ppem / 5) & 0xFF).U8(ppem);
            w.Bytes(new byte[9]);
        }

        // Glyphs 0..0, ppemX, ppemY, bit depth (32 for CBLC's colour bitmaps), flags (horizontal metrics).
        return w.U16(0).U16(0).U8(ppem).U8(ppem).U8(major == 3 ? 32 : 1).U8(1).ToArray();
    }

    /// <summary>An <c>EBDT</c> (<paramref name="major"/> 2) or <c>CBDT</c> (3) table header with no bitmaps.</summary>
    public static byte[] BitmapData(int major) => new BigEndianWriter().U16(major).U16(0).ToArray();

    /// <summary>An <c>sbix</c> table (version 1, flags 1) with one strike of <paramref name="ppem"/> pixels and no bitmaps.</summary>
    public static byte[] Sbix(int glyphCount, int ppem)
    {
        var w = new BigEndianWriter().U16(1).U16(1).U32(1).U32(12);
        var dataStart = 4 + 4 * (glyphCount + 1);
        w.U16(ppem).U16(72);
        for (var i = 0; i <= glyphCount; i++)
            w.U32((uint)dataStart);

        return w.ToArray();
    }
}
