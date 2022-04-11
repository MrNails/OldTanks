using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Loaders;
using OpenTK.Mathematics;

using CollMesh = CoolEngine.PhysicEngine.Core.Mesh;

namespace OldTanks.Models;

public class Sphere : WorldObject
{
    public Sphere() : base(GlobalCache<Scene>.GetItemOrDefault("Sphere"))
    {
    }
}