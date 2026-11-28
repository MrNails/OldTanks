using System.Globalization;
using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services;
using OldTanks.Models;
using OldRay = CoolEngine.Models.Ray;
using Ray = CoolEngine.Physics.Ray;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Stage 2 of CoolEngine.Physics against today's code in one process (transitional until the old code goes in WP5):
/// today's <see cref="World"/> and the library with <see cref="WorldSpaceDrive"/> run the same scenes tick by tick
/// (<see cref="SceneComparison"/>). Stage 2 is not equal by bits by design (plan §9). The owner's bar (2026-11-28): a
/// scene stage 2 still reproduces by bits keeps a bitwise test (<c>EqualsWorldBitForBit</c>); every other named scene
/// stays within <see cref="SceneComparison.PositionBound"/> of today's positions on every tick; of the 200 random scenes
/// at least <see cref="RequiredRandomScenesWithinBound"/> do. Ground-flag differences are printed, never asserted. The
/// whole table is written by <see cref="Stage2DifferenceTests"/>.
/// </summary>
[TestFixture]
public sealed class PhysicsParityTests
{
    private const float TickTime = 1f / 60f;

    // The owner's bar of 2026-11-28 ("1 mm, 190 of 200"): how many of the 200 random scenes must stay within
    // SceneComparison.PositionBound.
    private const int RequiredRandomScenesWithinBound = 190;

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

    // Still equal by bits in stage 2, as measured: every heading is 0, so the forward axis is exactly (1, 0, 0). The cube
    // turned 45° about X has geometry that differs from today's in the last bit, but no contact outcome changes, so the
    // positions, velocities and ground flags come out equal.
    [Test]
    public void Step_DefaultScene_EqualsWorldBitForBit() => SceneComparison.AssertBitEqual(Scenes.Default());

