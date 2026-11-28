using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Benchmarks;

/// <summary>
/// The detection and geometry of today's Collision (the baseline of each category) against the library's on the same
/// 1,024 seeded inputs, geometry precomputed for the pair tests. <c>CubeCube</c>: a third separated, a third face
/// contact, a third edge to edge. <c>SphereCube</c>: sphere centres around a cube. <c>Geometry</c>: the world vertices,
/// normals and bounds of a cube for 1,024 transforms, the matrix built on both sides.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DetectionBenchmarks
{
    private const int Count = 1024;

    private Body[] m_cubeFirsts = null!;
    private Body[] m_cubeSeconds = null!;
    private Body[] m_sphereCubes = null!;
    private Body[] m_spheres = null!;
    private Body[] m_geometryBodies = null!;

    [GlobalSetup]
    public void Setup()
    {
        var oldCube = OldCode.CubeShape();
        var oldSphere = new CollisionData(CollisionType.Sphere);
        var newCube = ToShape(oldCube);
        var newSphere = new SphereShape();
        var random = new Random(2026);
        var size = new Vector3(2);

        m_cubeFirsts = new Body[Count];
        m_cubeSeconds = new Body[Count];
        m_sphereCubes = new Body[Count];
        m_spheres = new Body[Count];
        m_geometryBodies = new Body[Count];

        for (var i = 0; i < Count; i++)
        {
            var rotationA = Rotation(random);
            m_cubeFirsts[i] = new Body(oldCube, newCube, Vector3.Zero, new Matrix3(rotationA), size);

            Vector3 positionB;
            Matrix4 rotationB;
            switch (i % 3)
            {
                case 0:
                    // Separated: farther apart than the cubes' circumscribed spheres.
                    positionB = Vector3.Normalize(new Vector3(Next(random, -1, 1), Next(random, -1, 1),
                        Next(random, -1, 1))) * Next(random, 3.5f, 4.5f);
                    rotationB = Rotation(random);
                    break;
                case 1:
                    // Face contact: B's face rests slightly inside A's top face, turned about Y.
                    m_cubeFirsts[i] = new Body(oldCube, newCube, Vector3.Zero, Matrix3.Identity, size);
                    positionB = new Vector3(Next(random, -0.5f, 0.5f), Next(random, 1.9f, 1.99f), Next(random, -0.5f, 0.5f));
                    rotationB = Matrix4.CreateRotationY(Next(random, -3.14f, 3.14f));
                    break;
                default:
                    // Edge to edge: A turned 45° about Z, B about X, a gap of −0.1 … 0.1 along Y.
                    m_cubeFirsts[i] = new Body(oldCube, newCube, Vector3.Zero,
                        new Matrix3(Matrix4.CreateRotationZ(MathHelper.PiOver4)), size);
                    positionB = new Vector3(0, 2 * MathF.Sqrt(2f) + Next(random, -0.1f, 0.1f), 0);
                    rotationB = Matrix4.CreateRotationX(MathHelper.PiOver4);
                    break;
            }

            m_cubeSeconds[i] = new Body(oldCube, newCube, positionB, new Matrix3(rotationB), size);

            m_sphereCubes[i] = new Body(oldCube, newCube, Vector3.Zero,
                new Matrix3(Rotation(random) * Matrix4.CreateScale(Next(random, 0.5f, 1.5f), Next(random, 0.5f, 1.5f),
                    Next(random, 0.5f, 1.5f))), size);
            var centre = new Vector3(Next(random, -2.2f, 2.2f), Next(random, -2.2f, 2.2f), Next(random, -2.2f, 2.2f));
            m_spheres[i] = new Body(oldSphere, newSphere, centre, new Matrix3(Matrix4.Identity), new Vector3(1));

            m_geometryBodies[i] = new Body(oldCube, newCube,
                new Vector3(Next(random, -50, 50), Next(random, -50, 50), Next(random, -50, 50)),
                new Matrix3(Rotation(random) * Matrix4.CreateScale(Next(random, 0.3f, 3), Next(random, 0.3f, 3),
                    Next(random, 0.3f, 3))), size);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CubeCube")]
    public uint CubeCube_Old()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var collides = m_cubeFirsts[i].Old.Collision!.CheckCollision(m_cubeSeconds[i].Old.Collision!,
                out var normal, out var depth);
            checksum = Checksum.Add(checksum, collides, normal, depth);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("CubeCube")]
    public uint CubeCube_New()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var collides = NarrowPhase.CheckCollision(m_cubeFirsts[i].View(), m_cubeSeconds[i].View(), out var normal,
                out var depth);
            checksum = Checksum.Add(checksum, collides, normal, depth);
        }

        return checksum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SphereCube")]
    public uint SphereCube_Old()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var collides = m_spheres[i].Old.Collision!.CheckCollision(m_sphereCubes[i].Old.Collision!,
                out var normal, out var depth);
            checksum = Checksum.Add(checksum, collides, normal, depth);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("SphereCube")]
    public uint SphereCube_New()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var collides = NarrowPhase.CheckCollision(m_spheres[i].View(), m_sphereCubes[i].View(), out var normal,
                out var depth);
            checksum = Checksum.Add(checksum, collides, normal, depth);
        }

        return checksum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Geometry")]
    public uint Geometry_Old()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var body = m_geometryBodies[i];
            body.Old.Transformation = Geometry.ComputeMatrix(body.Position, body.Linear);
            body.Old.Collision!.UpdateCollision();
            var data = body.Old.Collision.CollisionData;
            checksum = Checksum.Add(checksum, false, data.Vertices[0], data.BoundingBox.Min.X);
            checksum = Checksum.Add(checksum, false, data.Faces[0].Normal, data.BoundingBox.Max.Y);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("Geometry")]
    public uint Geometry_New()
    {
        var checksum = 0u;
        for (var i = 0; i < Count; i++)
        {
            var body = m_geometryBodies[i];
            body.Update();
            checksum = Checksum.Add(checksum, false, body.Vertices[0], body.BoundsMin.X);
            checksum = Checksum.Add(checksum, false, body.Normals[0], body.BoundsMax.Y);
        }

        return checksum;
    }

    private static PolygonShape ToShape(CollisionData data)
    {
        var faces = new ShapeFace[data.Faces.Count];
        for (var i = 0; i < faces.Length; i++)
            faces[i] = new ShapeFace(data.Faces[i].Indices, data.Faces[i].Normal);

        return new PolygonShape(data.Vertices, faces);
    }

    private static Matrix4 Rotation(Random random) =>
        Matrix4.CreateRotationZ(Next(random, -3.14f, 3.14f)) *
        (Matrix4.CreateRotationY(Next(random, -3.14f, 3.14f)) * Matrix4.CreateRotationX(Next(random, -3.14f, 3.14f)));

    private static float Next(Random random, float low, float high) => (float)(low + random.NextDouble() * (high - low));

    /// <summary>One body on both sides, with the stage-1 transform <c>CreateTranslation(position) * new Matrix4(linear)</c>.</summary>
    private sealed class Body
    {
        private readonly Shape m_shape;
        private Vector3 m_boundsMin;
        private Vector3 m_boundsMax;

        public Body(CollisionData oldShape, Shape shape, Vector3 position, Matrix3 linear, Vector3 size)
        {
            m_shape = shape;
            Position = position;
            Linear = linear;
            Size = size;
            Old = OldBody.Create(oldShape, Geometry.ComputeMatrix(position, linear), position, size);

            var polygon = shape as PolygonShape;
            Vertices = new Vector3[polygon?.Vertices.Length ?? 0];
            Normals = new Vector3[polygon?.FaceCount ?? 0];
            Update();
        }

        public OldBody Old { get; }

        public Vector3 Position { get; }

        public Matrix3 Linear { get; }

        public Vector3 Size { get; }

        public Vector3[] Vertices { get; }

        public Vector3[] Normals { get; }

        public Vector3 BoundsMin => m_boundsMin;

        public Vector3 BoundsMax => m_boundsMax;

        public void Update()
        {
            var polygon = m_shape as PolygonShape;
            if (polygon != null)
            {
                var transformation = Geometry.ComputeMatrix(Position, Linear);
                Geometry.ComputeVertices(polygon, transformation, Vertices);
                Geometry.ComputeNormals(polygon, transformation, Normals);
            }

            Geometry.ComputeBounds(polygon, Vertices, Position, Size.X, out m_boundsMin, out m_boundsMax);
        }

        public BodyGeometry View() =>
            new BodyGeometry(m_shape as PolygonShape, Vertices, Normals, Position, Size.X, m_boundsMin, m_boundsMax);
    }
}
