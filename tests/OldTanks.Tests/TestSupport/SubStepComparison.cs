using System.Globalization;
using System.Text;
using CoolEngine.PhysicEngine;
using CoolEngine.Services;
using OldTanks.Models;

namespace OldTanks.Tests.TestSupport;

/// <summary>The outcome of one terminal-speed drop onto Floor 1.</summary>
internal readonly record struct DropOutcome(int Phase, float FinalY, bool FinalOnGround, float MaxPreCorrectionDepth,
    float RestingJitter, int SettleTick);

/// <summary>The outcome of the default scene run at one sub-step count.</summary>
internal readonly record struct DefaultSceneOutcome(float CubeY, float SphereY, int SettleTick, float CubeJitter,
    float SphereJitter, int TicksOffGround);

/// <summary>
/// The metrics of the sub-step comparison (1, 2, 3, 5, 8 and 10 sub-steps): a body at terminal speed dropped onto the
/// 1 m thick Floor 1 from 16 start phases, and the default scene run for 600 ticks.
/// </summary>
internal static class SubStepComparison
{
    public static readonly int[] Counts = [1, 2, 3, 5, 8, 10];

    /// <summary>Start phases of a drop: they sweep the arrival over one sub-step's travel.</summary>
    public const int Phases = 16;

    /// <summary>Floor 1: its bottom face.</summary>
    public const float FloorBottom = -2.5f;

    // The resting window of a drop (ticks 61 to 120) and of the default scene (ticks 301 to 600).
    private const int DropWindowAfterTick = 60;
    private const int SceneWindowAfterTick = 300;

    public static float PhaseStartHeight(int subSteps, int phase) =>
        3f + phase * (PhysicsConstants.MaxFreeFallingSpeed * SimulationRun.TickSeconds / subSteps / Phases);

    /// <summary>
    /// Drops a body of size 1 (a cube or a sphere) at terminal speed and steps it <paramref name="ticks"/> ticks, one
    /// <see cref="World.CollideObjects"/> call per sub-step with <c>CollisionIterations</c> at 1 (bit-equal to one call with
    /// <paramref name="subSteps"/> sub-steps when no force is applied, which a test asserts). Leaves
    /// <c>CollisionIterations</c> at 1. <paramref name="afterTick"/> sees the world after each tick.
    /// </summary>
    public static DropOutcome RunTerminalDrop(bool sphere, int subSteps, int phase, int ticks,
        Action<int, World>? afterTick = null)
    {
        var world = SimulationScenes.CreateTerminalDropScene(sphere, PhaseStartHeight(subSteps, phase));
        var floor = world.WorldObjects[0];
        var body = world.WorldObjects[1];
        EngineSettings.Current.CollisionIterations = 1;

        // The first ApplyTransformation puts each collision into world space; the first depth measurement needs that.
        foreach (var worldObject in world.WorldObjects)
            worldObject.ApplyTransformation();

        var maxDepth = 0f;
        var settleTick = 0;
        var previous = SimulationRun.State(world);
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        for (var tick = 1; tick <= ticks; tick++)
        {
            for (var subStep = 0; subStep < subSteps; subStep++)
            {
                maxDepth = Math.Max(maxDepth, PreCorrectionDepth(floor, body, subSteps));
                world.CollideObjects(SimulationRun.TickSeconds / subSteps);
            }

            var state = SimulationRun.State(world);

            if (BitsDiffer(previous, state, 1))
                settleTick = tick;

            previous = state;

            if (tick > DropWindowAfterTick)
            {
                minY = Math.Min(minY, body.Position.Y);
                maxY = Math.Max(maxY, body.Position.Y);
            }

            afterTick?.Invoke(tick, world);
        }

        var jitter = minY <= maxY ? maxY - minY : 0f;
        return new DropOutcome(phase, body.Position.Y, body.RigidBody.OnGround, maxDepth, jitter, settleTick);
    }

    /// <summary>
    /// The depth the next sub-step is about to correct: a twin of <paramref name="body"/> (same type, size, rotation,
    /// position, velocity and force) is moved by the same <see cref="WorldObject.Move"/> and tested against the floor with
    /// the call <c>World.CollideObjects</c> makes for that pair (the floor is index 0). 0 when there is no contact.
    /// </summary>
    public static float PreCorrectionDepth(WorldObject floor, WorldObject body, int subSteps)
    {
        WorldObject twin;

        if (body is Sphere)
        {
            twin = SimulationScenes.CreateDynamicSphere(body.Position);
            twin.Size = body.Size;
        }
        else
        {
            twin = SimulationScenes.CreateCube(body.Size, body.Position, body.Rotation, null, false);
        }

        twin.Rotation = body.Rotation;
        twin.RigidBody.Velocity = body.RigidBody.Velocity;
        twin.RigidBody.Force = body.RigidBody.Force;

        twin.Move(SimulationRun.TickSeconds / subSteps, 0);
        twin.ApplyTransformation();

        return floor.Collision.CheckCollision(twin.Collision, out _, out var depth) ? depth : 0f;
    }

