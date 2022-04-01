using System.Collections.ObjectModel;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public enum CollisionType
{
    Cube,
    Sphere
}

public abstract class Collision
{
    private readonly List<CollisionMesh> m_originalCollision;
    private readonly List<CollisionMesh> m_meshes;
    private readonly ReadOnlyCollection<CollisionMesh> m_readOnlyMeshes;

    private IPhysicObject m_currentObj;

    public Collision(IPhysicObject physicObject, List<CollisionMesh> originalCollision)
    {
        m_meshes = new List<CollisionMesh>();
        m_readOnlyMeshes = new ReadOnlyCollection<CollisionMesh>(m_meshes);
        m_currentObj = physicObject;

        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));

        m_originalCollision = originalCollision;

        InitCollision(originalCollision);
    }

    public ReadOnlyCollection<CollisionMesh> Meshes => m_readOnlyMeshes;

    public IPhysicObject CurrentObject => m_currentObj;

    public CollisionType CollisionType { get; protected set; }
    
    public bool IsActive { get; set; }

    public virtual void UpdateCollision()
    {
        var transformation = m_currentObj.Transform;

        for (int i = 0, m = 0; i < m_originalCollision.Count && m < m_meshes.Count; i++)
        {
            var originalVertices = m_originalCollision[i].Vertices;
            var currentMesh = m_meshes[m++];

            for (int j = 0; j < originalVertices.Length; j++)
                currentMesh.Vertices[j] =
                    new Vector3(new Vector4(originalVertices[j], 1) * transformation);
        }
    }

    private void InitCollision(List<CollisionMesh> originalCollision)
    {
        foreach (var mesh in originalCollision)
        {
            if (mesh.Vertices.Length == 0)
            {
                Console.WriteLine(
                    $"Cannot copy mesh for object {m_currentObj.GetType().FullName}. It's not fit both textured mesh and common mesh type.");
                continue;
            }

            var newVertices = new Vector3[mesh.Vertices.Length];

            for (int i = 0; i < mesh.Vertices.Length; i++)
                newVertices[i] = mesh.Vertices[i];

            m_meshes.Add(new CollisionMesh(newVertices, mesh.Indices) { Normal = mesh.Normal });
        }
    }

    public abstract bool CheckCollision(IPhysicObject t2);
}