    [Test]
    public void Step_TerminalSpeed_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.TerminalSpeed());

    [Test]
    public void Step_ForcesHeadingsAndJump_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.ForcesHeadingsAndJump());

    [Test]
    public void Step_DynamicAgainstDynamic_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.DynamicAgainstDynamic());

    [Test]
    public void Step_StaticToggled_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.StaticToggled());

    [Test]
    public void Step_RemoveAndAdd_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.RemoveAndAdd());

    // Still equal by bits in stage 2 (no rotation, heading 0), −0 signs included.
    [Test]
    public void Step_NegativeZeroCoordinatesAndAZeroTimeStep_EqualsWorldBitForBit() =>
        SceneComparison.AssertBitEqual(Scenes.NegativeZeroAndZeroTimeStep(), Scenes.NegativeZeroTickTime);

    [Test]
    public void Step_ZeroLimitsBackwardForceOnGround_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.ZeroLimitsBackwardForce());

    [Test]
    public void Step_BodyHitWhileStatic_StaysWithinOneMillimetreOfWorld()
    {
        var scene = Scenes.BodyHitWhileStatic();

        // The two preconditions are about today's world alone (that the scene does what it is named for); they compare
        // nothing between the two sides.
        SceneComparison.AssertWithinPositionBound(scene, afterTick: (tick, old) =>
        {
            if (tick == 29)
                Assert.That(old.BySlot[1]!.RigidBody.OnGround, Is.True, "precondition: the cube rests on the floor");
            else if (tick == scene.Ticks - 1)
                Assert.That(old.BySlot[2]!.Position.Y, Is.EqualTo(0f).Within(0.05f), "precondition: the sphere rests on the cube");
        });
    }

    // WorldSpaceDrive's reading of a local force: it is summed in local terms and turned into world space with the
    // heading the next tick runs under, so a force and a heading change arriving before the same tick (in either order)
    // act along the new heading, as today's local force does.
    [Test]
    public void Step_ForceAndHeadingChangeBeforeTheSameTick_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.ForceAndHeadingChangeBeforeTheSameTick());

    // WorldSpaceDrive's rule for a velocity written in today's local terms: today's setter clamps the local X with the
    // body's limits (one write above MaxSpeed × MaxSpeedMultiplier, one below −MaxBackSpeed), and the result is turned
    // into world space with the heading, v = H(ψ) · clamp(u).
    [Test]
    public void Step_VelocityWrittenAfterAHeadingChange_StaysWithinOneMillimetreOfWorld() =>
        SceneComparison.AssertWithinPositionBound(Scenes.VelocityWrittenAfterAHeadingChange());

    // The stored velocity right after each write, before the tick runs: today's setter clamps the local X with the
    // asymmetric limits (to MaxSpeed × MaxSpeedMultiplier = 3, then to −MaxBackSpeed = −2), and the library stores that
    // velocity turned into world space with today's frame. The expected value is built from today's stored velocity, not
    // through WorldSpaceDrive.ToWorldVelocity, which is the code under test.
    [Test]
    public void Step_VelocityWrittenAfterAHeadingChange_StoresTodaysClampedVelocityTurnedToWorld()
    {
        const float tolerance = 1e-4f;
        var scene = Scenes.VelocityWrittenAfterAHeadingChange();
        var old = new OldWorldRunner(scene);
        var library = new LibraryRunner(scene, OldWorldRunner.CubeShape());
        var events = scene.Events.ToLookup(tickEvent => tickEvent.Tick);
        var expectedTodayX = new Dictionary<int, float> { [20] = 3f, [90] = -2f };
        var checks = new List<(int Tick, Vector3 TodayLocal, Vector3 Expected, Vector3 Stored)>();

        for (var tick = 0; tick < scene.Ticks; tick++)
        {
            foreach (var tickEvent in events[tick])
            {
                old.Apply(tickEvent);
                library.Apply(tickEvent);
            }

            if (events[tick].Any(tickEvent => tickEvent.Velocity != null))
            {
                var cube = old.BySlot[1]!;
                var todayLocal = cube.RigidBody.Velocity;
                var expected = WorldSpaceDrive.Frame(cube.Rotation.Y) * todayLocal;
                var stored = library.World.GetVelocity(library.BySlot[1]!.Value);
                checks.Add((tick, todayLocal, expected, stored));
            }

            old.World.CollideObjects(TickTime);
            library.RunTick(TickTime);
        }

        Assert.Multiple(() =>
        {
            Assert.That(checks, Has.Count.EqualTo(2), "velocity writes checked");
            Assert.That(checks.Select(check => check.Tick), Is.EqualTo(new[] { 20, 90 }), "the ticks with a velocity write");

            foreach (var (tick, todayLocal, expected, stored) in checks)
            {
                Assert.That(todayLocal.X, Is.EqualTo(expectedTodayX[tick]),
                    $"tick {tick}: today's clamped local X ({todayLocal})");
                Assert.That(stored.X, Is.EqualTo(expected.X).Within(tolerance),
                    $"tick {tick}: stored X ({stored} against {expected})");
                Assert.That(stored.Y, Is.EqualTo(expected.Y).Within(tolerance),
                    $"tick {tick}: stored Y ({stored} against {expected})");
                Assert.That(stored.Z, Is.EqualTo(expected.Z).Within(tolerance),
                    $"tick {tick}: stored Z ({stored} against {expected})");
            }
        });
    }

    // The 200 random scenes of the seeds 1 … 4 (seed × 1000 + scene, scenes 0 … 49), 300 ticks each. This replaces the
    // four per-seed-group measurement tests, which ran the same 200 scenes with only the non-finite check.
    [Test]
    public void Step_SeededRandomScenes_AtLeast190Of200StayWithinOneMillimetre()
    {
        var within = 0;
        var outside = new List<string>();
        var libraryOnlyNonFinite = new List<string>();

        for (var seed = 1; seed <= 4; seed++)
        {
            for (var scene = 0; scene < 50; scene++)
            {
                var difference = SceneComparison.Run(Scenes.Random(seed * 1000 + scene));

                if (difference.LibraryOnlyNonFinite != null)
                    libraryOnlyNonFinite.Add($"{difference.Name}: {difference.LibraryOnlyNonFinite}");

                if (difference.MaxPosition <= SceneComparison.PositionBound)
                    within++;
                else
                    outside.Add(SceneComparison.Summary(difference));
            }
        }

        TestContext.Out.WriteLine(FormattableString.Invariant(
            $"Random scenes within {SceneComparison.PositionBound} m of today's positions: {within} of 200 ") +
            FormattableString.Invariant($"(at least {RequiredRandomScenesWithinBound} required)."));
        foreach (var line in outside)
            TestContext.Out.WriteLine($"  outside: {line}");

        Assert.Multiple(() =>
        {
            Assert.That(libraryOnlyNonFinite, Is.Empty, "values not finite on the library side only");
            Assert.That(within, Is.GreaterThanOrEqualTo(RequiredRandomScenesWithinBound),
                $"random scenes within the bound; outside: {string.Join("; ", outside)}");
        });
    }

    [Test]
    public void Step_LimitsOutsideTheExactDomain_AreRejectedByTheDrive()
    {
        var runner = new LibraryRunner(Scenes.Default(), OldWorldRunner.CubeShape());
        var handle = runner.BySlot[4]!.Value;

        // MaxSpeed × MaxSpeedMultiplier = −2 < −MaxBackSpeed = −1.
        Assert.Throws<ArgumentException>(() => runner.Drive.SetLimits(handle, -2, 1, 1));
    }

    // Today's nearest hit (MainWindow's loop over Collision.IntersectRay) against PhysicsWorld.RayCast after 60 ticks of
    // the same scene on both sides; the states differ by stage 2's difference, so the hits are counted, not compared.
    [Test]
    public void RayCast_SeededRays_DifferenceFromWorldIsMeasured()
    {
        var rays = 0;
        var hitMismatches = 0;
        var bodyMismatches = 0;
        var maxPointDistance = 0.0;
        var libraryOnlyNonFinite = new List<string>();

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
                rays++;

                if (libraryHit && !SceneComparison.IsFinite(hit.Point) && (!oldHit || SceneComparison.IsFinite(oldPoint)))
                    libraryOnlyNonFinite.Add($"{scene.Name}, ray {i}: today {oldHit} {oldPoint}, library {hit.Point}");

                if (oldHit != libraryHit)
                    hitMismatches++;
                else if (oldHit && hit.Body.Slot != oldIndex)
                    bodyMismatches++;
                else if (oldHit)
                    maxPointDistance = Math.Max(maxPointDistance, SceneComparison.Distance(oldPoint, hit.Point));
            }
        }

        TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Rays: {rays}; hit against miss: {hitMismatches}; another body hit: {bodyMismatches}; " +
            $"largest |Δpoint| on the same body: {maxPointDistance:E2} m"));

        Assert.That(libraryOnlyNonFinite, Is.Empty);
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

    private static float Next(System.Random random, float low, float high) =>
        (float)(low + random.NextDouble() * (high - low));
}