    /// <summary>
    /// The default scene stepped <paramref name="ticks"/> ticks at <paramref name="subSteps"/> sub-steps per call. Sets
    /// <c>CollisionIterations</c> to <paramref name="subSteps"/>. The resting window is ticks 301 to <paramref name="ticks"/>.
    /// </summary>
    public static DefaultSceneOutcome RunDefaultScene(int subSteps, int ticks)
    {
        EngineSettings.Current.CollisionIterations = subSteps;
        var world = SimulationScenes.CreateDefaultScene();
        var cube = world.WorldObjects[4];
        var sphere = world.WorldObjects[5];

        var settleTick = 0;
        var ticksOffGround = 0;
        var previous = SimulationRun.State(world);
        var cubeMin = float.MaxValue;
        var cubeMax = float.MinValue;
        var sphereMin = float.MaxValue;
        var sphereMax = float.MinValue;

        for (var tick = 1; tick <= ticks; tick++)
        {
            world.CollideObjects(SimulationRun.TickSeconds);

            var state = SimulationRun.State(world);

            if (BitsDiffer(previous, state, 4))
                settleTick = tick;

            previous = state;

            if (tick > SceneWindowAfterTick)
            {
                cubeMin = Math.Min(cubeMin, cube.Position.Y);
                cubeMax = Math.Max(cubeMax, cube.Position.Y);
                sphereMin = Math.Min(sphereMin, sphere.Position.Y);
                sphereMax = Math.Max(sphereMax, sphere.Position.Y);

                if (!cube.RigidBody.OnGround || !sphere.RigidBody.OnGround)
                    ticksOffGround++;
            }
        }

        var cubeJitter = cubeMin <= cubeMax ? cubeMax - cubeMin : 0f;
        var sphereJitter = sphereMin <= sphereMax ? sphereMax - sphereMin : 0f;

        return new DefaultSceneOutcome(cube.Position.Y, sphere.Position.Y, settleTick, cubeJitter, sphereJitter,
            ticksOffGround);
    }

    /// <summary>The terminal-drop comparison as a text table, one row per sub-step count; for the test output only.</summary>
    public static string FormatDropTable(IReadOnlyList<(int Count, DropOutcome CubeWorst, DropOutcome SphereWorst, int CubeRests, int CubeTunnels, int SphereRests, int SphereTunnels)> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "{0,5} | {1,9} | {2,12} | {3,12} | {4,9} | {5,9} | {6,7} | {7,6}",
            "Count", "Travel m", "Cube rests/t", "Sph rests/t", "Cube dep", "Sph dep", "Cube st", "Sph st"));

        foreach (var row in rows)
        {
            var travel = PhysicsConstants.MaxFreeFallingSpeed * SimulationRun.TickSeconds / row.Count;

            builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,5} | {1,9:F3} | {2,12} | {3,12} | {4,9:F4} | {5,9:F4} | {6,7} | {7,6}",
                row.Count, travel,
                $"{row.CubeRests}/{row.CubeTunnels}", $"{row.SphereRests}/{row.SphereTunnels}",
                row.CubeWorst.MaxPreCorrectionDepth, row.SphereWorst.MaxPreCorrectionDepth,
                row.CubeWorst.SettleTick, row.SphereWorst.SettleTick));
        }

        return builder.ToString();
    }

    /// <summary>The default-scene comparison as a text table, one row per sub-step count; for the test output only.</summary>
    public static string FormatSceneTable(IReadOnlyList<(int Count, DefaultSceneOutcome Scene)> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "{0,5} | {1,12} | {2,12} | {3,10} | {4,10} | {5,10} | {6,6}",
            "Count", "Cube Y", "Sphere Y", "Scene st", "Cube jit", "Sph jit", "Off gr"));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,5} | {1,12} | {2,12} | {3,10} | {4,10:E2} | {5,10:E2} | {6,6}",
                row.Count, row.Scene.CubeY.ToString("R", CultureInfo.InvariantCulture),
                row.Scene.SphereY.ToString("R", CultureInfo.InvariantCulture), row.Scene.SettleTick, row.Scene.CubeJitter,
                row.Scene.SphereJitter, row.Scene.TicksOffGround));
        }

        return builder.ToString();
    }

    // True when any position or velocity (the first six values of an object) of the objects from firstObject on differs
    // in any bit.
    private static bool BitsDiffer(float[] before, float[] after, int firstObject)
    {
        for (var i = firstObject * SimulationRun.ValuesPerObject; i < before.Length; i++)
        {
            if (i % SimulationRun.ValuesPerObject == SimulationRun.ValuesPerObject - 1)
                continue;

            if (BitConverter.SingleToInt32Bits(before[i]) != BitConverter.SingleToInt32Bits(after[i]))
                return true;
        }

        return false;
    }
}
