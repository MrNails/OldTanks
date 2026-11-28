using System.Numerics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Model-space shapes; each call returns a fresh instance. Copied from CoolEngine.Tests.TestSupport.CollisionTestShapes
/// (the same vertices, index lists and normals, in the same order), as <see cref="PolygonShape"/>.
/// </summary>
internal static class TestShapes
{
    /// <summary>The game's cube: 8 vertices, 6 quads, outward normals, as OldTanks.Models.Cube's static constructor.</summary>
    public static PolygonShape Cube() => new PolygonShape(CubeVertices(), CubeFaces());

    /// <summary>The cube's faces in the game's order: +Z, -Z, +Y, -Y, +X, -X.</summary>
    public static ShapeFace[] CubeFaces() =>
    [
        new ShapeFace([0, 1, 3, 2], new Vector3(0, 0, 1)),
        new ShapeFace([5, 4, 6, 7], new Vector3(0, 0, -1)),
        new ShapeFace([1, 5, 0, 4], new Vector3(0, 1, 0)),
        new ShapeFace([6, 2, 7, 3], new Vector3(0, -1, 0)),
        new ShapeFace([6, 5, 2, 1], new Vector3(1, 0, 0)),
        new ShapeFace([3, 0, 7, 4], new Vector3(-1, 0, 0)),
    ];

    /// <summary>
    /// The cube's 8 vertices with each quad split into two triangles that keep the quad's normal: 12 faces, so the
    /// shape is a box geometrically but not a cuboid by face count.
    /// </summary>
    public static PolygonShape TriangulatedCube()
    {
        var quads = CubeFaces();
        var faces = new ShapeFace[quads.Length * 2];

        for (var i = 0; i < quads.Length; i++)
        {
            // A quad lists a corner, its two neighbours, then the opposite corner.
            var indices = quads[i].Indices;

            faces[2 * i] = new ShapeFace([indices[0], indices[1], indices[2]], quads[i].Normal);
            faces[2 * i + 1] = new ShapeFace([indices[3], indices[2], indices[1]], quads[i].Normal);
        }

        return new PolygonShape(CubeVertices(), faces);
    }

    /// <summary>A regular tetrahedron inscribed in the cube; face k is the one opposite vertex k.</summary>
    public static PolygonShape Tetrahedron()
    {
        Vector3[] vertices =
        [
            new Vector3(1, 1, 1),
            new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1),
        ];
        var faces = new ShapeFace[vertices.Length];

        for (uint k = 0; k < vertices.Length; k++)
        {
            var indices = new uint[3];
            var next = 0;

            for (uint i = 0; i < vertices.Length; i++)
            {
                if (i != k)
                    indices[next++] = i;
            }

            faces[k] = new ShapeFace(indices, -vertices[k] / MathF.Sqrt(3f));
        }

        return new PolygonShape(vertices, faces);
    }

    public static Vector3[] CubeVertices() =>
    [
        new Vector3(-1.0f, 1.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(1.0f, -1.0f, 1.0f),
        new Vector3(-1.0f, -1.0f, 1.0f),
        new Vector3(-1.0f, 1.0f, -1.0f),
        new Vector3(1.0f, 1.0f, -1.0f),
        new Vector3(1.0f, -1.0f, -1.0f),
        new Vector3(-1.0f, -1.0f, -1.0f),
    ];
}
