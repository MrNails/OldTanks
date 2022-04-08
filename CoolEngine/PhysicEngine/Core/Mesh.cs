using CoolEngine.Services;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class Mesh
{
    private uint[] m_indices;

    public Mesh() : this(Array.Empty<uint>()) { }
    
    public Mesh(uint[] indices)
    {
        Indices = indices;
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
    public Vector4 Color { get; set; } = Colors.Orange;

    public Mesh Copy()
    {
        return new Mesh
        {
            Indices = m_indices,
            Normal = Normal
        };
    }
}