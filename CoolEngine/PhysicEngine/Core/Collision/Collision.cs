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
    private readonly CollisionData m_originalCollision;
    private readonly CollisionData m_currentCollision;

    private IPhysicObject m_currentObj;

    public Collision(IPhysicObject physicObject, CollisionData originalCollision)
    {
        m_currentCollision = new CollisionData();
        m_currentObj = physicObject;

        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));

        m_originalCollision = originalCollision;

        InitCollision(originalCollision);
    }

    public CollisionData CollisionData => m_currentCollision;

    public IPhysicObject CurrentObject => m_currentObj;

    public CollisionType CollisionType { get; protected set; }

    public bool IsActive { get; set; }

    public virtual void UpdateCollision()
    {
        var transformation = m_currentObj.Transform;

        for (int i = 0; i < m_currentCollision.Vertices.Length; i++)
            m_currentCollision.Vertices[i] =
                new Vector3(new Vector4(m_originalCollision.Vertices[i], 1) * transformation);
    }

    private void InitCollision(CollisionData originalCollision)
    {
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
    public abstract bool CheckCollision(IPhysicObject t2, out Vector3 normal);
}