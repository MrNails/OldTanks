using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Physics;
using CoolEngine.Services;
using OldTanks.Models;
using OldRay = CoolEngine.Models.Ray;
using Ray = CoolEngine.Physics.Ray;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Stage 1 of CoolEngine.Physics against today's code in one process (transitional until the old code goes in WP5):
/// today's <see cref="World"/> and the library with <see cref="StageOneDrive"/> run the same scenes tick by tick, and
/// every position, velocity, force and ground flag must be equal by bits after every tick. A single differing bit
/// fails the test; there is no tolerance.
/// </summary>
[TestFixture]
public sealed class PhysicsParityTests
{
    private const float TickTime = 1f / 60f;

    // The scene names Cube's and Sphere's constructors read from the cache; the game's loader puts the real models there.
    private const string CubeSceneName = "Cube_Cube.005";
    private const string SphereSceneName = "Sphere";

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

    // The library's world geometry for the game's cube from (position, linear part) against today's
    // ApplyTransformation and Collision.UpdateCollision on a real Cube.
    [Test]
    public void LinearPart_RandomTransforms_RebuildsApplyTransformationBitForBit()
    {
        var random = new System.Random(20261127);
        var mismatches = new List<string>();
        var oldShape = OldWorldRunner.CubeShape();
        var shape = LibraryRunner.ToShape(oldShape);
        var vertices = new Vector3[shape.Vertices.Length];
        var normals = new Vector3[shape.FaceCount];

        for (var i = 0; i < 20_000 && mismatches.Count < 20; i++)
        {
            var position = new Vector3(Pick(random, 100), Pick(random, 100), Pick(random, 100));
            var rotation = new Vector3(Pick(random, 7), Pick(random, 7), Pick(random, 7));
            var size = new Vector3(Pick(random, 10), Pick(random, 10), Pick(random, 10));
            var cube = new Cube { Size = size, Rotation = rotation, Position = position };
            cube.Collision = new Collision(cube, oldShape);
            cube.ApplyTransformation();
            var today = cube.Collision.CollisionData;

            var linear = LibraryRunner.Linear(rotation, size);
            PhysicsQueries.ComputeWorldVertices(shape, position, linear, vertices);
            PhysicsQueries.ComputeWorldNormals(shape, position, linear, normals);

            for (var k = 0; k < vertices.Length; k++)
            {
                if (!SameBits(today.Vertices[k], vertices[k]))
                    mismatches.Add($"input {i}, vertex {k}: position {position}, rotation {rotation}, size {size}: " +
                                   $"today {Bits(today.Vertices[k])}, library {Bits(vertices[k])}");
            }

            for (var k = 0; k < normals.Length; k++)
            {
                if (!SameBits(today.Faces[k].Normal, normals[k]))
                    mismatches.Add($"input {i}, normal {k}: position {position}, rotation {rotation}, size {size}: " +
                                   $"today {Bits(today.Faces[k].Normal)}, library {Bits(normals[k])}");
            }
        }

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void Step_DefaultScene_EqualsWorldBitForBit() => AssertParity(Scenes.Default());

    [Test]
    public void Step_TerminalSpeed_EqualsWorldBitForBit() => AssertParity(Scenes.TerminalSpeed());

    [Test]
    public void Step_ForcesHeadingsAndJump_EqualsWorldBitForBit() => AssertParity(Scenes.ForcesHeadingsAndJump());

    [Test]
    public void Step_DynamicAgainstDynamic_EqualsWorldBitForBit() => AssertParity(Scenes.DynamicAgainstDynamic());

    [Test]
    public void Step_StaticToggled_EqualsWorldBitForBit() => AssertParity(Scenes.StaticToggled());

    [Test]
    public void Step_RemoveAndAdd_EqualsWorldBitForBit() => AssertParity(Scenes.RemoveAndAdd());

    // −0 coordinates survive only where today's WorldObject.Position setter skips an equal value (+0 equals −0): a tick
    // of 0 s moves nothing, so the cube keeps X = −0, and the two overlapping spheres meet on the Y axis with X and Z
    // of −0 against +0, whose signs reach the contact normal and the pushed positions (so it also pins that detection
    // takes the body's position, not the matrix translation, which turns −0 into +0). The tick of 0 s comes again among
    // normal ticks.
    [Test]
    public void Step_NegativeZeroCoordinatesAndAZeroTimeStep_EqualsWorldBitForBit()
    {
        var scene = new SceneDescription("negative zero and a zero time step",
        [
            new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20), Vector3.Zero, 1, 0, 0,
                0),
            new BodySpec(false, false, new Vector3(-0f, 5, 3), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
            new BodySpec(true, false, new Vector3(-0f, 0.5f, -0f), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
            new BodySpec(true, false, new Vector3(0, 1.3f, 0), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
            new BodySpec(false, false, new Vector3(4, -1, -0f), Vector3.Zero, Vector3.One, new Vector3(1, 0, 0), 1, 2, 2,
                1),
        ], [], 240);

        AssertParity(scene, tick => tick is 0 or 1 or 100 or 101 ? 0f : TickTime);
    }

    // Today's RigidBody defaults (limits 0, 0, 0) and a backward force: the forward clamp sets X to −MaxBackSpeed = −0,
    // and the contact's full-vector cancel against a floor with a stale velocity X of −1 turns it into +0.
    [Test]
    public void Step_ZeroLimitsBackwardForceOnGround_EqualsWorldBitForBit()
    {
        var floor = new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20),
            new Vector3(-1, 0, 0), 1, 2, 2, 1);
        var cube = new BodySpec(false, false, new Vector3(0.25f, -0.9f, 0.5f), new Vector3(0, 0.4f, 0), Vector3.One,
            Vector3.Zero, 1, 0, 0, 0);
        var events = new List<TickEvent>();
        for (var tick = 30; tick < 90; tick++)
            events.Add(new TickEvent(tick, 1, new Vector3(-15, 0, 0), null, null, false, null));

        AssertParity(new SceneDescription("zero limits and a backward force", [floor, cube], events, 120));
    }

