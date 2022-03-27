using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public abstract class WorldObject : IDrawable, IPhysicObject
{
    private Scene m_scene;
    private Vector3 m_size;
    private Vector3 m_position;
    private Vector3 m_direction;

    private bool m_haveTransformation;

    protected Matrix4 m_transform;

    protected WorldObject(Scene scene)
    {
        var currType = GetType();

        m_scene = scene ??
                  throw new ObjectException(currType, $"Cannot create {currType.Name}. Scene is not exists");
    }

    public Scene Scene => m_scene;

    public bool HaveChanged => m_haveTransformation;

    public RigidBody RigidBody { get; set; }
    public Collision Collision { get; set; }

    public Vector3 Size
    {
        get => m_size;
        set
        {
            m_size = value;
            m_haveTransformation = true;
        }
    }

    public Vector3 Position
    {
        get => m_position;
        set
        {
            m_position = value;
            m_haveTransformation = true;
        }
    }

    public Vector3 Direction
    {
        get => m_direction;
        set
        {
            m_direction = value;
            m_haveTransformation = true;
        }
    }

    public float X
    {
        get => m_position.X;
        set
        {
            m_position.X = value;
            m_haveTransformation = true;
        }
    }

    public float Y
    {
        get => m_position.Y;
        set
        {
            m_position.Y = value;
            m_haveTransformation = true;
        }
    }

    public float Z
    {
        get => m_position.Z;
        set
        {
            m_position.Z = value;
            m_haveTransformation = true;
        }
    }

    public float Width
    {
        get => m_size.X;
        set
        {
            m_size.X = value;
            m_haveTransformation = true;
        }
    }

    public float Height
    {
        get => m_size.Y;
        set
        {
            m_size.Y = value;
            m_haveTransformation = true;
        }
    }

    public float Length
    {
        get => m_size.Z;
        set
        {
            m_size.Z = value;
            m_haveTransformation = true;
        }
    }

    public float Pitch
    {
        get => m_direction.X;
        set
        {
            m_direction.X = value;
            m_haveTransformation = true;
        }
    }

    public float Yaw
    {
        get => m_direction.Y;
        set
        {
            m_direction.Y = value;
            m_haveTransformation = true;
        }
    }

    public float Roll
    {
        get => m_direction.Z;
        set
        {
            m_direction.Z = value;
            m_haveTransformation = true;
        }
    }

    public Matrix4 Transform => m_transform;

    public virtual void AcceptTransform()
    {
        if (!m_haveTransformation)
            return;

        m_transform = Matrix4.CreateScale(Width / 2, Height / 2, Length / 2) *
                      Matrix4.CreateRotationX(MathHelper.DegreesToRadians(Pitch)) *
                      Matrix4.CreateRotationY(MathHelper.DegreesToRadians(Yaw)) *
                      Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Roll));

        m_haveTransformation = false;
    }
}