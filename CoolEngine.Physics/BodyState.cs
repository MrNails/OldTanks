using System.Numerics;
using CoolEngine.Mathematics;

namespace CoolEngine.Physics;

/// <summary>
/// One body's whole state, for a snapshot. Stage 1 (transitional): <see cref="Linear"/>, <see cref="Size"/> and
/// <see cref="VelocityFrame"/> as in <see cref="BodyDescription"/>; velocity and force in the velocity frame.
/// </summary>
public readonly record struct BodyState(
    BodyHandle Handle,
    Vector3 Position,
    Matrix3 Linear,
    Vector3 Size,
    Matrix3 VelocityFrame,
    Vector3 Velocity,
    Vector3 Force,
    float Mass,
    bool IsStatic,
    bool OnGround);
