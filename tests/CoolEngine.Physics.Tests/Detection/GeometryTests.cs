using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Tests.TestSupport;
using Geometry = CoolEngine.Physics.Detection.Geometry;

namespace CoolEngine.Physics.Tests.Detection;

/// <summary>
/// The world geometry of a shape (ports of today's CollisionTests geometry tests and CollisionGoldenTests), through
/// <see cref="PhysicsQueries"/>: position, orientation (from today's Euler angles by the game's conversion) and
/// half-extents.
/// </summary>
[TestFixture]
public sealed class GeometryTests
{
    private const float NormalTolerance = 1e-4f;

    // M of today's CollisionTests: rotation Z (−0.4) × rotation Y (0.7) × rotation X (0.3), scale (2, 0.5, 1), translation
    // (1, 2, 3). As Euler angles (X yaw, Y pitch, Z roll, rotation Y taking −pitch): (0.3, −0.7, −0.4).
    private static readonly Matrix4 s_rotationOfM =
        Matrix4.CreateRotationZ(-0.4f) * (Matrix4.CreateRotationY(0.7f) * Matrix4.CreateRotationX(0.3f));
    private static readonly Quaternion s_orientationOfM = TestBodies.Orientation(new Vector3(0.3f, -0.7f, -0.4f));
    private static readonly Vector3 s_halfExtentsOfM = new Vector3(2, 0.5f, 1);
    private static readonly Vector3 s_positionOfM = new Vector3(1, 2, 3);

    [Test]
    public void ComputeWorldGeometry_CubeAtIdentity_CopiesEveryVertexAndNormal()
    {
        var shape = TestShapes.Cube();
        var vertices = new Vector3[8];
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, Quaternion.Identity, Vector3.One, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, Quaternion.Identity, Vector3.One, normals);

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
        Geometry.ComputeBounds(null, ReadOnlySpan<Vector3>.Empty, new Vector3(0, 5, 0),
            Geometry.SphereWidth(new Vector3(0.5f)), out var min, out var max);

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

