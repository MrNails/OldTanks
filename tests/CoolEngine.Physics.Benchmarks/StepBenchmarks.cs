using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CoolEngine.Physics.Tests.TestSupport;
using OldTanks.Models;

namespace CoolEngine.Physics.Benchmarks;

/// <summary>
/// One tick of 1/60 s, 5 sub-steps, after 120 warm-up ticks; the worlds keep stepping between iterations (they settle).
/// <c>Step</c>: the default scene, today's World against the library driven through the phase calls by the tests'
/// drive (today's damping and forward clamp) and the library's plain <see cref="PhysicsWorld.Step"/>.
/// <c>Stress</c>: a 200 × 1 × 200 static floor and 999 dynamic unit cubes on a 10 × 10 × 10 grid 2 m apart with seeded
/// headings; no old baseline (minutes per operation).
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StepBenchmarks
{
    private const float TickTime = 1f / 60f;
    private const int WarmUpTicks = 120;

    private World m_oldWorld = null!;
    private PhysicsWorld m_defaultWorld = null!;
    private TestDrive m_defaultDrive = null!;
    private PhysicsWorld m_defaultPlainWorld = null!;
    private PhysicsWorld m_stressWorld = null!;
    private TestDrive m_stressDrive = null!;
    private PhysicsWorld m_stressPlainWorld = null!;

    [GlobalSetup]
    public void Setup()
    {
        m_oldWorld = OldCode.DefaultWorld();

        m_defaultWorld = TestBodies.World();
        m_defaultDrive = new TestDrive();
        Scenes.Build(m_defaultWorld, m_defaultDrive, Scenes.Default());

        m_defaultPlainWorld = TestBodies.World();
        Scenes.Build(m_defaultPlainWorld, new TestDrive(), Scenes.Default());

        m_stressWorld = TestBodies.World(initialCapacity: 1000);
        m_stressDrive = new TestDrive();
        Scenes.Build(m_stressWorld, m_stressDrive, Stress1000());

        m_stressPlainWorld = TestBodies.World(initialCapacity: 1000);
        Scenes.Build(m_stressPlainWorld, new TestDrive(), Stress1000());

        for (var tick = 0; tick < WarmUpTicks; tick++)
        {
            m_oldWorld.CollideObjects(TickTime);
            m_defaultDrive.RunTick(m_defaultWorld, TickTime);
            m_defaultPlainWorld.Step(TickTime);
            m_stressDrive.RunTick(m_stressWorld, TickTime);
            m_stressPlainWorld.Step(TickTime);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Step")]
    public void Step_DefaultScene_Old() => m_oldWorld.CollideObjects(TickTime);

    [Benchmark]
    [BenchmarkCategory("Step")]
    public void Step_DefaultScene() => m_defaultDrive.RunTick(m_defaultWorld, TickTime);

    [Benchmark]
    [BenchmarkCategory("Step")]
    public void Step_DefaultScene_PlainStep() => m_defaultPlainWorld.Step(TickTime);

    [Benchmark]
    [BenchmarkCategory("Stress")]
    public void Step_Stress1000() => m_stressDrive.RunTick(m_stressWorld, TickTime);

    [Benchmark]
    [BenchmarkCategory("Stress")]
    public void Step_Stress1000_PlainStep() => m_stressPlainWorld.Step(TickTime);

    private static SceneBody[] Stress1000()
    {
        var random = new Random(1000);
        var bodies = new SceneBody[1000];
        bodies[0] = new SceneBody(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(200, 1, 200)), 0, 0, 0);

        for (var i = 1; i < bodies.Length; i++)
        {
            var cell = i - 1;
            var position = new Vector3(2 * (cell % 10) - 9, 2 * (cell / 100), 2 * (cell / 10 % 10) - 9);
            var heading = (float)(random.NextDouble() * 2 * Math.PI - Math.PI);
            bodies[i] = new SceneBody(TestBodies.Cube(position, Vector3.One, new Vector3(0, heading, 0)), 2, 2, 1);
        }

        return bodies;
    }
}
