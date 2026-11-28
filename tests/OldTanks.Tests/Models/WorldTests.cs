using System.Numerics;
using CoolEngine.PhysicEngine;
using CoolEngine.Services;
using OldTanks.Models;
using OldTanks.Tests.TestSupport;

namespace OldTanks.Tests.Models;

/// <summary>
/// Goldens of today's simulation: <see cref="World.CollideObjects"/> with <c>RigidBody.OnTick</c>, the
/// <c>RigidBody.Velocity</c> setter and <c>WorldObject.Move</c>, at 60 Hz and 5 sub-steps, recorded on today's code and
/// compared within the golden tolerance. The sub-step comparison tables go to the test output.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class WorldTests
{
    [SetUp]
    public void SetUp()
    {
        SimulationScenes.AddFakeScenes();
        // The setting starts at 0 and cannot be set back to 0, so every test sets it.
        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
        Assert.That(PhysicsConstants.GravityDirection, Is.EqualTo(new Vector3(0, -1, 0)));
    }

    [TearDown]
    public void TearDown()
    {
        SimulationScenes.RemoveFakeScenes();
        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
    }

    [Test]
    public void CollideObjects_DefaultScene_MatchesGolden()
    {
        var world = SimulationScenes.CreateDefaultScene();

        var actual = SimulationRun.Record(world, s_defaultSceneTicks);

        SimulationRun.AssertGolden(s_defaultSceneTicks, s_defaultSceneGolden, actual, "default scene");
    }

    [Test]
    public void CollideObjects_DefaultSceneRunTwice_GivesSameBits()
    {
        var first = SimulationRun.Record(SimulationScenes.CreateDefaultScene(), [600]);
        var second = SimulationRun.Record(SimulationScenes.CreateDefaultScene(), [600]);

        SimulationGoldenAssert.AreBitEqual(first[0], second[0], "second run against first run, tick 600");
    }

    [Test]
    public void CollideObjects_TerminalDropCube_MatchesGolden()
    {
        var world = SimulationScenes.CreateTerminalDropScene(false, 3f);

        var actual = SimulationRun.Record(world, s_terminalDropTicks);

        SimulationRun.AssertGolden(s_terminalDropTicks, s_terminalDropCubeGolden, actual, "terminal drop, cube");
    }

    [Test]
    public void CollideObjects_TerminalDropSphere_MatchesGolden()
    {
        var world = SimulationScenes.CreateTerminalDropScene(true, 3f);

        var actual = SimulationRun.Record(world, s_terminalDropTicks);

        SimulationRun.AssertGolden(s_terminalDropTicks, s_terminalDropSphereGolden, actual, "terminal drop, sphere");
    }

    [Test]
    public void CollideObjects_UpwardSpeedAboveTerminal_FlipsToFalling()
    {
        var world = SimulationScenes.CreateUpwardFlipScene();

        var actual = SimulationRun.Record(world, s_upwardFlipTicks);

        SimulationRun.AssertGolden(s_upwardFlipTicks, s_upwardFlipGolden, actual, "upward flip");
    }

    [Test]
    public void CollideObjects_CubeOnStaticSphere_MatchesGolden()
    {
        var world = SimulationScenes.CreateCubeOnStaticSphereScene();

        var actual = SimulationRun.Record(world, s_cubeOnStaticSphereTicks);

        SimulationRun.AssertGolden(s_cubeOnStaticSphereTicks, s_cubeOnStaticSphereGolden, actual, "cube on static sphere");
    }

    [Test]
    public void CollideObjects_DynamicStack_MatchesGolden()
    {
        var world = SimulationScenes.CreateStackScene();

        var actual = SimulationRun.Record(world, s_dynamicStackTicks);

        SimulationRun.AssertGolden(s_dynamicStackTicks, s_dynamicStackGolden, actual, "dynamic stack");
    }

    [Test]
    public void CollideObjects_SpawnedStaticsAndJump_MatchesGolden()
    {
        var world = SimulationScenes.CreateSpawnedStaticsScene();
        var cube = world.WorldObjects[0];

        var actual = SimulationRun.Record(world, s_spawnedStaticsTicks, tick =>
        {
            if (tick != 11)
                return;

            // The guard HandleObjectMove checks before it applies the jump.
            Assert.That(cube.RigidBody.OnGround, Is.True, "the cube rests on its platform before the jump");
            SimulationScenes.ApplyJump(cube);
        });

        SimulationRun.AssertGolden(s_spawnedStaticsTicks, s_spawnedStaticsGolden, actual, "spawned statics and jump");
    }

    [Test]
    public void CollideObjects_DriveForwardReverseCoast_MatchesGolden()
    {
        var world = SimulationScenes.CreateDriveScene();
        var cube = world.WorldObjects[1];

        var actual = SimulationRun.Record(world, s_driveTicks, tick =>
        {
            if (tick <= 120)
                SimulationScenes.ApplyDrive(cube, 1);
            else if (tick <= 300)
                SimulationScenes.ApplyDrive(cube, -1);
        });

        SimulationRun.AssertGolden(s_driveTicks, s_driveGolden, actual, "drive forward, reverse, coast");
    }

    [Test]
    public void CollideObjects_RestingBodyGivenUpwardVelocity_LeavesTheGroundAndMatchesGolden()
    {
        var world = SimulationScenes.CreateRestingCubeScene();
        var cube = world.WorldObjects[1];

        var actual = SimulationRun.Record(world, s_restingHopTicks, tick =>
        {
            if (tick != 30)
                return;

            Assert.That(cube.RigidBody.OnGround, Is.True, "the cube rests on the floor before the write");
            // The debug window's Velocity control; OnGround is not touched.
            cube.RigidBody.Velocity = new Vector3(0, 3, 0);
        });

        SimulationRun.AssertGolden(s_restingHopTicks, s_restingHopGolden, actual, "resting cube given upward velocity");

        var onGround = SimulationRun.ValuesPerObject * 2 - 1;
        Assert.Multiple(() =>
        {
            Assert.That(actual[Array.IndexOf(s_restingHopTicks, 31)][onGround], Is.EqualTo(0f), "OnGround in tick 31, the second tick after the write");
            Assert.That(actual[^1][onGround], Is.EqualTo(1f), "OnGround in the last row");
        });
    }

    [Test]
    public void CollideObjects_OverlappingStaticBodies_LeavesBothUnchanged()
    {
        var world = SimulationScenes.CreateOverlappingStaticsScene();
        var before = SimulationRun.State(world);

        for (var tick = 1; tick <= 10; tick++)
            world.CollideObjects(SimulationRun.TickSeconds);

        SimulationGoldenAssert.AreBitEqual(before, SimulationRun.State(world), "state after 10 ticks against before");
    }

    [Test]
    public void CollideObjects_StepModePaused_LeavesStateUnchanged()
    {
        // Stop is true by default; IsActive turns the step mode on.
        var world = SimulationScenes.CreateDefaultScene();
        world.IsActive = true;
        var before = SimulationRun.State(world);

        world.CollideObjects(SimulationRun.TickSeconds);
        world.CollideObjects(SimulationRun.TickSeconds);

        var after = SimulationRun.State(world);
        Assert.Multiple(() =>
        {
            SimulationGoldenAssert.AreBitEqual(before, after, "state after two paused calls against before");
            Assert.That(world.Stop, Is.True, "Stop");
        });
    }

    [Test]
    public void CollideObjects_StepModeSingleStep_AdvancesOneTickThenPauses()
    {
        var world = SimulationScenes.CreateDefaultScene();
        world.IsActive = true;
        // Key I.
        world.Stop = false;

        world.CollideObjects(SimulationRun.TickSeconds);
        var afterStep = SimulationRun.State(world);
        var stopAfterStep = world.Stop;

        world.CollideObjects(SimulationRun.TickSeconds);
        var afterPausedCall = SimulationRun.State(world);

        Assert.Multiple(() =>
        {
            SimulationGoldenAssert.AreClose(s_defaultSceneGolden[0], afterStep, "one step against the default scene's tick 1");
            Assert.That(stopAfterStep, Is.True, "Stop after the step (AmountOfFrames 1)");
            SimulationGoldenAssert.AreBitEqual(afterStep, afterPausedCall, "the paused call against the step");
        });
    }

    [Test]
    public void CollideObjects_TerminalDropAtFiveSubSteps_RestsOnFloorFromEveryPhase()
    {
        // Both drops, 16 phases each, per sub-step count. "Worst" is the phase with the greatest pre-correction depth (the
        // first on a tie); rests is Y exactly -1 and on the ground at the last tick, tunnels is Y below the floor's bottom face.
        var rows = new List<(int Count, DropOutcome CubeWorst, DropOutcome SphereWorst, int CubeRests, int CubeTunnels, int SphereRests, int SphereTunnels)>();

        foreach (var count in SubStepComparison.Counts)
        {
            var cubes = Enumerable.Range(0, SubStepComparison.Phases)
                .Select(phase => SubStepComparison.RunTerminalDrop(false, count, phase, 120)).ToList();
            var spheres = Enumerable.Range(0, SubStepComparison.Phases)
                .Select(phase => SubStepComparison.RunTerminalDrop(true, count, phase, 120)).ToList();

            rows.Add((count, Worst(cubes), Worst(spheres), Rests(cubes), Tunnels(cubes), Rests(spheres), Tunnels(spheres)));
        }

        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
        TestContext.Out.WriteLine("Terminal drop by sub-step count (asserted at 5 sub-steps only):");
        TestContext.Out.WriteLine(SubStepComparison.FormatDropTable(rows));

        // The sub-step-wise run of phase 0 against the one-call run: proves the harness steps as the game does.
        var comparisons = new List<(bool Sphere, List<float[]> StepWise, List<float[]> OneCall)>();

        foreach (var sphere in new[] { false, true })
        {
            var stepWise = new List<float[]>();
            SubStepComparison.RunTerminalDrop(sphere, SimulationRun.SubSteps, 0, 120,
                (_, world) => stepWise.Add(SimulationRun.State(world)));

            EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
            var oneCallWorld =
                SimulationScenes.CreateTerminalDropScene(sphere, SubStepComparison.PhaseStartHeight(SimulationRun.SubSteps, 0));
            var oneCall = new List<float[]>();

            for (var tick = 1; tick <= 120; tick++)
            {
                oneCallWorld.CollideObjects(SimulationRun.TickSeconds);
                oneCall.Add(SimulationRun.State(oneCallWorld));
            }

            comparisons.Add((sphere, stepWise, oneCall));
        }

        Assert.Multiple(() =>
        {
            foreach (var sphere in new[] { false, true })
            {
                for (var phase = 0; phase < SubStepComparison.Phases; phase++)
                {
                    var outcome = SubStepComparison.RunTerminalDrop(sphere, SimulationRun.SubSteps, phase, 120);
                    var what = $"{(sphere ? "sphere" : "cube")} phase {phase}";

                    SimulationGoldenAssert.AreClose([-1f], [outcome.FinalY], $"{what} final Y");
                    Assert.That(outcome.FinalOnGround, Is.True, $"{what} final OnGround");
                    Assert.That(outcome.RestingJitter, Is.LessThanOrEqualTo(1e-5f), $"{what} resting jitter");
                }
            }

            foreach (var (sphere, stepWise, oneCall) in comparisons)
            {
                Assert.That(stepWise, Has.Count.EqualTo(oneCall.Count));

                for (var i = 0; i < Math.Min(stepWise.Count, oneCall.Count); i++)
                {
                    SimulationGoldenAssert.AreBitEqual(oneCall[i], stepWise[i],
                        $"{(sphere ? "sphere" : "cube")} phase 0, sub-step-wise run against one call, tick {i + 1}");
                }
            }
        });
    }

    [Test]
    public void CollideObjects_DefaultSceneAtFiveSubSteps_SettlesWithoutJitter()
    {
        var rows = new List<(int Count, DefaultSceneOutcome Scene)>();

        foreach (var count in SubStepComparison.Counts)
            rows.Add((count, SubStepComparison.RunDefaultScene(count, 600)));

        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
        TestContext.Out.WriteLine("Default scene by sub-step count (asserted at 5 sub-steps only):");
        TestContext.Out.WriteLine(SubStepComparison.FormatSceneTable(rows));

        var outcome = rows.Single(row => row.Count == SimulationRun.SubSteps).Scene;

        // The tick the dynamic sphere is on the ground at (tick 61 when recorded); the settle tick itself is only reported.
        var world = SimulationScenes.CreateDefaultScene();
        for (var tick = 1; tick <= 61; tick++)
            world.CollideObjects(SimulationRun.TickSeconds);

        TestContext.Out.WriteLine($"Default scene at 5 sub-steps: settle tick {outcome.SettleTick}");

        Assert.Multiple(() =>
        {
            Assert.That(world.WorldObjects[5].RigidBody.OnGround, Is.True, "the dynamic sphere is on the ground at tick 61");
            Assert.That(outcome.CubeJitter, Is.LessThanOrEqualTo(1e-5f), "cube resting jitter");
            Assert.That(outcome.SphereJitter, Is.LessThanOrEqualTo(1e-5f), "sphere resting jitter");
            Assert.That(outcome.TicksOffGround, Is.Zero, "ticks with a dynamic body off the ground in ticks 301 to 600");
        });
    }

    private static DropOutcome Worst(List<DropOutcome> outcomes) =>
        outcomes.Aggregate((worst, next) => next.MaxPreCorrectionDepth > worst.MaxPreCorrectionDepth ? next : worst);

    private static int Rests(List<DropOutcome> outcomes) =>
        outcomes.Count(outcome => outcome.FinalY == -1f && outcome.FinalOnGround);

    private static int Tunnels(List<DropOutcome> outcomes) =>
        outcomes.Count(outcome => outcome.FinalY < SubStepComparison.FloorBottom);

    // Golden rows: 7 values per object (position, velocity, OnGround) in list order, per recorded tick.

    private static readonly int[] s_defaultSceneTicks = [1, 2, 3, 4, 45, 46, 60, 61, 62, 600];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_defaultSceneGolden = [
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.9983656f, 1.5f, 0f, -0.16344416f, 0f, 0f,
        0f, 4.987569f, 0f, 0f, -0.16344416f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.9940073f, 1.5f, 0f, -0.3268883f, 0f, 0f,
        0f, 4.5560007f, 0f, 0f, -0.3268883f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.9869245f, 1.5f, 0f, -0.49033242f, 0f, 0f,
        0f, 3.9954238f, 0f, 0f, -0.49033242f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.9771178f, 1.5f, 0f, -0.6537767f, 0f, 0f,
        0f, 3.9856176f, 0f, 0f, -0.6537767f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.770376f, 1.5f, 0f, -7.3549676f, 0f, 0f,
        0f, 1.2381226f, 0f, 0f, -7.3549676f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.79289323f, 1.5f, 0f, 0f, 0f, 1f,
        0f, 1.1139053f, 0f, 0f, -7.5184107f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.79289323f, 1.5f, 0f, 0f, 0f, 1f,
        0f, -0.9111638f, 0f, 0f, -9.806641f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.79289323f, 1.5f, 0f, 0f, 0f, 1f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.79289323f, 1.5f, 0f, 0f, 0f, 1f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.79289323f, 1.5f, 0f, 0f, 0f, 1f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_terminalDropTicks = [1, 2, 3, 4, 60];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_terminalDropCubeGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.5290027f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.058005273f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_terminalDropSphereGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.5290027f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.058005273f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_upwardFlipTicks = [1, 4, 5, 60];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_upwardFlipGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 3.5290027f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.88398945f, 0f, 0f, -88.25985f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_cubeOnStaticSphereTicks = [1, 24, 25, 26, 60, 61, 600];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_cubeOnStaticSphereGolden = [
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.998365f, 0f, 0f, -0.16344416f, 0f, 0f,
        0f, 4.987569f, 0f, 0f, -0.16344416f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.208929f, 0f, 0f, -3.9226623f, 0f, 0f,
        0f, 3.2174287f, 0f, 0f, -3.9226623f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.2071066f, 0f, 0f, 0f, 0f, 1f,
        0f, 3.1504164f, 0f, 0f, -4.086106f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.2071066f, 0f, 0f, 0f, 0f, 1f,
        0f, 3.0806801f, 0f, 0f, -4.249549f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.2071066f, 0f, 0f, 0f, 0f, 1f,
        0f, -0.9111638f, 0f, 0f, -9.806641f, 0f, 0f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.2071066f, 0f, 0f, 0f, 0f, 1f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, 0f, -10f, 0f, 0f, 0f, 0f,
        9f, 0f, 0f, 0f, 0f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 5f, 0f, 0f, 0f, 0f, 0f,
        0.2f, 6.2071066f, 0f, 0f, 0f, 0f, 1f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_dynamicStackTicks = [1, 8, 9, 10, 11, 12, 13, 14, 120, 600];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_dynamicStackGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.1983657f, 0f, 0f, -0.16344416f, 0f, 0f,
        0f, -0.90163434f, 0f, 0f, -0.16344416f, 0f, 0f,
        0f, 0.14836557f, 0f, 0f, -0.16344416f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.1106505f, 0f, 0f, -1.3075538f, 0f, 0f,
        0f, -0.9893494f, 0f, 0f, -1.3075538f, 0f, 0f,
        0f, 0.060650516f, 0f, 0f, -1.3075538f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.0872235f, 0f, 0f, -1.470998f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, 0.03722351f, 0f, 0f, -1.470998f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.0610726f, 0f, 0f, -1.6344423f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, 0.011072434f, 0f, 0f, -1.6344423f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.0321975f, 0f, 0f, -1.7978866f, 0f, 0f,
        0f, -1.0001048f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.00010476887f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 1.0005981f, 0f, 0f, -1.9613309f, 0f, 0f,
        0f, -1.0001088f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.00010881969f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.99856174f, 0f, 0f, -0.032688834f, 0f, 1f,
        0f, -1.0007192f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.0007191391f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.99916005f, 0f, 0f, -0.032688834f, 0f, 1f,
        0f, -1.00042f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.00041995116f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.99934614f, 0f, 0f, -0.032688834f, 0f, 1f,
        0f, -1.0003269f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.0003269174f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.99934614f, 0f, 0f, -0.032688834f, 0f, 1f,
        0f, -1.0003269f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.0003269062f, 0f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_spawnedStaticsTicks = [1, 8, 9, 10, 11, 13, 14, 35, 36, 47, 48, 120];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_spawnedStaticsGolden = [
    [
        0f, -0.90163434f, 0f, 0f, -0.16344416f, 0f, 0f,
        3f, -0.90163434f, 0f, 0f, -0.16344416f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.9893494f, 0f, 0f, -1.3075538f, 0f, 0f,
        3f, -0.9893494f, 0f, 0f, -1.3075538f, 0f, 0f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.9599678f, 0f, 0f, 4.0032225f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.83252f, 0f, 0f, 3.676334f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.8f, 0f, 0f, 3.5128896f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.8f, 0f, 0f, 0.0805601f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.80050194f, 0f, 0f, -0.082884066f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -0.98350006f, 0f, 0f, -1.8807708f, 0f, 0f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ],
    [
        0f, -1f, 0f, 0f, 0f, 0f, 1f,
        3f, -1f, 0f, 0f, 0f, 0f, 1f,
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, 0.2f, 0f, 0f, 0f, 0f, 0f
    ]
    ];

    private static readonly int[] s_driveTicks = [1, 8, 9, 120, 121, 136, 137, 300, 301, 829, 830, 900];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_driveGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0.0021592986f, -1f, 0.0012466717f, 0.249002f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0.115337126f, -1f, 0.066589914f, 1.9239392f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0.1441183f, -1f, 0.08320672f, 2f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3.348428f, -1f, 1.933205f, 2f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3.3749633f, -1f, 1.9485253f, 1.7310783f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3.330836f, -1f, 1.9230486f, -1.995802f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        3.3019683f, -1f, 1.906382f, -2f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        -1.4034535f, -1f, -0.8102823f, -2f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        -1.4321482f, -1f, -0.8268492f, -1.9800799f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        -4.269983f, -1f, -2.4652762f, -0.010030262f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        -4.270012f, -1f, -2.465293f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        -4.270012f, -1f, -2.465293f, 0f, 0f, 0f, 1f
    ]
    ];

    private static readonly int[] s_restingHopTicks = [1, 29, 30, 31, 32, 47, 65, 66, 120];

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
    private static readonly float[][] s_restingHopGolden = [
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.90163434f, 0f, 0f, -0.16344416f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.9516344f, 0f, 0f, 2.8365557f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.9059929f, 0f, 0f, 2.6731114f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.8630755f, 0f, 0f, 2.5096672f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.54620284f, 0f, 0f, 0.058003344f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -0.9750046f, 0f, 0f, -2.8839931f, 0f, 0f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ],
    [
        0f, -2f, 0f, 0f, 0f, 0f, 0f,
        0f, -1f, 0f, 0f, 0f, 0f, 1f
    ]
    ];
}
