using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class CubeCollision : Collision
{
    private Vector3 m_minVertex;
    private Vector3 m_maxVertex;

    public CubeCollision(IPhysicObject physicObject, CollisionData originalCollision) : base(physicObject,
        originalCollision)
    {
        CollisionType = CollisionType.Cube;
    }

    public override void UpdateCollision()
    {
        base.UpdateCollision();

        if (CurrentObject.Collision.CollisionData.Vertices.Length == 0)
            return;

        var first = CurrentObject.Collision.CollisionData.Vertices[0];
        m_maxVertex = first;
        m_minVertex = first;


        var vertices = CurrentObject.Collision.CollisionData.Vertices;

        for (int j = 0; j < vertices.Length; j++)
        {
            var current = vertices[j];

            if (m_maxVertex.X < current.X) m_maxVertex.X = current.X;
            if (m_maxVertex.Y < current.Y) m_maxVertex.Y = current.Y;
            if (m_maxVertex.Z < current.Z) m_maxVertex.Z = current.Z;

            if (m_minVertex.X > current.X) m_minVertex.X = current.X;
            if (m_minVertex.Y > current.Y) m_minVertex.Y = current.Y;
            if (m_minVertex.Z > current.Z) m_minVertex.Z = current.Z;
        }
    }

    //TODO: implement collision check
    /// <inheritdoc/>
    public override bool CheckCollision(IPhysicObject t2, out Vector3 side)
    {
        side = Vector3.Zero;

        if (t2 == null)
            return false;

        if (t2.Collision.CollisionType != CollisionType.Cube)
            return false;

        var collisionCheck = CurrentObject.Direction == Vector3.Zero ? AABBCollisionCheck(t2) : OBBCollisionCheck(t2);

        if (!collisionCheck)
            return collisionCheck;

        return collisionCheck;
    }

    private bool AABBCollisionCheck(IPhysicObject t2)
    {
        var outerVertices = t2.Collision.CollisionData.Vertices;

        for (int oJ = 0; oJ < outerVertices.Length; oJ++)
        {
            if (VectorExtensions.GreaterThan(m_maxVertex, outerVertices[oJ]) &&
                VectorExtensions.LoverThan(m_minVertex, outerVertices[oJ]))
                return true;
        }

        return false;
    }

    private bool OBBCollisionCheck(IPhysicObject t2)
    {
        for (int i = 0; i < CurrentObject.Collision.CollisionData.Meshes.Count; i++)
        {
            var mesh = CurrentObject.Collision.CollisionData.Meshes[i];
            float currMin, currMax, t2Min, t2Max;

            SATTest(mesh.Normal, CurrentObject.Collision.CollisionData.Vertices, out currMin, out currMax);
            SATTest(mesh.Normal, t2.Collision.CollisionData.Vertices, out t2Min, out t2Max);

            if (!Overlaps(currMin, currMax, t2Min, t2Max))
                return false;
        }

        return true;
    }

    private void SATTest(in Vector3 normal, Vector3[] vertices, 
        out float min, out float max)
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

    private bool Overlaps(float min1, float max1, float min2, float max2)
    {
        return IsBetweenOrdered(min2, min1, max1) ||
               IsBetweenOrdered(min1, min2, max2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBetweenOrdered(float val, float lowerBound, float upperBound)
    {
        return lowerBound <= val && val <= upperBound;
    }
}