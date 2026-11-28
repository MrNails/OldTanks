using System.Numerics;
using CoolEngine.Mathematics;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>One body as today's game makes it: a Cube or a Sphere with its rigid-body settings.</summary>
internal sealed record BodySpec(bool IsSphere, bool IsStatic, Vector3 Position, Vector3 Rotation, Vector3 Size,
    Vector3 Velocity, float Weight, float MaxSpeed, float MaxBackSpeed, float MaxSpeedMultiplier);

/// <summary>
/// A change applied to both worlds before the step of <see cref="Tick"/>, to the body in slot <see cref="Body"/>
/// (ignored for <see cref="Add"/>): a force added (static bodies skipped, as today's setter ignores it), a new rotation,
/// static or dynamic, a removal, or a new body (which takes the slot the library gives it). <see cref="Velocity"/> is a
/// write through today's <c>RigidBody.Velocity</c> setter (clamped forward, ignored for a static body).
/// </summary>
internal sealed record TickEvent(int Tick, int Body, Vector3? Force, Vector3? Rotation, bool? MakeStatic, bool Remove,
    BodySpec? Add, Vector3? Velocity = null);

/// <summary>A scene for the parity harness: bodies in slot order, events and the tick count.</summary>
internal sealed record SceneDescription(string Name, IReadOnlyList<BodySpec> Bodies, IReadOnlyList<TickEvent> Events,
    int Ticks);

