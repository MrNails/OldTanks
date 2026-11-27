using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using System.Numerics;

namespace CoolEngine.Tests.TestSupport;

/// <summary>Model-space collision shapes; each call returns a fresh instance.</summary>
internal static class CollisionTestShapes
{
    /// <summary>
    /// The game's cube: the same 8 vertices, 6 faces, index lists and normals as the static constructor of
    /// OldTanks.Models.Cube, copied here because engine tests never reference the game.
    /// </summary>
    public static CollisionData Cube()
    {
        var shape = new CollisionData(CollisionType.Polygon);

        shape.Faces.Add(new Face(new uint[] { 0, 1, 3, 2 }) { Normal = new Vector3(0, 0, 1) });
        shape.Faces.Add(new Face(new uint[] { 5, 4, 6, 7 }) { Normal = new Vector3(0, 0, -1) });
        shape.Faces.Add(new Face(new uint[] { 1, 5, 0, 4 }) { Normal = new Vector3(0, 1, 0) });
        shape.Faces.Add(new Face(new uint[] { 6, 2, 7, 3 }) { Normal = new Vector3(0, -1, 0) });
        shape.Faces.Add(new Face(new uint[] { 6, 5, 2, 1 }) { Normal = new Vector3(1, 0, 0) });
        shape.Faces.Add(new Face(new uint[] { 3, 0, 7, 4 }) { Normal = new Vector3(-1, 0, 0) });
        shape.Vertices = CubeVertices();

        return shape;
    }

    /// <summary>
    /// The cube's 8 vertices with each quad split into two triangles that keep the quad's normal: 12 faces,
    /// so the shape is a box geometrically but not a cuboid by face count.
    /// </summary>
    public static CollisionData TriangulatedCube()
    {
        var cube = Cube();
        var shape = new CollisionData(CollisionType.Polygon);

        foreach (var quad in cube.Faces)
        {
            // A quad lists a corner, its two neighbours, then the opposite corner.
            var indices = quad.Indices;

            shape.Faces.Add(new Face(new[] { indices[0], indices[1], indices[2] }) { Normal = quad.Normal });
            shape.Faces.Add(new Face(new[] { indices[3], indices[2], indices[1] }) { Normal = quad.Normal });
        }

        shape.Vertices = CubeVertices();

        return shape;
    }

    /// <summary>A regular tetrahedron inscribed in the cube; face k is the one opposite vertex k.</summary>
    public static CollisionData Tetrahedron()
    {
        var shape = new CollisionData(CollisionType.Polygon);
        Vector3[] vertices =
        {
            new Vector3(1, 1, 1),
            new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1),
        };

        for (uint k = 0; k < vertices.Length; k++)
        {
            var indices = new uint[3];
            var next = 0;

            for (uint i = 0; i < vertices.Length; i++)
            {
                if (i != k)
                    indices[next++] = i;
            }

            shape.Faces.Add(new Face(indices) { Normal = -vertices[k] / MathF.Sqrt(3f) });
        }

        shape.Vertices = vertices;

        return shape;
    }

    public static CollisionData Sphere() => new CollisionData(CollisionType.Sphere);

    public static CollisionData PolygonWithoutVertices() => new CollisionData(CollisionType.Polygon);

    private static Vector3[] CubeVertices() =>
        new[]
        {
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
        };
}
