using OldTanks.Models;

namespace OldTanks.Tests.TestSupport;

/// <summary>Steps a <see cref="World"/> as the game's loop does and records its state for the goldens.</summary>
internal static class SimulationRun
{
    /// <summary>One tick: 60 Hz (Denys's decision Q3, 2026-11-27).</summary>
    public const float TickSeconds = 1f / 60f;

    /// <summary>Sub-steps per tick (Denys's decision of 2026-11-27: 5).</summary>
    public const int SubSteps = 5;

    /// <summary>Position XYZ, velocity XYZ and OnGround (1 or 0) per object.</summary>
    public const int ValuesPerObject = 7;

    /// <summary>
    /// The state of every object in list order, statics included: <c>Position.X, .Y, .Z, RigidBody.Velocity.X, .Y, .Z</c>
    /// and <c>RigidBody.OnGround ? 1f : 0f</c>.
    /// </summary>
    public static float[] State(World world)
    {
        var state = new float[world.WorldObjects.Count * ValuesPerObject];

        for (var i = 0; i < world.WorldObjects.Count; i++)
        {
            var body = world.WorldObjects[i];
            var velocity = body.RigidBody.Velocity;
            var offset = i * ValuesPerObject;

            state[offset] = body.Position.X;
            state[offset + 1] = body.Position.Y;
            state[offset + 2] = body.Position.Z;
            state[offset + 3] = velocity.X;
            state[offset + 4] = velocity.Y;
            state[offset + 5] = velocity.Z;
            state[offset + 6] = body.RigidBody.OnGround ? 1f : 0f;
        }

        return state;
    }

    /// <summary>
    /// Runs ticks 1 to <c>ticks.Max()</c>, one <see cref="World.CollideObjects"/> each, and returns the state after each
    /// listed tick. <paramref name="beforeTick"/> runs first in every tick, where <c>WorldHandler</c> calls
    /// <c>HandleObjectMove</c>. <paramref name="ticks"/> is strictly ascending.
    /// </summary>
    public static float[][] Record(World world, int[] ticks, Action<int>? beforeTick = null)
    {
        var recorded = new float[ticks.Length][];
        var next = 0;

        for (var tick = 1; tick <= ticks[^1]; tick++)
        {
            beforeTick?.Invoke(tick);
            world.CollideObjects(TickSeconds);

            if (tick == ticks[next])
            {
                recorded[next] = State(world);
                next++;
            }
        }

        return recorded;
    }

    /// <summary>Compares every recorded tick with its golden row within the golden tolerance.</summary>
    public static void AssertGolden(int[] ticks, float[][] expected, float[][] actual, string scene)
    {
        Assert.That(expected, Has.Length.EqualTo(actual.Length),
            () => $"{scene}: expected {expected.Length} recorded ticks, actual {actual.Length}. actual as C#: " +
                  SimulationGoldenAssert.ToCSharpLiteral(actual));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < ticks.Length; i++)
                SimulationGoldenAssert.AreClose(expected[i], actual[i], $"{scene} tick {ticks[i]}");
        });
    }
}
