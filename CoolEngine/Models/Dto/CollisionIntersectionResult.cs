using OpenTK.Mathematics;

namespace CoolEngine.Models.Dto;

public readonly record struct CollisionIntersectionResult(Vector3 Normal, float Depth, HashSet<Vector3> contactPoints);