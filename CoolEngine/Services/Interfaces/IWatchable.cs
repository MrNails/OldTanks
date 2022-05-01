using CoolEngine.GraphicalEngine.Core;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IWatchable
{
    Vector3 CameraOffset { get; set; }
    Vector2 CameraOffsetAngle { get; set; }
    
    Camera? Camera { get; set; }
}