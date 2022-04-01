using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class CubeCollision : Collision
{
    private Vector3 m_minVertex;
    private Vector3 m_maxVertex;
    
    public CubeCollision(IPhysicObject physicObject, List<Mesh> originalCollision) : base(physicObject, originalCollision)
    {
        CollisionType = CollisionType.Cube;
    }

    public override void UpdateCollision()
    {
        base.UpdateCollision();

        if (CurrentObject.Collision.Meshes.Count == 0)
            return;
        
        var first = CurrentObject.Collision.Meshes[0].Vertices[0];
        m_maxVertex = first;
        m_minVertex = first;

        for (int i = 0; i < CurrentObject.Collision.Meshes.Count; i++)
        {
            var vertices = CurrentObject.Collision.Meshes[i].Vertices;

            for (int j = 0; j < vertices.Length; j++)
            {
                var current = vertices[j];
                
                if (m_maxVertex.X < current.X)
                    m_maxVertex.X = current.X;

                if (m_maxVertex.Y < current.Y)
                    m_maxVertex.Y = current.Y;
                
                if (m_maxVertex.Z < current.Z)
                    m_maxVertex.Z = current.Z;
                
                if (m_minVertex.X > current.X)
                    m_minVertex.X = current.X;

                if (m_minVertex.Y > current.Y)
                    m_minVertex.Y = current.Y;
                
                if (m_minVertex.Z > current.Z)
                    m_minVertex.Z = current.Z;
            }
        }
    }

    //TODO: implement collision check
    public override bool CheckCollision(IPhysicObject t2)
    {
        if (t2 == null)
            return false;

        if (t2.Collision.CollisionType != CollisionType.Cube)
            return false;

        for (int oI = 0; oI < t2.Collision.Meshes.Count; oI++)
        {
            var outerVertices = t2.Collision.Meshes[oI].Vertices;

            for (int oJ = 0; oJ < outerVertices.Length; oJ++)
            {
                if (VectorExtensions.GreaterThan(m_maxVertex, outerVertices[oJ]) &&
                    VectorExtensions.LoverThan(m_minVertex, outerVertices[oJ]))
                    return true;
            }
        }

        return false;
    }
}