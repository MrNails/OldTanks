using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Tests.TestSupport;
using Geometry = CoolEngine.Physics.Detection.Geometry;

namespace CoolEngine.Physics.Tests.Detection;

/// <summary>
/// The world geometry of a shape (ports of today's CollisionTests geometry tests and CollisionGoldenTests), through
/// <see cref="PhysicsQueries"/>' stage-1 form: position and the linear part.
/// </summary>
[TestFixture]
public sealed class GeometryTests
{
    private const float NormalTolerance = 1e-4f;

    // M of today's CollisionTests: uneven scale, three rotations and a translation (1, 2, 3).
    private static readonly Matrix4 s_rotationOfM =
        Matrix4.CreateRotationZ(-0.4f) * (Matrix4.CreateRotationY(0.7f) * Matrix4.CreateRotationX(0.3f));
    private static readonly Matrix3 s_linearOfM = new Matrix3(s_rotationOfM * Matrix4.CreateScale(2, 0.5f, 1));
    private static readonly Vector3 s_positionOfM = new Vector3(1, 2, 3);

    [Test]
    public void ComputeWorldGeometry_CubeAtIdentity_CopiesEveryVertexAndNormal()
    {
        var shape = TestShapes.Cube();
        var vertices = new Vector3[8];
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, Matrix3.Identity, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, Vector3.Zero, Matrix3.Identity, normals);

