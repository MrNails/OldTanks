using OpenTK.Mathematics;

using SystemVector3 = System.Numerics.Vector3;
using GLVector3 = OpenTK.Mathematics.Vector3;

namespace OldTanks.Helpers;

public static class UIVectorHelper
{
    public static GLVector3 FromSystemVector3DegreesToGLVector3Radians(SystemVector3 vector3) => 
        new(MathHelper.DegreesToRadians(vector3.X), MathHelper.DegreesToRadians(vector3.Y), MathHelper.DegreesToRadians(vector3.Z));
    
    public static SystemVector3 FromGLVector3RadiansToSystemVector3Degrees(GLVector3 vector3) => 
        new(MathHelper.RadiansToDegrees(vector3.X), MathHelper.RadiansToDegrees(vector3.Y), MathHelper.RadiansToDegrees(vector3.Z));
}