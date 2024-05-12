using System.Collections.ObjectModel;
using Common.Models;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public abstract class WorldObject : ObservableObject, IDrawable, IPhysicObject, IWatchable
{
    private string m_name;
    
    private bool m_haveTransformation;
    
    private Vector3 m_position;
    private Vector3 m_rotation;
    private Vector3 m_cameraOffset;
    private Vector2 m_cameraOffsetAngle;
    private Vector4 m_color;
    
    private Collision m_collision;
    
    protected Vector3 m_size;
    
    protected Matrix4 m_transform;

    protected WorldObject(Scene scene)
    {
        var currType = GetType();
        Visible = true;

        CameraOffset = new Vector3(-1, 1, 0);

        RigidBody = new RigidBody();

        Scene = scene ??
                  throw new ObjectException(currType, $"Cannot create {currType.Name}. Scene is not exists");
    }

    public Scene Scene { get; }

    public ObservableCollection<TexturedObjectInfo> TexturedObjectInfos { get; } = new();

    public bool NeedTransformationApply => m_haveTransformation;

    public string Name
    {
        get => m_name;
        set => SetField(ref m_name, value);
    }
    
    public RigidBody RigidBody { get; }

    public Collision Collision
    {
        get => m_collision;
        set => SetField(ref m_collision, value);
    }

    public Vector4 Color
    {
        get => m_color;
        set => SetField(ref m_color, value);
    }

    public virtual Vector3 Size
    {
        get => m_size;
        set
        {
            SetField(ref m_size, value);
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public Vector3 Position
    {
        get => m_position;
        set
        {
            SetField(ref m_position, value);
            m_haveTransformation = true;
            SetCameraData();
        }
    }

    public Vector3 Rotation
    {
        get => m_rotation;
        set
        {
            SetField(ref m_rotation, value);
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }

    public virtual float Width
    {
        get => m_size.X;
        set
        {
            m_size.X = value;
            m_haveTransformation = true;
            SetCameraData();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
        }
    }

    public float Yaw
    {
        get => m_rotation.X;
        set
        {
            m_rotation.X = value;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Rotation));
        }
    }

    public float Pitch
    {
        get => m_rotation.Y;
        set
        {
            m_rotation.Y = value;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Rotation));
        }
    }

    public float Roll
    {
        get => m_rotation.Z;
        set
        {
            m_rotation.Z = value;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Rotation));
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
            OnPropertyChanged();
        }
    }

    public Camera? Camera { get; set; }

    public Vector3 CameraOffset
    {
        get => m_cameraOffset;
        set => SetField(ref m_cameraOffset, value);
    }

    public Matrix4 Transformation => m_transform;

    public bool Visible { get; set; }

    public virtual void ApplyTransformation()
    {
        if (!m_haveTransformation)
            return;
        
        m_transform = Matrix4.CreateScale(Width / 2, Height / 2, Length / 2) *
                      Matrix4.CreateRotationX(Yaw) *
                      Matrix4.CreateRotationY(-Pitch) *
                      Matrix4.CreateRotationZ(Roll) *
                      Matrix4.CreateTranslation(Position);

        Collision?.UpdateCollision();

        m_haveTransformation = false;
    }

    public void Move(float timeDelta, int collisionIteration = -1)
    {
        if (RigidBody == null || RigidBody.IsStatic)
            return;

        RigidBody.OnTick(timeDelta, collisionIteration);

        var rotation = Matrix3.CreateRotationY(MathHelper.DegreesToRadians(Rotation.Y));

        EngineSettings.Current.GlobalLock.EnterWriteLock();
        
        Position += rotation * RigidBody.Velocity * timeDelta;
        
        // if (Camera != null)
        // {
        //     var normalizedDirection = (Camera.Position - m_position).Normalized();
        //
        //     Camera.Yaw = (float)MathHelper.RadiansToDegrees(-Math.Atan2(normalizedDirection.Z, -normalizedDirection.X));
        //     Camera.Pitch = (float)MathHelper.RadiansToDegrees(Math.Asin(-normalizedDirection.Y));
        // }
        
        EngineSettings.Current.GlobalLock.ExitWriteLock();
    }
    
    private void SetCameraData()
    {
        if (Camera == null)
            return;

        var camOffset = Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(m_cameraOffsetAngle.Y)) * m_cameraOffset * 
                        Matrix3.CreateRotationY(MathHelper.DegreesToRadians(m_cameraOffsetAngle.X));

        Camera.Position = m_position + new Vector3(0, m_size.Y / 2, 0) +
                          Matrix3.CreateRotationY(MathHelper.DegreesToRadians(Rotation.Y)) * camOffset;
    }
}