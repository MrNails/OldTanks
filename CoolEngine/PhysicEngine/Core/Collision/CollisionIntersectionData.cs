using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core.Collision;

public readonly struct CollisionIntersectionData
{
    public CollisionIntersectionData(Vector3 normal, float depth)
    {
        Normal = normal;
        Depth = depth;
    }

    public Vector3 Normal { get; }
    public float Depth { get; }
}