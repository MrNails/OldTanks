using CoolEngine.GraphicalEngine.Core;
using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.Services.Extensions;

public static class FaceTypeExtensions
{
    public static BeginMode ToBeginMode(this FaceType faceType)
    {
        return faceType switch
        {
            FaceType.Dot => BeginMode.Points,
            FaceType.Line => BeginMode.Lines,
            FaceType.Triangle => BeginMode.Triangles,
            FaceType.Quad => BeginMode.Quads,
            FaceType.Unknown => throw new ArgumentOutOfRangeException(nameof(faceType), faceType,
                $"Unhandled value of FaceType {faceType}"),
            _ => throw new ArgumentOutOfRangeException(nameof(faceType), faceType,
                $"Unhandled value of FaceType {faceType}")
        };
    }
    
    public static PrimitiveType ToPrimitiveType(this FaceType faceType)
    {
        return faceType switch
        {
            FaceType.Dot => PrimitiveType.Points,
            FaceType.Line => PrimitiveType.Lines,
            FaceType.Triangle => PrimitiveType.Triangles,
            FaceType.Quad => PrimitiveType.Quads,
            FaceType.Unknown => throw new ArgumentOutOfRangeException(nameof(faceType), faceType,
                $"Unhandled value of FaceType {faceType}"),
            _ => throw new ArgumentOutOfRangeException(nameof(faceType), faceType,
                $"Unhandled value of FaceType {faceType}")
        };
    }
}