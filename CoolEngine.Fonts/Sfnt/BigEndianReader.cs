using System.Buffers.Binary;

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// Bounds-checked big-endian reads from one table (or the whole file). A read past the end throws
/// <see cref="InvalidDataException"/> naming the table, so malformed data never escapes as an index exception.
/// </summary>
internal readonly ref struct BigEndianReader
{
    private readonly ReadOnlySpan<byte> m_data;
    private readonly string m_name;

    /// <param name="data">The bytes to read.</param>
    /// <param name="name">What they are, for messages: a table tag such as <c>cmap</c>, or <c>font</c>.</param>
    public BigEndianReader(ReadOnlySpan<byte> data, string name)
    {
        m_data = data;
        m_name = name;
    }

    public int Length => m_data.Length;

    public ReadOnlySpan<byte> Span => m_data;

    public string Name => m_name;

    public byte U8(int offset)
    {
        Check(offset, 1);
        return m_data[offset];
    }

    public sbyte I8(int offset) => (sbyte)U8(offset);

    public ushort U16(int offset)
    {
        Check(offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(m_data[offset..]);
    }

    public short I16(int offset) => (short)U16(offset);

    public uint U24(int offset)
    {
        Check(offset, 3);
        return (uint)(m_data[offset] << 16 | m_data[offset + 1] << 8 | m_data[offset + 2]);
    }

    public uint U32(int offset)
    {
        Check(offset, 4);
        return BinaryPrimitives.ReadUInt32BigEndian(m_data[offset..]);
    }

    public int I32(int offset) => (int)U32(offset);

    /// <summary>The bytes at <paramref name="offset"/>, which must lie inside the data.</summary>
    public ReadOnlySpan<byte> Slice(long offset, long length)
    {
        Check(offset, length);
        return m_data.Slice((int)offset, (int)length);
    }

    /// <summary>A reader over part of the data, under another name.</summary>
    public BigEndianReader Sub(long offset, long length, string name) => new(Slice(offset, length), name);

    /// <summary>Whether <paramref name="length"/> bytes at <paramref name="offset"/> lie inside the data.</summary>
    public bool Contains(long offset, long length) => offset >= 0 && length >= 0 && offset <= m_data.Length - length;

    /// <summary>Throws unless <paramref name="length"/> bytes at <paramref name="offset"/> lie inside the data.</summary>
    public void Check(long offset, long length)
    {
        if (!Contains(offset, length))
            throw new InvalidDataException(
                $"The '{m_name}' data is too short: {length} bytes at offset {offset} run past its {m_data.Length} bytes.");
    }
}
