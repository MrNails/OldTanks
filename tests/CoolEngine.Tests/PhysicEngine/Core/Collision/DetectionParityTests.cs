namespace CoolEngine.Tests.PhysicEngine.Core.Collision;

// Inside the namespace so that "Collision" names the engine class, not this test namespace's last segment.
using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Physics;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Shapes;
using CoolEngine.Tests.TestSupport;
using OldRay = CoolEngine.Models.Ray;
using Ray = CoolEngine.Physics.Ray;

/// <summary>
/// CoolEngine.Physics' detection, ray test and geometry against today's <see cref="Collision"/> on seeded shapes and
/// transforms (transitional until the old code goes in WP5): every result equal by bits, NaN included. Each body's
/// transform is the stage-1 one, <c>CreateTranslation(position) * new Matrix4(linear)</c>, given to both sides, with
/// rotations, uneven and zero scales, shear and mirror in the linear part.
/// </summary>
[TestFixture]
public sealed class DetectionParityTests
{
    private const int Count = 20_000;

    private static readonly CollisionData[] s_oldShapes =
    [
        CollisionTestShapes.Cube(), CollisionTestShapes.TriangulatedCube(), CollisionTestShapes.Tetrahedron(),
        CollisionTestShapes.Sphere()
    ];

    private static readonly Shape[] s_newShapes =
    [
        ToShape(s_oldShapes[0]), ToShape(s_oldShapes[1]), ToShape(s_oldShapes[2]), new SphereShape()
    ];

    [Test]
    public void CheckCollision_SeededPairs_EqualOldCollisionBitForBit()
    {
        var random = new Random(20261127);
        var mismatches = new List<string>();

        for (var i = 0; i < Count && mismatches.Count < 20; i++)
        {
            var first = Body.Random(random);
            var second = Body.Random(random);

            Compare(first, second, $"pair {i}");
            Compare(second, first, $"pair {i} reversed");
        }

        Assert.That(mismatches, Is.Empty);

        void Compare(Body a, Body b, string what)
        {
            var oldCollides = a.Old.Collision!.CheckCollision(b.Old.Collision!, out var oldNormal, out var oldDepth);
            var newCollides = NarrowPhase.CheckCollision(a.View(), b.View(), out var newNormal, out var newDepth);

            if (oldCollides != newCollides || !SameBits(oldNormal, newNormal) || !SameBits(oldDepth, newDepth))
                mismatches.Add($"{what}: today {oldCollides} {oldNormal} {oldDepth}, library {newCollides} {newNormal} {newDepth}");
        }
    }

    // The pair of CoolEngine.Physics.Tests' CheckCollision_SpheresExactlyTouchingOnAnAxis_ReturnsTrueWithZeroDepth: the
    // bounds touch and the distance equals the sum of the radii.
    [Test]
    public void CheckCollision_SpheresExactlyTouchingOnAnAxis_EqualsOldCollisionBitForBit()
    {
        var first = Body.Sphere(Vector3.Zero, 1);
        var second = Body.Sphere(new Vector3(1, 0, 0), 1);

        Assert.Multiple(() =>
        {
            Compare(first, second, "first, second");
            Compare(second, first, "second, first");
        });

        static void Compare(Body a, Body b, string what)
        {
            var oldCollides = a.Old.Collision!.CheckCollision(b.Old.Collision!, out var oldNormal, out var oldDepth);
            var newCollides = NarrowPhase.CheckCollision(a.View(), b.View(), out var newNormal, out var newDepth);

            Assert.That(oldCollides && oldDepth == 0, Is.True, $"{what}: today {oldCollides} with depth {oldDepth}");
            Assert.That(newCollides == oldCollides && SameBits(oldNormal, newNormal) && SameBits(oldDepth, newDepth),
                Is.True, $"{what}: today {oldCollides} {oldNormal} {oldDepth}, library {newCollides} {newNormal} {newDepth}");
        }
    }

