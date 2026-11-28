using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Detection;

/// <summary>
/// A body's world geometry from its transform: ports of today's <c>Collision.UpdateCollision</c> and
/// <c>CollisionData.UpdateBoundingBox</c> with the same arithmetic. Pure; any thread; no allocation.
/// </summary>
internal static class Geometry
{
    /// <summary>
    /// Stage 1 (transitional): <c>Matrix4.CreateTranslation(position) * new Matrix4(linear)</c>, bit for bit today's
    /// <c>WorldObject.ApplyTransformation</c> when <paramref name="linear"/> is its rotation × scale built with the
    /// Matrix4 factories (the product, unlike a direct assembly of the columns, turns a −0 translation into +0 as
    /// today's does).
    /// </summary>
    internal static Matrix4 ComputeMatrix(Vector3 position, Matrix3 linear) =>
        Matrix4.CreateTranslation(position) * new Matrix4(linear);

    /// <summary>Each model vertex through <paramref name="transformation"/> (<c>TransformPosition</c>), in shape order.</summary>
    internal static void ComputeVertices(PolygonShape shape, in Matrix4 transformation, Span<Vector3> destination)
    {
        var vertices = shape.Vertices;

        for (int i = 0; i < vertices.Length; i++)
            destination[i] = transformation.TransformPosition(vertices[i]);
    }

    /// <summary>
    /// Each model face normal through the inverse-transpose of the transformation's 3 × 3, computed as its cofactors
    /// (no division, no exception for a singular matrix), negated for a mirror, then normalized: today's formula.
    /// A zero size gives NaN for the faces it collapses.
    /// </summary>
    internal static void ComputeNormals(PolygonShape shape, in Matrix4 transformation, Span<Vector3> destination)
    {
        // row0..row2 are the images of the X, Y, Z axes: the 3 × 3's Column0..Column2, taken from the full matrix as
        // today (not from the stage-1 linear part, which can differ from it in a −0).
        var linear = new Matrix3(transformation);
        var row0 = linear.Column0;
        var row1 = linear.Column1;
        var row2 = linear.Column2;
        var cofactor0 = Vector3.Cross(row1, row2);
        var cofactor1 = Vector3.Cross(row2, row0);
        var cofactor2 = Vector3.Cross(row0, row1);
        // A negative determinant (a mirror) would turn every normal inward.
        var sign = Vector3.Dot(row0, cofactor0) < 0 ? -1f : 1f;
        var normals = shape.Normals;

        for (int i = 0; i < normals.Length; i++)
        {
            var normal = normals[i];
            destination[i] = Vector3.Normalize(sign * (normal.X * cofactor0 + normal.Y * cofactor1 + normal.Z * cofactor2));
        }
    }

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
