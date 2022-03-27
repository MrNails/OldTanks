using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class Mesh
{
    private Vector3[] m_vertices;
    private uint[] m_indices;

    public Mesh() : this( Array.Empty<Vector3>(), Array.Empty<uint>()) { }
    
    public Mesh( Vector3[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

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

    public uint[] Indices
    {
        get => m_indices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            m_indices = value;
        }
    }

    public Vector3 Normal { get; set; }

    public Mesh Copy()
    {
        return new Mesh
        {
            Vertices = m_vertices,
            Indices = m_indices,
            Normal = Normal
        };
    }
}