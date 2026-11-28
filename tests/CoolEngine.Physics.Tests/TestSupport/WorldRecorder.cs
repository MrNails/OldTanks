using System.Numerics;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Records the bits of every live body's position, velocity, force and ground flag, tick after tick, so two runs can be
/// compared bit for bit (a NaN equals the same NaN).
/// </summary>
internal sealed class WorldRecorder
{
    private static readonly string[] s_fieldNames =
    [
        "position.X", "position.Y", "position.Z", "velocity.X", "velocity.Y", "velocity.Z",
        "force.X", "force.Y", "force.Z", "ground flag"
    ];

    private readonly List<uint> m_bits = new();
    private readonly List<(int Tick, BodyHandle Handle)> m_keys = new();

    public int Count => m_bits.Count;

    public void Record(PhysicsWorld world, int tick)
    {
        foreach (var handle in world)
        {
            m_keys.Add((tick, handle));
            Add(world.GetPosition(handle));
            Add(world.GetVelocity(handle));
            Add(world.GetForce(handle));
            m_bits.Add(world.IsOnGround(handle) ? 1u : 0u);
        }
    }

    /// <summary>Asserts that both recordings hold the same bits; the message names the first difference.</summary>
    public static void AssertSame(WorldRecorder expected, WorldRecorder actual, string what)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{what}: recorded value count");

        for (var i = 0; i < expected.Count; i++)
        {
            if (expected.m_bits[i] == actual.m_bits[i])
                continue;

            var (tick, handle) = expected.m_keys[i / s_fieldNames.Length];
            Assert.Fail($"{what}: tick {tick}, {handle}, {s_fieldNames[i % s_fieldNames.Length]} differs: " +
                        $"0x{expected.m_bits[i]:X8} against 0x{actual.m_bits[i]:X8}");
        }
    }

    private void Add(Vector3 value)
    {
        m_bits.Add(BitConverter.SingleToUInt32Bits(value.X));
        m_bits.Add(BitConverter.SingleToUInt32Bits(value.Y));
        m_bits.Add(BitConverter.SingleToUInt32Bits(value.Z));
    }
}
