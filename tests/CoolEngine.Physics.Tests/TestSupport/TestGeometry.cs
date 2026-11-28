using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// One body as the detection sees it, built from a shape and any transformation (shear and mirror included) with the
/// stage-1 formulas of <see cref="Geometry"/>, so the detection tests do not depend on how a world stores a body.
/// </summary>
internal sealed class TestGeometry
{
    private readonly Vector3[] m_vertices;
    private readonly Vector3[] m_normals;
    private Vector3 m_boundsMin;
    private Vector3 m_boundsMax;

    private TestGeometry(Shape shape, Vector3[] vertices, Vector3[] normals)
    {
        Shape = shape;
        m_vertices = vertices;
        m_normals = normals;
    }

    public Shape Shape { get; }

    public Matrix4 Transformation { get; private set; }

    public Vector3 Position { get; private set; }

    public Vector3 Size { get; private set; }

    public Vector3[] Vertices => m_vertices;

    public Vector3[] Normals => m_normals;

    public Vector3 BoundsMin => m_boundsMin;

    public Vector3 BoundsMax => m_boundsMax;

    /// <summary>
    /// A body of <paramref name="shape"/> under <paramref name="transformation"/>; <paramref name="position"/> is the
    /// body's position (the sphere centre and the polygon normal's orientation) and <paramref name="size"/>'s X the
    /// sphere's width, as today's <c>IPhysicObject.Position</c> and <c>Width</c>.
    /// </summary>
    public static TestGeometry Create(Shape shape, Matrix4 transformation, Vector3 position, Vector3 size)
    {
        var polygon = shape as PolygonShape;
        var body = new TestGeometry(shape,
            new Vector3[polygon?.Vertices.Length ?? 0],
            new Vector3[polygon?.FaceCount ?? 0]);

        body.Update(transformation, position, size);

        return body;
    }

    public void Update(Matrix4 transformation, Vector3 position, Vector3 size)
    {
        Transformation = transformation;
        Position = position;
        Size = size;

        var polygon = Shape as PolygonShape;
        if (polygon != null)
        {
            Geometry.ComputeVertices(polygon, transformation, m_vertices);
            Geometry.ComputeNormals(polygon, transformation, m_normals);
        }

        Geometry.ComputeBounds(polygon, m_vertices, position, size.X, out m_boundsMin, out m_boundsMax);
    }

    public BodyGeometry View() =>
        new BodyGeometry(Shape as PolygonShape, m_vertices, m_normals, Position, Size.X, m_boundsMin, m_boundsMax);
}
