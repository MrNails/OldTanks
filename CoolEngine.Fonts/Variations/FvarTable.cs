// Follows FreeType 2.13.2 src/sfnt/sfobjs.c (sfnt_init_face: the 'fvar' header check that makes a face variable) and
// src/truetype/ttgxvar.c (TT_Get_MM_Var: the axes and named instances) (FreeType Project License; credit in docs).

using System.Collections.ObjectModel;
using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>One <c>fvar</c> axis as FreeType keeps it: 16.16 values, an inconsistent range collapsed to the default.</summary>
/// <param name="Tag">The axis tag.</param>
/// <param name="Minimum">The minimum, 16.16.</param>
/// <param name="Default">The default, 16.16.</param>
/// <param name="Maximum">The maximum, 16.16.</param>
/// <param name="Flags">The axis flags (bit 0: hidden).</param>
/// <param name="NameId">The <c>name</c> ID of the axis name.</param>
internal readonly record struct FvarAxis(uint Tag, long Minimum, long Default, long Maximum, ushort Flags, ushort NameId);

/// <summary>A face's public axes and named instances, read-only, built from one (<c>fvar</c>, <c>name</c>) pair.</summary>
internal sealed record FvarNames(IReadOnlyList<VariationAxis> Axes, IReadOnlyList<NamedInstance> Instances);

/// <summary>
/// The <c>fvar</c> table, read when its header is valid as FreeType's <c>sfnt_init_face</c> requires it (version 1.0, axis
/// records of 20 bytes, 1 to 16,382 axes, instance records of 4 + 4 × axes or 6 + 4 × axes bytes, at most 32,511 instances, all
/// records inside the table); a face whose <c>fvar</c> fails that check is not variable, as in FreeType. Immutable.
/// </summary>
/// <remarks>
/// <para>
/// Read as FreeType reads it: an axis whose minimum is above its default or whose default is above its maximum takes its
/// default as all three (<c>TT_Get_MM_Var</c>); the instances are those of the table (FreeType's synthesized default instance,
/// added when no instance lies at the defaults, is not added here: a documented difference).
/// </para>
/// <para>
/// Checked at load (<see cref="InvalidDataException"/>; Denys, 2026-11-27: "Reject at load"): an axis tag with a byte outside
/// 0x20..0x7E, or a tag that two axes share (coordinates are given by tag, so such an axis could not be set).
/// </para>
/// <para>
/// The structure is parsed once per file (<see cref="Parse"/>, no input besides its bytes). The public
/// <see cref="FontFace.Axes"/> and <see cref="FontFace.NamedInstances"/> need a face's <c>name</c> table, so
/// <see cref="BuildNames"/> builds them once per distinct (<c>fvar</c> record, <c>name</c> record) pair
/// (<see cref="ParsedTables.GetPair{T}"/>), their memory reserved against the file's budget first; faces with the same pair
/// share the read-only lists, faces sharing an <c>fvar</c> with names of their own get their own.
/// </para>
/// </remarks>
internal sealed class FvarTable
{
    public const ushort HiddenAxis = 0x0001;

    private const int AxisRecordLength = 20;
    private const int MaxAxes = 0x3FFE;
    private const int MaxInstances = 0x7EFF;

    // What BuildNames allocates per axis (a VariationAxis and its slots) and per instance (a NamedInstance, its float
    // coordinates and their read-only wrapper, its slot in the list, a name-ID list slot), arrays and objects included, rounded
    // up; plus the fixed name-ID bit set and the lists' wrappers. Measured on 2026-11-27 (VariationTests.Measure_FvarParseAllocation):
    // an fvar of 32,511 instances of two axes allocates 3,578,072 bytes in all, 110.1 per instance; these costs account 3.97 MB.
    private const int AxisCost = 128;
    private const int InstanceCost = 112;
    private const int InstanceCostPerAxis = 4;
    private const int FixedCost = 65_536 + 1024;

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int m_instancesStart;
    private readonly int m_instanceSize;
    private readonly int m_instanceCount;

    private FvarTable(ReadOnlyMemory<byte> data, FvarAxis[] axes, int instancesStart, int instanceSize, int instanceCount)
    {
        m_data = data;
        Axes = axes;
        m_instancesStart = instancesStart;
        m_instanceSize = instanceSize;
        m_instanceCount = instanceCount;
    }

    public FvarAxis[] Axes { get; }

    public int AxisCount => Axes.Length;

