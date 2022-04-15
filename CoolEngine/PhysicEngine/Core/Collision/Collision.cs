using System.Collections.ObjectModel;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public enum CollisionType
{
    Polygon,
    Sphere
}

public class Collision
{
    private readonly CollisionData m_originalCollision;
    private readonly CollisionData m_currentCollision;
    
    private IPhysicObject m_currentObj;

    public struct BoundingBox
    {
        public Vector3 MinVertex;
        public Vector3 MaxVertex;
    }
    
    public Collision(IPhysicObject physicObject, CollisionData originalCollision, CollisionType collisionType)
    {
        m_currentCollision = new CollisionData();
        m_currentObj = physicObject;

        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));

        m_originalCollision = originalCollision;
        CollisionType = collisionType;

        InitCollision(originalCollision);
        IsActive = true;
        IsDrawing = true;
    }

    public CollisionData CollisionData => m_currentCollision;

    public IPhysicObject CurrentObject => m_currentObj;

    public CollisionType CollisionType { get; protected set; }

    public bool IsActive { get; set; }
    
    public bool IsDrawing { get; set; }

    public void UpdateCollision()
    {
        var transformation = m_currentObj.Transform;
        var rotation = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(m_currentObj.Yaw)) * 
                       Matrix4.CreateRotationY(MathHelper.DegreesToRadians(m_currentObj.Pitch)) * 
                       Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(m_currentObj.Roll));

        for (int i = 0; i < m_currentCollision.Vertices.Length; i++)
            m_currentCollision.Vertices[i] =
                new Vector3(new Vector4(m_originalCollision.Vertices[i], 1) * transformation);
        
        for (int i = 0; i < m_currentCollision.Meshes.Count; i++)
            m_currentCollision.Meshes[i].Normal = Vector3.Normalize(new Vector3(new Vector4(m_originalCollision.Meshes[i].Normal, 1) * rotation));
    }

    public bool CheckCollision(IPhysicObject t2, out Vector3 normal, out float depth)
     {
         normal = Vector3.Zero;
         depth = float.MaxValue;

         if (t2 == null)
             return false;

         normal = (t2.Position - CurrentObject.Position).Normalized();
         
         return CollisionType == CollisionType.Sphere ?
             t2.Collision.CollisionType == CollisionType.Sphere ? 
                 SphereXSphereCollision(t2, out depth) : SphereXPolygonCollision(t2, out depth) 
                : 
             t2.Collision.CollisionType == CollisionType.Sphere ? 
             SphereOBBCollisionCheck(t2, out normal, out depth) : OBBCollisionCheck(t2, out normal, out depth);
     }
    
    private void InitCollision(CollisionData originalCollision)
    {
        // if (CollisionType == CollisionType.Sphere)
        //     return;
        
        if (originalCollision.Vertices.Length == 0)
        {
            Console.WriteLine(
                $"InitCollision error. Cannot copy mesh for object {m_currentObj.GetType().FullName}. It's not fit both textured mesh and common mesh type.");
            return;
        }

        m_currentCollision.Vertices = new Vector3[originalCollision.Vertices.Length];
        originalCollision.Vertices.CopyTo(m_currentCollision.Vertices, 0);

        foreach (var mesh in originalCollision.Meshes)
            m_currentCollision.Meshes.Add(new CollisionMesh(mesh.Indices) { Normal = mesh.Normal });
    }

    /// <summary>
    /// Check collision between two physic objects
    /// </summary>
    /// <param name="t2">From which object will takes vertices</param>
    /// <param name="normal">Normal of side where collision was</param>
    /// <returns></returns>
    private void GetBoundingBoxCoors(Vector3[] vertices, out Vector3 min, out Vector3 max)
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

    private bool SphereXSphereCollision(IPhysicObject t2, out float depth)
    {
        var distance = Math.Abs(Vector3.Distance(CurrentObject.Position, t2.Position));
        var radii = (CurrentObject.Width + t2.Width) / 2;

        depth = Math.Abs(radii - t2.Width);
        
        return distance <= radii;
    }
    
    private bool SphereXPolygonCollision(IPhysicObject t2, out float depth)
    {
        depth = float.MaxValue;

        for (int i = 0; i < t2.Collision.CollisionData.Vertices.Length; i++)
        {
            var distance = Math.Abs(Vector3.Distance(CurrentObject.Position, t2.Collision.CollisionData.Vertices[i]));
            var radius = CurrentObject.Width / 2;
            
            if (distance <= radius)
            {
                depth = radius - distance;
                return true;
            }
        }

        return false;
    }

    private bool OBBCollisionCheck(IPhysicObject t2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;

        for (int i = 0; i < CurrentObject.Collision.CollisionData.Meshes.Count; i++)
        {
            var mesh = CurrentObject.Collision.CollisionData.Meshes[i];
            float currMin, currMax, t2Min, t2Max;

            ProjectionMinMaxVertices(mesh.Normal, CurrentObject.Collision.CollisionData.Vertices,
                out currMin, out currMax);
            ProjectionMinMaxVertices(mesh.Normal, t2.Collision.CollisionData.Vertices,
                out t2Min, out t2Max);

            if (currMin >= t2Max || t2Min >= currMax)
            {
                mesh.Color = Colors.Red;
                t2.Collision.CollisionData.Meshes[i].Color = Colors.Red;
                return false;
            }

            var min = Math.Min(t2Max - currMin, currMax - t2Min);
            if (min < depth)
            {
                normal = mesh.Normal;
                depth = min;
            }
        }

        if (Vector3.Dot(t2.Position - CurrentObject.Position, normal) < 0)
            normal = -normal;

        return true;
    }
    
    //TODO: Fix sphere obb collision check
    private bool SphereOBBCollisionCheck(IPhysicObject t2, out Vector3 normal, out float depth)
    {
        normal = Vector3.Zero;
        depth = float.MaxValue;


        return false;
        
        float currMin, currMax, t2Min, t2Max;
        
        var _normal = Vector3.Zero;
        var axisDepth = 0.0f;

        for (int i = 0; i < CurrentObject.Collision.CollisionData.Meshes.Count; i++)
        {
            var mesh = CurrentObject.Collision.CollisionData.Meshes[i];
            _normal = mesh.Normal;

            ProjectionMinMaxVertices(mesh.Normal, CurrentObject.Collision.CollisionData.Vertices,
                out currMin, out currMax);
            SphereProjectionMinMaxVertices(mesh.Normal, t2.Position, t2.Width,
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

        var cpIdx = ClosestPointIndex(t2.Position, CurrentObject.Collision.CollisionData.Vertices);
        
        if (cpIdx == -1)
            return false;
        
        _normal = CurrentObject.Collision.CollisionData.Vertices[cpIdx] - t2.Position;
        _normal.Normalize();
        
        ProjectionMinMaxVertices(_normal, CurrentObject.Collision.CollisionData.Vertices,
            out currMin, out currMax);
        SphereProjectionMinMaxVertices(_normal, t2.Position, t2.Width,
            out t2Min, out t2Max);
        
        if (currMin >= t2Max || t2Min >= currMax)
        {
            return false;
        }
        
        axisDepth = Math.Min(t2Max - currMin, currMax - t2Min);
        if (axisDepth < depth)
        {
            normal = _normal;
            depth = axisDepth;
        }
        
        if (Vector3.Dot(CurrentObject.Position - t2.Position, normal) < 0)
            normal = -normal;
        
        return true;
    }

    private void ProjectionMinMaxVertices(in Vector3 normal, Vector3[] vertices, out float min, out float max)
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
    
    private void SphereProjectionMinMaxVertices(in Vector3 normal, in Vector3 center, float radius, 
        out float min, out float max)
    {
        var directedRadius = normal * radius;
        
        min = Vector3.Dot(directedRadius - center, normal);
        max = Vector3.Dot(directedRadius + center, normal);

        if (min > max)
            (min, max) = (max, min);
    }

    private int ClosestPointIndex(in Vector3 to, Vector3[] vertices)
    {
        var length = float.MaxValue;
        int idx = -1;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            var tmpLength = Vector3.Distance(to, vertices[i]);
            if (length > tmpLength)
            {
                length = tmpLength;
                idx = i;
            }
        }

        return idx;
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