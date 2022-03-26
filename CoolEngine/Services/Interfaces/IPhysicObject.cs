using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;

namespace CoolEngine.Services.Interfaces;

public interface IPhysicObject : ITransformable
{
    public RigidBody RigidBody { get; set; }
}