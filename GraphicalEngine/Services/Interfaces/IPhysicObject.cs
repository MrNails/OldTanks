using GraphicalEngine.Core;

namespace GraphicalEngine.Services.Interfaces;

public interface IPhysicObject : ITransformable
{
    public RigidBody RigidBody { get; set; }
}