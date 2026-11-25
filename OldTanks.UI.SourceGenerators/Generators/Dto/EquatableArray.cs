using System;
using System.Linq;

namespace OldTanks.UI.SourceGenerators.Generators.Dto;

/// <summary>
/// An array compared by its elements, so a generator model holding it keeps the incremental pipeline's caching working.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    private readonly T[]? m_array;

    public EquatableArray(T[] array)
    {
        m_array = array;
    }

    public T[] AsArray() => m_array ?? Array.Empty<T>();

    public bool Equals(EquatableArray<T> other) => AsArray().SequenceEqual(other.AsArray());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in AsArray())
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }
}