    [Test]
    public void IntersectRay_SeededRays_EqualOldCollisionBitForBit()
    {
        var random = new Random(20261128);
        var mismatches = new List<string>();

        for (var i = 0; i < Count && mismatches.Count < 20; i++)
        {
            var body = Body.Random(random);
            var start = new Vector3(Next(random, -6, 6), Next(random, -6, 6), Next(random, -6, 6));
            // Aimed near the body, so most rays meet it.
            var target = body.Position + new Vector3(Next(random, -1.5f, 1.5f), Next(random, -1.5f, 1.5f),
                Next(random, -1.5f, 1.5f));
            var end = start + (target - start) * Next(random, 0.5f, 20);

            var oldHit = body.Old.Collision!.IntersectRay(new OldRay(start, end), out var oldPoint);
            var ray = new Ray(start, end);
            var newHit = body.Shape is PolygonShape polygon
                ? PhysicsQueries.IntersectRay(polygon, body.Vertices, body.Normals, ray, out var newPoint)
                : PhysicsQueries.IntersectRay(body.Position, body.Size.X / 2, ray, out newPoint);

            if (oldHit != newHit || !SameBits(oldPoint, newPoint))
                mismatches.Add($"ray {i}: today {oldHit} {oldPoint}, library {newHit} {newPoint}");
        }

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void ComputeWorldGeometry_SeededTransforms_EqualsUpdateCollisionBitForBit()
    {
        var random = new Random(20261129);
        var mismatches = new List<string>();

        for (var i = 0; i < Count && mismatches.Count < 20; i++)
        {
            var body = Body.Random(random);
            var oldData = body.Old.Collision!.CollisionData;

            if (body.Shape is PolygonShape polygon)
            {
                var vertices = new Vector3[polygon.Vertices.Length];
                var normals = new Vector3[polygon.FaceCount];
                PhysicsQueries.ComputeWorldVertices(polygon, body.Position, body.Linear, vertices);
                PhysicsQueries.ComputeWorldNormals(polygon, body.Position, body.Linear, normals);

                for (var k = 0; k < vertices.Length; k++)
                {
                    if (!SameBits(oldData.Vertices[k], vertices[k]))
                        mismatches.Add($"transform {i}, vertex {k}: today {oldData.Vertices[k]}, library {vertices[k]}");
                }

                for (var k = 0; k < normals.Length; k++)
                {
                    if (!SameBits(oldData.Faces[k].Normal, normals[k]))
                        mismatches.Add($"transform {i}, normal {k}: today {oldData.Faces[k].Normal}, library {normals[k]}");
                }
            }

            if (!SameBits(oldData.BoundingBox.Min, body.BoundsMin) || !SameBits(oldData.BoundingBox.Max, body.BoundsMax))
                mismatches.Add($"transform {i}, bounds: today {oldData.BoundingBox}, library {body.BoundsMin} {body.BoundsMax}");
        }

        Assert.That(mismatches, Is.Empty);
    }

    private static PolygonShape ToShape(CollisionData data)
    {
        var faces = new ShapeFace[data.Faces.Count];
        for (var i = 0; i < faces.Length; i++)
            faces[i] = new ShapeFace(data.Faces[i].Indices, data.Faces[i].Normal);

        return new PolygonShape(data.Vertices, faces);
    }

    private static float Next(Random random, float low, float high) => (float)(low + random.NextDouble() * (high - low));

    private static bool SameBits(float a, float b) => BitConverter.SingleToUInt32Bits(a) == BitConverter.SingleToUInt32Bits(b);

    private static bool SameBits(Vector3 a, Vector3 b) => SameBits(a.X, b.X) && SameBits(a.Y, b.Y) && SameBits(a.Z, b.Z);

    /// <summary>One seeded body on both sides: today's object with its Collision, and the library's geometry.</summary>
    private sealed class Body
    {
        private Body(Shape shape, CollisionTestPhysicObject old, Vector3 position, Matrix3 linear, Vector3 size)
        {
            Shape = shape;
            Old = old;
            Position = position;
            Linear = linear;
            Size = size;

            var polygon = shape as PolygonShape;
            Vertices = new Vector3[polygon?.Vertices.Length ?? 0];
            Normals = new Vector3[polygon?.FaceCount ?? 0];
            var transformation = Geometry.ComputeMatrix(position, linear);

            if (polygon != null)
            {
                Geometry.ComputeVertices(polygon, transformation, Vertices);
                Geometry.ComputeNormals(polygon, transformation, Normals);
            }

            Geometry.ComputeBounds(polygon, Vertices, position, size.X, out var min, out var max);
            BoundsMin = min;
            BoundsMax = max;
        }

        public Shape Shape { get; }

        public CollisionTestPhysicObject Old { get; }

        public Vector3 Position { get; }

        public Matrix3 Linear { get; }

        public Vector3 Size { get; }

        public Vector3[] Vertices { get; }

        public Vector3[] Normals { get; }

        public Vector3 BoundsMin { get; }

        public Vector3 BoundsMax { get; }

        public BodyGeometry View() =>
            new BodyGeometry(Shape as PolygonShape, Vertices, Normals, Position, Size.X, BoundsMin, BoundsMax);

        /// <summary>An unrotated sphere of the given width at <paramref name="position"/>.</summary>
        public static Body Sphere(Vector3 position, float width)
        {
            var size = new Vector3(width);
            var linear = new Matrix3(Matrix4.CreateScale(size / 2));
            var old = CollisionTestPhysicObject.Create(s_oldShapes[3], Geometry.ComputeMatrix(position, linear),
                position, size);

            return new Body(s_newShapes[3], old, position, linear, size);
        }

        /// <summary>
        /// A cube, triangulated cube, tetrahedron or sphere near the origin; the linear part rotates and scales
        /// (a component negative 15 % of the time, zero 5 %), and 30 % of the time rotates again before the scale,
        /// which shears.
        /// </summary>
        public static Body Random(Random random)
        {
            var kind = random.Next(4);
            var position = new Vector3(Next(random, -2.5f, 2.5f), Next(random, -2.5f, 2.5f), Next(random, -2.5f, 2.5f));
            var scale = new Vector3(Scale(random), Scale(random), Scale(random));
            var inner = Rotation(random) * Matrix4.CreateScale(scale);
            if (random.Next(10) < 3)
                inner *= Rotation(random);
            var linear = new Matrix3(inner);
            var size = kind == 3 ? new Vector3(random.Next(20) == 0 ? 0 : Next(random, 0.2f, 3f)) : 2 * scale;
            var old = CollisionTestPhysicObject.Create(s_oldShapes[kind], Geometry.ComputeMatrix(position, linear),
                position, size);

            return new Body(s_newShapes[kind], old, position, linear, size);
        }

        private static Matrix4 Rotation(Random random) =>
            Matrix4.CreateRotationZ(Next(random, -7, 7)) *
            (Matrix4.CreateRotationY(Next(random, -7, 7)) * Matrix4.CreateRotationX(Next(random, -7, 7)));

        private static float Scale(Random random)
        {
            var draw = random.Next(20);
            if (draw == 0)
                return 0;

            var magnitude = Next(random, 0.3f, 2f);
            return draw <= 3 ? -magnitude : magnitude;
        }
    }
}