    /// <summary>The table, or null when its header fails FreeType's check (the face is then not variable).</summary>
    /// <param name="data">The <c>fvar</c> table.</param>
    /// <param name="parsed">The file's load state: the walk is charged, the arrays reserved.</param>
    /// <exception cref="InvalidDataException">An axis tag is not printable ASCII, or two axes share a tag.</exception>
    public static FvarTable? Parse(ReadOnlyMemory<byte> data, ParsedTables parsed)
    {
        // sfnt_init_face: a table shorter than 20 bytes reads as all zeros, which fails the check.
        if (data.Length < 20)
            return null;

        var r = new BigEndianReader(data.Span, "fvar");
        var version = r.U32(0);
        int offset = r.U16(4);
        int axisCount = r.U16(8);
        int axisSize = r.U16(10);
        int instanceCount = r.U16(12);
        int instanceSize = r.U16(14);

        if (version != 0x00010000 || axisSize != AxisRecordLength || axisCount == 0 || axisCount > MaxAxes ||
            !(instanceSize == 4 + 4 * axisCount || instanceSize == 6 + 4 * axisCount) || instanceCount > MaxInstances ||
            offset + (long)axisSize * axisCount + (long)instanceSize * instanceCount > data.Length)
            return null;

        parsed.Charge((long)axisSize * axisCount, "the 'fvar' axes");
        parsed.Reserve(64 + 64L * axisCount, "the 'fvar' axes");

        var axes = new FvarAxis[axisCount];
        var tags = new HashSet<uint>(axisCount);
        for (var i = 0; i < axisCount; i++)
        {
            var at = offset + AxisRecordLength * i;
            var tag = r.U32(at);
            for (var shift = 0; shift < 32; shift += 8)
            {
                if (((tag >> shift) & 0xFF) is < 0x20 or > 0x7E)
                    throw new InvalidDataException(
                        $"The 'fvar' table's axis {i} has the tag 0x{tag:X8}, with a byte outside printable ASCII (0x20..0x7E).");
            }

            if (!tags.Add(tag))
                throw new InvalidDataException($"The 'fvar' table's axis {i} repeats the tag '{Tags.ToString(tag)}' of an earlier axis.");

            long minimum = r.I32(at + 4), def = r.I32(at + 8), maximum = r.I32(at + 12);
            if (minimum > def || def > maximum)
                minimum = maximum = def;

            axes[i] = new FvarAxis(tag, minimum, def, maximum, r.U16(at + 16), r.U16(at + 18));
        }

        return new FvarTable(data, axes, offset + AxisRecordLength * axisCount, instanceSize, instanceCount);
    }

    /// <summary>
    /// The public axes and named instances with their names from <paramref name="name"/> (each distinct name ID decoded once
    /// into it), read-only. Called once per (<c>fvar</c> record, <c>name</c> record) pair; its memory is reserved first.
    /// </summary>
    public FvarNames BuildNames(NameTable name, ParsedTables parsed)
    {
        var axisCount = Axes.Length;
        parsed.Charge(2L * m_instanceSize * m_instanceCount, "the 'fvar' instances");
        parsed.Reserve(FixedCost + (long)AxisCost * axisCount + (InstanceCost + (long)InstanceCostPerAxis * axisCount) * m_instanceCount,
            "the 'fvar' records");

        var r = new BigEndianReader(m_data.Span, "fvar");
        var seen = new bool[ushort.MaxValue + 1];
        var nameIds = new List<ushort>();
        foreach (var axis in Axes)
            Add(axis.NameId);

        for (var i = 0; i < m_instanceCount; i++)
            Add(r.U16(m_instancesStart + m_instanceSize * i));

        name.DecodeMore(nameIds, parsed);

        var axes = new VariationAxis[axisCount];
        for (var i = 0; i < axisCount; i++)
        {
            var axis = Axes[i];
            axes[i] = new VariationAxis(Tags.ToString(axis.Tag), ToFloat(axis.Minimum), ToFloat(axis.Default),
                ToFloat(axis.Maximum), name.Get(axis.NameId) ?? "", (axis.Flags & HiddenAxis) != 0);
        }

        var instances = new NamedInstance[m_instanceCount];
        for (var i = 0; i < m_instanceCount; i++)
        {
            var at = m_instancesStart + m_instanceSize * i;
            var coordinates = new float[axisCount];
            for (var j = 0; j < axisCount; j++)
                coordinates[j] = ToFloat(r.I32(at + 4 + 4 * j));

            instances[i] = new NamedInstance(name.Get(r.U16(at)) ?? "", new ReadOnlyCollection<float>(coordinates));
        }

        return new FvarNames(new ReadOnlyCollection<VariationAxis>(axes), new ReadOnlyCollection<NamedInstance>(instances));

        void Add(ushort nameId)
        {
            if (!seen[nameId])
            {
                seen[nameId] = true;
                nameIds.Add(nameId);
            }
        }
    }

    /// <summary>A 16.16 value as the nearest float.</summary>
    private static float ToFloat(long value) => (float)(value / 65536.0);
}
