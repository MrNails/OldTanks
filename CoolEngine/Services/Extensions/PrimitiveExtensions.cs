using CoolEngine.GraphicalEngine.Core;

namespace CoolEngine.Services.Extensions;

public static class PrimitiveExtensions
{
    public static FaceType GetFaceType(this int value)
    {
        return value is > 0 and <= (int)FaceType.Quad
            ? (FaceType)value
            : FaceType.Unknown;
    }
}