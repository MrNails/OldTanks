using System.Numerics;
using CoolEngine.Physics.Internal;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Detection;

/// <summary>
/// The narrow and broad tests of today's CoolEngine.PhysicEngine.Core.Collision.Collision, ported on spans with the
/// same arithmetic, comparison forms (<c>&gt;=</c>, <c>!(x &gt;= e)</c>) and loop orders, so every result has today's
/// bits. Pure; any thread; no allocation.
/// </summary>
internal static class NarrowPhase
{
    // Below this squared length of the cross product of two unit directions, the two are taken as parallel.
    internal const float ParallelEpsilon = 1e-6f;

    // Below this squared length a sphere axis is skipped: the centre is on the polygon, which then overlaps anyway.
    internal const float ZeroAxisEpsilon = 1e-12f;

    // Tolerance of the cuboid detection's comparisons of normals and coordinates.
    internal const float CuboidTolerance = 1e-6f;

    // Largest |dot| of two of a cuboid's current axes for the closest-point test; beyond it the box is sheared.
    internal const float CuboidOrthogonalityTolerance = 1e-4f;

    /// <summary>
    /// Today's <c>Collision.CheckCollision</c>: the bounds first, then the narrow test of the pair. The normal of a
    /// polygon pair points from <paramref name="first"/> to <paramref name="second"/>. When it returns false,
    /// <paramref name="normal"/> and <paramref name="depth"/> hold what the failing test left in them, as today.
    /// </summary>
    internal static bool CheckCollision(in BodyGeometry first, in BodyGeometry second, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        if (!BoundsOverlap(first.BoundsMin, first.BoundsMax, second.BoundsMin, second.BoundsMax))
            return false;

        // A body is a polygon or a sphere (Shape is a closed hierarchy), so the four cases below are all there is.
        if (first.Polygon is { } firstPolygon)
        {
            return second.Polygon is { } secondPolygon
                ? OBBCollisionCheck(firstPolygon, first, secondPolygon, second, out normal, out depth)
                : SpherePolygonCollisionCheck(firstPolygon, first, second, out normal, out depth);
        }

        return second.Polygon is { } polygon
            ? SpherePolygonCollisionCheck(polygon, second, first, out normal, out depth)
            : SphereXSphereCollision(first, second, out normal, out depth);
    }

    /// <summary>Today's <c>AABBCollisionCheck</c>: touching boxes overlap.</summary>
    internal static bool BoundsOverlap(in Vector3 min1, in Vector3 max1, in Vector3 min2, in Vector3 max2)
    {
        return min1.X <= max2.X &&
               max1.X >= min2.X &&
               min1.Y <= max2.Y &&
               max1.Y >= min2.Y &&
               min1.Z <= max2.Z &&
               max1.Z >= min2.Z;
    }

    private static bool SphereXSphereCollision(in BodyGeometry sphere1, in BodyGeometry sphere2, out Vector3 normal,
        out float depth)
    {
        var directionalVector = sphere1.Position - sphere2.Position;

        var distance = directionalVector.Length();
        var radii = (sphere1.SphereWidth + sphere2.SphereWidth) / 2;

        normal = -Vector3.Normalize(directionalVector);

        var sphere1EndPoint = sphere1.Position + normal * (sphere1.SphereWidth / 2);

        depth = sphere2.SphereWidth / 2 - Vector3.Distance(sphere1EndPoint, sphere2.Position);

        return distance <= radii;
    }

    private static bool OBBCollisionCheck(PolygonShape shape1, in BodyGeometry polygon1, PolygonShape shape2,
        in BodyGeometry polygon2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        if (!OBBCollisionCheckLoop(polygon1.Normals, polygon1.Vertices, polygon2.Vertices, ref normal, ref depth))
            return false;

        if (!OBBCollisionCheckLoop(polygon2.Normals, polygon2.Vertices, polygon1.Vertices, ref normal, ref depth))
            return false;

        // Face normals alone miss shapes whose edges pass each other skewed: they overlap on every face axis
        // across a gap. The edge x edge axes close that hole; ties stay with the face axes found first.
        if (!EdgeAxesCollisionCheckLoop(shape1, polygon1.Vertices, shape2, polygon2.Vertices, ref normal, ref depth))
            return false;

        if (Vector3.Dot(polygon2.Position - polygon1.Position, normal) < 0)
            normal = -normal;

        return true;
    }

