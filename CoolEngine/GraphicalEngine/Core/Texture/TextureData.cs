using Common.Models;
using CoolEngine.Services;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public sealed class TextureData : ObservableObject, ICloneable
{
    private Vector2 m_position;
    private Vector2 m_scale;
    private float m_rotationAngle;
    
    private Texture m_texture;

    public TextureData()
    {
        Position = new Vector2();
        Scale = new Vector2(1);
        RotationAngle = 0;
        
        Texture = Texture.Empty;
    }

    public Vector2 Position
    {
        get => m_position;
        set => SetField(ref m_position, value);
    }

    public Vector2 Scale
    {
        get => m_scale;
        set => SetField(ref m_scale, value);
    }

    public float RotationAngle
    {
        get => m_rotationAngle;
        set => SetField(ref m_rotationAngle, value);
    }

    public Texture Texture
    {
        get => m_texture;
        set => SetField(ref m_texture, value);
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}