using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IMovable
{
    Vector3 Position { get; set; }
    Quaternion Rotation { get; set; }
    
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }

    void Move(float timeDelta, int collisionIteration = -1);
}