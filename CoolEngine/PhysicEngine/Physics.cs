using CoolEngine.PhysicEngine.Core;

namespace CoolEngine.PhysicEngine;

public static class Physics
{
    public static float GetFreeFallingAcceleration(WorldSettings worldSettings)
    {
        return PhysicsConstants.G * worldSettings.PlanetMass / (worldSettings.PlanetRadius * worldSettings.PlanetRadius);
    }
}