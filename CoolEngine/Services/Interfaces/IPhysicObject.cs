using CoolEngine.PhysicEngine.Core;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Interfaces;

public interface IPhysicObject : ITransformable, IMovable, ICollisionable
{
    Vector3 Size { get; set; }

    float Width { get; set; }
    float Height { get; set; }
    float Length { get; set; }

    
    public RigidBody RigidBody { get; }
}