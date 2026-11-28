using System.Numerics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Detection;

/// <summary>
/// What one body shows the detection: for a polygon its shape and its world vertices and face normals (empty for a
/// sphere); the body's <see cref="Position"/> (the sphere centre and the polygon normal's orientation, as today's
/// <c>PhysicObject.Position</c>, not the matrix translation, which differs in −0); <see cref="SphereWidth"/> (stage 1
/// <c>Size.X</c>, today's <c>Width</c>); and the axis-aligned bounds.
/// </summary>
internal readonly ref struct BodyGeometry
{
    public BodyGeometry(PolygonShape? polygon, ReadOnlySpan<Vector3> vertices, ReadOnlySpan<Vector3> normals,
        Vector3 position, float sphereWidth, Vector3 boundsMin, Vector3 boundsMax)
    {
        Polygon = polygon;
        Vertices = vertices;
        Normals = normals;
        Position = position;
        SphereWidth = sphereWidth;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
    }

    /// <summary>The polygon shape, or null for a sphere.</summary>
    public PolygonShape? Polygon { get; }

    public ReadOnlySpan<Vector3> Vertices { get; }

    public ReadOnlySpan<Vector3> Normals { get; }

    public Vector3 Position { get; }

    public float SphereWidth { get; }

    public Vector3 BoundsMin { get; }

    public Vector3 BoundsMax { get; }
}
