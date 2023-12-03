using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public enum CollisionType
{
    Polygon,
    Sphere
}

public class CollisionData
{
    private readonly List<Mesh> m_meshes;
    private Vector3[] m_vertices;

    public CollisionData(CollisionType collisionType)
    {
        Vertices = Array.Empty<Vector3>();
        m_meshes = new List<Mesh>();

        CollisionType = collisionType;
    }

    public List<Mesh> Meshes => m_meshes;
    
    public Vector3[] Vertices
    {
        get => m_vertices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            m_vertices = value;
        }
    }
    
    public IPhysicObject? PhysicObject { get; set; }
    
    public CollisionType CollisionType { get; }
}