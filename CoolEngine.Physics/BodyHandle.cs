using System.Globalization;

namespace CoolEngine.Physics;

/// <summary>
/// A body of a <see cref="PhysicsWorld"/>: its slot and the slot's generation when the body was added. A value type that
/// allocates nothing. <c>default(BodyHandle)</c> is never valid (generations start at 1); a handle of a removed body
/// is rejected by every member that takes one, until the slot's generation wraps after 2³¹ − 1 reuses. A handle is
/// valid only for the world that returned it; using it with another world is not detected (the handle does not name
/// its world, so it addresses whatever body that world holds in the same slot with the same generation).
/// </summary>
public readonly struct BodyHandle : IEquatable<BodyHandle>
{
    internal BodyHandle(int slot, int generation)
    {
        Slot = slot;
        Generation = generation;
    }

    public int Slot { get; }

    public int Generation { get; }

    public bool Equals(BodyHandle other) => Slot == other.Slot && Generation == other.Generation;

    public override bool Equals(object? obj) => obj is BodyHandle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Slot, Generation);

    public static bool operator ==(BodyHandle left, BodyHandle right) => left.Equals(right);

    public static bool operator !=(BodyHandle left, BodyHandle right) => !left.Equals(right);

    /// <summary><c>Body(slot 3, generation 2)</c>, in the invariant culture.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Body(slot {Slot}, generation {Generation})");
}
