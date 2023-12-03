using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Primitives;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex : IEquatable<Vertex>
{
    private readonly Vector3 m_position;
    private readonly Vector3 m_normal;
    private readonly Vector2 m_texturePosition;
    
    public Vertex(Vector3 position, Vector3 normal, Vector2 texturePosition)
    {
        m_position = position;
        m_texturePosition = texturePosition;
        m_normal = normal;
    }

    public Vector3 Position => m_position;
    public Vector3 Normal => m_normal;
    public Vector2 TexturePosition => m_texturePosition;

    public float X => m_position.X;
    public float Y => m_position.Y;
    public float Z => m_position.Z;

    public float U => m_texturePosition.X;
    public float V => m_texturePosition.Y;

    public float XNormal => m_normal.X;
    public float YNormal => m_normal.Y;
    public float ZNormal => m_normal.Z;

    public override string ToString()
    {
        return $"Position: {m_position.ToString()}; Normal: {m_normal.ToString()}; TexturePosition: {TexturePosition.ToString()}";
    }

    public bool Equals(Vertex other)
    {
        return m_position == other.m_position && 
               m_normal == other.m_normal && 
               m_texturePosition == other.m_texturePosition;
    }

    public override bool Equals(object? obj)
    {
        return obj is Vertex other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(m_position, m_normal, m_texturePosition);
    }
    
    public static unsafe int SizeInBytes => sizeof(Vertex);

    public static bool operator ==(Vertex left, Vertex right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vertex left, Vertex right)
    {
        return !left.Equals(right);
    }
}