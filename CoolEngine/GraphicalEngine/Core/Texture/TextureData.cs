using Common.Models;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public sealed class TextureData : ObservableObject, ICloneable, IColorable
{
    private float m_rotationAngle;
    private Vector2 m_position;
    private Vector2 m_scale;
    private Vector4 m_color;
    
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

    public Vector4 Color
    {
        get => m_color;
        set => SetField(ref m_color, value);
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