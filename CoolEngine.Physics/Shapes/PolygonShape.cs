using System.Numerics;
using CoolEngine.Physics.Detection;

namespace CoolEngine.Physics.Shapes;

/// <summary>
/// A convex polygon shape in model space: vertices and faces whose index lists share vertex indices (two faces that
/// share exactly two indices and are not coplanar share an edge). Immutable; any thread. The constructor copies its
/// inputs and finds the edges, one edge per parallel direction and the cuboid axis faces once, with today's
/// <c>Collision.FindEdges</c>, <c>FindEdgeDirections</c> and <c>FindCuboidAxisFaces</c> (same loops, order and
/// tolerances, so tie-breaks are unchanged). Only the constructor allocates.
/// </summary>
public sealed class PolygonShape : Shape
{
    private readonly Vector3[] m_vertices;
    private readonly uint[] m_indices;
    // Face i's indices are m_indices[m_faceStarts[i] .. m_faceStarts[i + 1]).
    private readonly int[] m_faceStarts;
    private readonly Vector3[] m_normals;
    private readonly (uint A, uint B)[] m_edges;
    private readonly int[] m_edgeDirections;
    private readonly int[] m_cuboidAxisFaces;

    /// <exception cref="ArgumentException">
    /// <paramref name="vertices"/> is empty or has a non-finite component (<c>ParamName "vertices"</c>); a face has
    /// fewer than 3 indices, an index not below the vertex count or a non-finite normal component
    /// (<c>ParamName "faces"</c>).
    /// </exception>
    public PolygonShape(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<ShapeFace> faces)
    {
        if (vertices.Length == 0)
            throw new ArgumentException("A polygon shape needs at least one vertex.", nameof(vertices));

        for (var i = 0; i < vertices.Length; i++)
        {
            if (!IsFinite(vertices[i]))
                throw new ArgumentException($"Vertex {i} has a component that is not finite.", nameof(vertices));
        }

        var indexCount = 0;
        for (var i = 0; i < faces.Length; i++)
        {
            var indices = faces[i].Indices;

            if (indices.Length < 3)
                throw new ArgumentException($"Face {i} has fewer than 3 indices.", nameof(faces));

            foreach (var index in indices)
            {
                if (index >= (uint)vertices.Length)
                    throw new ArgumentException($"Face {i} has the index {index}, not below the vertex count.",
                        nameof(faces));
            }

            if (!IsFinite(faces[i].Normal))
                throw new ArgumentException($"The normal of face {i} has a component that is not finite.", nameof(faces));

            indexCount += indices.Length;
        }

        m_vertices = vertices.ToArray();
        m_indices = new uint[indexCount];
        m_faceStarts = new int[faces.Length + 1];
        m_normals = new Vector3[faces.Length];

        var start = 0;
        for (var i = 0; i < faces.Length; i++)
        {
            m_faceStarts[i] = start;
            faces[i].Indices.CopyTo(m_indices.AsSpan(start));
            start += faces[i].Indices.Length;
            m_normals[i] = faces[i].Normal;
        }

        m_faceStarts[faces.Length] = start;

        m_edges = FindEdges();
        m_edgeDirections = FindEdgeDirections();
        m_cuboidAxisFaces = FindCuboidAxisFaces() ?? [];
    }

    public ReadOnlySpan<Vector3> Vertices => m_vertices;

    public int FaceCount => m_normals.Length;

    /// <summary>8 corners of an axis-aligned box with nonzero extent and 6 faces whose normals are the six signed axes.</summary>
    public bool IsCuboid => m_cuboidAxisFaces.Length != 0;

    /// <summary>Every edge as vertex index pairs, A &lt; B, in today's order.</summary>
    internal ReadOnlySpan<(uint A, uint B)> Edges => m_edges;

    /// <summary>Indices into <see cref="Edges"/>, one edge per class of parallel edges.</summary>
    internal ReadOnlySpan<int> EdgeDirections => m_edgeDirections;

    /// <summary>The faces whose normals are +X, +Y and +Z, in that order; empty when the shape is not a cuboid.</summary>
    internal ReadOnlySpan<int> CuboidAxisFaces => m_cuboidAxisFaces;

    /// <summary>The model-space face normals, in face order.</summary>
    internal ReadOnlySpan<Vector3> Normals => m_normals;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="face"/> is not below <see cref="FaceCount"/>.</exception>
    public ReadOnlySpan<uint> GetFaceIndices(int face)
    {
        if ((uint)face >= (uint)m_normals.Length)
            throw new ArgumentOutOfRangeException(nameof(face), face, "No such face.");

        return FaceIndices(face);
    }

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="face"/> is not below <see cref="FaceCount"/>.</exception>
    public Vector3 GetFaceNormal(int face)
    {
        if ((uint)face >= (uint)m_normals.Length)
            throw new ArgumentOutOfRangeException(nameof(face), face, "No such face.");

        return m_normals[face];
    }

