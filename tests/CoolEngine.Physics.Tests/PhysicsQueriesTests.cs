using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Tests.TestSupport;

namespace CoolEngine.Physics.Tests;

[TestFixture]
public sealed class PhysicsQueriesTests
{
    [Test]
    public void IntersectRay_TwoFacesOnTheRay_ReturnsTheFirstInListOrder()
    {
        // Two unit quads facing -X, at x = 3 (listed first) and x = 1; each lists a corner, its two neighbours, then
        // the opposite corner.
        Vector3[] vertices =
        [
            new Vector3(3, 0, 0), new Vector3(3, 1, 0), new Vector3(3, 0, 1), new Vector3(3, 1, 1),
            new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1),
        ];
        ShapeFace[] faces =
        [
            new ShapeFace([0, 1, 2, 3], new Vector3(-1, 0, 0)),
            new ShapeFace([4, 5, 6, 7], new Vector3(-1, 0, 0)),
        ];
        var shape = new PolygonShape(vertices, faces);
        Vector3[] normals = [faces[0].Normal, faces[1].Normal];
        var ray = new Ray(new Vector3(-5, 0.5f, 0.5f), new Vector3(5, 0.5f, 0.5f));

        var hit = PhysicsQueries.IntersectRay(shape, vertices, normals, ray, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(point.X, Is.EqualTo(3f).Within(1e-5f));
        });
    }

    [Test]
    public void IntersectRay_RayMissesCube_ReturnsFalse()
    {
        var shape = TestShapes.Cube();
        var normals = new Vector3[shape.FaceCount];
        PhysicsQueries.ComputeWorldNormals(shape, Vector3.Zero, Matrix3.Identity, normals);
        var ray = new Ray(new Vector3(-10, 5, 0), new Vector3(10, 5, 0));

        var hit = PhysicsQueries.IntersectRay(shape, shape.Vertices, normals, ray, out _);

        Assert.That(hit, Is.False);
    }

    [Test]
    public void IntersectRay_SphereAhead_ReturnsTheNearerIntersection()
    {
        var ray = new Ray(Vector3.Zero, new Vector3(100, 0, 0));

        var hit = PhysicsQueries.IntersectRay(new Vector3(5, 0, 0), 1, ray, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(point.X, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(point.Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(point.Z, Is.EqualTo(0f).Within(1e-5f));
        });
    }

    [Test]
    public void IntersectRay_SphereBehindTheStart_ReturnsFalse()
    {
        var ray = new Ray(Vector3.Zero, new Vector3(100, 0, 0));

        var hit = PhysicsQueries.IntersectRay(new Vector3(-5, 0, 0), 1, ray, out _);

        Assert.That(hit, Is.False);
    }

    [Test]
    public void RayCastNearest_TwoBodiesOnTheRay_ReturnsTheNearer()
    {
        var size = new Vector3(2);
        Shape[] shapes = [TestBodies.CubeShape, TestBodies.CubeShape];
        Vector3[] positions = [new Vector3(10, 0, 0), new Vector3(4, 0, 0)];
        Matrix3[] linears = [TestBodies.Linear(Vector3.Zero, size), TestBodies.Linear(Vector3.Zero, size)];
        Vector3[] sizes = [size, size];
        var ray = new Ray(Vector3.Zero, new Vector3(100, 0, 0));

        var hit = PhysicsQueries.RayCastNearest(shapes, positions, linears, sizes, ray, new Vector3[8], new Vector3[6],
            out var index, out var point);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(point.X, Is.EqualTo(3f).Within(1e-5f));
        });
    }

    [Test]
    public void RayCastNearest_EqualDistances_ReturnsTheLowerIndex()
    {
        var size = new Vector3(2);
        Shape[] shapes = [TestBodies.CubeShape, TestBodies.CubeShape];
        Vector3[] positions = [new Vector3(4, 0, 0), new Vector3(4, 0, 0)];
        Matrix3[] linears = [TestBodies.Linear(Vector3.Zero, size), TestBodies.Linear(Vector3.Zero, size)];
        Vector3[] sizes = [size, size];
        var ray = new Ray(Vector3.Zero, new Vector3(100, 0, 0));

        var hit = PhysicsQueries.RayCastNearest(shapes, positions, linears, sizes, ray, new Vector3[8], new Vector3[6],
            out var index, out _);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(index, Is.EqualTo(0));
        });
    }
}
