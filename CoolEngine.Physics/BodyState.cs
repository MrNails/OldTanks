using System.Numerics;

namespace CoolEngine.Physics;

/// <summary>One body's whole state, for a snapshot; velocity and force in world space.</summary>
public readonly record struct BodyState(
    BodyHandle Handle,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 HalfExtents,
    Vector3 Velocity,
    Vector3 Force,
    float Mass,
    bool IsStatic,
    bool OnGround);
