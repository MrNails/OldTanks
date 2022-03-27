using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;

namespace CoolEngine.Services.Interfaces;

public interface IPhysicObject : ITransformable, ICollisionable
{
    public RigidBody RigidBody { get; set; }
}