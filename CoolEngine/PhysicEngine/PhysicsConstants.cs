using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine;

public static class PhysicsConstants
{
    public static readonly float FreeFallingAcceleration = 9.80665f;
    public static readonly float MaxFreeFallingSpeed = FreeFallingAcceleration * 9;
    
    /// <summary>
    /// Gravity constant
    /// </summary>
    public static readonly float G = (float)6.67e-11;

    public static Vector3 GravityDirection = new Vector3(0, -1, 0);

    public static Vector3 MoveDirection = new Vector3(1, 0, 0);
}