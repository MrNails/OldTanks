namespace CoolEngine.Tests.PhysicEngine.Core.Collision;

// Inside the namespace so that "Collision" names the engine class, not this test namespace's last segment.
using CoolEngine.Models;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Tests.TestSupport;
using OpenTK.Mathematics;

[TestFixture]
public sealed class CollisionTests
{
    private const float DepthTolerance = 1e-4f;
    private const float NormalTolerance = 1e-4f;

    private static readonly Vector3 s_polygonSize = new Vector3(2, 2, 2);
    private static readonly Vector3 s_sphereSize = new Vector3(1, 1, 1);
    private static readonly float s_sqrt2 = MathF.Sqrt(2f);

    // Q in the plan: a tilt that keeps the edge-to-edge arrangements off every world axis.
    private static readonly Matrix4 s_q =
        Matrix4.CreateRotationX(MathHelper.DegreesToRadians(30)) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(30));

    // M in the plan: uneven scale, three rotations and a translation.
    private static readonly Matrix4 s_rotationOfM =
        Matrix4.CreateRotationX(0.3f) * Matrix4.CreateRotationY(0.7f) * Matrix4.CreateRotationZ(-0.4f);
    private static readonly Matrix4 s_m =
        Matrix4.CreateScale(2, 0.5f, 1) * s_rotationOfM * Matrix4.CreateTranslation(1, 2, 3);
    private static readonly Vector3 s_positionOfM = new Vector3(1, 2, 3);

    [Test]
    public void Constructor_CubeShape_CopiesEveryVertexAndFace()
    {
        var original = CollisionTestShapes.Cube();

        var cube = CreatePolygon(original, Matrix4.Identity, Vector3.Zero);

        var copy = cube.Collision!.CollisionData;
        Assert.Multiple(() =>
        {
            Assert.That(copy.Vertices, Is.EqualTo(original.Vertices));
            Assert.That(copy.Faces, Has.Count.EqualTo(6));

            for (var i = 0; i < original.Faces.Count; i++)
            {
                Assert.That(copy.Faces[i].Indices, Is.EqualTo(original.Faces[i].Indices), $"face {i} indices");
                AssertVector(copy.Faces[i].Normal, original.Faces[i].Normal, NormalTolerance, $"face {i} normal");
            }
        });
    }

    [Test]
    public void UpdateCollision_SphereShape_BoxIsPositionPlusMinusRadius()
    {
        var position = new Vector3(0, 5, 0);

        var sphere = CreateSphere(position);

        var box = sphere.Collision!.CollisionData.BoundingBox;
        Assert.Multiple(() =>
        {
            Assert.That(box.Min, Is.EqualTo(new Vector3(-0.5f, 4.5f, -0.5f)));
            Assert.That(box.Max, Is.EqualTo(new Vector3(0.5f, 5.5f, 0.5f)));
        });
    }

    [Test]
    public void UpdateCollision_CubeScaledUnevenlyAndRotated_NormalsAreTheRotatedModelNormals()
    {
        var original = CollisionTestShapes.Cube();
        var rotation = new Matrix3(s_rotationOfM);

        var cube = CreatePolygon(original, s_m, s_positionOfM);

        var faces = cube.Collision!.CollisionData.Faces;
        Assert.Multiple(() =>
        {
            for (var i = 0; i < faces.Count; i++)
            {
                var expected = Vector3.Normalize(original.Faces[i].Normal * rotation);
                AssertVector(faces[i].Normal, expected, 1e-5f, $"face {i} normal");
            }
        });
    }

    [Test]
    public void UpdateCollision_CubeMirroredAlongX_NormalsPointOutward()
    {
        var transformation = Matrix4.CreateScale(-2, 1, 1) * Matrix4.CreateRotationZ(0.5f);

        var cube = CreatePolygon(CollisionTestShapes.Cube(), transformation, Vector3.Zero);

        var data = cube.Collision!.CollisionData;
        Assert.Multiple(() =>
        {
            for (var i = 0; i < data.Faces.Count; i++)
            {
                var face = data.Faces[i];
                var centroid = Vector3.Zero;
                foreach (var index in face.Indices)
                    centroid += data.Vertices[index];
                centroid /= face.Indices.Length;

                Assert.That(Vector3.Dot(face.Normal, centroid), Is.GreaterThan(0f), $"face {i} points inward");
                Assert.That(face.Normal.Length, Is.EqualTo(1f).Within(1e-5f), $"face {i} normal length");
            }
        });
    }

    [Test]
    public void CheckCollision_RotatedCubeRestingOnCubeFace_ReturnsFaceNormalAndDepth()
    {
        var a = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var positionB = new Vector3(0, 1.9f, 0);
        var b = CreatePolygon(CollisionTestShapes.Cube(),
            Matrix4.CreateRotationY(MathHelper.DegreesToRadians(30)) * Matrix4.CreateTranslation(positionB), positionB);

        var collides = a.Collision!.CheckCollision(b.Collision!, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
    }

    [Test]
    public void CheckCollision_AxisAlignedCubesOverlapping_ReturnsFaceNormalAndDepth()
    {
        var a = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var positionB = new Vector3(0, 1.9f, 0);
        var b = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.CreateTranslation(positionB), positionB);

        var collides = a.Collision!.CheckCollision(b.Collision!, out var normal, out var depth);

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
        var cube = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0, 1.4f, 0));

        var collides = cube.Collision!.CheckCollision(sphere.Collision!, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.1f);
    }

    [Test]
    public void CheckCollision_SphereCentreInsideCube_ReturnsNearestFaceNormalAndDepth()
    {
        var cube = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(0, 0.8f, 0));

        var collides = cube.Collision!.CheckCollision(sphere.Collision!, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(0, 1, 0), 0.7f);
    }

    [Test]
    public void CheckCollision_SphereOverTetrahedronFace_ReturnsFaceNormalAndDepth()
    {
        var tetrahedron = CreatePolygon(CollisionTestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var sphere = CreateSphere(new Vector3(-0.564273f, -0.564273f, -0.564273f));

        var collides = tetrahedron.Collision!.CheckCollision(sphere.Collision!, out var normal, out var depth);

        AssertContact(collides, normal, depth, new Vector3(-0.577350f, -0.577350f, -0.577350f), 0.1f);
    }

    [Test]
    public void CheckCollision_SpheresAroundRotatedScaledBox_CuboidAndTriangulatedShapesAgree()
    {
        var cuboid = CreatePolygon(CollisionTestShapes.Cube(), s_m, s_positionOfM);
        var triangulated = CreatePolygon(CollisionTestShapes.TriangulatedCube(), s_m, s_positionOfM);
        var random = new Random(1);
        var mismatches = new List<string>();

        for (var i = 0; i < 2000; i++)
        {
            var u = (float)(random.NextDouble() * 6 - 3);
            var v = (float)(random.NextDouble() * 6 - 3);
            var w = (float)(random.NextDouble() * 6 - 3);
            var sphere = CreateSphere(s_positionOfM + new Vector3(u, v, w));

            var cuboidCollides = cuboid.Collision!.CheckCollision(sphere.Collision!, out _, out var cuboidDepth);
            var triangulatedCollides =
                triangulated.Collision!.CheckCollision(sphere.Collision!, out _, out var triangulatedDepth);

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
        var cubeShape = CollisionTestShapes.Cube();
        var tetrahedronShape = CollisionTestShapes.Tetrahedron();
        var pairs = new List<(CollisionTestPhysicObject First, CollisionTestPhysicObject Second)>();

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
        var (cubeA, cubeB) = CreateCubesEdgeToEdge(CollisionTestShapes.Cube(), -0.1f);
        var cube = CreatePolygon(CollisionTestShapes.Cube(), Matrix4.Identity, Vector3.Zero);
        var cubeSphere = CreateSphereOffVertex(new Vector3(1, 1, 1), 0.4f);
        var tetrahedron = CreatePolygon(CollisionTestShapes.Tetrahedron(), Matrix4.Identity, Vector3.Zero);
        var tetrahedronSphere = CreateSphere(new Vector3(-0.564273f, -0.564273f, -0.564273f));
        var cubeACollision = cubeA.Collision!;
        var cubeBCollision = cubeB.Collision!;
        var cubeCollision = cube.Collision!;
        var cubeSphereCollision = cubeSphere.Collision!;
        var tetrahedronCollision = tetrahedron.Collision!;
        var tetrahedronSphereCollision = tetrahedronSphere.Collision!;
        cubeACollision.CheckCollision(cubeBCollision, out _, out _);
        cubeCollision.CheckCollision(cubeSphereCollision, out _, out _);
        tetrahedronCollision.CheckCollision(tetrahedronSphereCollision, out _, out _);
        cubeACollision.UpdateCollision();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            cubeACollision.CheckCollision(cubeBCollision, out _, out _);
            cubeCollision.CheckCollision(cubeSphereCollision, out _, out _);
            tetrahedronCollision.CheckCollision(tetrahedronSphereCollision, out _, out _);
            cubeACollision.UpdateCollision();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_SphereShape_WritesNothingToConsole()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            CreateSphere(new Vector3(0, 5, 0));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.That(captured.ToString(), Is.Empty);
    }

    [Test]
    public void Constructor_PolygonWithoutVertices_ThrowsArgumentException()
    {
        var physicObject = new CollisionTestPhysicObject { Size = s_polygonSize };
        var shape = CollisionTestShapes.PolygonWithoutVertices();

        var exception = Assert.Throws<ArgumentException>(() => _ = new Collision(physicObject, shape));

        Assert.That(exception?.ParamName, Is.EqualTo("originalCollision"));
    }

    [Test]
    public void Constructor_CubeShape_LeavesTheSharedOriginalUnchanged()
    {
        var original = CollisionTestShapes.Cube();
        var originalVertices = original.Vertices;
        var vertexValues = (Vector3[])originalVertices.Clone();
        var normals = new Vector3[original.Faces.Count];
        for (var i = 0; i < normals.Length; i++)
            normals[i] = original.Faces[i].Normal;

        for (var i = 0; i < 3; i++)
            CreatePolygon(original, s_m, s_positionOfM);

        Assert.Multiple(() =>
        {
            Assert.That(original.BoundingBox, Is.EqualTo(default(BoundingBox)));
            Assert.That(original.Vertices, Is.SameAs(originalVertices));
            Assert.That(original.Vertices, Is.EqualTo(vertexValues));
            Assert.That(original.Faces, Has.Count.EqualTo(6));

            for (var i = 0; i < normals.Length; i++)
                Assert.That(original.Faces[i].Normal, Is.EqualTo(normals[i]), $"face {i} normal");
        });
    }

    [Test]
    public void UpdateCollision_TetrahedronScaledTwiceAlongX_FaceNormalIsPerpendicularToTheFace()
    {
        var tetrahedron = CreatePolygon(CollisionTestShapes.Tetrahedron(), Matrix4.CreateScale(2, 1, 1), Vector3.Zero);

        var data = tetrahedron.Collision!.CollisionData;
        var face = data.Faces[0];
        var first = data.Vertices[face.Indices[0]];
        var edge1 = data.Vertices[face.Indices[1]] - first;
        var edge2 = data.Vertices[face.Indices[2]] - first;
        Assert.Multiple(() =>
        {
            AssertVector(face.Normal, new Vector3(-0.333333f, -0.666667f, -0.666667f), NormalTolerance, "face 0 normal");
            Assert.That(Vector3.Dot(face.Normal, edge1), Is.EqualTo(0f).Within(1e-5f), "normal . edge 1");
            Assert.That(Vector3.Dot(face.Normal, edge2), Is.EqualTo(0f).Within(1e-5f), "normal . edge 2");
        });
    }

    private static CollisionTestPhysicObject CreatePolygon(CollisionData shape, Matrix4 transformation, Vector3 position) =>
        CollisionTestPhysicObject.Create(shape, transformation, position, s_polygonSize);

    private static CollisionTestPhysicObject CreateSphere(Vector3 centre) =>
        CollisionTestPhysicObject.Create(CollisionTestShapes.Sphere(), Matrix4.CreateTranslation(centre), centre, s_sphereSize);

    // A sphere whose centre lies on the ray from the origin through the vertex, the given distance past it.
    private static CollisionTestPhysicObject CreateSphereOffVertex(Vector3 vertex, float distance) =>
        CreateSphere(vertex + Vector3.Normalize(vertex) * distance);

    // Two cubes, one turned 45 degrees about Z and one about X, both tilted by Q, so an edge of each faces the
    // other across the given gap (negative: overlapping) along Q's Y.
    private static (CollisionTestPhysicObject First, CollisionTestPhysicObject Second) CreateCubesEdgeToEdge(CollisionData shape, float gap)
    {
        var first = CreatePolygon(shape, Matrix4.CreateRotationZ(MathHelper.PiOver4) * s_q, Vector3.Zero);
        var positionB = Vector3.TransformPosition(new Vector3(0, 2 * s_sqrt2 + gap, 0), s_q);
        var second = CreatePolygon(shape,
            Matrix4.CreateRotationX(MathHelper.PiOver4) * s_q * Matrix4.CreateTranslation(positionB), positionB);

        return (first, second);
    }

    private static ContactResult Check((CollisionTestPhysicObject First, CollisionTestPhysicObject Second) pair)
    {
        var collides = pair.First.Collision!.CheckCollision(pair.Second.Collision!, out var normal, out var depth);

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
