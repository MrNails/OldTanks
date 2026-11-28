using System.Numerics;
using CoolEngine.Physics.Shapes;
using CoolEngine.Physics.Tests.TestSupport;

namespace CoolEngine.Physics.Tests.Shapes;

[TestFixture]
public sealed class PolygonShapeTests
{
    [Test]
    public void Constructor_NoVertices_ThrowsArgumentException()
    {
        ShapeFace[] faces = [new ShapeFace([0, 1, 2], new Vector3(0, 0, 1))];

        var exception = Assert.Throws<ArgumentException>(() => _ = new PolygonShape(ReadOnlySpan<Vector3>.Empty, faces));

        Assert.That(exception?.ParamName, Is.EqualTo("vertices"));
    }

    [Test]
    public void Constructor_FaceWithFewerThanThreeIndices_ThrowsArgumentException()
    {
        var faces = TestShapes.CubeFaces();
        faces[0] = new ShapeFace([0, 1], faces[0].Normal);

        var exception = Assert.Throws<ArgumentException>(() => _ = new PolygonShape(TestShapes.CubeVertices(), faces));

        Assert.That(exception?.ParamName, Is.EqualTo("faces"));
    }

    [Test]
    public void Constructor_IndexOutOfRange_ThrowsArgumentException()
    {
        var faces = TestShapes.CubeFaces();
        faces[3] = new ShapeFace([6, 2, 7, 8], faces[3].Normal);

        var exception = Assert.Throws<ArgumentException>(() => _ = new PolygonShape(TestShapes.CubeVertices(), faces));

        Assert.That(exception?.ParamName, Is.EqualTo("faces"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Constructor_NonFiniteVertexOrNormal_ThrowsArgumentException(bool vertex)
    {
        var vertices = TestShapes.CubeVertices();
        var faces = TestShapes.CubeFaces();
        if (vertex)
            vertices[0].X = float.NaN;
        else
            faces[2] = new ShapeFace(faces[2].Indices.ToArray(), new Vector3(0, float.PositiveInfinity, 0));

        Assert.Throws<ArgumentException>(() => _ = new PolygonShape(vertices, faces));
    }

    [Test]
    public void Constructor_CallerArraysChangedAfterwards_ShapeUnchanged()
    {
        var vertices = TestShapes.CubeVertices();
        uint[] firstFaceIndices = [0, 1, 3, 2];
        var faces = TestShapes.CubeFaces();
        faces[0] = new ShapeFace(firstFaceIndices, faces[0].Normal);
        var expectedVertices = TestShapes.CubeVertices();
        var expectedFaces = TestShapes.CubeFaces();

        var shape = new PolygonShape(vertices, faces);
        vertices[0] = new Vector3(9, 9, 9);
        firstFaceIndices[1] = 7;
        faces[1] = new ShapeFace(faces[1].Indices.ToArray(), new Vector3(1, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(shape.Vertices.ToArray(), Is.EqualTo(expectedVertices));
            Assert.That(shape.FaceCount, Is.EqualTo(expectedFaces.Length));

            for (var i = 0; i < expectedFaces.Length; i++)
            {
                Assert.That(shape.GetFaceIndices(i).ToArray(), Is.EqualTo(expectedFaces[i].Indices.ToArray()),
                    $"face {i} indices");
                Assert.That(shape.GetFaceNormal(i), Is.EqualTo(expectedFaces[i].Normal), $"face {i} normal");
            }
        });
    }

    [Test]
    public void Constructor_Cube_FindsTwelveEdgesThreeDirectionsAndCuboidFaces()
    {
        var shape = TestShapes.Cube();

        // Today's Collision.FindEdges order: face pairs (i, j) with i < j, each edge as (lower, higher) index.
        (uint A, uint B)[] expectedEdges =
        [
            (0, 1), (2, 3), (1, 2), (0, 3), (4, 5), (6, 7), (5, 6), (4, 7), (1, 5), (0, 4), (2, 6), (3, 7)
        ];

        Assert.Multiple(() =>
        {
            Assert.That(shape.Edges.ToArray(), Is.EqualTo(expectedEdges));
            Assert.That(shape.EdgeDirections.ToArray(), Is.EqualTo(new[] { 0, 2, 8 }));
            Assert.That(shape.CuboidAxisFaces.ToArray(), Is.EqualTo(new[] { 4, 2, 0 }));
            Assert.That(shape.IsCuboid, Is.True);
        });
    }

    [Test]
    public void Constructor_Tetrahedron_FindsSixEdgesAndSixDirectionsAndNoCuboid()
    {
        var shape = TestShapes.Tetrahedron();

        Assert.Multiple(() =>
        {
            Assert.That(shape.Edges.Length, Is.EqualTo(6));
            Assert.That(shape.EdgeDirections.Length, Is.EqualTo(6));
            Assert.That(shape.CuboidAxisFaces.Length, Is.EqualTo(0));
            Assert.That(shape.IsCuboid, Is.False);
        });
    }

    [Test]
    public void Constructor_TriangulatedCube_KeepsTwelveEdgesAndIsNotACuboid()
    {
        var shape = TestShapes.TriangulatedCube();
        var cube = TestShapes.Cube();
        var edges = shape.Edges.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(edges, Has.Length.EqualTo(12));
            // The same 12 box edges; the 6 face diagonals are excluded because their two triangles are coplanar.
            Assert.That(edges, Is.EquivalentTo(cube.Edges.ToArray()));
            Assert.That(shape.IsCuboid, Is.False);
            Assert.That(shape.CuboidAxisFaces.Length, Is.EqualTo(0));
        });
    }
}
