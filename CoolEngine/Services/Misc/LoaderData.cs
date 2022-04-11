using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;

namespace CoolEngine.Services.Misc;

public readonly struct LoaderData
{
    public LoaderData(Scene scene, CollisionData? collisionData)
    {
        Scene = scene;
        CollisionData = collisionData;
    }

    public Scene Scene { get; }
    public CollisionData? CollisionData { get; }
}