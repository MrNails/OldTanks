using CoolEngine.Models;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class Collision
{
    private readonly CollisionData m_originalCollision;
    private readonly CollisionData m_currentCollision;
    
    public Collision(IPhysicObject physicObject, CollisionData originalCollision)
    {
        if (physicObject == null)
            throw new ArgumentNullException(nameof(physicObject));
        
        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));
        
        m_currentCollision = new CollisionData(originalCollision.CollisionType) { PhysicObject = physicObject };

        m_originalCollision = originalCollision;

        InitCollision(originalCollision);
        
        IsActive = true;
        IsDrawing = true;
    }

    public CollisionData CollisionData => m_currentCollision;

    public bool IsActive { get; set; }
    
    public bool IsDrawing { get; set; }

    public void UpdateCollision()
    {
        var transformation = CollisionData.PhysicObject!.Transformation;
        var rotation = transformation.ClearTranslation().ClearScale();

        for (int i = 0; i < m_currentCollision.Vertices.Length; i++)
            m_currentCollision.Vertices[i] =
                new Vector3(new Vector4(m_originalCollision.Vertices[i], 1) * transformation);
        
        for (int i = 0; i < m_currentCollision.Faces.Count; i++)
            m_currentCollision.Faces[i].Normal = Vector3.Normalize(new Vector3(new Vector4(m_originalCollision.Faces[i].Normal, 1) * rotation));
        
        CollisionData.UpdateBoundingBox();
    }

    public bool CheckCollision(Collision polygon2, out Vector3 normal, out float depth)
     {
         normal = Vector3.Zero;
         depth = float.MaxValue;

         if (polygon2?.CollisionData.PhysicObject == null ||
             !AABBCollisionCheck(CollisionData, polygon2.CollisionData))
             return false;

         var p2CollData = polygon2.CollisionData;

         return CollisionData.CollisionType switch
         {
             CollisionType.Polygon when p2CollData.CollisionType == CollisionType.Polygon => 
                 OBBCollisionCheck(CollisionData, p2CollData, out normal, out depth),
             CollisionType.Polygon when p2CollData.CollisionType == CollisionType.Sphere => 
                 SphereOBBCollisionCheck(CollisionData, p2CollData, out normal, out depth),
             CollisionType.Sphere when p2CollData.CollisionType == CollisionType.Polygon => 
                 SphereOBBCollisionCheck(p2CollData, CollisionData, out normal, out depth),
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
        if (originalCollision.Vertices.Length == 0)
        {
            Console.WriteLine("InitCollision error. Cannot copy mesh from original collision because there are no vertices.");
            return;
        }

        m_currentCollision.Vertices = new Vector3[originalCollision.Vertices.Length];
        originalCollision.Vertices.CopyTo(m_currentCollision.Vertices, 0);

        foreach (var mesh in originalCollision.Faces)
            m_currentCollision.Faces.Add(new Face(mesh.Indices) { Normal = mesh.Normal });

        originalCollision.UpdateBoundingBox();
    }

    private bool IntersectRayWithPolygon(Ray ray, ref Vector3 intersectionResult)
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
                intersectionResult = localRayEnd;
                return true;
            }
        }

        return false;
    }
    
    private bool IntersectRayWithSphere(Ray ray, ref Vector3 intersectionResult)
    {
        var rayDelta = ray.RayDelta;
        var rayLength = rayDelta.Length;
        var sphereCenter = CollisionData.PhysicObject.Position;
        var sphereSize = CollisionData.PhysicObject.Width;

        var rayToSphereVector = ray.Start - sphereCenter;
        var rayToSphereLength = Math.Abs(rayToSphereVector.Length);

        if (rayLength < rayToSphereLength - sphereSize)
            return true;

        var rayDirection = rayDelta.Normalized();
        var localEnd = ray.Start + rayDirection * (rayToSphereLength - sphereSize);

        if (Math.Abs((localEnd - sphereCenter).Length) <= sphereSize)
        {
            intersectionResult = localEnd;
            return true;
        }

        return false;
    }
    
    private static bool SphereXSphereCollision(CollisionData sphere1, CollisionData sphere2, out Vector3 normal, out float depth)
    {
        var distance = Math.Abs(Vector3.Distance(sphere1.PhysicObject!.Position, sphere2.PhysicObject!.Position));
        var radii = (sphere1.PhysicObject!.Width + sphere2.PhysicObject!.Width) / 2;

        normal = (sphere2.PhysicObject.Position - sphere1.PhysicObject.Position).Normalized();
        
        depth = Math.Abs(radii - sphere2.PhysicObject!.Width) / 2;
        
        return distance <= radii;
    }

    private static bool OBBCollisionCheck(CollisionData polygon1, CollisionData polygon2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        for (int i = 0; i < polygon1.Faces.Count; i++)
        {
            var mesh = polygon1.Faces[i];
            float poly1Min, poly1Max, poly2Min, poly2Max;

            ProjectionMinMaxVertices(mesh.Normal, polygon1.Vertices,
                out poly1Min, out poly1Max);
            ProjectionMinMaxVertices(mesh.Normal, polygon2.Vertices,
                out poly2Min, out poly2Max);

            if (poly1Min >= poly2Max || poly2Min >= poly1Max)
            {
                return false;
            }

            var min = Math.Min(poly2Max - poly1Min, poly1Max - poly2Min);
            if (min < depth)
            {
                normal = mesh.Normal;
                depth = min;
            }
        }

        if (Vector3.Dot(polygon2.PhysicObject!.Position - polygon1.PhysicObject!.Position, normal) < 0)
            normal = -normal;

        return true;
    }
    
    //TODO: Fix sphere obb collision check
    private static bool SphereOBBCollisionCheck(CollisionData polygon, CollisionData sphere, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        float currMin, currMax, t2Min, t2Max;
        
        var _normal = Vector3.Zero;
        var axisDepth = 0.0f;

        for (int i = 0; i < polygon.Faces.Count; i++)
        {
            var mesh = polygon.Faces[i];
            _normal = mesh.Normal;

            ProjectionMinMaxVertices(mesh.Normal, polygon.Vertices,
                out currMin, out currMax);
            SphereProjectionMinMaxVertices(mesh.Normal, sphere.PhysicObject!.Position, sphere.PhysicObject.Width / 2,
                out t2Min, out t2Max);

            if (currMin >= t2Max || t2Min >= currMax)
            {
                mesh.Color = Colors.Red;
                return false;
            }

            axisDepth = Math.Min(t2Max - currMin, currMax - t2Min);
            if (axisDepth < depth)
            {
                normal = mesh.Normal;
                depth = axisDepth;
            }
        }

        var cpIdx = ClosestPointIndex(sphere.PhysicObject!.Position, polygon.Vertices);
        
        if (cpIdx == -1)
            return false;
        
        _normal = polygon.Vertices[cpIdx] - sphere.PhysicObject!.Position;
        _normal.Normalize();
        
        ProjectionMinMaxVertices(_normal, polygon.Vertices,
            out currMin, out currMax);
        SphereProjectionMinMaxVertices(_normal, sphere.PhysicObject!.Position, sphere.PhysicObject!.Width / 2,
            out t2Min, out t2Max);
        
        if (currMin >= t2Max || t2Min >= currMax)
            return false;
        
        axisDepth = Math.Min(t2Max - currMin, currMax - t2Min);
        if (axisDepth < depth)
        {
            normal = _normal;
            depth = axisDepth;
        }
        
        //TODO: Fix collision detection for dynamic sphere
        if (Vector3.Dot(polygon.PhysicObject!.Position - sphere.PhysicObject!.Position, normal) < 0)
            normal = -normal;
        
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

    private static int ClosestPointIndex(in Vector3 to, Vector3[] vertices)
    {
        var length = float.MaxValue;
        int idx = -1;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            var tmpLength = Vector3.Distance(vertices[i], to);
            if (length > tmpLength)
            {
                length = tmpLength;
                idx = i;
            }
        }

        return idx;
    }
    
    private static bool AABBCollisionCheck(CollisionData polygon1, CollisionData polygon2)
    {
        var poly1BB = polygon1.BoundingBox;
        var poly2BB = polygon2.BoundingBox;

        return poly1BB.Min.GreaterOrEqualThan(poly2BB.Min) &&
               poly1BB.Min.LowerOrEqualThan(poly2BB.Max) ||
               poly1BB.Max.GreaterOrEqualThan(poly2BB.Min) &&
               poly1BB.Max.LowerOrEqualThan(poly2BB.Max) ||
               poly2BB.Min.GreaterOrEqualThan(poly1BB.Min) &&
               poly2BB.Min.LowerOrEqualThan(poly1BB.Max) ||
               poly2BB.Max.GreaterOrEqualThan(poly1BB.Min) &&
               poly2BB.Max.LowerOrEqualThan(poly1BB.Max);
    }
}