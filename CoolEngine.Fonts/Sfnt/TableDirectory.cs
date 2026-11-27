namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// One record of an sfnt table directory. The checksum is not kept: it is not verified, and two records that differ only in it
/// are the same table, parsed once.
/// </summary>
internal readonly record struct TableRecord(uint Tag, int Offset, int Length);

/// <summary>The tables of one face: the records of its directory over the (shared) file bytes.</summary>
internal sealed class TableDirectory
{
    private readonly byte[] m_data;
    private readonly Dictionary<uint, TableRecord> m_tables;

    public TableDirectory(byte[] data, uint sfntVersion, IReadOnlyList<TableRecord> records)
    {
        m_data = data;
        SfntVersion = sfntVersion;
        Records = records;
        // Of two records with one tag the first is used, as FreeType does; then a zero-length record makes its table absent, as
        // FreeType's tt_face_lookup_table treats it ("for compatibility with Windows"). Every record, zero-length ones too, has
        // passed the bounds checks.
        m_tables = [];
        foreach (var record in records)
            m_tables.TryAdd(record.Tag, record);

        foreach (var record in records)
        {
            if (m_tables.TryGetValue(record.Tag, out var first) && first.Length == 0)
                m_tables.Remove(record.Tag);
        }
    }

    /// <summary>The whole file (the tables lie in it).</summary>
    public byte[] Data => m_data;

    /// <summary>The face's sfnt version: <c>0x00010000</c>, <c>true</c> or <c>OTTO</c>.</summary>
    public uint SfntVersion { get; }

    /// <summary>The records in directory order.</summary>
    public IReadOnlyList<TableRecord> Records { get; }

    public bool Contains(uint tag) => m_tables.ContainsKey(tag);

    /// <summary>The table's bytes; empty when the face has no such table.</summary>
    public ReadOnlyMemory<byte> Get(uint tag) =>
        m_tables.TryGetValue(tag, out var record) ? m_data.AsMemory(record.Offset, record.Length) : ReadOnlyMemory<byte>.Empty;

    /// <summary>The table's bytes; <see cref="InvalidDataException"/> when the face has no such table.</summary>
    public ReadOnlyMemory<byte> GetRequired(uint tag)
    {
        var record = GetRequiredRecord(tag);
        return m_data.AsMemory(record.Offset, record.Length);
    }

    public bool TryGetRecord(uint tag, out TableRecord record) => m_tables.TryGetValue(tag, out record);

    /// <summary>The first record with this tag, zero-length or not; null when the directory has none.</summary>
    public TableRecord? FirstRecord(uint tag)
    {
        foreach (var record in Records)
        {
            if (record.Tag == tag)
                return record;
        }

        return null;
    }

    /// <summary>The table's record; <see cref="InvalidDataException"/> when the face has no such table.</summary>
    public TableRecord GetRequiredRecord(uint tag)
    {
        if (!m_tables.TryGetValue(tag, out var record))
            throw new InvalidDataException($"The font has no '{Tags.ToString(tag)}' table, which it requires.");

        return record;
    }
}
