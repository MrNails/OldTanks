using System.Numerics;

namespace CoolEngine.Models;

public readonly record struct BoundingBox(Vector3 Min, Vector3 Max);