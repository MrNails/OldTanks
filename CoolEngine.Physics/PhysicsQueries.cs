using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Internal;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics;

/// <summary>
/// Pure functions over shapes and poses, for code that holds a snapshot instead of a world (the debug view and the pick
/// ray on the GL thread). Safe on any thread; no allocation; the caller owns every buffer.
/// </summary>
/// <remarks>
/// A pose is a position, an orientation (a unit quaternion, not checked or normalized here) and half-extents; a
/// sphere's radius is its X half-extent.
/// </remarks>
public static class PhysicsQueries
{
    /// <summary>
    /// The shape's vertices in world space, <c>position + R × (halfExtents ⊙ vertex)</c> with <c>R</c> the rotation of
    /// <paramref name="orientation"/>, in shape order, into the first <c>shape.Vertices.Length</c> elements of
    /// <paramref name="destination"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="shape"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the vertex count.</exception>
    public static void ComputeWorldVertices(PolygonShape shape, Vector3 position, Quaternion orientation,
        Vector3 halfExtents, Span<Vector3> destination)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (destination.Length < shape.Vertices.Length)
            throw new ArgumentException("The destination is shorter than the shape's vertex count.", nameof(destination));

        Geometry.ComputeVertices(shape, position, Matrix3.CreateFromQuaternion(orientation), halfExtents, destination);
    }

    /// <summary>
    /// The shape's face normals in world space (the cofactors of rotation × scale, negated for a mirror, normalized),
    /// in face order, into the first <c>shape.FaceCount</c> elements of <paramref name="destination"/>. A zero
    /// half-extent gives NaN normals for the faces it collapses.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="shape"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the face count.</exception>
    public static void ComputeWorldNormals(PolygonShape shape, Quaternion orientation, Vector3 halfExtents,
        Span<Vector3> destination)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (destination.Length < shape.FaceCount)
            throw new ArgumentException("The destination is shorter than the shape's face count.", nameof(destination));

        Geometry.ComputeNormals(shape, Matrix3.CreateFromQuaternion(orientation), halfExtents, destination);
    }

    /// <summary>
    /// Today's ray test of a polygon on its world geometry: each face's plane is hit-tested in face order and the
    /// <b>first</b> face whose hit falls inside it wins, not the nearest (the inside test projects onto the edges from
    /// face index 0 to 1 and to 2, correct only for faces with perpendicular edges). <paramref name="point"/> is zero
    /// on a miss.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="shape"/> is null.</exception>
    /// <exception cref="ArgumentException">A span is shorter than the shape's vertex or face count.</exception>
    public static bool IntersectRay(PolygonShape shape, ReadOnlySpan<Vector3> worldVertices,
        ReadOnlySpan<Vector3> worldNormals, in Ray ray, out Vector3 point)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (worldVertices.Length < shape.Vertices.Length)
            throw new ArgumentException("The vertices are fewer than the shape's.", nameof(worldVertices));

        if (worldNormals.Length < shape.FaceCount)
            throw new ArgumentException("The normals are fewer than the shape's faces.", nameof(worldNormals));

        return IntersectRayWithPolygon(shape, worldVertices, worldNormals, ray, out point);
    }

    /// <summary>
    /// Today's ray test of a sphere: the quadratic is solved (its square root in double) and the nearer intersection
    /// in front of the start wins; false when both are behind it. <paramref name="point"/> is zero on a miss.
    /// </summary>
    public static bool IntersectRay(Vector3 sphereCentre, float sphereRadius, in Ray ray, out Vector3 point)
    {
        point = Vector3.Zero;

        var rayToSphereDirectionalVector = ray.Start - sphereCentre;
        var rayDirection = -Vector3.Normalize(ray.Delta);
        var a = Vector3.Dot(rayDirection, rayDirection);
        var b = 2 * Vector3.Dot(rayDirection, rayToSphereDirectionalVector);
        var c = Vector3.Dot(rayToSphereDirectionalVector, rayToSphereDirectionalVector) - sphereRadius * sphereRadius;

        if (!LegacyMath.QuadraticEquation(a, b, c, out var intersectPoint1, out var intersectPoint2))
            return false;

        if (intersectPoint1 < 0)
        {
            intersectPoint1 = intersectPoint2;

            if (intersectPoint1 < 0)
                return false;
        }

        point = ray.Start + rayDirection * intersectPoint1;

        return true;
    }

    /// <summary>
    /// Today's nearest hit over a snapshot of bodies, index by index (the same rule as
    /// <see cref="PhysicsWorld.RayCast"/>): a hit replaces the best one only when it is strictly nearer to the start,
    /// so the lower index wins a tie. Each polygon's world geometry is computed into the scratch spans first.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The four body spans differ in length, a shape is null, or a scratch span is smaller than the largest polygon's
    /// vertex or face count.
    /// </exception>
    public static bool RayCastNearest(ReadOnlySpan<Shape> shapes, ReadOnlySpan<Vector3> positions,
        ReadOnlySpan<Quaternion> orientations, ReadOnlySpan<Vector3> halfExtents, in Ray ray,
        Span<Vector3> vertexScratch, Span<Vector3> normalScratch, out int index, out Vector3 point)
    {
        if (positions.Length != shapes.Length || orientations.Length != shapes.Length ||
            halfExtents.Length != shapes.Length)
            throw new ArgumentException("The shapes, positions, orientations and half-extents differ in length.",
                nameof(shapes));

        for (var i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] is null)
                throw new ArgumentException($"Shape {i} is null.", nameof(shapes));

            if (shapes[i] is PolygonShape polygon)
            {
                if (vertexScratch.Length < polygon.Vertices.Length)
                    throw new ArgumentException("The vertex scratch is smaller than a polygon's vertex count.",
                        nameof(vertexScratch));

                if (normalScratch.Length < polygon.FaceCount)
                    throw new ArgumentException("The normal scratch is smaller than a polygon's face count.",
                        nameof(normalScratch));
            }
        }

        var nearest = new NearestHit(ray);

        for (var i = 0; i < shapes.Length; i++)
        {
            bool hit;
            Vector3 hitPoint;

            if (shapes[i] is PolygonShape polygon)
            {
                var vertices = vertexScratch[..polygon.Vertices.Length];
                var normals = normalScratch[..polygon.FaceCount];
                var rotation = Matrix3.CreateFromQuaternion(orientations[i]);
                Geometry.ComputeVertices(polygon, positions[i], rotation, halfExtents[i], vertices);
                Geometry.ComputeNormals(polygon, rotation, halfExtents[i], normals);
                hit = IntersectRayWithPolygon(polygon, vertices, normals, ray, out hitPoint);
            }
            else
            {
                hit = IntersectRay(positions[i], Geometry.SphereWidth(halfExtents[i]) / 2, ray, out hitPoint);
            }

            nearest.Consider(i, hit, hitPoint);
        }

        index = nearest.Index;
        point = nearest.Point;

        return nearest.Found;
    }

    /// <summary>Today's <c>Collision.IntersectRayWithPolygon</c>, unchecked.</summary>
    internal static bool IntersectRayWithPolygon(PolygonShape shape, ReadOnlySpan<Vector3> vertices,
        ReadOnlySpan<Vector3> normals, in Ray ray, out Vector3 point)
    {
        point = Vector3.Zero;

        for (int i = 0; i < shape.FaceCount; i++)
        {
            var indices = shape.FaceIndices(i);
            var normal = normals[i];
            var rayDelta = ray.Delta;

            var nDotRayDelta = Vector3.Dot(normal, rayDelta);

            // Skips a face the ray meets from behind, which would come before the face it enters.
            if (nDotRayDelta < 1e-6f)
                continue;

            var meshFirstVertex = vertices[(int)indices[0]];
            var t = Vector3.Dot(-normal, ray.Start - meshFirstVertex) / nDotRayDelta;

            // Skips a plane behind the start.
            if (t > 0)
                continue;

            var localRayEnd = ray.Start + rayDelta * t;

            var deltaMeshAndLocalRayStart = localRayEnd - meshFirstVertex;
            var deltaV21 = vertices[(int)indices[1]] - meshFirstVertex;
            var deltaV31 = vertices[(int)indices[2]] - meshFirstVertex;

            var u = Vector3.Dot(deltaMeshAndLocalRayStart, deltaV21);
            var v = Vector3.Dot(deltaMeshAndLocalRayStart, deltaV31);

            if (u >= 0 && u <= Vector3.Dot(deltaV21, deltaV21) &&
                v >= 0 && v <= Vector3.Dot(deltaV31, deltaV31))
            {
                point = localRayEnd;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Today's nearest-hit rule (OldTanks.Windows.MainWindow's ray loop), shared by <see cref="RayCastNearest"/> and
    /// <see cref="PhysicsWorld.RayCast"/>: the distance is <c>Math.Abs((point − start).Length())</c> and a hit replaces
    /// the best only when strictly nearer.
    /// </summary>
    internal struct NearestHit
    {
        private readonly Vector3 m_start;

        public NearestHit(in Ray ray)
        {
            m_start = ray.Start;
            Distance = float.MaxValue;
            Index = -1;
            Point = Vector3.Zero;
            Found = false;
        }

        public float Distance { get; private set; }

        public int Index { get; private set; }

        public Vector3 Point { get; private set; }

        public bool Found { get; private set; }

        public void Consider(int index, bool hit, Vector3 point)
        {
            var lengthToRayStart = Math.Abs((point - m_start).Length());
            if (hit && Distance > lengthToRayStart)
            {
                Found = true;
                Distance = lengthToRayStart;
                Index = index;
                Point = point;
            }
        }
    }
}