/// <summary>The parity harness's scenes (the WP2 probe's, plus the static toggle and the removal and add).</summary>
internal static class Scenes
{
    private static readonly BodySpec s_floor =
        new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20), Vector3.Zero, 1, 0, 0, 0);

    /// <summary>OldTanks.Windows.MainWindow.InitDefaultObjects: two walls, a floor, a static sphere, a cube and a sphere.</summary>
    public static SceneDescription Default() => new SceneDescription("default scene",
    [
        new BodySpec(false, true, new Vector3(0, 0, -10), Vector3.Zero, new Vector3(10, 2, 5), Vector3.Zero, 1, 0, 0, 0),
        new BodySpec(false, true, new Vector3(9, 0, 0), Vector3.Zero, new Vector3(1, 3, 20), Vector3.Zero, 1, 0, 0, 0),
        s_floor,
        new BodySpec(true, true, new Vector3(0, 5, 0), Vector3.Zero, new Vector3(1, 1, 1), Vector3.Zero, 1, 0, 0, 0),
        new BodySpec(false, false, new Vector3(0, 2, 1.5f), new Vector3(MathHelper.DegreesToRadians(45), 0, 0),
            new Vector3(1, 1, 1), Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(0, 5, 0), Vector3.Zero, new Vector3(1, 1, 1), Vector3.Zero, 1, 2, 2, 1),
    ], [], 1200);

    /// <summary>A cube falling from y = 300 at −50 m/s and a sphere thrown up at +200 m/s: the terminal speed both ways.</summary>
    public static SceneDescription TerminalSpeed() => new SceneDescription("terminal speed",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(0, 300, 0), new Vector3(0.3f, 0.7f, 0), new Vector3(1, 1, 1),
            new Vector3(1, -50, 0.5f), 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(3, 0, 0), Vector3.Zero, new Vector3(1, 1, 1), new Vector3(0, 200, 0), 1, 2,
            2, 1),
    ], [], 900);

    /// <summary>Forward and back forces, a jump force (0, 250, 0) on the ground, heading changes, a multiplier of 3.</summary>
    public static SceneDescription ForcesHeadingsAndJump() => new SceneDescription("forces, headings and jump",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(0, 0, 0), new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1), Vector3.Zero, 1,
            2, 2, 1),
        new BodySpec(false, false, new Vector3(2, 0, 0), new Vector3(0, -1.2f, 0.2f), new Vector3(1, 2, 1), Vector3.Zero,
            2, 2, 2, 3),
    ],
    [
        Force(60, 1, new Vector3(15, 0, 0)),
        Force(61, 1, new Vector3(15, 0, 0)),
        Force(90, 1, new Vector3(0, 250, 0)),
        Rotate(120, 1, new Vector3(0, 1.5f, 0)),
        Force(121, 1, new Vector3(-15, 0, 0)),
        Force(150, 2, new Vector3(15, 0, 3)),
        Rotate(180, 2, new Vector3(0, 2.5f, -0.1f)),
        Force(181, 2, new Vector3(15, 0, 0)),
        Force(182, 2, new Vector3(15, 0, 0)),
    ], 600);

    /// <summary>A stack of two cubes and two spheres on the floor.</summary>
    public static SceneDescription DynamicAgainstDynamic() => new SceneDescription("dynamic against dynamic",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(0, 0, 0), Vector3.Zero, new Vector3(1, 1, 1), new Vector3(1, 0, 0), 1, 2, 2,
            1),
        new BodySpec(false, false, new Vector3(0, 1.2f, 0), new Vector3(0.2f, 0.4f, 0), new Vector3(1, 1, 1),
            Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(0.3f, 3, 0.1f), Vector3.Zero, new Vector3(1, 1, 1), Vector3.Zero, 1, 2, 2,
            1),
        new BodySpec(true, false, new Vector3(0.1f, 4.5f, 0.2f), Vector3.Zero, new Vector3(0.8f, 0.8f, 0.8f),
            Vector3.Zero, 1, 2, 2, 1),
    ], [], 900);

    /// <summary>A moving cube pushed by a force, made static at tick 200 and dynamic again at tick 260.</summary>
    public static SceneDescription StaticToggled() => new SceneDescription("static toggled",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(-3, 0, 0), new Vector3(0, 0.3f, 0), new Vector3(1, 1, 1),
            new Vector3(1.5f, 0, 0), 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(3, 1, 0), Vector3.Zero, new Vector3(1, 1, 1), new Vector3(-1, 0, 0), 1, 2, 2,
            1),
    ],
    [
        Force(150, 1, new Vector3(15, 0, 0)),
        Force(199, 1, new Vector3(15, 0, 0)),
        new TickEvent(200, 1, null, null, true, false, null),
        new TickEvent(230, 1, new Vector3(15, 0, 0), null, null, false, null),
        new TickEvent(260, 1, null, null, false, false, null),
        Force(261, 1, new Vector3(-15, 0, 0)),
    ], 600);

    /// <summary>The body in slot 2 removed at tick 100 and a cube added at tick 101 (it takes slot 2).</summary>
    public static SceneDescription RemoveAndAdd() => new SceneDescription("remove and add",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(-2, 0, 0), Vector3.Zero, new Vector3(1, 1, 1), new Vector3(1, 0, 0), 1, 2,
            2, 1),
        new BodySpec(true, false, new Vector3(0, 2, 0), Vector3.Zero, new Vector3(1, 1, 1), Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(false, false, new Vector3(2, 0, 0), new Vector3(0, 3.1f, 0), new Vector3(1, 1.5f, 1),
            new Vector3(0.5f, 0, 0), 1, 2, 2, 1),
    ],
    [
        new TickEvent(100, 2, null, null, null, true, null),
        new TickEvent(101, 0, null, null, null, false,
            new BodySpec(false, false, new Vector3(0, 3, 0.2f), new Vector3(0.1f, 0.2f, 0.3f), new Vector3(1, 1, 1),
                new Vector3(0.5f, 0, 0), 1, 2, 2, 1)),
        Force(150, 1, new Vector3(15, 0, 0)),
    ], 400);

    /// <summary>
    /// −0 coordinates survive only where today's WorldObject.Position setter skips an equal value (+0 equals −0): a tick
    /// of 0 s moves nothing, so the cube keeps X = −0, and the two overlapping spheres meet on the Y axis with X and Z of
    /// −0 against +0, whose signs reach the contact normal and the pushed positions. Run with
    /// <see cref="NegativeZeroTickTime"/>: the tick of 0 s comes again among normal ticks.
    /// </summary>
    public static SceneDescription NegativeZeroAndZeroTimeStep() => new SceneDescription(
        "negative zero and a zero time step",
    [
        new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20), Vector3.Zero, 1, 0, 0, 0),
        new BodySpec(false, false, new Vector3(-0f, 5, 3), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(-0f, 0.5f, -0f), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(0, 1.3f, 0), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(false, false, new Vector3(4, -1, -0f), Vector3.Zero, Vector3.One, new Vector3(1, 0, 0), 1, 2, 2, 1),
    ], [], 240);

    /// <summary><see cref="NegativeZeroAndZeroTimeStep"/>'s tick times: 0 s at ticks 0, 1, 100 and 101, else 1/60 s.</summary>
    public static float NegativeZeroTickTime(int tick) => tick is 0 or 1 or 100 or 101 ? 0f : 1f / 60f;

    /// <summary>
    /// Today's RigidBody defaults (limits 0, 0, 0) and a backward force: the forward clamp sets X to −MaxBackSpeed = −0,
    /// and the contact's full-vector cancel against a floor with a stale velocity X of −1 turns it into +0.
    /// </summary>
    public static SceneDescription ZeroLimitsBackwardForce()
    {
        var floor = new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20),
            new Vector3(-1, 0, 0), 1, 2, 2, 1);
        var cube = new BodySpec(false, false, new Vector3(0.25f, -0.9f, 0.5f), new Vector3(0, 0.4f, 0), Vector3.One,
            Vector3.Zero, 1, 0, 0, 0);
        var events = new List<TickEvent>();
        for (var tick = 30; tick < 90; tick++)
            events.Add(new TickEvent(tick, 1, new Vector3(-15, 0, 0), null, null, false, null));

        return new SceneDescription("zero limits and a backward force", [floor, cube], events, 120);
    }

    /// <summary>
    /// A cube resting on the floor is pushed, then made static while its ground flag is set, and a dynamic sphere lands
    /// on it: the static cube's stale flag and velocity take part in the contact but its velocity never changes.
    /// </summary>
    public static SceneDescription BodyHitWhileStatic() => new SceneDescription("body hit while static",
    [
        new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20), Vector3.Zero, 1, 0, 0, 0),
        new BodySpec(false, false, new Vector3(0, -1, 0), new Vector3(0, 0.3f, 0), Vector3.One, Vector3.Zero, 1, 2, 2, 1),
        new BodySpec(true, false, new Vector3(0.2f, 4, 0.1f), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
    ],
    [
        new TickEvent(28, 1, new Vector3(15, 0, 0), null, null, false, null),
        new TickEvent(29, 1, new Vector3(15, 0, 0), null, null, false, null),
        new TickEvent(30, 1, null, null, true, false, null),
    ], 200);

    /// <summary>
    /// A cube resting on the floor pushed by forces that arrive before the same tick as a heading change: force then
    /// heading at ticks 10 and 40, heading then force at tick 70; today's force is local, so it acts along the new
    /// heading whatever the order.
    /// </summary>
    public static SceneDescription ForceAndHeadingChangeBeforeTheSameTick() => new SceneDescription(
        "force and heading change before the same tick",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(0, -1, 0), new Vector3(0, 0.3f, 0), Vector3.One, Vector3.Zero, 1, 2, 2, 1),
    ],
    [
        Force(10, 1, new Vector3(15, 0, 0)),
        Rotate(10, 1, new Vector3(0, 1.8f, 0)),
        Force(11, 1, new Vector3(15, 0, 0)),
        Force(40, 1, new Vector3(15, 0, 3)),
        Rotate(40, 1, new Vector3(0, -0.7f, 0)),
        Rotate(70, 1, new Vector3(0, 2.5f, 0)),
        Force(70, 1, new Vector3(-15, 0, 0)),
    ], 120);

    /// <summary>
    /// A cube resting on the floor turned to a heading of 30° at tick 10, then given velocities through today's setter in
    /// local terms: (5, 0, 1) at tick 20, whose forward part is above MaxSpeed × MaxSpeedMultiplier, and (−5, 0, 1) at
    /// tick 90, below −MaxBackSpeed. Today's setter clamps the local X (to 3, then to −2) and the step moves the body
    /// along the heading. The limits are asymmetric on purpose (MaxSpeed 1, MaxBackSpeed 2, MaxSpeedMultiplier 3): with
    /// equal speeds and a multiplier of 1, swapping MaxSpeed and MaxBackSpeed or dropping the multiplier in the drive's
    /// conversion would give the same clamp and go unnoticed.
    /// </summary>
    public static SceneDescription VelocityWrittenAfterAHeadingChange() => new SceneDescription(
        "velocity written after a heading change",
    [
        s_floor,
        new BodySpec(false, false, new Vector3(0, -1, 0), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 1, 2, 3),
    ],
    [
        Rotate(10, 1, new Vector3(0, MathHelper.DegreesToRadians(30), 0)),
        new TickEvent(20, 1, null, null, null, false, null, new Vector3(5, 0, 1)),
        new TickEvent(90, 1, null, null, null, false, null, new Vector3(-5, 0, 1)),
    ], 160);

    /// <summary>
    /// The WP2 probe's random scene from <paramref name="seed"/>: a static floor and 2-11 bodies (a quarter spheres, a
    /// fifth static), forces on a third of the ticks and a rotation change on a 25th, over 300 ticks.
    /// </summary>
    public static SceneDescription Random(int seed)
    {
        var random = new System.Random(seed);
        var bodies = new List<BodySpec> { s_floor };
        var count = random.Next(2, 12);

        for (var i = 0; i < count; i++)
        {
            var sphere = random.Next(4) == 0;
            var size = sphere
                ? new Vector3(Next(random, 0.3f, 2f))
                : new Vector3(Next(random, 0.3f, 3f), Next(random, 0.3f, 3f), Next(random, 0.3f, 3f));
            var maxSpeed = Next(random, 0.5f, 5f);
            var maxBackSpeed = Next(random, 0.5f, 5f);
            var multiplier = random.Next(2) == 0 ? 1f : 3f;
            var velocityX = Math.Clamp(Next(random, -6, 6), -maxBackSpeed, maxSpeed * multiplier);

            bodies.Add(new BodySpec(sphere, random.Next(5) == 0,
                new Vector3(Next(random, -4, 4), Next(random, -1, 8), Next(random, -4, 4)),
                new Vector3(Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f)), size,
                new Vector3(velocityX, Next(random, -5, 5), Next(random, -3, 3)), Next(random, 0.5f, 5f), maxSpeed,
                maxBackSpeed, multiplier));
        }

        var events = new List<TickEvent>();
        for (var tick = 0; tick < 300; tick++)
        {
            if (random.Next(3) == 0)
                events.Add(Force(tick, random.Next(1, bodies.Count),
                    new Vector3(Next(random, -60, 60), Next(random, -30, 300), Next(random, -60, 60))));

            if (random.Next(25) == 0)
                events.Add(Rotate(tick, random.Next(1, bodies.Count),
                    new Vector3(Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f))));
        }

        return new SceneDescription($"random scene {seed}", bodies, events, 300);
    }

    private static TickEvent Force(int tick, int body, Vector3 force) =>
        new TickEvent(tick, body, force, null, null, false, null);

    private static TickEvent Rotate(int tick, int body, Vector3 rotation) =>
        new TickEvent(tick, body, null, rotation, null, false, null);

    private static float Next(System.Random random, float low, float high) =>
        (float)(low + random.NextDouble() * (high - low));
}