    // A cube resting on the floor is pushed, then made static while its ground flag is set, and a dynamic sphere lands
    // on it: the static cube's stale flag and velocity take part in the contact but its velocity never changes.
    [Test]
    public void Step_BodyHitWhileStatic_EqualsWorldBitForBit()
    {
        var scene = new SceneDescription("body hit while static",
        [
            new BodySpec(false, true, new Vector3(0, -2, 0), Vector3.Zero, new Vector3(20, 1, 20), Vector3.Zero, 1, 0, 0,
                0),
            new BodySpec(false, false, new Vector3(0, -1, 0), new Vector3(0, 0.3f, 0), Vector3.One, Vector3.Zero, 1, 2, 2,
                1),
            new BodySpec(true, false, new Vector3(0.2f, 4, 0.1f), Vector3.Zero, Vector3.One, Vector3.Zero, 1, 2, 2, 1),
        ],
        [
            new TickEvent(28, 1, new Vector3(15, 0, 0), null, null, false, null),
            new TickEvent(29, 1, new Vector3(15, 0, 0), null, null, false, null),
            new TickEvent(30, 1, null, null, true, false, null),
        ], 200);

        AssertParity(scene, afterTick: (tick, old) =>
        {
            if (tick == 29)
                Assert.That(old.BySlot[1]!.RigidBody.OnGround, Is.True, "precondition: the cube rests on the floor");
            else if (tick == scene.Ticks - 1)
                Assert.That(old.BySlot[2]!.Position.Y, Is.EqualTo(0f).Within(0.05f), "precondition: the sphere rests on the cube");
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Step_SeededRandomScenes_EqualWorldBitForBit(int seed)
    {
        for (var scene = 0; scene < 50; scene++)
            AssertParity(Scenes.Random(seed * 1000 + scene));
    }

    [Test]
    public void Step_LimitsOutsideTheExactDomain_AreRejectedByTheDrive()
    {
        var runner = new LibraryRunner(Scenes.Default(), OldWorldRunner.CubeShape());
        var handle = runner.BySlot[4]!.Value;

        // MaxSpeed × MaxSpeedMultiplier = −2 < −MaxBackSpeed = −1.
        Assert.Throws<ArgumentException>(() => runner.Drive.SetLimits(handle, -2, 1, 1));
    }

    [Test]
    public void RayCast_SeededRays_EqualTodaysNearestHitBitForBit()
    {
        var mismatches = new List<string>();

        for (var sceneIndex = 0; sceneIndex < 20; sceneIndex++)
        {
            var scene = Scenes.Random(10_000 + sceneIndex);
            var old = new OldWorldRunner(scene);
            var library = new LibraryRunner(scene, OldWorldRunner.CubeShape());
            for (var tick = 0; tick < 60; tick++)
            {
                old.World.CollideObjects(TickTime);
                library.RunTick(TickTime);
            }

            var random = new System.Random(sceneIndex);
            for (var i = 0; i < 100; i++)
            {
                var start = new Vector3(Next(random, -6, 6), Next(random, -1, 8), Next(random, -6, 6));
                var direction = Vector3.Normalize(new Vector3(Next(random, -1, 1), Next(random, -1, 1),
                    Next(random, -1, 1)));
                var end = start + direction * 100;

                var oldHit = TodaysNearestHit(old.World, new OldRay(start, end), out var oldIndex, out var oldPoint);
                var libraryHit = library.World.RayCast(new Ray(start, end), out var hit);

                if (oldHit != libraryHit)
                    mismatches.Add($"{scene.Name}, ray {i}: today {oldHit}, library {libraryHit}");
                else if (oldHit && (hit.Body.Slot != oldIndex || !SameBits(hit.Point, oldPoint)))
                    mismatches.Add($"{scene.Name}, ray {i}: today object {oldIndex} {oldPoint}, library {hit.Body} {hit.Point}");
            }
        }

        Assert.That(mismatches, Is.Empty);
    }

    // OldTanks.Windows.MainWindow's ray loop after the step.
    private static bool TodaysNearestHit(World world, OldRay ray, out int index, out Vector3 point)
    {
        var previousLength = float.MaxValue;
        var found = false;
        index = -1;
        point = Vector3.Zero;

        for (var i = 0; i < world.WorldObjects.Count; i++)
        {
            var worldObject = world.WorldObjects[i];

            if (worldObject.Collision == null)
                continue;

            var isRayIntersected = worldObject.Collision.IntersectRay(ray, out var intersectedPoint);

            var lengthToRayStart = Math.Abs((intersectedPoint - ray.Start).Length());
            if (isRayIntersected && previousLength > lengthToRayStart)
            {
                found = true;
                previousLength = lengthToRayStart;
                index = i;
                point = intersectedPoint;
            }
        }

        return found;
    }

    // tickTime gives each tick's time (default 1/60 s); afterTick checks a scene's preconditions on today's world.
    internal static void AssertParity(SceneDescription scene, Func<int, float>? tickTime = null,
        Action<int, OldWorldRunner>? afterTick = null)
    {
        var old = new OldWorldRunner(scene);
        var library = new LibraryRunner(scene, OldWorldRunner.CubeShape());
        var events = scene.Events.ToLookup(tickEvent => tickEvent.Tick);

        for (var tick = 0; tick < scene.Ticks; tick++)
        {
            foreach (var tickEvent in events[tick])
            {
                if (tickEvent.Add is { } spec)
                {
                    var handle = library.Add(spec);
                    old.Add(spec, handle.Slot);
                }
                else
                {
                    old.Apply(tickEvent);
                    library.Apply(tickEvent);
                }
            }

            var time = tickTime?.Invoke(tick) ?? TickTime;
            old.World.CollideObjects(time);
            library.RunTick(time);

            Compare(scene, tick, old, library);
            afterTick?.Invoke(tick, old);
        }
    }

    private static void Compare(SceneDescription scene, int tick, OldWorldRunner old, LibraryRunner library)
    {
        var slotCount = Math.Max(old.BySlot.Count, library.BySlot.Count);
        var listIndex = 0;

        for (var slot = 0; slot < slotCount; slot++)
        {
            var worldObject = slot < old.BySlot.Count ? old.BySlot[slot] : null;
            var handle = slot < library.BySlot.Count ? library.BySlot[slot] : null;
            var where = $"{scene.Name}: tick {tick}, slot {slot}";

            if ((worldObject == null) != (handle == null))
                Assert.Fail($"{where}: today {(worldObject == null ? "empty" : "a body")}, library {(handle == null ? "empty" : "a body")}");

            if (worldObject == null || handle == null)
                continue;

            if (!ReferenceEquals(old.World.WorldObjects[listIndex++], worldObject))
                Assert.Fail($"{where}: today's object list is not in slot order");

            var state = library.World.GetState(handle.Value);
            AssertSameBits(worldObject.Position, state.Position, where, "position");
            AssertSameBits(worldObject.RigidBody.Velocity, state.Velocity, where, "velocity");
            AssertSameBits(worldObject.RigidBody.Force, state.Force, where, "force");

            if (worldObject.RigidBody.OnGround != state.OnGround)
                Assert.Fail($"{where}, ground flag: today {worldObject.RigidBody.OnGround}, library {state.OnGround}");
        }
    }

    private static void AssertSameBits(Vector3 today, Vector3 library, string where, string field)
    {
        if (!SameBits(today, library))
            Assert.Fail($"{where}, {field}: today {today} ({Bits(today)}), library {library} ({Bits(library)})");
    }

    private static string Bits(Vector3 v) =>
        $"0x{BitConverter.SingleToUInt32Bits(v.X):X8} 0x{BitConverter.SingleToUInt32Bits(v.Y):X8} " +
        $"0x{BitConverter.SingleToUInt32Bits(v.Z):X8}";

    private static bool SameBits(Vector3 a, Vector3 b) =>
        BitConverter.SingleToUInt32Bits(a.X) == BitConverter.SingleToUInt32Bits(b.X) &&
        BitConverter.SingleToUInt32Bits(a.Y) == BitConverter.SingleToUInt32Bits(b.Y) &&
        BitConverter.SingleToUInt32Bits(a.Z) == BitConverter.SingleToUInt32Bits(b.Z);

    // 10 % each of +0, −0, tiny positive and tiny negative per component, else uniform in ±range.
    private static float Pick(System.Random random, float range) => random.Next(10) switch
    {
        0 => 0f,
        1 => -0f,
        2 => (float)(random.NextDouble() * 1e-30),
        3 => -(float)(random.NextDouble() * 1e-30),
        _ => (float)((random.NextDouble() * 2 - 1) * range)
    };

    private static float Next(System.Random random, float low, float high) =>
        (float)(low + random.NextDouble() * (high - low));
}
