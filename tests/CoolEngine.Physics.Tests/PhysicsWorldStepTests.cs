using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Tests.TestSupport;

namespace CoolEngine.Physics.Tests;

[TestFixture]
public sealed class PhysicsWorldStepTests
{
    private const float SubStepTime = TestBodies.TickTime / 5;

    [Test]
    public void Step_CubeDroppedOnFloor_RestsOnTheFloorWithOnGround()
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(20, 1, 20)));
        var cube = world.Add(TestBodies.Cube(new Vector3(0, 2, 0), Vector3.One));

        for (var tick = 0; tick < 600; tick++)
            world.Step(TestBodies.TickTime);

        Assert.Multiple(() =>
        {
            Assert.That(world.GetPosition(cube).Y, Is.EqualTo(-1f).Within(1e-3f));
            Assert.That(world.IsOnGround(cube), Is.True);
        });
    }

    [Test]
    public void Step_FastUpwardSpeed_FlipsToFallingAtTerminalSpeed()
    {
        var world = TestBodies.World();
        var sphere = world.Add(TestBodies.Sphere(Vector3.Zero, 1, velocity: new Vector3(0, 200, 0)));

        world.Step(TestBodies.TickTime);

        Assert.That(BitConverter.SingleToUInt32Bits(world.GetVelocity(sphere).Y),
            Is.EqualTo(BitConverter.SingleToUInt32Bits(-TestBodies.TerminalSpeed)));
    }

    // Q5 quirk 2: the ground flag comes from the direction between the centres, not from the contact normal. One
    // sub-step, because with five the first push separates the bodies and the later sub-steps clear the flag.
    [TestCase(9f, true)]
    [TestCase(1f, false)]
    public void Step_BodyBesideHigherWall_GroundFlagFollowsTheCentres(float cubeY, bool expectedOnGround)
    {
        var world = TestBodies.World(subStepCount: 1);
        world.Add(TestBodies.Cube(new Vector3(0, 5, 0), new Vector3(1, 10, 10), isStatic: true));
        var cube = world.Add(TestBodies.Cube(new Vector3(0.9f, cubeY, 0), Vector3.One));

        world.Step(TestBodies.TickTime);

        Assert.Multiple(() =>
        {
            Assert.That(world.IsOnGround(cube), Is.EqualTo(expectedOnGround), "ground flag");
            // Pushed out of the wall's side by the 0.1 overlap: the contact normal is horizontal.
            Assert.That(world.GetPosition(cube).X, Is.EqualTo(1f).Within(1e-5f), "X after the push");
        });
    }

    // Today's WorldObject.Position setter skips a value equal to the old one (+0 equals −0), so a zero displacement
    // keeps a −0 coordinate.
    [Test]
    public void IntegratePositions_ZeroDisplacementOnNegativeZero_KeepsTheSign()
    {
        var world = TestBodies.World();
        var cube = world.Add(TestBodies.Cube(new Vector3(-0f, 5, 0), Vector3.One));

        world.Step(0f);

        Assert.That(BitConverter.SingleToUInt32Bits(world.GetPosition(cube).X), Is.EqualTo(0x80000000u));
    }

    [Test]
    public void Step_ForceSet_IsClearedAfterTheLastSubStep()
    {
        var world = TestBodies.World();
        var cube = world.Add(TestBodies.Cube(new Vector3(0, 10, 0), Vector3.One));
        var force = new Vector3(15, 0, 0);
        world.SetForce(cube, force);
        var settings = world.Settings;
        var expected = Vector3.Zero;
        for (var subStep = 0; subStep < 5; subStep++)
            expected = expected + (force / 1f + settings.GravityDirection * settings.GravityAcceleration) * SubStepTime;

        world.Step(TestBodies.TickTime);

        Assert.Multiple(() =>
        {
            Assert.That(world.GetForce(cube), Is.EqualTo(Vector3.Zero));
            Assert.That(world.GetVelocity(cube), Is.EqualTo(expected));
        });
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void Step_TimeStepNegativeOrNotFinite_ThrowsArgumentOutOfRangeException(float timeStep)
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(timeStep));
    }

    [Test]
    public void Step_StaticBodiesOverlapping_NeitherMoves()
    {
        var world = TestBodies.World();
        var first = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One, isStatic: true));
        var second = world.Add(TestBodies.Cube(new Vector3(0.5f, 0.25f, 0), Vector3.One, isStatic: true));

        for (var tick = 0; tick < 10; tick++)
            world.Step(TestBodies.TickTime);

        Assert.Multiple(() =>
        {
            AssertSameBits(world.GetPosition(first), new Vector3(0, 0, 0), "first");
            AssertSameBits(world.GetPosition(second), new Vector3(0.5f, 0.25f, 0), "second");
        });
    }

    [Test]
    public void Step_DynamicPairOverlapping_EachPushedByHalfTheDepth()
    {
        var world = TestBodies.World(subStepCount: 1);
        world.Add(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(20, 1, 20)));
        var left = world.Add(TestBodies.Cube(new Vector3(-3, 0, 0), Vector3.One));
        var right = world.Add(TestBodies.Cube(new Vector3(3, 0, 0), Vector3.One));
        for (var tick = 0; tick < 120; tick++)
            world.Step(TestBodies.TickTime);
        // Centres 0.8 apart along X: each cube is 1 wide, so they overlap by 0.2.
        world.SetPosition(left, new Vector3(-0.4f, world.GetPosition(left).Y, 0));
        world.SetPosition(right, new Vector3(0.4f, world.GetPosition(right).Y, 0));

        world.Step(TestBodies.TickTime);

        var leftX = world.GetPosition(left).X;
        var rightX = world.GetPosition(right).X;
        Assert.Multiple(() =>
        {
            Assert.That(rightX - leftX, Is.EqualTo(0.8f + 0.2f).Within(1e-5f), "distance");
            Assert.That(leftX, Is.EqualTo(-0.5f).Within(1e-5f), "left moved by 0.1");
            Assert.That(rightX, Is.EqualTo(0.5f).Within(1e-5f), "right moved by 0.1");
        });
    }

    [Test]
    public void Step_SameSceneTwice_IdenticalBits()
    {
        foreach (var (name, scene) in new[] { ("default", Scenes.Default()), ("seeded 100", Scenes.Seeded(76, 100)) })
        {
            var first = Run(scene, 600);
            // Made after the first run finished, so tiered compilation has moved on between the two runs.
            var second = Run(scene, 600);

            WorldRecorder.AssertSame(first, second, name);
        }
    }

    [Test]
    public void Step_TwoWorldsOnTwoThreads_EachEqualsItsSerialRun()
    {
        var sceneA = Scenes.Seeded(771, 50);
        var sceneB = Scenes.Seeded(772, 50);
        WorldRecorder? parallelA = null;
        WorldRecorder? parallelB = null;

        var threadA = new Thread(() => parallelA = Run(sceneA, 300));
        var threadB = new Thread(() => parallelB = Run(sceneB, 300));
        threadA.Start();
        threadB.Start();
        threadA.Join();
        threadB.Join();
        var serialA = Run(sceneA, 300);
        var serialB = Run(sceneB, 300);

        Assert.Multiple(() =>
        {
            WorldRecorder.AssertSame(serialA, parallelA!, "scene A");
            WorldRecorder.AssertSame(serialB, parallelB!, "scene B");
        });
    }

    [Test]
    public void Step_WarmedUp_AllocatesNothing()
    {
        var defaultWorld = TestBodies.World();
        var defaultDrive = new TestDrive();
        Scenes.Build(defaultWorld, defaultDrive, Scenes.Default());
        var seededWorld = TestBodies.World(initialCapacity: 256);
        var seededDrive = new TestDrive();
        Scenes.Build(seededWorld, seededDrive, Scenes.Seeded(78, 200));
        for (var tick = 0; tick < 120; tick++)
            RunAll(TestBodies.TickTime);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < 1000; tick++)
            RunAll(TestBodies.TickTime);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.EqualTo(0));

        void RunAll(float timeStep)
        {
            defaultWorld.Step(timeStep);
            defaultDrive.RunTick(defaultWorld, timeStep);
            seededWorld.Step(timeStep);
            seededDrive.RunTick(seededWorld, timeStep);
        }
    }

    [Test]
    public void RayCastAndQueries_WarmedUp_AllocateNothing()
    {
        var world = TestBodies.World();
        var drive = new TestDrive();
        Scenes.Build(world, drive, Scenes.Seeded(10, 20));
        for (var tick = 0; tick < 60; tick++)
            drive.RunTick(world, TestBodies.TickTime);

        var shapes = new List<Shape>();
        var positions = new List<Vector3>();
        var linears = new List<Matrix3>();
        var sizes = new List<Vector3>();
        foreach (var handle in world)
        {
            var state = world.GetState(handle);
            shapes.Add(world.GetShape(handle));
            positions.Add(state.Position);
            linears.Add(state.Linear);
            sizes.Add(state.Size);
        }

        var shapeArray = shapes.ToArray();
        var positionArray = positions.ToArray();
        var linearArray = linears.ToArray();
        var sizeArray = sizes.ToArray();
        var cube = TestBodies.CubeShape;
        var vertices = new Vector3[8];
        var normals = new Vector3[6];
        var cubeLinear = TestBodies.Linear(new Vector3(0.3f, 0.5f, 0.7f), new Vector3(2, 1, 3));
        var cubePosition = new Vector3(1, 2, 3);
        var rays = new Ray[16];
        var random = new Random(10);
        for (var i = 0; i < rays.Length; i++)
        {
            var start = new Vector3(Next(random, -10, 10), Next(random, -2, 10), Next(random, -10, 10));
            rays[i] = new Ray(start, cubePosition + new Vector3(Next(random, -1, 1), Next(random, -1, 1), Next(random, -1, 1)));
        }

        RunAll();
        var allocated = new long[3];
        for (var block = 0; block < allocated.Length; block++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
                RunAll();
            allocated[block] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.That(allocated.Min(), Is.EqualTo(0), $"bytes per block: {string.Join(", ", allocated)}");

        void RunAll()
        {
            foreach (var ray in rays)
            {
                world.RayCast(ray, out _);
                PhysicsQueries.RayCastNearest(shapeArray, positionArray, linearArray, sizeArray, ray, vertices, normals,
                    out _, out _);
                PhysicsQueries.ComputeWorldVertices(cube, cubePosition, cubeLinear, vertices);
                PhysicsQueries.ComputeWorldNormals(cube, cubePosition, cubeLinear, normals);
                PhysicsQueries.IntersectRay(cube, vertices, normals, ray, out _);
                PhysicsQueries.IntersectRay(cubePosition, 1.5f, ray, out _);
            }
        }
    }

    [Test]
    public void RayCast_NoBodies_ReturnsFalse()
    {
        var world = TestBodies.World();

        var hit = world.RayCast(new Ray(Vector3.Zero, new Vector3(100, 0, 0)), out _);

        Assert.That(hit, Is.False);
    }

    [Test]
    public void RayCast_SeededRays_EqualRayCastNearestOverTheSameBodies()
    {
        var world = TestBodies.World();
        var drive = new TestDrive();
        Scenes.Build(world, drive, Scenes.Seeded(80, 20));
        for (var tick = 0; tick < 60; tick++)
            drive.RunTick(world, TestBodies.TickTime);

        var handles = new List<BodyHandle>();
        var shapes = new List<Shape>();
        var positions = new List<Vector3>();
        var linears = new List<Matrix3>();
        var sizes = new List<Vector3>();
        foreach (var handle in world)
        {
            var state = world.GetState(handle);
            handles.Add(handle);
            shapes.Add(world.GetShape(handle));
            positions.Add(state.Position);
            linears.Add(state.Linear);
            sizes.Add(state.Size);
        }

        var random = new Random(80);
        var mismatches = new List<string>();
        for (var i = 0; i < 500; i++)
        {
            var start = new Vector3(Next(random, -10, 10), Next(random, -2, 10), Next(random, -10, 10));
            var direction = Vector3.Normalize(new Vector3(Next(random, -1, 1), Next(random, -1, 1), Next(random, -1, 1)));
            var ray = new Ray(start, start + direction * 100);

            var worldHit = world.RayCast(ray, out var hit);
            var queryHit = PhysicsQueries.RayCastNearest(shapes.ToArray(), positions.ToArray(), linears.ToArray(),
                sizes.ToArray(), ray, new Vector3[8], new Vector3[6], out var index, out var point);

            if (worldHit != queryHit)
                mismatches.Add($"ray {i}: world {worldHit}, query {queryHit}");
            else if (worldHit && (hit.Body != handles[index] || !SameBits(hit.Point, point)))
                mismatches.Add($"ray {i}: world {hit.Body} {hit.Point}, query {handles[index]} {point}");
        }

        Assert.That(mismatches, Is.Empty);
    }

    #region Phase calls

    [Test]
    public void IntegrateVelocities_NotIdle_ThrowsInvalidOperationException()
    {
        var world = OneBodyWorld(out _);
        world.IntegrateVelocities(SubStepTime);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.IntegrateVelocities(SubStepTime));
            Assert.That(world.Phase, Is.EqualTo(StepPhase.VelocitiesIntegrated));
        });
    }

    [TestCase(StepPhase.Idle)]
    [TestCase(StepPhase.TerminalSpeedClamped)]
    [TestCase(StepPhase.PositionsIntegrated)]
    public void ClampTerminalSpeed_WrongPhase_ThrowsInvalidOperationException(StepPhase phase)
    {
        var world = OneBodyWorld(out _);
        BringTo(world, phase);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.ClampTerminalSpeed());
            Assert.That(world.Phase, Is.EqualTo(phase));
        });
    }

    [TestCase(StepPhase.Idle)]
    [TestCase(StepPhase.VelocitiesIntegrated)]
    [TestCase(StepPhase.PositionsIntegrated)]
    public void IntegratePositions_WrongPhase_ThrowsInvalidOperationException(StepPhase phase)
    {
        var world = OneBodyWorld(out _);
        BringTo(world, phase);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.IntegratePositions(SubStepTime));
            Assert.That(world.Phase, Is.EqualTo(phase));
        });
    }

    [TestCase(StepPhase.Idle)]
    [TestCase(StepPhase.VelocitiesIntegrated)]
    [TestCase(StepPhase.TerminalSpeedClamped)]
    public void ResolveContacts_WrongPhase_ThrowsInvalidOperationException(StepPhase phase)
    {
        var world = OneBodyWorld(out _);
        BringTo(world, phase);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.ResolveContacts());
            Assert.That(world.Phase, Is.EqualTo(phase));
        });
    }

    [TestCase("Add")]
    [TestCase("Remove")]
    [TestCase("SetPosition")]
    [TestCase("SetMass")]
    [TestCase("SetStatic")]
    [TestCase("SetLinear")]
    [TestCase("SetSize")]
    [TestCase("SetVelocityFrame")]
    public void StructuralChange_OutsideIdle_ThrowsInvalidOperationException(string call)
    {
        var world = OneBodyWorld(out var handle);
        world.IntegrateVelocities(SubStepTime);
        var stateBefore = world.GetState(handle);
        Action action = call switch
        {
            "Add" => () => world.Add(TestBodies.Cube(new Vector3(5, 0, 0), Vector3.One)),
            "Remove" => () => world.Remove(handle),
            "SetPosition" => () => world.SetPosition(handle, new Vector3(1, 2, 3)),
            "SetMass" => () => world.SetMass(handle, 7),
            "SetStatic" => () => world.SetStatic(handle, true),
            "SetLinear" => () => world.SetLinear(handle, Matrix3.Identity),
            "SetSize" => () => world.SetSize(handle, new Vector3(3)),
            _ => () => world.SetVelocityFrame(handle, Matrix3.CreateRotationY(1).Transposed()),
        };

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => action());
            Assert.That(world.Count, Is.EqualTo(1), "count");
            Assert.That(world.SlotCount, Is.EqualTo(1), "slot count");
            Assert.That(world.Contains(handle), Is.True, "contains");
            Assert.That(world.GetState(handle), Is.EqualTo(stateBefore), "state");
            Assert.That(world.Phase, Is.EqualTo(StepPhase.VelocitiesIntegrated), "phase");
        });
    }

    [Test]
    public void SetVelocity_AfterIntegrateVelocities_IsApplied()
    {
        var world = OneBodyWorld(out var handle);
        var start = world.GetPosition(handle);
        var velocity = new Vector3(2, 0.5f, -1);
        world.IntegrateVelocities(SubStepTime);

        world.SetVelocity(handle, velocity);
        world.ClampTerminalSpeed();
        world.IntegratePositions(SubStepTime);
        world.ResolveContacts();

        AssertSameBits(world.GetPosition(handle), start + world.GetVelocityFrame(handle) * velocity * SubStepTime,
            "position");
    }

    [Test]
    public void SetVelocity_AfterIntegratePositions_ThrowsInvalidOperationException()
    {
        var world = OneBodyWorld(out var handle);
        BringTo(world, StepPhase.PositionsIntegrated);

        Assert.Throws<InvalidOperationException>(() => world.SetVelocity(handle, Vector3.Zero));
    }

    [Test]
    public void SetForce_AfterIntegratePositions_ThrowsInvalidOperationException()
    {
        var world = OneBodyWorld(out var handle);
        BringTo(world, StepPhase.PositionsIntegrated);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.SetForce(handle, new Vector3(1, 0, 0)));
            Assert.That(world.GetForce(handle), Is.EqualTo(Vector3.Zero));
        });
    }

    [Test]
    public void AddForce_AfterIntegratePositions_ThrowsInvalidOperationException()
    {
        var world = OneBodyWorld(out var handle);
        BringTo(world, StepPhase.PositionsIntegrated);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.AddForce(handle, new Vector3(1, 0, 0)));
            Assert.That(world.GetForce(handle), Is.EqualTo(Vector3.Zero));
        });
    }

    [Test]
    public void Settings_SetOutsideIdle_ThrowsInvalidOperationException()
    {
        var world = OneBodyWorld(out _);
        var settings = world.Settings;
        world.IntegrateVelocities(SubStepTime);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => world.Settings = TestBodies.Settings(subStepCount: 1));
            Assert.That(world.Settings, Is.SameAs(settings));
        });
    }

    [TestCase(StepPhase.VelocitiesIntegrated)]
    [TestCase(StepPhase.TerminalSpeedClamped)]
    [TestCase(StepPhase.PositionsIntegrated)]
    public void AbortStep_MidSubStep_ReturnsToIdleAndTheNextStepRuns(StepPhase phase)
    {
        var world = OneBodyWorld(out _);
        BringTo(world, phase);

        world.AbortStep();

        Assert.Multiple(() =>
        {
            Assert.That(world.Phase, Is.EqualTo(StepPhase.Idle));
            Assert.DoesNotThrow(() => world.Step(TestBodies.TickTime));
            Assert.That(world.Phase, Is.EqualTo(StepPhase.Idle));
        });
    }

    [Test]
    public void Step_EqualsThePhaseCallsInOrder_BitForBit()
    {
        var stepped = TestBodies.World();
        var byHand = TestBodies.World();
        Scenes.Build(stepped, new TestDrive(), Scenes.Default());
        Scenes.Build(byHand, new TestDrive(), Scenes.Default());
        var steppedRecord = new WorldRecorder();
        var byHandRecord = new WorldRecorder();

        for (var tick = 0; tick < 600; tick++)
        {
            stepped.Step(TestBodies.TickTime);

            var subStepCount = byHand.Settings.SubStepCount;
            var subStepTime = TestBodies.TickTime / subStepCount;
            for (var subStep = 0; subStep < subStepCount; subStep++)
            {
                byHand.IntegrateVelocities(subStepTime);
                byHand.ClampTerminalSpeed();
                if (subStep == subStepCount - 1)
                    byHand.ClearForces();
                byHand.IntegratePositions(subStepTime);
                byHand.ResolveContacts();
            }

            steppedRecord.Record(stepped, tick);
            byHandRecord.Record(byHand, tick);
        }

        WorldRecorder.AssertSame(steppedRecord, byHandRecord, "default scene");
    }

    [Test]
    public void ClearForces_StaticBodyWithAForce_KeepsIt()
    {
        var world = TestBodies.World();
        var staticBody = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        var dynamicBody = world.Add(TestBodies.Cube(new Vector3(5, 0, 0), Vector3.One));
        world.SetForce(staticBody, new Vector3(1, 2, 3));
        world.SetForce(dynamicBody, new Vector3(4, 5, 6));
        world.SetStatic(staticBody, true);

        world.ClearForces();

        Assert.Multiple(() =>
        {
            Assert.That(world.GetForce(staticBody), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(world.GetForce(dynamicBody), Is.EqualTo(Vector3.Zero));
        });
    }

    #endregion

    private static WorldRecorder Run(SceneBody[] scene, int ticks)
    {
        var world = TestBodies.World(initialCapacity: scene.Length);
        var drive = new TestDrive();
        Scenes.Build(world, drive, scene);
        var recorder = new WorldRecorder();

        for (var tick = 0; tick < ticks; tick++)
        {
            drive.RunTick(world, TestBodies.TickTime);
            recorder.Record(world, tick);
        }

        return recorder;
    }

    private static PhysicsWorld OneBodyWorld(out BodyHandle handle)
    {
        var world = TestBodies.World();
        handle = world.Add(TestBodies.Cube(new Vector3(0, 10, 0), Vector3.One));

        return world;
    }

    private static void BringTo(PhysicsWorld world, StepPhase phase)
    {
        if (phase == StepPhase.Idle)
            return;

        world.IntegrateVelocities(SubStepTime);
        if (phase == StepPhase.VelocitiesIntegrated)
            return;

        world.ClampTerminalSpeed();
        if (phase == StepPhase.TerminalSpeedClamped)
            return;

        world.IntegratePositions(SubStepTime);
    }

    private static bool SameBits(Vector3 a, Vector3 b) =>
        BitConverter.SingleToUInt32Bits(a.X) == BitConverter.SingleToUInt32Bits(b.X) &&
        BitConverter.SingleToUInt32Bits(a.Y) == BitConverter.SingleToUInt32Bits(b.Y) &&
        BitConverter.SingleToUInt32Bits(a.Z) == BitConverter.SingleToUInt32Bits(b.Z);

    private static void AssertSameBits(Vector3 actual, Vector3 expected, string what) =>
        Assert.That(SameBits(actual, expected), Is.True, $"{what}: {actual} against {expected} by bits");

    private static float Next(Random random, float low, float high) => (float)(low + random.NextDouble() * (high - low));
}
