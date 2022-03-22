using GraphicalEngine.Core;

namespace GraphicalEngine.Services.Interfaces;

public interface IDrawable : ITransformable
{
    Scene Scene { get; }
}