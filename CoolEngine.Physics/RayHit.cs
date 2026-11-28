using System.Numerics;

namespace CoolEngine.Physics;

/// <summary>
/// The nearest hit of <see cref="PhysicsWorld.RayCast"/>: the body, the point, and
/// <c>MathF.Abs((Point − Start).Length())</c>, the value today's nearest-hit loop compares.
/// </summary>
public readonly record struct RayHit(BodyHandle Body, Vector3 Point, float Distance);
