using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public abstract class WorldObject : IDrawable, IPhysicObject, IWatchable
{
    private Scene m_scene;
    private Vector3 m_position;
    private Vector3 m_direction;

    protected Vector3 m_size;

    private bool m_haveTransformation;

    protected Matrix4 m_transform;
    private Vector3 m_cameraOffset;
    private Vector2 m_cameraOffsetAngle;

    protected WorldObject(Scene scene)
    {
        var currType = GetType();
        Visible = true;

        CameraOffset = new Vector3(-1, 1, 0);

        RigidBody = new RigidBody();

        m_scene = scene ??
                  throw new ObjectException(currType, $"Cannot create {currType.Name}. Scene is not exists");
    }

    public Scene Scene => m_scene;

    public bool HaveChanged => m_haveTransformation;

    public RigidBody RigidBody { get; set; }
    public Collision Collision { get; set; }
    
    public string Name { get; set; }

    public virtual Vector3 Size
    {
        get => m_size;
        set
        {
            m_size = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public Vector3 Position
    {
        get => m_position;
        set
        {
            m_position = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public Vector3 Direction
    {
        get => m_direction;
        set
        {
            m_direction = new Vector3(value.X % 360, value.Y % 360, value.Z % 360);
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
            SetCameraData();
        }
    }

    public float Y
    {
        get => m_position.Y;
        set
        {
            m_position.Y = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public float Z
    {
        get => m_position.Z;
        set
        {
            m_position.Z = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public virtual float Width
    {
        get => m_size.X;
        set
        {
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public virtual float Height
    {
        get => m_size.Y;
        set
        {
            m_size.Y = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public virtual float Length
    {
        get => m_size.Z;
        set
        {
            m_size.Z = value;
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public float Yaw
    {
        get => m_direction.X;
        set
        {
            m_direction.X = value % 360;
            m_haveTransformation = true;
        }
    }

    public float Pitch
    {
        get => m_direction.Y;
        set
        {
            m_direction.Y = value % 360;
            m_haveTransformation = true;
        }
    }

    public float Roll
    {
        get => m_direction.Z;
        set
        {
            m_direction.Z = value % 360;
            m_haveTransformation = true;
        }
    }

    public Vector2 CameraOffsetAngle
    {
        get => m_cameraOffsetAngle;
        set
        {
            value.Y = Math.Clamp(value.Y, -20, 40);
            value.X %= 360;

            m_cameraOffsetAngle = value;
        }
    }

    public Camera? Camera { get; set; }

    public Vector3 CameraOffset
    {
        get => m_cameraOffset;
        set => m_cameraOffset = value;
    }

    public Matrix4 Transform => m_transform;

    public bool Visible { get; set; }

    public virtual void AcceptTransform()
    {
        if (!m_haveTransformation)
            return;

        m_transform = Matrix4.CreateScale(Width / 2, Height / 2, Length / 2) *
                      Matrix4.CreateRotationX(MathHelper.DegreesToRadians(Yaw)) *
                      Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-Pitch)) *
                      Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Roll)) *
                      Matrix4.CreateTranslation(Position);

        Collision?.UpdateCollision();

        m_haveTransformation = false;
    }

    public void Move(float timeDelta, int collisionIteration = -1)
    {
        if (RigidBody == null || RigidBody.IsStatic)
            return;

        RigidBody.OnTick(timeDelta, collisionIteration);

        var rotation = Matrix3.CreateRotationY(MathHelper.DegreesToRadians(Direction.Y));

        GlobalSettings.GlobalLock.EnterWriteLock();
        
        Position += rotation * RigidBody.Velocity * timeDelta;
        
        if (Camera != null)
        {
            var normalizedDirection = (Camera.Position - m_position).Normalized();

            Camera.Yaw = (float)MathHelper.RadiansToDegrees(-Math.Atan2(normalizedDirection.Z, -normalizedDirection.X));
            Camera.Pitch = (float)MathHelper.RadiansToDegrees(Math.Asin(-normalizedDirection.Y));
        }
        
        GlobalSettings.GlobalLock.ExitWriteLock();
    }

    private void SetCameraData()
    {
        if (Camera == null)
            return;

        var camOffset = Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(m_cameraOffsetAngle.Y)) * m_cameraOffset * 
                        Matrix3.CreateRotationY(MathHelper.DegreesToRadians(m_cameraOffsetAngle.X));

        Camera.Position = m_position + new Vector3(0, m_size.Y / 2, 0) +
                          Matrix3.CreateRotationY(MathHelper.DegreesToRadians(Direction.Y)) * camOffset;
    }
}