using OpenTK.Mathematics;

namespace GraphicalEngine.Services.Interfaces;

public interface ITransformable : IMovable
{
    public Vector3 Size { get; set; }

    public float Width { get; set; }
    public float Height { get; set; }
    public float Length { get; set; }

    public Matrix4 Transform { get; }

    public void AcceptTransform();
}