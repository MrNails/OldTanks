using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services.Interfaces;
using System.Numerics;
using CoolEngine.Mathematics;

namespace CoolEngine.Tests.TestSupport;

/// <summary>
/// A physics body with a transformation set by the test instead of built from position, rotation and size,
/// so a test controls exactly what <see cref="Collision.UpdateCollision"/> sees.
/// </summary>
internal sealed class CollisionTestPhysicObject : IPhysicObject
{
    private Vector3 m_position;
    private Vector3 m_size;

    public Vector3 Position
    {
        get => m_position;
        set => m_position = value;
    }

    public Vector3 Rotation { get; set; }

    public float X
    {
        get => m_position.X;
        set => m_position.X = value;
    }

    public float Y
    {
        get => m_position.Y;
        set => m_position.Y = value;
    }

    public float Z
    {
        get => m_position.Z;
        set => m_position.Z = value;
    }

    public Vector3 Size
    {
        get => m_size;
        set => m_size = value;
    }

    public float Width
    {
        get => m_size.X;
        set => m_size.X = value;
    }

    public float Height
    {
        get => m_size.Y;
        set => m_size.Y = value;
    }

    public float Length
    {
        get => m_size.Z;
        set => m_size.Z = value;
    }

    public Matrix4 Transformation { get; set; }

    public Collision? Collision { get; set; }

    public RigidBody RigidBody { get; } = new RigidBody();

    public bool NeedTransformationApply => false;

    public void ApplyTransformation() => Collision?.UpdateCollision();

    public static CollisionTestPhysicObject Create(CollisionData shape, Matrix4 transformation, Vector3 position, Vector3 size)
    {
        var physicObject = new CollisionTestPhysicObject
        {
            Transformation = transformation,
            Position = position,
            Size = size
        };

        physicObject.Collision = new Collision(physicObject, shape);
        physicObject.ApplyTransformation();

        return physicObject;
    }
}
