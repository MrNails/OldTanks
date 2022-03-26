using CoolEngine.Core;
using CoolEngine.PhysicEngine.Core;

namespace CoolEngine.Services.Interfaces;

public interface ICollisionable
{
    public Collision Collision { get; set; }
}