    private static bool EdgeAxesCollisionCheckLoop(PolygonShape shape1, ReadOnlySpan<Vector3> vertices1,
        PolygonShape shape2, ReadOnlySpan<Vector3> vertices2, ref Vector3 normal, ref float depth)
    {
        var edges1 = shape1.Edges;
        var edges2 = shape2.Edges;

        foreach (var edgeIndex1 in shape1.EdgeDirections)
        {
            var edge1 = edges1[edgeIndex1];
            var direction1 = Vector3.Normalize(vertices1[(int)edge1.B] - vertices1[(int)edge1.A]);

            foreach (var edgeIndex2 in shape2.EdgeDirections)
            {
                var edge2 = edges2[edgeIndex2];
                var direction2 = Vector3.Normalize(vertices2[(int)edge2.B] - vertices2[(int)edge2.A]);

                var axis = Vector3.Cross(direction1, direction2);
                // Parallel edges span no axis; their face axes were tested already. Written as !(>=) so the NaN
                // axis of an edge collapsed to a point is skipped too instead of always separating.
                if (!(axis.LengthSquared() >= ParallelEpsilon))
                    continue;

                axis = Vector3.Normalize(axis);

                ProjectionMinMaxVertices(axis, vertices1, out var poly1Min, out var poly1Max);
                ProjectionMinMaxVertices(axis, vertices2, out var poly2Min, out var poly2Max);

                if (poly1Min >= poly2Max || poly2Min >= poly1Max)
                    return false;

                var overlap = Math.Min(poly2Max - poly1Min, poly1Max - poly2Min);
                if (overlap < depth)
                {
                    normal = axis;
                    depth = overlap;
                }
            }
        }

        return true;
    }

    private static bool OBBCollisionCheckLoop(ReadOnlySpan<Vector3> normals1, ReadOnlySpan<Vector3> vertices1,
        ReadOnlySpan<Vector3> vertices2, ref Vector3 normal, ref float depth)
    {
        for (int i = 0; i < normals1.Length; i++)
        {
            var faceNormal = normals1[i];

            ProjectionMinMaxVertices(faceNormal, vertices1, out var poly1Min, out var poly1Max);
            ProjectionMinMaxVertices(faceNormal, vertices2, out var poly2Min, out var poly2Max);

            if (poly1Min >= poly2Max || poly2Min >= poly1Max)
            {
                return false;
            }

            var min = Math.Min(poly2Max - poly1Min, poly1Max - poly2Min);
            if (min < depth)
            {
                normal = faceNormal;
                depth = min;
            }
        }

        return true;
    }

    private static bool SpherePolygonCollisionCheck(PolygonShape shape, in BodyGeometry polygon, in BodyGeometry sphere,
        out Vector3 normal, out float depth)
    {
        var axisFaces = shape.CuboidAxisFaces;

        return axisFaces.Length != 0 && AreAxesPerpendicular(polygon.Normals, axisFaces)
            ? SphereCuboidCollisionCheck(polygon, axisFaces, sphere, out normal, out depth)
            : SpherePolygonSatCollisionCheck(shape, polygon, sphere, out normal, out depth);
    }

    // The closest-point test is exact only for a box whose current axes are perpendicular: a shear tilts them, and
    // a zero size makes an axis NaN (every comparison with NaN is false, so NaN fails this check too).
    private static bool AreAxesPerpendicular(ReadOnlySpan<Vector3> normals, ReadOnlySpan<int> axisFaces)
    {
        var axis0 = normals[axisFaces[0]];
        var axis1 = normals[axisFaces[1]];
        var axis2 = normals[axisFaces[2]];

        return MathF.Abs(Vector3.Dot(axis0, axis1)) <= CuboidOrthogonalityTolerance &&
               MathF.Abs(Vector3.Dot(axis1, axis2)) <= CuboidOrthogonalityTolerance &&
               MathF.Abs(Vector3.Dot(axis0, axis2)) <= CuboidOrthogonalityTolerance;
    }

    // Exact for a cuboid: clamp the sphere centre into the box along the box's three axes to get the closest point.
    private static bool SphereCuboidCollisionCheck(in BodyGeometry cuboid, ReadOnlySpan<int> axisFaces,
        in BodyGeometry sphere, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        var center = sphere.Position;
        var radius = sphere.SphereWidth / 2;

        var isOutside = false;
        var delta = Vector3.Zero;
        // For a centre inside the box: the face it is least deep behind, + before - and k = 0..2 on a tie.
        var smallestPenetration = float.MaxValue;
        var smallestPenetrationNormal = Vector3.Zero;

        for (int k = 0; k < axisFaces.Length; k++)
        {
            var axis = cuboid.Normals[axisFaces[k]];

            ProjectionMinMaxVertices(axis, cuboid.Vertices, out var min, out var max);

            var halfExtent = (max - min) / 2;
            var offset = Vector3.Dot(center, axis) - (min + max) / 2;
            var clamped = Math.Clamp(offset, -halfExtent, halfExtent);

            if (Math.Abs(offset) > halfExtent)
                isOutside = true;

            delta += (offset - clamped) * axis;

            var positivePenetration = halfExtent - offset;
            if (positivePenetration < smallestPenetration)
            {
                smallestPenetration = positivePenetration;
                smallestPenetrationNormal = axis;
            }

            var negativePenetration = halfExtent + offset;
            if (negativePenetration < smallestPenetration)
            {
                smallestPenetration = negativePenetration;
                smallestPenetrationNormal = -axis;
            }
        }

        if (isOutside)
        {
            var distance = delta.Length();
            if (distance >= radius)
                return false;

            normal = delta / distance;
            depth = radius - distance;
            return true;
        }

        normal = smallestPenetrationNormal;
        depth = radius + smallestPenetration;
        return true;
    }

