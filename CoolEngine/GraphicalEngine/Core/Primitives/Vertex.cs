using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Primitives;

public struct Vertex
{
    private Vector3 m_position;
    private Vector2 m_texturePosition;
    private Vector3 m_normal;
    private float m_textureIndex;

    public Vertex(float x, float y, float z, float u, float v, Vector3 normal, float textureIndex) 
        : this(new Vector3(x, y, z), new Vector2(u, v), normal, textureIndex)
    { }
    
    public Vertex(Vector3 position, Vector2 texturePosition, Vector3 normal, float textureIndex)
    {
        m_position = position;
        m_texturePosition = texturePosition;
        m_normal = normal;
        m_textureIndex = textureIndex;
    }

    public Vector3 Position
    {
        get => m_position;
        set => m_position = value;
    }

    public Vector2 TexturePosition
    {
        get => m_texturePosition;
        set => m_texturePosition = value;
    }

    public Vector3 Normal
    {
        get => m_normal;
        set => m_normal = value;
    }

    public float TextureIndex
    {
        get => m_textureIndex;
        set => m_textureIndex = value;
    }

    public float X
    {
        get => m_position.X;
        set => m_position.X = value;
    }
    
    public float Y
    {
        get => m_position.Y;
        set => m_position.Y = value;
    }
    
    public float Z
    {
        get => m_position.Z;
        set => m_position.Z = value;
    }
    
    public float U
    {
        get => m_texturePosition.X;
        set => m_texturePosition.X = value;
    }
    
    public float V
    {
        get => m_texturePosition.Y;
        set => m_texturePosition.Y = value;
    }
}