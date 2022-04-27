using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class Collision
{
    private readonly CollisionData m_originalCollision;
    private readonly CollisionData m_currentCollision;

    public Collision(ITransformable transformable, CollisionData originalCollision)
    {
        if (transformable == null)
            throw new ArgumentNullException(nameof(transformable));
        
        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));
        
        m_currentCollision = new CollisionData(originalCollision.CollisionType) { Transformable = transformable };

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
        var transformation = CollisionData.Transformable!.Transform;
        var rotation = transformation.ClearTranslation().ClearScale();

        for (int i = 0; i < m_currentCollision.Vertices.Length; i++)
            m_currentCollision.Vertices[i] =
                new Vector3(new Vector4(m_originalCollision.Vertices[i], 1) * transformation);
        
        for (int i = 0; i < m_currentCollision.Meshes.Count; i++)
            m_currentCollision.Meshes[i].Normal = Vector3.Normalize(new Vector3(new Vector4(m_originalCollision.Meshes[i].Normal, 1) * rotation));
    }

    public bool CheckCollision(Collision t2, out Vector3 normal, out float depth)
     {
         normal = Vector3.Zero;
         depth = float.MaxValue;

         if (t2 == null || t2.CollisionData.Transformable == null)
             return false;

         return CollisionData.CollisionType == CollisionType.Sphere ?
                 t2.CollisionData.CollisionType == CollisionType.Sphere ? 
                     SphereXSphereCollision(CollisionData, t2.CollisionData, out normal, out depth) : 
                     SphereOBBCollisionCheck(t2.CollisionData, CollisionData, out normal, out depth) 
                    : 
                 t2.CollisionData.CollisionType == CollisionType.Sphere ? 
                     SphereOBBCollisionCheck(CollisionData, t2.CollisionData, out normal, out depth) : 
                     OBBCollisionCheck(CollisionData, t2.CollisionData, out normal, out depth);
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

        foreach (var mesh in originalCollision.Meshes)
            m_currentCollision.Meshes.Add(new CollisionMesh(mesh.Indices) { Normal = mesh.Normal });
    }

    private static bool SphereXSphereCollision(CollisionData sphere1, CollisionData sphere2, out Vector3 normal, out float depth)
    {
        var distance = Math.Abs(Vector3.Distance(sphere1.Transformable!.Position, sphere2.Transformable!.Position));
        var radii = (sphere1.Transformable!.Width + sphere2.Transformable!.Width) / 2;

        normal = (sphere2.Transformable.Position - sphere1.Transformable.Position).Normalized();
        
        depth = Math.Abs(radii - sphere2.Transformable!.Width) / 2;
        
        return distance <= radii;
    }

    private static bool OBBCollisionCheck(CollisionData polygon1, CollisionData polygon2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        for (int i = 0; i < polygon1.Meshes.Count; i++)
        {
            var mesh = polygon1.Meshes[i];
            float currMin, currMax, t2Min, t2Max;

            ProjectionMinMaxVertices(mesh.Normal, polygon1.Vertices,
                out currMin, out currMax);
            ProjectionMinMaxVertices(mesh.Normal, polygon2.Vertices,
                out t2Min, out t2Max);

            if (currMin >= t2Max || t2Min >= currMax)
            {
                mesh.Color = Colors.Red;
                polygon2.Meshes[i].Color = Colors.Red;
                return false;
            }

            var min = Math.Min(t2Max - currMin, currMax - t2Min);
            if (min < depth)
            {
                normal = mesh.Normal;
                depth = min;
            }
        }

        if (Vector3.Dot(polygon2.Transformable!.Position - polygon1.Transformable!.Position, normal) < 0)
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

        for (int i = 0; i < polygon.Meshes.Count; i++)
        {
            var mesh = polygon.Meshes[i];
            _normal = mesh.Normal;

            ProjectionMinMaxVertices(mesh.Normal, polygon.Vertices,
                out currMin, out currMax);
            SphereProjectionMinMaxVertices(mesh.Normal, sphere.Transformable!.Position, sphere.Transformable.Width / 2,
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

        var cpIdx = ClosestPointIndex(sphere.Transformable!.Position, polygon.Vertices);
        
        if (cpIdx == -1)
            return false;
        
        _normal = polygon.Vertices[cpIdx] - sphere.Transformable!.Position;
        _normal.Normalize();
        
        ProjectionMinMaxVertices(_normal, polygon.Vertices,
            out currMin, out currMax);
        SphereProjectionMinMaxVertices(_normal, sphere.Transformable!.Position, sphere.Transformable!.Width / 2,
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
        if (Vector3.Dot(polygon.Transformable!.Position - sphere.Transformable!.Position, normal) < 0)
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
    
    /// <summary>
    /// Check collision between two physic objects
    /// </summary>
    /// <returns></returns>
    private static void GetBoundingBoxCoords(Vector3[] vertices, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);

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
    
    // private bool AABBCollisionCheck(IPhysicObject t2)
    // {
    //     var outerVertices = t2.Collision.CollisionData.Vertices;
    //
    //     for (int oJ = 0; oJ < outerVertices.Length; oJ++)
    //     {
    //         if (VectorExtensions.GreaterThan(m_maxVertex, outerVertices[oJ]) &&
    //             VectorExtensions.LoverThan(m_minVertex, outerVertices[oJ]))
    //             return true;
    //     }
    //
    //     return false;
    // }
}