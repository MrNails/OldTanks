using CoolEngine.Mathematics;

using SystemVector3 = System.Numerics.Vector3;

namespace OldTanks.Helpers;

// The method names still say "GLVector3" because the debug window's binding attributes name them; both sides are
// System.Numerics vectors since OT-5.
public static class UIVectorHelper
{
    public static SystemVector3 FromSystemVector3DegreesToGLVector3Radians(SystemVector3 vector3) =>
        new(MathHelper.DegreesToRadians(vector3.X), MathHelper.DegreesToRadians(vector3.Y), MathHelper.DegreesToRadians(vector3.Z));

    public static SystemVector3 FromGLVector3RadiansToSystemVector3Degrees(SystemVector3 vector3) =>
        new(MathHelper.RadiansToDegrees(vector3.X), MathHelper.RadiansToDegrees(vector3.Y), MathHelper.RadiansToDegrees(vector3.Z));
}