    // Separating axes for a sphere and any convex polygon: the face normals, then the axes from every vertex and
    // from the closest point of every edge to the sphere centre, which catch a sphere off a corner or an edge.
    private static bool SpherePolygonSatCollisionCheck(PolygonShape shape, in BodyGeometry polygon,
        in BodyGeometry sphere, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        float polygonProjMin; float polygonProjMax;
        float sphereProjMin; float sphereProjMax;
        float intersectionDepth;
        var closestFacePointsIndex = LegacyMath.ClosestPointIndex(polygon.Vertices, sphere.Position);

        if (closestFacePointsIndex == -1)
            return false;

        var normals = polygon.Normals;

        for (int i = 0; i < normals.Length; i++)
        {
            var faceNormal = normals[i];

            ProjectionMinMaxVertices(faceNormal, polygon.Vertices, out polygonProjMin, out polygonProjMax);
            SphereProjectionMinMaxVertices(faceNormal, sphere.Position, sphere.SphereWidth / 2,
                out sphereProjMin, out sphereProjMax);

            if (polygonProjMin >= sphereProjMax || sphereProjMin >= polygonProjMax)
            {
                return false;
            }

            intersectionDepth = Math.Min(sphereProjMax - polygonProjMin, polygonProjMax - sphereProjMin);
            if (intersectionDepth < depth)
            {
                normal = faceNormal;
                depth = intersectionDepth;
            }
            else if (LegacyMath.ExactlyEqual(intersectionDepth, depth) &&
                     shape.FaceIndices(i).IndexOf((uint)closestFacePointsIndex) >= 0)
            {
                normal = faceNormal;
            }
        }

        var center = sphere.Position;
        var radius = sphere.SphereWidth / 2;
        var vertices = polygon.Vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            var axis = center - vertices[i];
            // The centre is on the vertex: the shapes overlap anyway, and the axis has no direction.
            if (axis.LengthSquared() < ZeroAxisEpsilon)
                continue;

            axis = Vector3.Normalize(axis);

            ProjectionMinMaxVertices(axis, vertices, out polygonProjMin, out polygonProjMax);
            SphereProjectionMinMaxVertices(axis, center, radius, out sphereProjMin, out sphereProjMax);

            if (polygonProjMin >= sphereProjMax || sphereProjMin >= polygonProjMax)
                return false;

            intersectionDepth = Math.Min(sphereProjMax - polygonProjMin, polygonProjMax - sphereProjMin);
            if (intersectionDepth < depth)
            {
                normal = axis;
                depth = intersectionDepth;
            }
        }

        var edges = shape.Edges;

        for (int i = 0; i < edges.Length; i++)
        {
            var start = vertices[(int)edges[i].A];
            var edge = vertices[(int)edges[i].B] - start;
            var t = Math.Clamp(Vector3.Dot(center - start, edge) / Vector3.Dot(edge, edge), 0, 1);
            var axis = center - (start + t * edge);
            // The centre is on the edge: the shapes overlap anyway, and the axis has no direction.
            if (axis.LengthSquared() < ZeroAxisEpsilon)
                continue;

            axis = Vector3.Normalize(axis);

            ProjectionMinMaxVertices(axis, vertices, out polygonProjMin, out polygonProjMax);
            SphereProjectionMinMaxVertices(axis, center, radius, out sphereProjMin, out sphereProjMax);

            if (polygonProjMin >= sphereProjMax || sphereProjMin >= polygonProjMax)
                return false;

            intersectionDepth = Math.Min(sphereProjMax - polygonProjMin, polygonProjMax - sphereProjMin);
            if (intersectionDepth < depth)
            {
                normal = axis;
                depth = intersectionDepth;
            }
        }

        return true;
    }

    private static void ProjectionMinMaxVertices(in Vector3 normal, ReadOnlySpan<Vector3> vertices, out float min,
        out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;

        for (int j = 0; j < vertices.Length; j++)
        {
            var dotRes = Vector3.Dot(vertices[j], normal);

            if (min > dotRes) min = dotRes;
            if (max < dotRes) max = dotRes;
        }
    }

    private static void SphereProjectionMinMaxVertices(in Vector3 normal, in Vector3 center, float radius,
        out float min, out float max)
    {
        var directedRadius = normal * radius;

        min = Vector3.Dot(center - directedRadius, normal);
        max = Vector3.Dot(center + directedRadius, normal);

        if (min > max)
            (min, max) = (max, min);
    }
}
