using OpenTK.Mathematics;

namespace CoolEngine.Models;

public readonly record struct BoundingBox(Vector3 Min, Vector3 Max);