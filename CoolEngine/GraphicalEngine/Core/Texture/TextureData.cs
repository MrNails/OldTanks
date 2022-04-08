using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public class TextureData : ICloneable
{
    public TextureData()
    {
        Position = new Vector3();
        Scale = new Vector3(1);
        Rotation = new Vector3();
    }
    
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public Vector3 Rotation { get; set; }
    
    public Texture? Texture { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}