using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IMovable
{
    Vector3 Position { get; set; }
    Vector3 Rotation { get; set; }
    
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
}