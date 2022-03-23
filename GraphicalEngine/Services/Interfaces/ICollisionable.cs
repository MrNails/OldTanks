using GraphicalEngine.Core;

namespace GraphicalEngine.Services.Interfaces;

public interface ICollisionable
{
    public Collision Collision { get; set; }
}