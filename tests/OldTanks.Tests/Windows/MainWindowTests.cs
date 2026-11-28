using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Models;
using CoolEngine.Services;
using OldTanks.Tests.TestSupport;

namespace OldTanks.Tests.Windows;

/// <summary>
/// Golden points of the pick-ray loop of <c>OldTanks.Windows.MainWindow.WorldHandler</c>, through the test-support copy
/// <see cref="PickRay"/> (the window needs OpenGL), over the default scene settled by 600 ticks at 5 sub-steps. The goldens
/// are recorded on today's code and compared within the golden tolerance.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class MainWindowTests
{
    [SetUp]
    public void SetUp()
    {
        SimulationScenes.AddFakeScenes();
        // The setting starts at 0 and cannot be set back to 0, so every test sets it.
        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
    }

    [TearDown]
    public void TearDown()
    {
        SimulationScenes.RemoveFakeScenes();
        EngineSettings.Current.CollisionIterations = SimulationRun.SubSteps;
    }

    [TestCaseSource(nameof(PickCases))]
    public void WorldHandler_PickRayInSettledDefaultScene_MatchesGolden(bool fromFreeCamera, Vector3 start, Vector3 end,
        float[] expectedPoint)
    {
        var world = SimulationScenes.CreateDefaultScene();
        for (var tick = 1; tick <= 600; tick++)
            world.CollideObjects(SimulationRun.TickSeconds);

        var ray = fromFreeCamera ? PickRay.FromFreeCamera(new Camera()) : new Ray(start, end);

        var hit = PickRay.Nearest(world.WorldObjects, ray, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True, "hit");
            SimulationGoldenAssert.AreClose(expectedPoint, [point.X, point.Y, point.Z], "nearest hit point");
        });
    }

    private static IEnumerable<TestCaseData> PickCases()
    {
        // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); 60 Hz, 5 sub-steps; compared within 1e-5 relative.
        // The first case is the default camera's own ray, (0, -1, 0) to (100, 0, 0).
        yield return Case("default camera ray", true, Vector3.Zero, Vector3.Zero, [0.49997497f, -0.99500024f, 0f]);
        yield return Case("down through the static sphere", false, new Vector3(0, 10, 0), new Vector3(0, -10, 0), [0f, 5.5f, 0f]);
        yield return Case("down onto the dynamic cube", false, new Vector3(0, 10, 1.5f), new Vector3(0, -10, 1.5f), [0f, -0.08578682f, 1.5f]);
        yield return Case("towards Wall 1", false, new Vector3(0, 0.5f, 5), new Vector3(0, 0.5f, -20), [0f, 0.5f, -7.5f]);
        yield return Case("towards Wall 2", false, new Vector3(-5, 0.5f, 0.5f), new Vector3(20, 0.5f, 0.5f), [8.500001f, 0.5f, 0.5f]);
    }

    private static TestCaseData Case(string name, bool fromFreeCamera, Vector3 start, Vector3 end, float[] expectedPoint) =>
        new TestCaseData(fromFreeCamera, start, end, expectedPoint)
            .SetName($"WorldHandler_PickRayInSettledDefaultScene_MatchesGolden({name})");
}
