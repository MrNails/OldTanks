using CoolEngine.Services;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class Face
{
    private uint[] m_indices;

    public Face() : this(Array.Empty<uint>()) { }
    
    public Face(uint[] indices)
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

    public Face Copy()
    {
        return new Face
        {
            Indices = m_indices,
            Normal = Normal
        };
    }
}