        Assert.Multiple(() =>
        {
            Assert.That(vertices, Is.EqualTo(shape.Vertices.ToArray()));

            for (var i = 0; i < shape.FaceCount; i++)
                AssertVector(normals[i], shape.GetFaceNormal(i), NormalTolerance, $"face {i} normal");
        });
    }

    [Test]
    public void ComputeBounds_Sphere_IsPositionPlusMinusRadius()
    {
        Geometry.ComputeBounds(null, ReadOnlySpan<Vector3>.Empty, new Vector3(0, 5, 0), 1, out var min, out var max);

        Assert.Multiple(() =>
        {
            Assert.That(min, Is.EqualTo(new Vector3(-0.5f, 4.5f, -0.5f)));
            Assert.That(max, Is.EqualTo(new Vector3(0.5f, 5.5f, 0.5f)));
        });
    }

    [Test]
    public void ComputeWorldNormals_CubeScaledUnevenlyAndRotated_AreTheRotatedModelNormals()
    {
        var shape = TestShapes.Cube();
        var rotation = new Matrix3(s_rotationOfM);
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldNormals(shape, s_positionOfM, s_linearOfM, normals);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < normals.Length; i++)
            {
                var expected = Vector3.Normalize(rotation * shape.GetFaceNormal(i));
                AssertVector(normals[i], expected, 1e-5f, $"face {i} normal");
            }
        });
    }

    [Test]
    public void ComputeWorldNormals_CubeMirroredAlongX_PointOutward()
    {
        var shape = TestShapes.Cube();
        var linear = new Matrix3(Matrix4.CreateRotationZ(0.5f) * Matrix4.CreateScale(-2, 1, 1));
        var vertices = new Vector3[8];
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, linear, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, Vector3.Zero, linear, normals);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < shape.FaceCount; i++)
            {
                var indices = shape.GetFaceIndices(i);
                var centroid = Vector3.Zero;
                foreach (var index in indices)
                    centroid += vertices[index];
                centroid /= indices.Length;

                Assert.That(Vector3.Dot(normals[i], centroid), Is.GreaterThan(0f), $"face {i} points inward");
                Assert.That(normals[i].Length(), Is.EqualTo(1f).Within(1e-5f), $"face {i} normal length");
            }
        });
    }

    [Test]
    public void ComputeWorldGeometry_ThreeTimes_LeavesTheSharedShapeUnchanged()
    {
        var shape = TestShapes.Cube();
        var vertexValues = shape.Vertices.ToArray();
        var normalValues = new Vector3[shape.FaceCount];
        for (var i = 0; i < normalValues.Length; i++)
            normalValues[i] = shape.GetFaceNormal(i);
        var vertices = new Vector3[8];
        var normals = new Vector3[6];

        for (var i = 0; i < 3; i++)
        {
            PhysicsQueries.ComputeWorldVertices(shape, s_positionOfM, s_linearOfM, vertices);
            PhysicsQueries.ComputeWorldNormals(shape, s_positionOfM, s_linearOfM, normals);
        }

        Assert.Multiple(() =>
        {
            Assert.That(shape.Vertices.ToArray(), Is.EqualTo(vertexValues));
            Assert.That(shape.FaceCount, Is.EqualTo(6));

            for (var i = 0; i < normalValues.Length; i++)
                Assert.That(shape.GetFaceNormal(i), Is.EqualTo(normalValues[i]), $"face {i} normal");
        });
    }

    [Test]
    public void ComputeWorldNormals_TetrahedronScaledTwiceAlongX_ArePerpendicularToTheFace()
    {
        var shape = TestShapes.Tetrahedron();
        var linear = new Matrix3(Matrix4.CreateScale(2, 1, 1));
        var vertices = new Vector3[4];
        var normals = new Vector3[4];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, linear, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, Vector3.Zero, linear, normals);

        var indices = shape.GetFaceIndices(0);
        var first = vertices[indices[0]];
        var edge1 = vertices[indices[1]] - first;
        var edge2 = vertices[indices[2]] - first;
        Assert.Multiple(() =>
        {
            AssertVector(normals[0], new Vector3(-0.333333f, -0.666667f, -0.666667f), NormalTolerance, "face 0 normal");
            Assert.That(Vector3.Dot(normals[0], edge1), Is.EqualTo(0f).Within(1e-5f), "normal . edge 1");
            Assert.That(Vector3.Dot(normals[0], edge2), Is.EqualTo(0f).Within(1e-5f), "normal . edge 2");
        });
    }

    [Test]
    public void ComputeWorldVertices_DestinationTooShort_ThrowsArgumentException()
    {
        var shape = TestShapes.Cube();
        var destination = new Vector3[7];

        Assert.Throws<ArgumentException>(() =>
            PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, Matrix3.Identity, destination));
    }

    [Test]
    public void ComputeWorldGeometry_RotatedScaledTranslated_MatchesGolden()
    {
        // CollisionGoldenTests' transform: uneven scale, three rotations, then the translation (-2, 1, 4).
        var position = new Vector3(-2f, 1f, 4f);
        var linear = new Matrix3(Matrix4.CreateRotationZ(0.25f) *
                                 (Matrix4.CreateRotationY(-0.9f) *
                                  (Matrix4.CreateRotationX(0.4f) * Matrix4.CreateScale(1.5f, 0.75f, 2f))));
        var cube = TestShapes.Cube();
        var tetrahedron = TestShapes.Tetrahedron();
        var cubeVertices = new Vector3[8];
        var cubeNormals = new Vector3[6];
        var tetrahedronVertices = new Vector3[4];
        var tetrahedronNormals = new Vector3[4];

        PhysicsQueries.ComputeWorldVertices(cube, position, linear, cubeVertices);
        PhysicsQueries.ComputeWorldNormals(cube, position, linear, cubeNormals);
        PhysicsQueries.ComputeWorldVertices(tetrahedron, position, linear, tetrahedronVertices);
        PhysicsQueries.ComputeWorldNormals(tetrahedron, position, linear, tetrahedronNormals);

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics (CollisionGoldenTests); shape order, three floats each.
        float[] expectedCubeVertices =
        [
            -4.501441f, 0.27041155f, 4.151641f,
            -2.694584f, 0.73177785f, 6.5016217f,
            -1.9094344f, -0.49366045f, 6.138522f,
            -3.7162914f, -0.95502687f, 3.7885413f,
            -2.0905657f, 2.4936604f, 1.8614779f,
            -0.28370857f, 2.9550269f, 4.2114587f,
            0.501441f, 1.7295885f, 3.8483593f,
            -1.3054161f, 1.2682221f, 1.4983783f
        ];
        float[] expectedCubeNormals =
        [
            -0.6027188f, -0.5558122f, 0.5725407f,
            0.6027188f, 0.5558122f, -0.5725407f,
            -0.52343297f, 0.81695884f, 0.24206632f,
            0.52343297f, -0.81695884f, -0.24206632f,
            0.6022856f, 0.15378875f, 0.783327f,
            -0.6022856f, -0.15378875f, -0.783327f
        ];
        float[] expectedTetrahedronVertices =
        [
            -2.694584f, 0.73177785f, 6.5016217f,
            0.501441f, 1.7295885f, 3.8483593f,
            -2.0905657f, 2.4936604f, 1.8614779f,
            -3.7162914f, -0.95502687f, 3.7885413f
        ];
        float[] expectedTetrahedronNormals =
        [
            0.38016587f, -0.58123803f, -0.7194693f,
            -0.890903f, 0.45082548f, 0.055209477f,
            0.5075746f, -0.80432135f, 0.3089257f,
            0.003162478f, 0.9347339f, 0.35533416f
        ];

        Assert.Multiple(() =>
        {
            GoldenAssert.AreClose(expectedCubeVertices, GoldenAssert.Floats(cubeVertices), "cube vertices");
            GoldenAssert.AreClose(expectedCubeNormals, GoldenAssert.Floats(cubeNormals), "cube normals");
            GoldenAssert.AreClose(expectedTetrahedronVertices, GoldenAssert.Floats(tetrahedronVertices),
                "tetrahedron vertices");
            GoldenAssert.AreClose(expectedTetrahedronNormals, GoldenAssert.Floats(tetrahedronNormals),
                "tetrahedron normals");
        });
    }

    [Test]
    public void ComputeWorldGeometry_SharedShapeOnParallelThreads_MatchesSerial()
    {
        const int count = 256;
        var shape = TestShapes.Cube();
        var random = new Random(20261127);
        var positions = new Vector3[count];
        var linears = new Matrix3[count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = new Vector3(Next(random, 100), Next(random, 100), Next(random, 100));
            linears[i] = TestBodies.Linear(new Vector3(Next(random, 7), Next(random, 7), Next(random, 7)),
                new Vector3(Next(random, 10), Next(random, 10), Next(random, 10)));
        }

        var serial = new uint[count][];
        var parallel = new uint[count][];

        for (var i = 0; i < count; i++)
            serial[i] = Compute(shape, positions[i], linears[i]);
        Parallel.For(0, count, i => parallel[i] = Compute(shape, positions[i], linears[i]));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < count; i++)
                Assert.That(parallel[i], Is.EqualTo(serial[i]), $"transform {i}");
        });
    }

    private static uint[] Compute(PolygonShape shape, Vector3 position, Matrix3 linear)
    {
        var vertices = new Vector3[shape.Vertices.Length];
        var normals = new Vector3[shape.FaceCount];
        PhysicsQueries.ComputeWorldVertices(shape, position, linear, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, position, linear, normals);

        return vertices.Concat(normals)
            .SelectMany(v => new[]
            {
                BitConverter.SingleToUInt32Bits(v.X), BitConverter.SingleToUInt32Bits(v.Y),
                BitConverter.SingleToUInt32Bits(v.Z)
            })
            .ToArray();
    }

    private static float Next(Random random, float range) => (float)((random.NextDouble() * 2 - 1) * range);

    private static void AssertVector(Vector3 actual, Vector3 expected, float tolerance, string what)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), $"{what}.X ({actual})");
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), $"{what}.Y ({actual})");
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), $"{what}.Z ({actual})");
    }
}
