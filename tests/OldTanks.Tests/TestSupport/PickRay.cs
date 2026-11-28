using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Models;
using OldTanks.Models;

namespace OldTanks.Tests.TestSupport;

/// <summary>
/// A copy of the pick-ray code in <c>OldTanks.Windows.MainWindow.WorldHandler</c> (the free-camera ray and the nearest-hit
/// loop). That window needs OpenGL and cannot be built headless, so the loop is copied here; a change to the original does
/// not reach this copy.
/// </summary>
internal static class PickRay
{
    /// <summary>The ray <c>WorldHandler</c> builds in free-camera mode.</summary>
    public static Ray FromFreeCamera(Camera camera) =>
        new(new Vector3(camera.Position.X, camera.Position.Y - 1, camera.Position.Z), camera.Position + camera.Rotation * 100);

    /// <summary>
    /// The nearest-hit loop of <c>WorldHandler</c>, line for line: an object without a <c>Collision</c> is skipped, and a
    /// hit replaces the previous one only when strictly nearer (on a tie the earlier object wins). <paramref name="point"/>
    /// is the nearest hit, or <see cref="Vector3.Zero"/> when nothing was hit.
    /// </summary>
    public static bool Nearest(IList<WorldObject> objects, Ray ray, out Vector3 point)
    {
        var previousLength = float.MaxValue;
        var tmpIntersectedPoint = Vector3.Zero;
        var rayIntersected = false;
        point = Vector3.Zero;

        for (var i = 0; i < objects.Count; i++)
        {
            var worldObject = objects[i];

            if (worldObject.Collision == null)
                continue;

            var isRayIntersected = worldObject.Collision.IntersectRay(ray, out tmpIntersectedPoint);

            var lengthToRayStart = Math.Abs((tmpIntersectedPoint - ray.Start).Length());
            if (isRayIntersected && previousLength > lengthToRayStart)
            {
                rayIntersected = true;
                previousLength = lengthToRayStart;
                point = tmpIntersectedPoint;
            }
        }

        return rayIntersected;
    }
}
