namespace CoolEngine.PhysicEngine.Core;

public class WorldSettings
{
    public static readonly WorldSettings Earth = new WorldSettings(12756.28f, 5.97e+24f);

    public WorldSettings(float planetRadius, float planetMass)
    {
        PlanetRadius = planetRadius;
        PlanetMass = planetMass;
    }
    
    public float PlanetRadius { get; set; }
    public float PlanetMass { get; set; }
}