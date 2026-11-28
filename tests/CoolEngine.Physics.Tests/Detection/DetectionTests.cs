using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Tests.TestSupport;

namespace CoolEngine.Physics.Tests.Detection;

/// <summary>
/// The 21 <c>CheckCollision_*</c> tests of CoolEngine.Tests.PhysicEngine.Core.Collision.CollisionTests, with the same
/// arrangements, expectations and tolerances, on <see cref="NarrowPhase.CheckCollision"/>.
/// </summary>
[TestFixture]
public sealed class DetectionTests
{
    private const float DepthTolerance = 1e-4f;
    private const float NormalTolerance = 1e-4f;

    private static readonly Vector3 s_polygonSize = new Vector3(2, 2, 2);
    private static readonly Vector3 s_sphereSize = new Vector3(1, 1, 1);
    private static readonly float s_sqrt2 = MathF.Sqrt(2f);

    // Q in the plan: a tilt that keeps the edge-to-edge arrangements off every world axis.
    private static readonly Matrix4 s_q =
        Matrix4.CreateRotationY(MathHelper.DegreesToRadians(30)) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(30));

    // M in the plan: uneven scale, three rotations and a translation.
    private static readonly Matrix4 s_rotationOfM =
        Matrix4.CreateRotationZ(-0.4f) * (Matrix4.CreateRotationY(0.7f) * Matrix4.CreateRotationX(0.3f));
    private static readonly Matrix4 s_m =
        Matrix4.CreateTranslation(1, 2, 3) * (s_rotationOfM * Matrix4.CreateScale(2, 0.5f, 1));
    private static readonly Vector3 s_positionOfM = new Vector3(1, 2, 3);

    [Test]
    public void CheckCollision_RotatedCubeRestingOnCubeFace_ReturnsFaceNormalAndDepth()
    {
        var a = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var positionB = new Vector3(0, 1.9f, 0);
        var b = CreatePolygon(TestShapes.Cube(),
            Matrix4.CreateTranslation(positionB) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(30)), positionB);

        var collides = Check(a, b, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
    }

    [Test]
    public void CheckCollision_AxisAlignedCubesOverlapping_ReturnsFaceNormalAndDepth()
    {
        var a = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var positionB = new Vector3(0, 1.9f, 0);
        var b = CreatePolygon(TestShapes.Cube(), Matrix4.CreateTranslation(positionB), positionB);

        var collides = Check(a, b, out var normal, out var depth);

        Assert.Multiple(() =>
        {
            AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
            Assert.That(float.IsNaN(normal.X) || float.IsNaN(normal.Y) || float.IsNaN(normal.Z), Is.False,
                "normal has a NaN component");
        });
    }

    [Test]
    public void CheckCollision_SphereOverCubeFace_ReturnsFaceNormalAndDepth()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0, 1.4f, 0));

        var collides = Check(cube, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
    }

    [Test]
    public void CheckCollision_SphereCentreInsideCube_ReturnsNearestFaceNormalAndDepth()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0, 0.8f, 0));

        var collides = Check(cube, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.7f);
    }

    [Test]
    public void CheckCollision_SphereOverTetrahedronFace_ReturnsFaceNormalAndDepth()
    {
        var tetrahedron = CreatePolygon(TestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(-0.564273f, -0.564273f, -0.564273f));

        var collides = Check(tetrahedron, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(-0.577350f, -0.577350f, -0.577350f), 0.1f);
    }

    [Test]
    public void CheckCollision_SpheresAroundRotatedScaledBox_CuboidAndTriangulatedShapesAgree()
    {
        var cuboid = CreatePolygon(TestShapes.Cube(), s_m, s_positionOfM);
        var triangulated = CreatePolygon(TestShapes.TriangulatedCube(), s_m, s_positionOfM);
        var random = new Random(1);
        var mismatches = new List<string>();

        for (var i = 0; i < 2000; i++)
        {
            var u = (float)(random.NextDouble() * 6 - 3);
            var v = (float)(random.NextDouble() * 6 - 3);
            var w = (float)(random.NextDouble() * 6 - 3);
            var sphere = CreateSphere(s_positionOfM + new Vector3(u, v, w));

            var cuboidCollides = Check(cuboid, sphere, out _, out var cuboidDepth);
            var triangulatedCollides = Check(triangulated, sphere, out _, out var triangulatedDepth);

            if (cuboidCollides != triangulatedCollides)
                mismatches.Add($"centre {i} ({u}, {v}, {w}): cuboid {cuboidCollides}, triangulated {triangulatedCollides}");
            else if (cuboidCollides && MathF.Abs(cuboidDepth - triangulatedDepth) > DepthTolerance)
                mismatches.Add($"centre {i} ({u}, {v}, {w}): depth cuboid {cuboidDepth}, triangulated {triangulatedDepth}");
        }

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void CheckCollision_ParallelOnIndependentPairs_MatchesSerialResults()
    {
        const int pairsPerKind = 64;
        var cubeShape = TestShapes.Cube();
        var tetrahedronShape = TestShapes.Tetrahedron();
        var pairs = new List<(TestGeometry First, TestGeometry Second)>();

        for (var i = 0; i < pairsPerKind; i++)
        {
            pairs.Add(CreateCubesEdgeToEdge(cubeShape, 0.1f - i * 0.003f));
            pairs.Add((CreatePolygon(cubeShape, Matrix4.Identity, Vector3.Zero),
                CreateSphereOffVertex(new Vector3(1, 1, 1), 0.3f + i * 0.01f)));
            pairs.Add((CreatePolygon(tetrahedronShape, Matrix4.Identity, Vector3.Zero),
                CreateSphereOffVertex(tetrahedronShape.Vertices[0], 0.3f + i * 0.01f)));
        }

        var serial = new ContactResult[pairs.Count];
        var parallel = new ContactResult[pairs.Count];

        for (var i = 0; i < pairs.Count; i++)
            serial[i] = Check(pairs[i]);
        Parallel.For(0, pairs.Count, i => parallel[i] = Check(pairs[i]));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < pairs.Count; i++)
                Assert.That(parallel[i], Is.EqualTo(serial[i]), $"pair {i}");
        });
    }

    [Test]
    public void CheckCollision_WarmedUp_AllocatesNothing()
    {
        var (cubeA, cubeB) = CreateCubesEdgeToEdge(TestShapes.Cube(), -0.1f);
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var cubeSphere = CreateSphereOffVertex(new Vector3(1, 1, 1), 0.4f);
        // Not a cuboid: takes the separating axes, whose face loop ties at a corner and reaches the tie-break.
        var triangulatedCube = CreatePolygon(TestShapes.TriangulatedCube(), Matrix4.Identity, Vector3.Zero);
        var tetrahedron = CreatePolygon(TestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var tetrahedronSphere = CreateSphere(new Vector3(-0.564273f, -0.564273f, -0.564273f));
        var transformation = cubeA.Transformation;
        var position = cubeA.Position;
        var size = cubeA.Size;
        RunAll();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            RunAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.EqualTo(0));

        void RunAll()
        {
            Check(cubeA, cubeB, out _, out _);
            Check(cube, cubeSphere, out _, out _);
            Check(triangulatedCube, cubeSphere, out _, out _);
            Check(tetrahedron, tetrahedronSphere, out _, out _);
            cubeA.Update(transformation, position, size);
        }
    }

    [Test]
    public void CheckCollision_CubesEdgeToEdgeWithGap_ReturnsFalse()
    {
        var (a, b) = CreateCubesEdgeToEdge(TestShapes.Cube(), 0.1f);

        var collides = Check(a, b, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_CubesEdgeToEdgeOverlapping_ReturnsEdgeAxisAndDepth()
    {
        var (a, b) = CreateCubesEdgeToEdge(TestShapes.Cube(), -0.1f);

        var collides = Check(a, b, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0.25f, 0.866025f, 0.433013f), 0.1f);
    }

    [Test]
    public void CheckCollision_TetrahedronAndCubeEdgeToEdgeWithGap_ReturnsFalse()
    {
        var tetrahedron = CreatePolygon(TestShapes.Tetrahedron(), s_q, Vector3.Zero);
        var positionB = s_q.TransformPosition(new Vector3(0, 1 + s_sqrt2 + 0.1f, 0));
        var cube = CreatePolygon(TestShapes.Cube(),
            Matrix4.CreateTranslation(positionB) *
            (s_q * (Matrix4.CreateRotationY(MathHelper.PiOver4) * Matrix4.CreateRotationX(MathHelper.PiOver4))), positionB);

        var collides = Check(tetrahedron, cube, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_SphereOffCubeCornerWithGap_ReturnsFalse()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(1.404145f, 1.404145f, 1.404145f));

        var collides = Check(cube, sphere, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_SphereOffCubeEdgeWithGap_ReturnsFalse()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(1.424264f, 1.424264f, 0));

        var collides = Check(cube, sphere, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_SphereOnCubeCorner_ReturnsDiagonalNormalAndDepth()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(1.230940f, 1.230940f, 1.230940f));

        var collides = Check(cube, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0.577350f, 0.577350f, 0.577350f), 0.1f);
    }

    [Test]
    public void CheckCollision_SphereFirstOnCubeCorner_MatchesCubeFirst()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(1.230940f, 1.230940f, 1.230940f));
        var cubeFirst = Check(cube, sphere, out var cubeFirstNormal, out var cubeFirstDepth);

        var sphereFirst = Check(sphere, cube, out var normal, out var depth);

        Assert.Multiple(() =>
        {
            Assert.That(sphereFirst, Is.EqualTo(cubeFirst), "collides");
            AssertVector(normal, cubeFirstNormal, NormalTolerance, "normal");
            Assert.That(depth, Is.EqualTo(cubeFirstDepth).Within(DepthTolerance), "depth");
        });
    }

    [Test]
    public void CheckCollision_SphereOffTetrahedronVertexWithGap_ReturnsFalse()
    {
        var tetrahedron = CreatePolygon(TestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(1.462622f, 1.231311f, 1.115655f));

        var collides = Check(tetrahedron, sphere, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_SphereOffTetrahedronEdgeWithGap_ReturnsFalse()
    {
        var tetrahedron = CreatePolygon(TestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0.243214f, 1.470358f, 0.556786f));

        var collides = Check(tetrahedron, sphere, out _, out _);

        Assert.That(collides, Is.False);
    }

    [Test]
    public void CheckCollision_SpheresAroundShearedBox_CuboidAndTriangulatedShapesAgree()
    {
        // Rotating before an uneven scale shears the box: its current axes are no longer perpendicular.
        var shear = Matrix4.CreateTranslation(1, 2, 3) *
                    (Matrix4.CreateRotationX(0.7f) * (Matrix4.CreateScale(2, 0.5f, 1) * Matrix4.CreateRotationZ(0.5f)));
        var cuboid = CreatePolygon(TestShapes.Cube(), shear, s_positionOfM);
        var triangulated = CreatePolygon(TestShapes.TriangulatedCube(), shear, s_positionOfM);
        var random = new Random(1);
        var mismatches = new List<string>();

        for (var i = 0; i < 2000; i++)
        {
            var u = (float)(random.NextDouble() * 6 - 3);
            var v = (float)(random.NextDouble() * 6 - 3);
            var w = (float)(random.NextDouble() * 6 - 3);
            var sphere = CreateSphere(s_positionOfM + new Vector3(u, v, w));

            var cuboidCollides = Check(cuboid, sphere, out _, out var cuboidDepth);
            var triangulatedCollides = Check(triangulated, sphere, out _, out var triangulatedDepth);

            if (cuboidCollides != triangulatedCollides)
                mismatches.Add($"centre {i} ({u}, {v}, {w}): cuboid {cuboidCollides}, triangulated {triangulatedCollides}");
            else if (cuboidCollides && MathF.Abs(cuboidDepth - triangulatedDepth) > DepthTolerance)
                mismatches.Add($"centre {i} ({u}, {v}, {w}): depth cuboid {cuboidDepth}, triangulated {triangulatedDepth}");
        }

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void CheckCollision_SphereNearCubeOfZeroWidth_ReturnsEdgeContactWithoutThrowing()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.CreateScale(0, 1, 1), Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0, 1.4f, 0));

        var collides = Check(cube, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
    }

    [Test]
    public void CheckCollision_SphereAtCubeCentre_ReturnsPlusXFaceNormal()
    {
        var cube = CreatePolygon(TestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(Vector3.Zero);

        var collides = Check(cube, sphere, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(1, 0, 0), 1.5f);
    }

    [Test]
    public void CheckCollision_CubeWithCollapsedEdgeOverlappingCube_ReturnsTrue()
    {
        // Vertex 1 moved onto vertex 0 collapses the edge between them to a point, whose direction is NaN.
        var vertices = TestShapes.CubeVertices();
        vertices[1] = new Vector3(-1, 1, 1);
        var collapsedShape = new PolygonShape(vertices, TestShapes.CubeFaces());
        var a = CreatePolygon(collapsedShape, Matrix4.Identity, Vector3.Zero);
        var positionB = new Vector3(0, 0.5f, 0);
        var b = CreatePolygon(TestShapes.Cube(), Matrix4.CreateTranslation(positionB), positionB);

        var collides = Check(a, b, out _, out _);

        Assert.That(collides, Is.True);
    }

    // Touching bounds still reach the narrow test, and today's sphere test counts a distance equal to the sum of the
    // radii as a contact (DetectionParityTests checks the same pair against the old Collision).
    [Test]
    public void CheckCollision_SpheresExactlyTouchingOnAnAxis_ReturnsTrueWithZeroDepth()
    {
        var first = CreateSphere(Vector3.Zero);
        var second = CreateSphere(new Vector3(1, 0, 0));

        var collides = Check(first, second, out var normal, out var depth);

        Assert.Multiple(() =>
        {
            Assert.That(collides, Is.True, "collides");
            AssertVector(normal, new Vector3(1, 0, 0), NormalTolerance, "normal");
            Assert.That(depth, Is.EqualTo(0f), "depth");
        });
    }

    private static TestGeometry CreatePolygon(PolygonShape shape, Matrix4 transformation, Vector3 position) =>
        TestGeometry.Create(shape, transformation, position, s_polygonSize);

    private static TestGeometry CreateSphere(Vector3 centre) =>
        TestGeometry.Create(TestBodies.SphereShape, Matrix4.CreateTranslation(centre), centre, s_sphereSize);

    // A sphere whose centre lies on the ray from the origin through the vertex, the given distance past it.
    private static TestGeometry CreateSphereOffVertex(Vector3 vertex, float distance) =>
        CreateSphere(vertex + Vector3.Normalize(vertex) * distance);

    // Two cubes, one turned 45 degrees about Z and one about X, both tilted by Q, so an edge of each faces the
    // other across the given gap (negative: overlapping) along Q's Y.
    private static (TestGeometry First, TestGeometry Second) CreateCubesEdgeToEdge(PolygonShape shape, float gap)
    {
        var first = CreatePolygon(shape, s_q * Matrix4.CreateRotationZ(MathHelper.PiOver4), Vector3.Zero);
        var positionB = s_q.TransformPosition(new Vector3(0, 2 * s_sqrt2 + gap, 0));
        var second = CreatePolygon(shape,
            Matrix4.CreateTranslation(positionB) * (s_q * Matrix4.CreateRotationX(MathHelper.PiOver4)), positionB);

        return (first, second);
    }

    private static bool Check(TestGeometry first, TestGeometry second, out Vector3 normal, out float depth) =>
        NarrowPhase.CheckCollision(first.View(), second.View(), out normal, out depth);

    private static ContactResult Check((TestGeometry First, TestGeometry Second) pair)
    {
        var collides = Check(pair.First, pair.Second, out var normal, out var depth);

        return new ContactResult(collides, BitConverter.SingleToInt32Bits(normal.X),
            BitConverter.SingleToInt32Bits(normal.Y), BitConverter.SingleToInt32Bits(normal.Z),
            BitConverter.SingleToInt32Bits(depth));
    }

    private static void AssertContact(bool collides, Vector3 normal, float depth, Vector3 expectedNormal,
        float expectedDepth)
    {
        Assert.Multiple(() =>
        {
            Assert.That(collides, Is.True, "collides");
            AssertVector(normal, expectedNormal, NormalTolerance, "normal");
            Assert.That(depth, Is.EqualTo(expectedDepth).Within(DepthTolerance), "depth");
        });
    }

    private static void AssertVector(Vector3 actual, Vector3 expected, float tolerance, string what)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), $"{what}.X ({actual})");
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), $"{what}.Y ({actual})");
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), $"{what}.Z ({actual})");
    }

    // Bit patterns rather than floats, so equality is bit-identical and a NaN equals the same NaN.
    private readonly record struct ContactResult(bool Collides, int NormalX, int NormalY, int NormalZ, int Depth);
}
