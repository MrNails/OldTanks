using OpenTK.Mathematics;

namespace GraphicalEngine.Services.Interfaces;

public interface IMovable
{
    public Vector3 Position { get; }
    public Vector3 Direction { get; }
    
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Roll { get; set; }
}