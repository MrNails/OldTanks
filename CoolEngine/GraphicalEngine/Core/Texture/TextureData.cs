using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public class TextureData : ICloneable
{
    public TextureData()
    {
        Position = new Vector2();
        Scale = new Vector2(1);
        RotationAngle = 0;
    }
    
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public float RotationAngle { get; set; }
    
    public Texture? Texture { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}