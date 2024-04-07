using CoolEngine.Models;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public enum CollisionType
{
    Polygon,
    Sphere
}

public sealed class CollisionData
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
    
    public BoundingBox BoundingBox { get; private set; }
    
    public IPhysicObject? PhysicObject { get; set; }
    
    public CollisionType CollisionType { get; }

    public void UpdateBoundingBox()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (int j = 0; j < m_vertices.Length; j++)
        {
            var current = m_vertices[j];

            if (max.X < current.X) max.X = current.X;
            if (max.Y < current.Y) max.Y = current.Y;
            if (max.Z < current.Z) max.Z = current.Z;

            if (min.X > current.X) min.X = current.X;
            if (min.Y > current.Y) min.Y = current.Y;
            if (min.Z > current.Z) min.Z = current.Z;
        }

        BoundingBox = new BoundingBox(min, max);
    }
}