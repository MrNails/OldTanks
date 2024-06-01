using Common.Extensions;
using CoolEngine.Models;
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

         if (!AABBCollisionCheck(CollisionData, polygon2.CollisionData))
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
        var rayDirection = -ray.RayDelta.Normalized();
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
        
        var distance = directionalVector.Length;
        var radii = (physObj1.Width + physObj2.Width) / 2;

        normal = -directionalVector.Normalized();
        
        var sphere1EndPoint = physObj1.Position + normal * (physObj1.Width / 2);

        depth = physObj2.Width / 2 - Vector3.Distance(sphere1EndPoint, physObj2.Position); 
        
        return distance <= radii;
    }

    private static bool OBBCollisionCheck(CollisionData polygon1, CollisionData polygon2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;


        if (!OBBCollisionCheckLoop(polygon1, polygon2, ref normal, ref depth)) 
            return false;
        
        if (!OBBCollisionCheckLoop(polygon2, polygon1, ref normal, ref depth)) 
            return false;

        if (Vector3.Dot(polygon2.PhysicObject.Position - polygon1.PhysicObject.Position, normal) < 0)
            normal = -normal;

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
    
    private static bool SphereOBBCollisionCheck(CollisionData polygon, CollisionData sphere, out Vector3 normal, out float depth)
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
                     face.Indices.Contains((uint)closestFacePointsIndex))
            {
                normal = face.Normal;
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