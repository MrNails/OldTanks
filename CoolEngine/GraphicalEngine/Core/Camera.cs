using Common.Models;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CoolEngine.GraphicalEngine.Core;

public class Camera : ObservableObject, IMovable
{
    private bool m_isLookAtChanged;

    private float m_fov;
    private float m_pitch;
    private float m_speedMultiplier;
    
    private Vector3 m_rotation;
    private Vector3 m_position;
    private Vector3 m_cameraUp;
    private Vector3 m_velocity;

    private Matrix4 m_lookAt;

    public Camera() : this(new Vector3(0, 0, 0), new Vector3(MathHelper.DegreesToRadians(90), 0, 0), Vector3.UnitY)
    {
    }

    public Camera(Vector3 position, Vector3 rotation, Vector3 cameraUp)
    {
        CameraUp = cameraUp;
        Rotation = rotation;
        Position = position;
        
        FOV = 45;
        SpeedMultiplier = 3;
        
        Velocity = new Vector3(5);
    }

    public Vector3 Rotation
    {
        get => m_rotation;
        set
        {
            m_rotation = value.Normalized();
            m_isLookAtChanged = true;
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
            OnPropertyChanged();
        }
    }

    public Vector3 CameraUp
    {
        get => m_cameraUp;
        set
        {
            if (value == m_cameraUp) 
                return;
            
            m_cameraUp = value.Normalized();
            m_isLookAtChanged = true;
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

    public Vector3 Velocity
    {
        get => m_velocity;
        set => SetField(ref m_velocity, value);
    }

    public float SpeedMultiplier
    {
        get => m_speedMultiplier;
        set => SetField(ref m_speedMultiplier, value);
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

    /// <summary>
    /// Rotate camera based on delta of mouse position
    /// </summary>
    /// <param name="xDelta">Mouse position X delta</param>
    /// <param name="yDelta">Mouse position Y delta</param>
    public virtual void Rotate(float xDelta, float yDelta)
    {
        var prevPitch = m_pitch;
        m_pitch = Math.Clamp(m_pitch + yDelta, -80, 80);
        
        var xRad = -MathHelper.DegreesToRadians(xDelta);
        var yRad = MathHelper.DegreesToRadians(m_pitch - prevPitch);

        var u = Vector3.Cross(CameraUp, Rotation);

        var q = Quaternion.FromAxisAngle(CameraUp, xRad) * 
                Quaternion.FromAxisAngle(u, yRad);
        
        Rotation = q * m_rotation;
    }

    public virtual void Move(float timeDelta, KeyboardState keyboardState)
    {
        var speedMultiplier = 1f;

        if (keyboardState.IsKeyDown(Keys.LeftControl))
            speedMultiplier = SpeedMultiplier;

        var posDelta = Vector3.Zero;
        if (keyboardState.IsKeyDown(Keys.D))
            posDelta += Vector3.Normalize(Vector3.Cross(m_rotation, m_cameraUp)) *
                        m_velocity.X * timeDelta * speedMultiplier;
        else if (keyboardState.IsKeyDown(Keys.A))
            posDelta -= Vector3.Normalize(Vector3.Cross(m_rotation, m_cameraUp)) *
                        m_velocity.X * timeDelta * speedMultiplier;

        if (keyboardState.IsKeyDown(Keys.W))
            posDelta += m_rotation * m_velocity.Z * timeDelta * speedMultiplier;
        else if (keyboardState.IsKeyDown(Keys.S))
            posDelta -= m_rotation * m_velocity.Z * timeDelta * speedMultiplier;

        if (keyboardState.IsKeyDown(Keys.Space))
            posDelta += m_cameraUp * m_velocity.Y * timeDelta * speedMultiplier;
        else if (keyboardState.IsKeyDown(Keys.LeftShift))
            posDelta -= m_cameraUp * m_velocity.Y * timeDelta * speedMultiplier;

        Position += posDelta;
    }
    
    private void ChangedLookAt()
    {
        m_lookAt = Matrix4.LookAt(m_position, m_position + m_rotation, CameraUp);
        m_isLookAtChanged = false;
    }
}