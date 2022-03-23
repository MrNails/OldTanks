using OpenTK.Mathematics;

namespace GraphicalEngine.Services.Interfaces;

public interface ITransformable : IMovable
{
    bool HaveChanged { get; }
    
    Vector3 Size { get; set; }

    float Width { get; set; }
    float Height { get; set; }
    float Length { get; set; }

    Matrix4 Transform { get; }

    void AcceptTransform();
}