    /// <summary>Face <paramref name="face"/>'s indices, unchecked.</summary>
    internal ReadOnlySpan<uint> FaceIndices(int face) =>
        m_indices.AsSpan(m_faceStarts[face], m_faceStarts[face + 1] - m_faceStarts[face]);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    // An edge is where two faces meet: they share exactly two vertices and are not coplanar (two triangles of one
    // split quad share their diagonal, which is not an edge of the shape). Today's Collision.FindEdges.
    private (uint A, uint B)[] FindEdges()
    {
        var edges = new List<(uint A, uint B)>();
        var faceCount = FaceCount;

        for (int i = 0; i < faceCount; i++)
        {
            for (int j = i + 1; j < faceCount; j++)
            {
                var sharedCount = 0;
                uint first = 0;
                uint second = 0;
                var facesJ = FaceIndices(j);

                foreach (var index in FaceIndices(i))
                {
                    if (facesJ.IndexOf(index) < 0)
                        continue;

                    if (sharedCount == 0)
                        first = index;
                    else if (sharedCount == 1)
                        second = index;

                    sharedCount++;
                }

                if (sharedCount != 2 ||
                    Vector3.Cross(m_normals[i], m_normals[j]).LengthSquared() < NarrowPhase.ParallelEpsilon)
                    continue;

                var edge = (Math.Min(first, second), Math.Max(first, second));
                if (!edges.Contains(edge))
                    edges.Add(edge);
            }
        }

        return edges.ToArray();
    }

    // Parallel edges give the same edge x edge axes, so one edge per parallel class is enough. Parallelism survives
    // any affine transform, so classes found in model space hold for the transformed shape. Today's FindEdgeDirections.
    private int[] FindEdgeDirections()
    {
        var kept = new List<int>();
        var edges = m_edges;

        for (int k = 0; k < edges.Length; k++)
        {
            var direction = Vector3.Normalize(m_vertices[edges[k].B] - m_vertices[edges[k].A]);
            var isParallelToKept = false;

            foreach (var keptIndex in kept)
            {
                var keptEdge = edges[keptIndex];
                var keptDirection = Vector3.Normalize(m_vertices[keptEdge.B] - m_vertices[keptEdge.A]);

                if (Vector3.Cross(direction, keptDirection).LengthSquared() < NarrowPhase.ParallelEpsilon)
                {
                    isParallelToKept = true;
                    break;
                }
            }

            if (!isParallelToKept)
                kept.Add(k);
        }

        return kept.ToArray();
    }

    // A cuboid in model space: 8 corners of an axis-aligned box and its 6 faces, one per signed axis. Today's
    // FindCuboidAxisFaces; null when the shape is not a cuboid.
    private int[]? FindCuboidAxisFaces()
    {
        if (m_vertices.Length != 8 || FaceCount != 6)
            return null;

        // The face for each signed axis, in the order +X, -X, +Y, -Y, +Z, -Z.
        Span<int> signedAxisFaces = [-1, -1, -1, -1, -1, -1];

        for (int i = 0; i < FaceCount; i++)
        {
            var faceNormal = m_normals[i];
            var slot = -1;

            for (int s = 0; s < signedAxisFaces.Length; s++)
            {
                var signedAxis = Vector3.Zero;
                signedAxis[s / 2] = s % 2 == 0 ? 1 : -1;

                if (Math.Abs(faceNormal.X - signedAxis.X) <= NarrowPhase.CuboidTolerance &&
                    Math.Abs(faceNormal.Y - signedAxis.Y) <= NarrowPhase.CuboidTolerance &&
                    Math.Abs(faceNormal.Z - signedAxis.Z) <= NarrowPhase.CuboidTolerance)
                {
                    slot = s;
                    break;
                }
            }

            if (slot < 0 || signedAxisFaces[slot] >= 0)
                return null;

            signedAxisFaces[slot] = i;
        }

        var vertices = m_vertices;
        var min = vertices[0];
        var max = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        for (int axis = 0; axis < 3; axis++)
        {
            if (max[axis] - min[axis] <= NarrowPhase.CuboidTolerance)
                return null;
        }

        // Each vertex is coded by which coordinates sit at the maximum; 8 different codes are the 8 corners.
        var foundCorners = 0;

        foreach (var vertex in vertices)
        {
            var corner = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                if (Math.Abs(vertex[axis] - max[axis]) <= NarrowPhase.CuboidTolerance)
                    corner |= 1 << axis;
                else if (Math.Abs(vertex[axis] - min[axis]) > NarrowPhase.CuboidTolerance)
                    return null;
            }

            if ((foundCorners & (1 << corner)) != 0)
                return null;

            foundCorners |= 1 << corner;
        }

        return [signedAxisFaces[0], signedAxisFaces[2], signedAxisFaces[4]];
    }
}
