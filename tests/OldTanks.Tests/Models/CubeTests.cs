using System.Numerics;
using CoolEngine.Models;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OldTanks.Infrastructure;
using OldTanks.Models;
using OldTanks.Tests.TestSupport;

namespace OldTanks.Tests.Models;

/// <summary>
/// Golden values of <c>Collision.IntersectRay</c> on the game's cube shape (<see cref="Cube"/>): the point the ray hits,
/// recorded on today's code and compared within the golden tolerance. Cube A is 2 × 2 × 2 at the origin, cube B is
/// 2 × 1 × 4 at (1, 2, 3) rotated by (0.3, 0.5, 0.2) radians.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class CubeTests
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
    public void IntersectRay_RayFromOutside_MatchesGolden(bool rotatedCube, Vector3 start, Vector3 end, float[] expectedPoint)
    {
        var cube = rotatedCube ? CreateCubeB() : CreateCubeA();

        var hit = cube.Collision.IntersectRay(new Ray(start, end), out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True, "hit");
            SimulationGoldenAssert.AreClose(expectedPoint, [point.X, point.Y, point.Z], "hit point");
        });
    }

    [Test]
    public void IntersectRay_RayStartsInside_ReportsNoHit()
    {
        // No face facing the ray lies ahead of a start inside the cube: a quirk, ported as it is.
        AssertNoHit(new Ray(new Vector3(0.2f, 0.1f, 0), new Vector3(0.2f, 0.1f, 10)));
    }

    [Test]
    public void IntersectRay_RayPointsAway_ReportsNoHit()
    {
        AssertNoHit(new Ray(new Vector3(0, 0, 5), new Vector3(0, 0, 10)));
    }

    [Test]
    public void IntersectRay_RayMisses_ReportsNoHit()
    {
        AssertNoHit(new Ray(new Vector3(3, 0, 5), new Vector3(3, 0, -5)));
    }

    [Test]
    public void IntersectRay_HitBeyondRayEnd_ReportsHit()
    {
        var cube = CreateCubeA();

        var hit = cube.Collision.IntersectRay(new Ray(new Vector3(0, 0, 50), new Vector3(0, 0, 49)), out var point);

        // The end only gives the direction.
        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True, "hit");
            // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); compared within 1e-5 relative.
            SimulationGoldenAssert.AreClose([0f, 0f, 1f], [point.X, point.Y, point.Z], "hit point");
        });
    }

    private static IEnumerable<TestCaseData> OutsideCases()
    {
        // Recorded on 2026-11-27 at 7f33680 (OT-20 WP1, today's code); compared within 1e-5 relative.
        // The edge and corner cases are the quirk "the first face in list order wins": the front face is tested before
        // the top and the right one.
        yield return Case("A front face centre", false, new Vector3(0, 0, 5), new Vector3(0, 0, -5), [0f, 0f, 1f]);
        yield return Case("A off centre", false, new Vector3(5, 0.3f, -0.2f), new Vector3(-5, 0.3f, -0.2f), [1f, 0.3f, -0.2f]);
        yield return Case("A oblique", false, new Vector3(3, 2, 4), new Vector3(-3, -2, -4), [0.75f, 0.5f, 1f]);
        yield return Case("A top-front edge", false, new Vector3(0.25f, 5, 5), new Vector3(0.25f, -3, -3), [0.25f, 1f, 1f]);
        yield return Case("A corner", false, new Vector3(5, 5, 5), new Vector3(0, 0, 0), [1f, 1f, 1f]);
        yield return Case("A grazing the top plane", false, new Vector3(5, 1, 0), new Vector3(-5, 1, 0), [1f, 1f, 0f]);
        yield return Case("B centre", true, new Vector3(1, 2, 10), new Vector3(1, 2, 3), [1f, 2f, 4.927946f]);
        yield return Case("B from below", true, new Vector3(1.2f, -5, 3.1f), new Vector3(1.2f, 5, 3.1f), [1.2f, 1.4932485f, 3.1f]);
    }

    private static TestCaseData Case(string name, bool rotatedCube, Vector3 start, Vector3 end, float[] expectedPoint) =>
        new TestCaseData(rotatedCube, start, end, expectedPoint).SetName($"IntersectRay_RayFromOutside_MatchesGolden({name})");

    private static void AssertNoHit(Ray ray)
    {
        var cube = CreateCubeA();

        var hit = cube.Collision.IntersectRay(ray, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.False, "hit");
            Assert.That(point, Is.EqualTo(Vector3.Zero), "point");
        });
    }

    private static Cube CreateCubeA() => CreateCube(new Vector3(2, 2, 2), Vector3.Zero, Vector3.Zero);

    private static Cube CreateCubeB() => CreateCube(new Vector3(2, 1, 4), new Vector3(1, 2, 3), new Vector3(0.3f, 0.5f, 0.2f));

    // The Collision must exist before the first ApplyTransformation, or its vertices stay in model space.
    private static Cube CreateCube(Vector3 size, Vector3 position, Vector3 rotation)
    {
        var cube = new Cube { Size = size, Rotation = rotation, Position = position };
        // The shapes are registered by the static constructors of Cube and Sphere, so they exist once an instance was made.
        cube.Collision = new Collision(cube,
            GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName)!);
        cube.ApplyTransformation();

        return cube;
    }
}
