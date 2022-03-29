using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core;

namespace CoolEngine.Services.Interfaces;

public interface IPhysicObject : ITransformable, ICollisionable
{
    public RigidBody RigidBody { get; set; }
}