using System.Numerics;

namespace CoolEngine.Physics.Shapes;

/// <summary>
/// One face of a <see cref="PolygonShape"/>, in model space (the cube spans −1…1): vertex indices and the outward
/// normal. An input record: <see cref="PolygonShape"/> copies it. Indices 0, 1 and 2 must be a corner and its two
/// neighbours (the ray test projects onto the edges from index 0 to 1 and to 2).
/// </summary>
public readonly struct ShapeFace
{
    private readonly uint[]? m_indices;

    public ShapeFace(uint[] indices, Vector3 normal)
    {
        m_indices = indices;
        Normal = normal;
    }

    /// <summary>The caller's array, not a copy; empty for <c>default</c> or a null array.</summary>
    public ReadOnlySpan<uint> Indices => m_indices;

    public Vector3 Normal { get; }
}
