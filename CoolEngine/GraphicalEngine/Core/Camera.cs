using Common.Models;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Camera : ObservableObject, IPhysicObject
{
    private bool m_isLookAtChanged;

    private Quaternion m_rotation;
    private Vector3 m_position;
    private float m_fov;

    private Matrix4 m_lookAt;
    private Collision m_collision;
    private Vector3 m_size;
    private Matrix4 m_transform;

    private bool m_haveTransformation;
    private RigidBody m_rigidBody;

    public Camera() : this(new Vector3(0, 0, 0))
    {
    }

    public Camera(Vector3 position)
    {
        m_position = position;
        m_rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(90), 0, 0);
        FOV = 45;

        RigidBody = new RigidBody
        {
            MaxSpeed = 100,
            MaxBackSpeed = 100,
            MaxSpeedMultiplier = 1,
            Velocity = new Vector3(5),
        };
    }

    public Quaternion Rotation
    {
        get => m_rotation;
        set
        {
            m_rotation = value;
            m_isLookAtChanged = true;
            m_haveTransformation = true;
            OnPropertyChanged();
        }
    }

    public Vector3 Position
    {
        get => m_position;
        set
        {
            m_position = value;
            m_isLookAtChanged = true;
            m_haveTransformation = true;
            OnPropertyChanged();
        }
    }

    public Vector3 CameraUp => Vector3.UnitY;

    public bool NeedTransformationApply => m_haveTransformation;
    
    public Matrix4 Transformation => m_transform;

    public RigidBody RigidBody
    {
        get => m_rigidBody;
        set
        {
            if (m_rigidBody == value)
            {
                return;
            }

            m_rigidBody = value ?? throw new ArgumentNullException(nameof(value));
            OnPropertyChanged();
        }
    }

    public Matrix4 LookAt
    {
        get
        {
            if (m_isLookAtChanged)
                ChangedLookAt();

            return m_lookAt;
        }
    }

    public float X
    {
        get => m_position.X;
        set
        {
            if (m_position.X == value)
            {
                return;
            }

            m_position.X = value;
            m_isLookAtChanged = true;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }

    public float Y
    {
        get => m_position.Y;
        set
        {
            if (m_position.Y == value)
            {
                return;
            }

            m_position.Y = value;
            m_isLookAtChanged = true;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }

    public float Z
    {
        get => m_position.Z;
        set
        {
            if (m_position.Z == value)
            {
                return;
            }

            m_position.Z = value;
            m_isLookAtChanged = true;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Position));
        }
    }
    
    public float FOV
    {
        get => m_fov;
        set
        {
            if (m_fov == value)
            {
                return;
            }

            m_fov = MathHelper.Clamp(value, 20, 90);
            OnPropertyChanged();
        }
    }

    public Collision? Collision
    {
        get => m_collision;
        set
        {
            if (value == m_collision)
            {
                return;
            }

            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            m_collision = value;
            OnPropertyChanged();
        }
    }

    public Vector3 Size
    {
        get => m_size;
        set
        {
            if (m_size == value)
            {
                return;
            }

            m_size = value;
            m_haveTransformation = true;
            OnPropertyChanged();
        }
    }

    public float Width
    {
        get => m_size.X;
        set
        {
            if (m_size.X == value)
            {
                return;
            }

            m_size.X = value;
            m_haveTransformation = true;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
        }
    }

    public float Height
    {
        get => m_size.Y;
        set
        {
            if (m_size.Y == value)
            {
                return;
            }

            m_size.Y = value;
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
        }
    }

    public float Length
    {
        get => m_size.Z;
        set
        {
            if (m_size.Z == value)
            {
                return;
            }

            m_size.Z = value; 
            m_haveTransformation = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Size));
        }
    }

    public void ApplyTransformation()
    {
        if (!m_haveTransformation)
            return;

        m_transform = Matrix4.CreateScale(Width / 2, Height / 2, Length / 2) *
                      Matrix4.CreateFromQuaternion(Rotation) *
                      Matrix4.CreateTranslation(Position);
        
        m_collision?.UpdateCollision();

        m_haveTransformation = false;
    }

    public void Move(float timeDelta, int collisionIteration = -1) {}
    
    private void ChangedLookAt()
    {
        m_lookAt = Matrix4.LookAt(Position, Position + m_rotation.ToEulerAngles(), CameraUp);
        m_isLookAtChanged = false;
    }
}