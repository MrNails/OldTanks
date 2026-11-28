using System.Numerics;

namespace CoolEngine.Physics;

/// <summary>A ray from <see cref="Start"/> towards <see cref="End"/>, as CoolEngine.Models.Ray.</summary>
public readonly struct Ray
{
    public Ray(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }

    public Vector3 Start { get; }

    public Vector3 End { get; }

    /// <summary><c>Start − End</c>, as today: the sign is part of the ray test's arithmetic.</summary>
    internal Vector3 Delta => Start - End;
}
