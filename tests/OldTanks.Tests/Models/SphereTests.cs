using System.Numerics;
using CoolEngine.Models;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OldTanks.Infrastructure;
using OldTanks.Models;
using OldTanks.Tests.TestSupport;

namespace OldTanks.Tests.Models;

/// <summary>
/// Golden values of <c>Collision.IntersectRay</c> on the game's sphere shape (<see cref="Sphere"/>): the point the ray
/// hits, recorded on today's code and compared within the golden tolerance. Sphere C has size 2 (radius 1) at the origin.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class SphereTests
{
    [SetUp]
    public void SetUp()
    {
        SimulationScenes.AddFakeScenes();
    }

    [TearDown]
    public void TearDown()
    {
        SimulationScenes.RemoveFakeScenes();
    }

    [TestCaseSource(nameof(OutsideCases))]
    public void IntersectRay_RayFromOutside_MatchesGolden(Vector3 start, Vector3 end, float[] expectedPoint)
    {
        var sphere = CreateSphereC();

        var hit = sphere.Collision.IntersectRay(new Ray(start, end), out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True, "hit");
            SimulationGoldenAssert.AreClose(expectedPoint, [point.X, point.Y, point.Z], "hit point");
        });
    }

    [Test]
    public void IntersectRay_RayStartsInside_ReportsExitPoint()
    {
        var sphere = CreateSphereC();

        var hit = sphere.Collision.IntersectRay(new Ray(new Vector3(0.2f, 0, 0), new Vector3(0.2f, 0, 10)), out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True, "hit");
            SimulationGoldenAssert.AreClose(s_exitPointGolden, [point.X, point.Y, point.Z], "exit point");
        });
    }

    [Test]
    public void IntersectRay_SphereBehindRay_ReportsNoHit()
    {
        AssertNoHit(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, 10)));
    }

    [Test]
    public void IntersectRay_RayMisses_ReportsNoHit()
    {
        AssertNoHit(new Ray(new Vector3(2, 0, 5), new Vector3(2, 0, -5)));
    }

    private static IEnumerable<TestCaseData> OutsideCases()
    {
        // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); compared within 1e-5 relative.
        yield return Case("centre", new Vector3(0, 0, 5), new Vector3(0, 0, -5), [0f, 0f, 1f]);
        yield return Case("off centre", new Vector3(0.5f, 0.3f, 5), new Vector3(0.5f, 0.3f, -5), [0.5f, 0.3f, 0.8124037f]);
        yield return Case("tangent", new Vector3(1, 0, 5), new Vector3(1, 0, -5), [1f, 0f, 0f]);
    }

    private static TestCaseData Case(string name, Vector3 start, Vector3 end, float[] expectedPoint) =>
        new TestCaseData(start, end, expectedPoint).SetName($"IntersectRay_RayFromOutside_MatchesGolden({name})");

    // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); compared within 1e-5 relative.
    private static readonly float[] s_exitPointGolden = [0.2f, 0f, 0.9797959f];

    private static void AssertNoHit(Ray ray)
    {
        var sphere = CreateSphereC();

        var hit = sphere.Collision.IntersectRay(ray, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.False, "hit");
            Assert.That(point, Is.EqualTo(Vector3.Zero), "point");
        });
    }

    // The Collision must exist before the first ApplyTransformation, or its vertices stay in model space.
    private static Sphere CreateSphereC()
    {
        var sphere = new Sphere { Size = new Vector3(2), Position = Vector3.Zero };
        // The shapes are registered by the static constructors of Cube and Sphere, so they exist once an instance was made.
        sphere.Collision = new Collision(sphere,
            GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.SphereCollisionName)!);
        sphere.ApplyTransformation();

        return sphere;
    }
}
