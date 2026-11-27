using System.Numerics;
using Common.Extensions;
using CoolEngine.Mathematics;
using CoolEngine.Models;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class Collision
{
    // Below this squared length of the cross product of two unit directions, the two are taken as parallel.
    private const float ParallelEpsilon = 1e-6f;

    // Below this squared length a sphere axis is skipped: the centre is on the polygon, which then overlaps anyway.
    private const float ZeroAxisEpsilon = 1e-12f;

    // Tolerance of the cuboid detection's comparisons of normals and coordinates.
    private const float CuboidTolerance = 1e-6f;

    // Largest |dot| of two of a cuboid's current axes for the closest-point test; beyond it the box is sheared.
    private const float CuboidOrthogonalityTolerance = 1e-4f;

    private readonly CollisionData m_originalCollision;
    private readonly CollisionData m_currentCollision;

    // Every edge of the original shape as vertex index pairs, A < B; set once in the constructor, never changed.
    private readonly (uint A, uint B)[] m_edges;

    // Indices into m_edges, one edge per class of parallel edges; set once in the constructor, never changed.
    private readonly int[] m_edgeDirections;

    // The faces whose model-space normals are +X, +Y and +Z, in that order, or null when the shape is not a cuboid;
    // set once in the constructor, never changed.
    private readonly int[]? m_cuboidAxisFaces;

    public Collision(IPhysicObject physicObject, CollisionData originalCollision)
    {
        if (physicObject == null)
            throw new ArgumentNullException(nameof(physicObject));
        
        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));
        
        m_currentCollision = new CollisionData(originalCollision.CollisionType) { PhysicObject = physicObject };

        m_originalCollision = originalCollision;

        InitCollision(originalCollision);

        m_edges = FindEdges(originalCollision);
        m_edgeDirections = FindEdgeDirections(originalCollision, m_edges);
        m_cuboidAxisFaces = FindCuboidAxisFaces(originalCollision);

        IsActive = true;
        IsDrawing = true;
    }

    public CollisionData CollisionData => m_currentCollision;

    public bool IsActive { get; set; }
    
    public bool IsDrawing { get; set; }

    public void UpdateCollision()
    {
        var transformation = CollisionData.PhysicObject!.Transformation;

        // Normals go through the inverse-transpose of the linear part, not the rotation alone: under uneven scale a
        // rotated normal is no longer perpendicular to its face. The cofactor rows are that inverse-transpose times
        // the determinant, so there is no division and no exception for a singular matrix. row0..row2 are the images of
        // the X, Y, Z axes: OpenTK's Row0..Row2, which are our Column0..Column2 (the same bytes).
        var linear = new Matrix3(transformation);
        var row0 = linear.Column0;
        var row1 = linear.Column1;
        var row2 = linear.Column2;
        var cofactor0 = Vector3.Cross(row1, row2);
        var cofactor1 = Vector3.Cross(row2, row0);
        var cofactor2 = Vector3.Cross(row0, row1);
        // A negative determinant (a mirror) would turn every normal inward.
        var sign = Vector3.Dot(row0, cofactor0) < 0 ? -1f : 1f;

        for (int i = 0; i < m_currentCollision.Vertices.Length; i++)
            m_currentCollision.Vertices[i] = transformation.TransformPosition(m_originalCollision.Vertices[i]);

        for (int i = 0; i < m_currentCollision.Faces.Count; i++)
        {
            var normal = m_originalCollision.Faces[i].Normal;
            m_currentCollision.Faces[i].Normal =
                Vector3.Normalize(sign * (normal.X * cofactor0 + normal.Y * cofactor1 + normal.Z * cofactor2));
        }

        CollisionData.UpdateBoundingBox();
    }

    public bool CheckCollision(Collision polygon2, out Vector3 normal, out float depth)
     {
         normal = Vector3.Zero;
         depth = float.MaxValue;

         if (!AABBCollisionCheck(CollisionData, polygon2.CollisionData))
             return false;

         var p2CollData = polygon2.CollisionData;

         return CollisionData.CollisionType switch
         {
             CollisionType.Polygon when p2CollData.CollisionType == CollisionType.Polygon => 
                 OBBCollisionCheck(this, polygon2, out normal, out depth),
             CollisionType.Polygon when p2CollData.CollisionType == CollisionType.Sphere =>
                 SpherePolygonCollisionCheck(this, p2CollData, out normal, out depth),
             CollisionType.Sphere when p2CollData.CollisionType == CollisionType.Polygon =>
                 SpherePolygonCollisionCheck(polygon2, CollisionData, out normal, out depth),
             CollisionType.Sphere when p2CollData.CollisionType == CollisionType.Sphere => 
                 SphereXSphereCollision(CollisionData, p2CollData, out normal, out depth),
             _ => throw new InvalidOperationException(
                 $"Cannot check collisions with types: {CollisionData.CollisionType} and {p2CollData.CollisionType}")
         };
     }

    public bool IntersectRay(Ray ray, out Vector3 intersectionResult)
    {
        intersectionResult = Vector3.Zero;

        if (CollisionData.CollisionType == CollisionType.Polygon) 
            return IntersectRayWithPolygon(ray, ref intersectionResult);
        else if (CollisionData is { CollisionType: CollisionType.Sphere, PhysicObject: not null })
            return IntersectRayWithSphere(ray, ref intersectionResult);
        
        return false;
    }

    private void InitCollision(CollisionData originalCollision)
    {
        // A sphere has no vertices by design (its radius is the object's width), so only a polygon needs them.
        if (originalCollision.CollisionType == CollisionType.Polygon && originalCollision.Vertices.Length == 0)
            throw new ArgumentException("A polygon collision needs at least one vertex.", nameof(originalCollision));

        m_currentCollision.Vertices = new Vector3[originalCollision.Vertices.Length];
        originalCollision.Vertices.CopyTo(m_currentCollision.Vertices, 0);

        foreach (var mesh in originalCollision.Faces)
            m_currentCollision.Faces.Add(new Face(mesh.Indices) { Normal = mesh.Normal });
    }

    // An edge is where two faces meet: they share exactly two vertices and are not coplanar (two triangles of one
    // split quad share their diagonal, which is not an edge of the shape).
    private static (uint A, uint B)[] FindEdges(CollisionData shape)
    {
        var edges = new List<(uint A, uint B)>();
        var faces = shape.Faces;

        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                var sharedCount = 0;
                uint first = 0;
                uint second = 0;

                foreach (var index in faces[i].Indices)
                {
                    if (Array.IndexOf(faces[j].Indices, index) < 0)
                        continue;

                    if (sharedCount == 0)
                        first = index;
                    else if (sharedCount == 1)
                        second = index;

                    sharedCount++;
                }

                if (sharedCount != 2 ||
                    Vector3.Cross(faces[i].Normal, faces[j].Normal).LengthSquared() < ParallelEpsilon)
                    continue;

                var edge = (Math.Min(first, second), Math.Max(first, second));
                if (!edges.Contains(edge))
                    edges.Add(edge);
            }
        }

        return edges.ToArray();
    }

    // Parallel edges give the same edge x edge axes, so one edge per parallel class is enough. Parallelism survives
    // any affine transform, so classes found in model space hold for the transformed shape.
    private static int[] FindEdgeDirections(CollisionData shape, (uint A, uint B)[] edges)
    {
        var kept = new List<int>();

        for (int k = 0; k < edges.Length; k++)
        {
            var direction = Vector3.Normalize(shape.Vertices[edges[k].B] - shape.Vertices[edges[k].A]);
            var isParallelToKept = false;

            foreach (var keptIndex in kept)
            {
                var keptEdge = edges[keptIndex];
                var keptDirection = Vector3.Normalize(shape.Vertices[keptEdge.B] - shape.Vertices[keptEdge.A]);

                if (Vector3.Cross(direction, keptDirection).LengthSquared() < ParallelEpsilon)
                {
                    isParallelToKept = true;
                    break;
                }
            }

            if (!isParallelToKept)
                kept.Add(k);
        }

        return kept.ToArray();
    }

    // A cuboid in model space: 8 corners of an axis-aligned box and its 6 faces, one per signed axis. The game's
    // transform scales before it rotates, which keeps a cuboid a cuboid, but IPhysicObject.Transformation forbids
    // neither shear nor a zero size, so the current axes are checked on every sphere test (AreAxesPerpendicular):
    // the closest-point test runs only while they are perpendicular, any other case takes the separating axes.
    private static int[]? FindCuboidAxisFaces(CollisionData shape)
    {
        if (shape.CollisionType != CollisionType.Polygon || shape.Vertices.Length != 8 || shape.Faces.Count != 6)
            return null;

        // The face for each signed axis, in the order +X, -X, +Y, -Y, +Z, -Z.
        var signedAxisFaces = new[] { -1, -1, -1, -1, -1, -1 };

        for (int i = 0; i < shape.Faces.Count; i++)
        {
            var faceNormal = shape.Faces[i].Normal;
            var slot = -1;

            for (int s = 0; s < signedAxisFaces.Length; s++)
            {
                var signedAxis = Vector3.Zero;
                signedAxis[s / 2] = s % 2 == 0 ? 1 : -1;

                if (Math.Abs(faceNormal.X - signedAxis.X) <= CuboidTolerance &&
                    Math.Abs(faceNormal.Y - signedAxis.Y) <= CuboidTolerance &&
                    Math.Abs(faceNormal.Z - signedAxis.Z) <= CuboidTolerance)
                {
                    slot = s;
                    break;
                }
            }

            if (slot < 0 || signedAxisFaces[slot] >= 0)
                return null;

            signedAxisFaces[slot] = i;
        }

        var vertices = shape.Vertices;
        var min = vertices[0];
        var max = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        for (int axis = 0; axis < 3; axis++)
        {
            if (max[axis] - min[axis] <= CuboidTolerance)
                return null;
        }

        // Each vertex is coded by which coordinates sit at the maximum; 8 different codes are the 8 corners.
        var foundCorners = 0;

        foreach (var vertex in vertices)
        {
            var corner = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                if (Math.Abs(vertex[axis] - max[axis]) <= CuboidTolerance)
                    corner |= 1 << axis;
                else if (Math.Abs(vertex[axis] - min[axis]) > CuboidTolerance)
                    return null;
            }

            if ((foundCorners & (1 << corner)) != 0)
                return null;

            foundCorners |= 1 << corner;
        }

        return new[] { signedAxisFaces[0], signedAxisFaces[2], signedAxisFaces[4] };
    }

    private bool IntersectRayWithPolygon(Ray ray, ref Vector3 intersectionResultPoint)
    {
        for (int i = 0; i < CollisionData.Faces.Count; i++)
        {
            var face = CollisionData.Faces[i];
            var normal = face.Normal;
            var rayDelta = ray.RayDelta;

            var nDotRayDelta = Vector3.Dot(normal, rayDelta);

            //Needs to avoiding situation when Ray point to face that comes firstly than needed face.
            if (nDotRayDelta < 1e-6f)
                continue;

            var meshFirstVertex = CollisionData.Vertices[face.Indices[0]];
            var t = Vector3.Dot(-normal, ray.Start - meshFirstVertex) / nDotRayDelta;
            
            //Needs to avoiding situation when Ray point to same place that face point at.  
            if (t > 0)
                continue;
            
            var localRayEnd = ray.Start + rayDelta * t;

            var deltaMeshAndLocalRayStart = localRayEnd - meshFirstVertex;
            var deltaV21 = CollisionData.Vertices[face.Indices[1]] - meshFirstVertex;
            var deltaV31 = CollisionData.Vertices[face.Indices[2]] - meshFirstVertex;

            var u = Vector3.Dot(deltaMeshAndLocalRayStart, deltaV21);
            var v = Vector3.Dot(deltaMeshAndLocalRayStart, deltaV31);

            if (u >= 0 && u <= Vector3.Dot(deltaV21, deltaV21) &&
                v >= 0 && v <= Vector3.Dot(deltaV31, deltaV31))
            {
                intersectionResultPoint = localRayEnd;
                return true;
            }
        }

        return false;
    }
    
    private bool IntersectRayWithSphere(Ray ray, ref Vector3 intersectionResult)
    {
        var sphereCenter = CollisionData.PhysicObject.Position;
        var sphereRadius = CollisionData.PhysicObject.Width / 2;
        
        var rayToSphereDirectionalVector = ray.Start - sphereCenter;
        var rayDirection = -Vector3.Normalize(ray.RayDelta);
        var a = Vector3.Dot(rayDirection, rayDirection);
        var b = 2 * Vector3.Dot(rayDirection, rayToSphereDirectionalVector);
        var c = Vector3.Dot(rayToSphereDirectionalVector, rayToSphereDirectionalVector) - sphereRadius * sphereRadius;

        if (!Common.Helpers.MathHelper.QuadraticEquation(a, b, c, out var intersectPoint1, out var intersectPoint2))
            return false;

        if (intersectPoint1 < 0)
        {
            intersectPoint1 = intersectPoint2;

            if (intersectPoint1 < 0)
                return false;
        }

        intersectionResult = ray.Start + rayDirection * intersectPoint1;

        return true;
    }
    
    private static bool SphereXSphereCollision(CollisionData sphere1, CollisionData sphere2, out Vector3 normal, out float depth)
    {
        var physObj1 = sphere1.PhysicObject;
        var physObj2 = sphere2.PhysicObject;
        
        var directionalVector = physObj1.Position - physObj2.Position;
        
        var distance = directionalVector.Length();
        var radii = (physObj1.Width + physObj2.Width) / 2;

        normal = -Vector3.Normalize(directionalVector);
        
        var sphere1EndPoint = physObj1.Position + normal * (physObj1.Width / 2);

        depth = physObj2.Width / 2 - Vector3.Distance(sphere1EndPoint, physObj2.Position); 
        
        return distance <= radii;
    }

    private static bool OBBCollisionCheck(Collision polygon1, Collision polygon2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        var polygon1Data = polygon1.CollisionData;
        var polygon2Data = polygon2.CollisionData;

        if (!OBBCollisionCheckLoop(polygon1Data, polygon2Data, ref normal, ref depth))
            return false;

        if (!OBBCollisionCheckLoop(polygon2Data, polygon1Data, ref normal, ref depth))
            return false;

        // Face normals alone miss shapes whose edges pass each other skewed: they overlap on every face axis
        // across a gap. The edge x edge axes close that hole; ties stay with the face axes found first.
        if (!EdgeAxesCollisionCheckLoop(polygon1, polygon2, ref normal, ref depth))
            return false;

        if (Vector3.Dot(polygon2Data.PhysicObject.Position - polygon1Data.PhysicObject.Position, normal) < 0)
            normal = -normal;

        return true;
    }

    private static bool EdgeAxesCollisionCheckLoop(Collision polygon1, Collision polygon2, ref Vector3 normal,
        ref float depth)
    {
        var vertices1 = polygon1.m_currentCollision.Vertices;
        var vertices2 = polygon2.m_currentCollision.Vertices;

        foreach (var edgeIndex1 in polygon1.m_edgeDirections)
        {
            var edge1 = polygon1.m_edges[edgeIndex1];
            var direction1 = Vector3.Normalize(vertices1[edge1.B] - vertices1[edge1.A]);

            foreach (var edgeIndex2 in polygon2.m_edgeDirections)
            {
                var edge2 = polygon2.m_edges[edgeIndex2];
                var direction2 = Vector3.Normalize(vertices2[edge2.B] - vertices2[edge2.A]);

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

    private static bool OBBCollisionCheckLoop(CollisionData polygon1, CollisionData polygon2, ref Vector3 normal, ref float depth)
    {
        for (int i = 0; i < polygon1.Faces.Count; i++)
        {
            var face = polygon1.Faces[i];

            ProjectionMinMaxVertices(face.Normal, polygon1.Vertices,
                out var poly1Min, out var poly1Max);
            ProjectionMinMaxVertices(face.Normal, polygon2.Vertices,
                out var poly2Min, out var poly2Max);
            
            if (poly1Min >= poly2Max || poly2Min >= poly1Max)
            {
                return false;
            }

            var min = Math.Min(poly2Max - poly1Min, poly1Max - poly2Min);
            if (min < depth)
            {
                normal = face.Normal;
                depth = min;
            }
        }

        return true;
    }
    
    private static bool SpherePolygonCollisionCheck(Collision polygon, CollisionData sphere, out Vector3 normal,
        out float depth)
    {
        return polygon.m_cuboidAxisFaces is not null &&
               AreAxesPerpendicular(polygon.m_currentCollision, polygon.m_cuboidAxisFaces)
            ? SphereCuboidCollisionCheck(polygon.m_currentCollision, polygon.m_cuboidAxisFaces, sphere, out normal, out depth)
            : SpherePolygonSatCollisionCheck(polygon.m_currentCollision, polygon.m_edges, sphere, out normal, out depth);
    }

    // The closest-point test is exact only for a box whose current axes are perpendicular: a shear tilts them, and
    // a zero size makes an axis NaN (every comparison with NaN is false, so NaN fails this check too).
    private static bool AreAxesPerpendicular(CollisionData cuboid, int[] axisFaces)
    {
        var axis0 = cuboid.Faces[axisFaces[0]].Normal;
        var axis1 = cuboid.Faces[axisFaces[1]].Normal;
        var axis2 = cuboid.Faces[axisFaces[2]].Normal;

        return MathF.Abs(Vector3.Dot(axis0, axis1)) <= CuboidOrthogonalityTolerance &&
               MathF.Abs(Vector3.Dot(axis1, axis2)) <= CuboidOrthogonalityTolerance &&
               MathF.Abs(Vector3.Dot(axis0, axis2)) <= CuboidOrthogonalityTolerance;
    }

    // Exact for a cuboid: clamp the sphere centre into the box along the box's three axes to get the closest point.
    private static bool SphereCuboidCollisionCheck(CollisionData cuboid, int[] axisFaces, CollisionData sphere,
        out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        var center = sphere.PhysicObject.Position;
        var radius = sphere.PhysicObject.Width / 2;

        var isOutside = false;
        var delta = Vector3.Zero;
        // For a centre inside the box: the face it is least deep behind, + before - and k = 0..2 on a tie.
        var smallestPenetration = float.MaxValue;
        var smallestPenetrationNormal = Vector3.Zero;

        for (int k = 0; k < axisFaces.Length; k++)
        {
            var axis = cuboid.Faces[axisFaces[k]].Normal;

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
    private static bool SpherePolygonSatCollisionCheck(CollisionData polygon, (uint A, uint B)[] edges,
        CollisionData sphere, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        float polygonProjMin; float polygonProjMax;
        float sphereProjMin; float sphereProjMax;
        float intersectionDepth;
        var closestFacePointsIndex = polygon.Vertices.ClosestPointIndex(sphere.PhysicObject.Position);

        if (closestFacePointsIndex == -1)
            return false;
        
        for (int i = 0; i < polygon.Faces.Count; i++)
        {
            var face = polygon.Faces[i];

            ProjectionMinMaxVertices(face.Normal, polygon.Vertices,
                out polygonProjMin, out polygonProjMax);
            SphereProjectionMinMaxVertices(face.Normal, sphere.PhysicObject.Position, sphere.PhysicObject.Width / 2,
                out sphereProjMin, out sphereProjMax);

            if (polygonProjMin >= sphereProjMax || sphereProjMin >= polygonProjMax)
            {
                return false;
            }

            intersectionDepth = Math.Min(sphereProjMax - polygonProjMin, polygonProjMax - sphereProjMin);
            if (intersectionDepth < depth)
            {
                normal = face.Normal;
                depth = intersectionDepth;
            }
            else if (intersectionDepth.ApproximateEqual(depth) &&
                     Array.IndexOf(face.Indices, (uint)closestFacePointsIndex) >= 0)
            {
                normal = face.Normal;
            }
        }

        var center = sphere.PhysicObject.Position;
        var radius = sphere.PhysicObject.Width / 2;
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

        for (int i = 0; i < edges.Length; i++)
        {
            var start = vertices[edges[i].A];
            var edge = vertices[edges[i].B] - start;
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

    private static void ProjectionMinMaxVertices(in Vector3 normal, Vector3[] vertices, out float min, out float max)
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
    
    private static bool AABBCollisionCheck(CollisionData polygon1, CollisionData polygon2)
    {
        var poly1BB = polygon1.BoundingBox;
        var poly2BB = polygon2.BoundingBox;

        return poly1BB.Min.X <= poly2BB.Max.X &&
               poly1BB.Max.X >= poly2BB.Min.X &&
               poly1BB.Min.Y <= poly2BB.Max.Y &&
               poly1BB.Max.Y >= poly2BB.Min.Y &&
               poly1BB.Min.Z <= poly2BB.Max.Z &&
               poly1BB.Max.Z >= poly2BB.Min.Z;
    }
}