        PhysicsQueries.ComputeWorldNormals(shape, s_orientationOfM, s_halfExtentsOfM, normals);

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
        // Today's rotation Z (0.5) × scale (−2, 1, 1): a negative half-extent mirrors.
        var shape = TestShapes.Cube();
        var orientation = TestBodies.Orientation(new Vector3(0, 0, 0.5f));
        var halfExtents = new Vector3(-2, 1, 1);
        var vertices = new Vector3[8];
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, orientation, halfExtents, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, orientation, halfExtents, normals);

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
            PhysicsQueries.ComputeWorldVertices(shape, s_positionOfM, s_orientationOfM, s_halfExtentsOfM, vertices);
            PhysicsQueries.ComputeWorldNormals(shape, s_orientationOfM, s_halfExtentsOfM, normals);
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
        var halfExtents = new Vector3(2, 1, 1);
        var vertices = new Vector3[4];
        var normals = new Vector3[4];

        PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, Quaternion.Identity, halfExtents, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, Quaternion.Identity, halfExtents, normals);

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
            PhysicsQueries.ComputeWorldVertices(shape, Vector3.Zero, Quaternion.Identity, Vector3.One, destination));
    }

    [Test]
    public void ComputeWorldGeometry_RotatedScaledTranslated_MatchesGolden()
    {
        // CollisionGoldenTests' transform: scale (1.5, 0.75, 2), rotation X (0.4), rotation Y (−0.9), rotation Z (0.25),
        // then the translation (−2, 1, 4); as Euler angles (X yaw, Y pitch, Z roll, rotation Y taking −pitch): (0.4, 0.9, 0.25).
        var position = new Vector3(-2f, 1f, 4f);
        var orientation = TestBodies.Orientation(new Vector3(0.4f, 0.9f, 0.25f));
        var halfExtents = new Vector3(1.5f, 0.75f, 2f);
        var cube = TestShapes.Cube();
        var tetrahedron = TestShapes.Tetrahedron();
        var cubeVertices = new Vector3[8];
        var cubeNormals = new Vector3[6];
        var tetrahedronVertices = new Vector3[4];
        var tetrahedronNormals = new Vector3[4];

        PhysicsQueries.ComputeWorldVertices(cube, position, orientation, halfExtents, cubeVertices);
        PhysicsQueries.ComputeWorldNormals(cube, orientation, halfExtents, cubeNormals);
        PhysicsQueries.ComputeWorldVertices(tetrahedron, position, orientation, halfExtents, tetrahedronVertices);
        PhysicsQueries.ComputeWorldNormals(tetrahedron, orientation, halfExtents, tetrahedronNormals);

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
        var orientations = new Quaternion[count];
        var halfExtents = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = new Vector3(Next(random, 100), Next(random, 100), Next(random, 100));
            orientations[i] = TestBodies.Orientation(new Vector3(Next(random, 7), Next(random, 7), Next(random, 7)));
            halfExtents[i] = new Vector3(Next(random, 10), Next(random, 10), Next(random, 10)) / 2;
        }

        var serial = new uint[count][];
        var parallel = new uint[count][];

        for (var i = 0; i < count; i++)
            serial[i] = Compute(shape, positions[i], orientations[i], halfExtents[i]);
        Parallel.For(0, count, i => parallel[i] = Compute(shape, positions[i], orientations[i], halfExtents[i]));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < count; i++)
                Assert.That(parallel[i], Is.EqualTo(serial[i]), $"transform {i}");
        });
    }

    // Today's behaviour, kept: a zero half-extent X flattens the cube onto the YZ plane; the ±X faces keep their normals
    // and the four faces it collapses to segments (±Y, ±Z) get NaN normals, from Normalize of a zero vector.
    [Test]
    public void ComputeWorldNormals_ZeroHalfExtent_ReturnsNaNForTheCollapsedFaces()
    {
        var shape = TestShapes.Cube();
        var normals = new Vector3[6];

        PhysicsQueries.ComputeWorldNormals(shape, Quaternion.Identity, new Vector3(0, 1, 1), normals);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < shape.FaceCount; i++)
            {
                var modelNormal = shape.GetFaceNormal(i);

                if (modelNormal.X != 0)
                {
                    Assert.That(normals[i], Is.EqualTo(modelNormal), $"face {i} ({modelNormal}) keeps its normal");
                }
                else
                {
                    Assert.That(float.IsNaN(normals[i].X) && float.IsNaN(normals[i].Y) && float.IsNaN(normals[i].Z),
                        Is.True, $"face {i} ({modelNormal}) collapsed: {normals[i]}");
                }
            }
        });
    }

    // The cofactor form of the normals against stage 1's (the cofactors of the full 3 × 3) on the same rotation and scale.
    [Test]
    public void ComputeWorldNormals_CofactorForm_EqualsInverseTransposeWithinTolerance()
    {
        const float tolerance = 1e-6f;
        var shapes = new[] { TestShapes.Cube(), TestShapes.Tetrahedron() };
        var random = new Random(83);
        var mismatches = new List<string>();

        for (var i = 0; i < 1000; i++)
        {
            var shape = shapes[i % shapes.Length];
            var orientation = TestBodies.Orientation(new Vector3(Next(random, 7), Next(random, 7), Next(random, 7)));
            var halfExtents = new Vector3(HalfExtent(random), HalfExtent(random), HalfExtent(random));
            var normals = new Vector3[shape.FaceCount];
            var expected = new Vector3[shape.FaceCount];

            PhysicsQueries.ComputeWorldNormals(shape, orientation, halfExtents, normals);
            TestGeometry.StageOneNormals(shape,
                new Matrix4(Matrix3.CreateFromQuaternion(orientation) * Matrix3.CreateScale(halfExtents)), expected);

            for (var k = 0; k < normals.Length; k++)
            {
                var difference = Vector3.Abs(normals[k] - expected[k]);
                if (!(MathF.Max(difference.X, MathF.Max(difference.Y, difference.Z)) <= tolerance))
                    mismatches.Add($"input {i}, face {k}: q {orientation}, h {halfExtents}: {normals[k]} against {expected[k]}");
            }
        }

        Assert.That(mismatches, Is.Empty);
    }

    // The library's vertices from the game's Euler-to-quaternion conversion against stage 1's (today's) matrix on the same
    // position, rotation and size.
    [Test]
    public void ComputeWorldVertices_EulerConvertedToQuaternion_MatchesStageOneWithinTolerance()
    {
        var shapes = new[] { TestShapes.Cube(), TestShapes.Tetrahedron() };
        var random = new Random(84);
        var mismatches = new List<string>();

        for (var i = 0; i < 1000; i++)
        {
            var shape = shapes[i % shapes.Length];
            var position = new Vector3(Next(random, 100), Next(random, 100), Next(random, 100));
            var rotation = new Vector3(Next(random, 7), Next(random, 7), Next(random, 7));
            var size = new Vector3(Next(random, 10), Next(random, 10), Next(random, 10));
            var vertices = new Vector3[shape.Vertices.Length];
            var expected = new Vector3[shape.Vertices.Length];

            PhysicsQueries.ComputeWorldVertices(shape, position, TestBodies.Orientation(rotation), size / 2, vertices);
            TestGeometry.StageOneVertices(shape,
                Matrix4.CreateTranslation(position) * new Matrix4(TestBodies.StageOneLinear(rotation, size)), expected);

            for (var k = 0; k < vertices.Length; k++)
            {
                if (!IsClose(vertices[k].X, expected[k].X) || !IsClose(vertices[k].Y, expected[k].Y) ||
                    !IsClose(vertices[k].Z, expected[k].Z))
                    mismatches.Add($"input {i}, vertex {k}: position {position}, rotation {rotation}, size {size}: " +
                                   $"{vertices[k]} against {expected[k]}");
            }
        }

        Assert.That(mismatches, Is.Empty);

        static bool IsClose(float actual, float expected) =>
            MathF.Abs(actual - expected) <= 1e-5f * MathF.Max(1f, MathF.Abs(expected));
    }

    private static uint[] Compute(PolygonShape shape, Vector3 position, Quaternion orientation, Vector3 halfExtents)
    {
        var vertices = new Vector3[shape.Vertices.Length];
        var normals = new Vector3[shape.FaceCount];
        PhysicsQueries.ComputeWorldVertices(shape, position, orientation, halfExtents, vertices);
        PhysicsQueries.ComputeWorldNormals(shape, orientation, halfExtents, normals);

        return vertices.Concat(normals)
            .SelectMany(v => new[]
            {
                BitConverter.SingleToUInt32Bits(v.X), BitConverter.SingleToUInt32Bits(v.Y),
                BitConverter.SingleToUInt32Bits(v.Z)
            })
            .ToArray();
    }

    private static float Next(Random random, float range) => (float)((random.NextDouble() * 2 - 1) * range);

    // 0.3 to 2 in magnitude, negative 15 % of the time (a mirror), as the detection parity tests' scales.
    private static float HalfExtent(Random random)
    {
        var magnitude = (float)(0.3 + random.NextDouble() * 1.7);
        return random.Next(20) < 3 ? -magnitude : magnitude;
    }

    private static void AssertVector(Vector3 actual, Vector3 expected, float tolerance, string what)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), $"{what}.X ({actual})");
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), $"{what}.Y ({actual})");
        Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), $"{what}.Z ({actual})");
    }
}
