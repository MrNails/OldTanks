using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Detection;

/// <summary>
/// A body's world geometry from its position, rotation and half-extents, and its bounds as today's
/// <c>CollisionData.UpdateBoundingBox</c> computes them. Pure; any thread; no allocation.
/// </summary>
internal static class Geometry
{
    /// <summary>
    /// Each model vertex in world space, in shape order: <c>position + rotation × (halfExtents ⊙ vertex)</c>, the
    /// half-extents multiplied component by component (for the cube's ±1 corners exactly ±halfExtents).
    /// </summary>
    internal static void ComputeVertices(PolygonShape shape, Vector3 position, in Matrix3 rotation,
        Vector3 halfExtents, Span<Vector3> destination)
    {
        var vertices = shape.Vertices;

        for (int i = 0; i < vertices.Length; i++)
            destination[i] = position + rotation * (halfExtents * vertices[i]);
    }

    /// <summary>
    /// Each model face normal in world space, in face order: <c>Normalize(sign × (rotation × (normal ⊙ c)))</c> with
    /// <c>c = (h.Y·h.Z, h.X·h.Z, h.X·h.Y)</c> and <c>sign = −1</c> when <c>h.X·h.Y·h.Z</c> is negative (a mirror), else 1:
    /// the cofactors of rotation × scale, so equal in exact arithmetic to today's inverse-transpose. No division, no
    /// exception for a zero half-extent, which gives NaN for the faces it collapses, as today.
    /// </summary>
    internal static void ComputeNormals(PolygonShape shape, in Matrix3 rotation, Vector3 halfExtents,
        Span<Vector3> destination)
    {
        var cofactorScale = new Vector3(halfExtents.Y * halfExtents.Z, halfExtents.X * halfExtents.Z,
            halfExtents.X * halfExtents.Y);
        // A negative determinant (a mirror) would turn every normal inward.
        var sign = halfExtents.X * halfExtents.Y * halfExtents.Z < 0 ? -1f : 1f;
        var normals = shape.Normals;

        for (int i = 0; i < normals.Length; i++)
            destination[i] = Vector3.Normalize(sign * (rotation * (normals[i] * cofactorScale)));
    }

    /// <summary>
    /// A sphere's width: twice its X half-extent (exact), so every sphere expression keeps today's form with
    /// <c>Width</c>.
    /// </summary>
    internal static float SphereWidth(Vector3 halfExtents) => 2 * halfExtents.X;

    /// <summary>
    /// The axis-aligned bounds: of the world vertices for a polygon (<paramref name="polygon"/> not null), else of the
    /// sphere <c>position ± sphereWidth / 2</c>, as today's <c>UpdateBoundingBox</c>.
    /// </summary>
    internal static void ComputeBounds(PolygonShape? polygon, ReadOnlySpan<Vector3> vertices, Vector3 position,
        float sphereWidth, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);

        if (polygon == null)
        {
            min = position - new Vector3(sphereWidth / 2);
            max = position + new Vector3(sphereWidth / 2);
        }
        else
        {
            for (int j = 0; j < vertices.Length; j++)
            {
                var current = vertices[j];

                if (max.X < current.X) max.X = current.X;
                if (max.Y < current.Y) max.Y = current.Y;
                if (max.Z < current.Z) max.Z = current.Z;

                if (min.X > current.X) min.X = current.X;
                if (min.Y > current.Y) min.Y = current.Y;
                if (min.Z > current.Z) min.Z = current.Z;
            }
        }
    }
}
