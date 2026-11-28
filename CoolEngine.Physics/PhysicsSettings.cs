using System.Numerics;

namespace CoolEngine.Physics;

/// <summary>
/// The world's constants, immutable and safe on any thread. The game passes today's numbers:
/// <c>(new Vector3(0, -1, 0), 9.80665f, 9.80665f * 9, 5)</c>, the terminal speed being that float product
/// (PhysicsConstants.MaxFreeFallingSpeed), not a decimal literal.
/// </summary>
public sealed class PhysicsSettings
{
    private const float UnitLengthTolerance = 1e-6f;
    private const int MaxSubStepCount = 128;

    /// <exception cref="ArgumentOutOfRangeException">
    /// A component of <paramref name="gravityDirection"/> is not finite or its squared length differs from 1 by more
    /// than 1e-6; <paramref name="gravityAcceleration"/> or <paramref name="terminalSpeed"/> is not finite or not
    /// positive; <paramref name="subStepCount"/> is outside 1-128.
    /// </exception>
    public PhysicsSettings(Vector3 gravityDirection, float gravityAcceleration, float terminalSpeed, int subStepCount)
    {
        if (!float.IsFinite(gravityDirection.X) || !float.IsFinite(gravityDirection.Y) ||
            !float.IsFinite(gravityDirection.Z) ||
            MathF.Abs(gravityDirection.LengthSquared() - 1f) > UnitLengthTolerance)
            throw new ArgumentOutOfRangeException(nameof(gravityDirection), gravityDirection,
                "The gravity direction must be a finite unit vector.");

        if (!float.IsFinite(gravityAcceleration) || gravityAcceleration <= 0)
            throw new ArgumentOutOfRangeException(nameof(gravityAcceleration), gravityAcceleration,
                "The gravity acceleration must be finite and positive.");

        if (!float.IsFinite(terminalSpeed) || terminalSpeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(terminalSpeed), terminalSpeed,
                "The terminal speed must be finite and positive.");

        if (subStepCount < 1 || subStepCount > MaxSubStepCount)
            throw new ArgumentOutOfRangeException(nameof(subStepCount), subStepCount,
                "The sub-step count must be in 1-128.");

        GravityDirection = gravityDirection;
        GravityAcceleration = gravityAcceleration;
        TerminalSpeed = terminalSpeed;
        SubStepCount = subStepCount;
    }

    /// <summary>The unit direction gravity pulls along; also the axis of the terminal speed and of the ground flag.</summary>
    public Vector3 GravityDirection { get; }

    public float GravityAcceleration { get; }

    /// <summary>The largest speed along the gravity axis; a faster body is set to falling at it (today's rule).</summary>
    public float TerminalSpeed { get; }

    /// <summary><see cref="PhysicsWorld.Step"/> splits its time step into this many equal sub-steps.</summary>
    public int SubStepCount { get; }
}
