using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine;

public static class PhysicsConstants
{
    public static readonly float g = 9.80665f;
    public static readonly float MaxFreeFallingSpeed = g * 9;
    
    /// <summary>
    /// Gravity constant
    /// </summary>
    public static readonly float G = (float)6.67e-11;

    public static readonly Vector3 GravityDirection = new Vector3(0, -1, 0);
}