using OpenTK.Mathematics;

namespace CoolEngine.Services.Misc;

public struct TextDrawInformation
{
    public Vector3 OriginRotation { get; set; }
    public Vector3 OriginPosition { get; set; }
    
    public Vector3 SelfRotation { get; set; }
    public Vector3 SelfPosition { get; set; }

    public Vector4 Color { get; set; }
    public float Scale { get; set; } = 1;
}