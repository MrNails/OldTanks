using CoolEngine.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface ITransformable
{
    bool NeedTransformationApply { get; }

    Matrix4 Transformation { get; }

    void ApplyTransformation();
}