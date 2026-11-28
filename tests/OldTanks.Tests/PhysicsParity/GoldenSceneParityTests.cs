using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine;
using CoolEngine.Services;
using OldTanks.Models;
using OldTanks.Tests.TestSupport;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// The scenes of the simulation goldens (<c>WorldTests</c>) run side by side through today's <see cref="World"/> and
/// <c>CoolEngine.Physics</c>, equal by bits on every tick. Each scene is described here once, by value; a drift against
/// <see cref="SimulationScenes"/> is caught by <see cref="Scenes_MirrorTheGoldenScenes"/>. A golden test's tick N
/// (1-based, inputs applied before it) is tick N − 1 here (0-based).
/// </summary>
[TestFixture]
public sealed class GoldenSceneParityTests
{
    // The scene names Cube's and Sphere's constructors read from the cache; the game's loader puts the real models there.
    private const string CubeSceneName = "Cube_Cube.005";
    private const string SphereSceneName = "Sphere";

    private static readonly Vector3 s_one = new Vector3(1, 1, 1);

    [SetUp]
    public void SetUp()
    {
        // Other fixtures of this assembly change it; today's game sets 5.
        EngineSettings.Current.CollisionIterations = 5;
        GlobalCache<Scene>.Default.AddOrUpdateItem(CubeSceneName,
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
        GlobalCache<Scene>.Default.AddOrUpdateItem(SphereSceneName,
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
    }

    [TearDown]
    public void TearDown()
    {
        GlobalCache<Scene>.Default.RemoveItem(CubeSceneName);
        GlobalCache<Scene>.Default.RemoveItem(SphereSceneName);
    }

    [Test]
    public void Step_DefaultScene_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(DefaultScene());

    [Test]
    public void Step_TerminalDropCube_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(TerminalDrop(false));

    [Test]
    public void Step_TerminalDropSphere_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(TerminalDrop(true));

    [Test]
    public void Step_UpwardSpeedAboveTerminal_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(UpwardFlip());

    [Test]
    public void Step_CubeOnStaticSphere_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(CubeOnStaticSphere());

    [Test]
    public void Step_DynamicStack_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(Stack());

    [Test]
    public void Step_SpawnedStaticsAndJump_EqualsWorldBitForBit()
    {
        // WorldTests asserts the cube rests on its platform before the jump (before tick 11, so after tick 9 here).
        PhysicsParityTests.AssertParity(SpawnedStatics(), afterTick: (tick, old) =>
        {
            if (tick == 9)
                Assert.That(old.BySlot[0]!.RigidBody.OnGround, Is.True, "precondition: the cube rests on its platform");
        });
    }

    [Test]
    public void Step_DriveForwardReverseCoast_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(Drive());

    [Test]
    public void Step_RestingCubeGivenUpwardVelocity_EqualsWorldBitForBit()
    {
        // WorldTests asserts the cube rests on the floor before the write (before tick 30, so after tick 28 here).
        PhysicsParityTests.AssertParity(RestingHop(), afterTick: (tick, old) =>
        {
            if (tick == 28)
                Assert.That(old.BySlot[1]!.RigidBody.OnGround, Is.True, "precondition: the cube rests on the floor");
        });
    }

    [Test]
    public void Step_OverlappingStatics_EqualsWorldBitForBit() => PhysicsParityTests.AssertParity(OverlappingStatics());

    [Test]
    public void Scenes_MirrorTheGoldenScenes()
    {
        var scenarios = new (SceneDescription Scene, Func<World> Golden)[]
        {
            (DefaultScene(), SimulationScenes.CreateDefaultScene),
            (TerminalDrop(false), () => SimulationScenes.CreateTerminalDropScene(false, 3f)),
            (TerminalDrop(true), () => SimulationScenes.CreateTerminalDropScene(true, 3f)),
            (UpwardFlip(), SimulationScenes.CreateUpwardFlipScene),
            (CubeOnStaticSphere(), SimulationScenes.CreateCubeOnStaticSphereScene),
            (Stack(), SimulationScenes.CreateStackScene),
            (SpawnedStatics(), SimulationScenes.CreateSpawnedStaticsScene),
            (Drive(), SimulationScenes.CreateDriveScene),
            (RestingHop(), SimulationScenes.CreateRestingCubeScene),
            (OverlappingStatics(), SimulationScenes.CreateOverlappingStaticsScene),
        };

        foreach (var (scene, golden) in scenarios)
        {
            var expected = golden().WorldObjects;
            var actual = new OldWorldRunner(scene).World.WorldObjects;

            Assert.That(actual, Has.Count.EqualTo(expected.Count), $"{scene.Name}: object count");

            for (var i = 0; i < expected.Count; i++)
            {
                var where = $"{scene.Name}, object {i}";
                var e = expected[i];
                var a = actual[i];

                Assert.That(a.GetType(), Is.EqualTo(e.GetType()), $"{where}: type");
                AssertSameBits(e.Position, a.Position, where, "position");
                AssertSameBits(e.Rotation, a.Rotation, where, "rotation");
                AssertSameBits(e.Size, a.Size, where, "size");
                AssertSameBits(e.RigidBody.Velocity, a.RigidBody.Velocity, where, "velocity");
                AssertSameBits(e.RigidBody.Force, a.RigidBody.Force, where, "force");
                Assert.That(a.RigidBody.IsStatic, Is.EqualTo(e.RigidBody.IsStatic), $"{where}: static flag");
                Assert.That(a.RigidBody.OnGround, Is.EqualTo(e.RigidBody.OnGround), $"{where}: ground flag");
                AssertSameBits(e.RigidBody.Weight, a.RigidBody.Weight, where, "weight");
                AssertSameBits(e.RigidBody.MaxSpeed, a.RigidBody.MaxSpeed, where, "MaxSpeed");
                AssertSameBits(e.RigidBody.MaxBackSpeed, a.RigidBody.MaxBackSpeed, where, "MaxBackSpeed");
                AssertSameBits(e.RigidBody.MaxSpeedMultiplier, a.RigidBody.MaxSpeedMultiplier, where, "MaxSpeedMultiplier");
            }
        }
    }

    // ---- The scenes, one description each (values of SimulationScenes, in its list order). ----

    // A static body: today's RigidBody defaults for the limits (0, 0, 0) and weight 1.
    private static BodySpec Static(bool sphere, Vector3 position, Vector3 rotation, Vector3 size) =>
        new BodySpec(sphere, true, position, rotation, size, Vector3.Zero, 1, 0, 0, 0);

    // A dynamic body with InitDefaultObjects' limits (2, 2, 1).
    private static BodySpec Dynamic(bool sphere, Vector3 position, Vector3 rotation, Vector3 size, Vector3 velocity) =>
        new BodySpec(sphere, false, position, rotation, size, velocity, 1, 2, 2, 1);

    private static BodySpec Floor() => Static(false, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20));

    private static SceneDescription DefaultScene() => new SceneDescription("golden default scene",
    [
        Static(false, new Vector3(0, 0, -10), Vector3.Zero, new Vector3(10, 2, 5)),
        Static(false, new Vector3(9, 0, 0), Vector3.Zero, new Vector3(1, 3, 20)),
        Floor(),
        Static(true, new Vector3(0, 5, 0), Vector3.Zero, s_one),
        Dynamic(false, new Vector3(0, 2, 1.5f), new Vector3(MathHelper.DegreesToRadians(45), 0, 0), s_one, Vector3.Zero),
        Dynamic(true, new Vector3(0, 5, 0), Vector3.Zero, s_one, Vector3.Zero),
    ], [], 600);

    // CreateTerminalDropScene(sphere, 3): Floor 1 and a body of size 1 at y = 3 with the velocity (0, -MaxFreeFallingSpeed, 0).
    private static SceneDescription TerminalDrop(bool sphere) => new SceneDescription(
        sphere ? "golden terminal drop, sphere" : "golden terminal drop, cube",
    [
        Floor(),
        Dynamic(sphere, new Vector3(0, 3, 0), Vector3.Zero, s_one, new Vector3(0, -PhysicsConstants.MaxFreeFallingSpeed, 0)),
    ], [], 120);

    private static SceneDescription UpwardFlip() => new SceneDescription("golden upward flip",
    [
        Floor(),
        Dynamic(false, new Vector3(0, 5, 0), Vector3.Zero, s_one, new Vector3(0, 100, 0)),
    ], [], 120);

    private static SceneDescription CubeOnStaticSphere() => new SceneDescription("golden cube on static sphere",
    [
        Static(false, new Vector3(0, 0, -10), Vector3.Zero, new Vector3(10, 2, 5)),
        Static(false, new Vector3(9, 0, 0), Vector3.Zero, new Vector3(1, 3, 20)),
        Floor(),
        Static(true, new Vector3(0, 5, 0), Vector3.Zero, s_one),
        Dynamic(false, new Vector3(0.2f, 7, 0), new Vector3(MathHelper.DegreesToRadians(45), 0, 0), s_one, Vector3.Zero),
        Dynamic(true, new Vector3(0, 5, 0), Vector3.Zero, s_one, Vector3.Zero),
    ], [], 600);

    private static SceneDescription Stack() => new SceneDescription("golden dynamic stack",
    [
        Floor(),
        Dynamic(true, new Vector3(0, 1.2f, 0), Vector3.Zero, s_one, Vector3.Zero),
        Dynamic(false, new Vector3(0, -0.9f, 0), Vector3.Zero, s_one, Vector3.Zero),
        Dynamic(false, new Vector3(0, 0.15f, 0), Vector3.Zero, s_one, Vector3.Zero),
    ], [], 600);

    // The spawned statics have size 1 and no limits. The write of (1, 1, 1) to the static body in slot 2 is ignored by
    // today's setter, so its velocity stays zero. The jump is ApplyJump's force GravityDirection * -250 = (-0, 250, -0)
    // before tick 11 (tick 10 here); its OnGround = false changes nothing, as World.CollideObjects resets the flag of every
    // dynamic body before anything reads it.
    private static SceneDescription SpawnedStatics() => new SceneDescription("golden spawned statics and jump",
    [
        Dynamic(false, new Vector3(0, -0.9f, 0), Vector3.Zero, s_one, Vector3.Zero),
        Dynamic(true, new Vector3(3, -0.9f, 0), Vector3.Zero, s_one, Vector3.Zero),
        Static(false, new Vector3(0, -2, 0), Vector3.Zero, s_one),
        Static(false, new Vector3(3, -2, 0), Vector3.Zero, s_one),
        Static(false, new Vector3(0, 0.2f, 0), Vector3.Zero, s_one),
    ],
    [
        new TickEvent(10, 0, new Vector3(-0f, 250, -0f), null, null, false, null),
    ], 120);

    // ApplyDrive: MovementDirectionUnit (1, 0, 0) * 15 * sign, before ticks 1 to 120 (forward) and 121 to 300 (reverse).
    private static SceneDescription Drive()
    {
        var events = new List<TickEvent>();

        for (var tick = 0; tick < 300; tick++)
        {
            var force = tick < 120 ? new Vector3(15, 0, 0) : new Vector3(-15, -0f, -0f);
            events.Add(new TickEvent(tick, 1, force, null, null, false, null));
        }

        return new SceneDescription("golden drive forward, reverse, coast",
        [
            Floor(),
            Dynamic(false, new Vector3(0, -1, 0), new Vector3(0, MathHelper.DegreesToRadians(30), 0), s_one, Vector3.Zero),
        ], events, 900);
    }

    // The debug window's velocity write (0, 3, 0) before tick 30 (tick 29 here).
    private static SceneDescription RestingHop() => new SceneDescription("golden resting cube given upward velocity",
    [
        Floor(),
        Dynamic(false, new Vector3(0, -0.9f, 0), Vector3.Zero, s_one, Vector3.Zero),
    ],
    [
        new TickEvent(29, 1, null, null, null, false, null, new Vector3(0, 3, 0)),
    ], 120);

    private static SceneDescription OverlappingStatics() => new SceneDescription("golden overlapping statics",
    [
        Static(false, Vector3.Zero, Vector3.Zero, s_one),
        Static(false, new Vector3(0.5f, 0, 0), Vector3.Zero, s_one),
    ], [], 60);

    private static void AssertSameBits(Vector3 expected, Vector3 actual, string where, string field)
    {
        if (BitConverter.SingleToUInt32Bits(expected.X) != BitConverter.SingleToUInt32Bits(actual.X) ||
            BitConverter.SingleToUInt32Bits(expected.Y) != BitConverter.SingleToUInt32Bits(actual.Y) ||
            BitConverter.SingleToUInt32Bits(expected.Z) != BitConverter.SingleToUInt32Bits(actual.Z))
            Assert.Fail($"{where}, {field}: golden scene {expected}, description {actual}");
    }

    private static void AssertSameBits(float expected, float actual, string where, string field)
    {
        if (BitConverter.SingleToUInt32Bits(expected) != BitConverter.SingleToUInt32Bits(actual))
            Assert.Fail($"{where}, {field}: golden scene {expected}, description {actual}");
    }
}
