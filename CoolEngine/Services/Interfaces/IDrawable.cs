using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;

namespace CoolEngine.Services.Interfaces;

public interface IDrawable : ITransformable
{
    Scene Scene { get; }
}