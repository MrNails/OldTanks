using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IMovable
{
    Vector3 Position { get; }
    Vector3 Direction { get; }
    
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
    
    float Pitch { get; set; }
    float Yaw { get; set; }
    float Roll { get; set; }

    void Move(float timeDelta, int collisionIteration = -1);
}