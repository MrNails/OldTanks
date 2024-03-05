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
    private readonly List<Face> m_faces;
    private Vector3[] m_vertices;

    public CollisionData(CollisionType collisionType)
    {
        Vertices = Array.Empty<Vector3>();
        m_faces = new List<Face>();

        CollisionType = collisionType;
    }

    public List<Face> Faces => m_faces;
    
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