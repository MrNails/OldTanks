using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Camera : IMovable
{
    private bool m_isLookAtChanged;

    private Vector3 m_direction;
    private Vector3 m_cameraUp;
    private Vector3 m_position;
    private float m_yaw;
    private float m_pitch;
    private float m_roll;
    private float m_fov;

    private Matrix4 m_lookAt;

    public Camera() : this(new Vector3(0, 0, 0))
    {
    }

    public Camera(Vector3 position)
    {
        Speed = 5;

        m_position = position;
        m_direction = new Vector3(0, 0, 0);
        m_cameraUp = new Vector3(0, 1, 0);

        Yaw = -90;
        Pitch = 0;

        FOV = 45;
    }

    public Vector3 Direction => m_direction;

    public Vector3 Position
    {
        get => m_position;
        set => m_position = value;
    }

    public Vector3 CameraUp => m_cameraUp;

    public float Speed { get; set; }

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
            m_position.X = value;
            m_isLookAtChanged = true;
        }
    }

    public float Y
    {
        get => m_position.Y;
        set
        {
            m_position.Y = value;
            m_isLookAtChanged = true;
        }
    }

    public float Z
    {
        get => m_position.Z;
        set
        {
            m_position.Z = value;
            m_isLookAtChanged = true;
        }
    }

    public float Yaw
    {
        get => m_yaw;
        set
        {
            m_yaw = value;

            m_direction.X = (float)Math.Cos(MathHelper.DegreesToRadians(m_pitch)) *
                            (float)Math.Cos(MathHelper.DegreesToRadians(m_yaw));
            m_direction.Z = (float)Math.Cos(MathHelper.DegreesToRadians(m_pitch)) *
                            (float)Math.Sin(MathHelper.DegreesToRadians(m_yaw));
            m_direction = Vector3.Normalize(m_direction);

            m_isLookAtChanged = true;
        }
    }

    public float Roll
    {
        get => m_roll;
        set => m_roll = value;
    }

    public float Pitch
    {
        get => m_pitch;
        set
        {
            m_pitch = MathHelper.Clamp(value, -80, 80);

            m_direction.X = (float)Math.Cos(MathHelper.DegreesToRadians(m_pitch)) *
                            (float)Math.Cos(MathHelper.DegreesToRadians(m_yaw));
            m_direction.Y = (float)Math.Sin(MathHelper.DegreesToRadians(m_pitch));
            m_direction.Z = (float)Math.Cos(MathHelper.DegreesToRadians(m_pitch)) *
                            (float)Math.Sin(MathHelper.DegreesToRadians(m_yaw));
            m_direction = Vector3.Normalize(m_direction);

            m_isLookAtChanged = true;
        }
    }

    public float FOV
    {
        get => m_fov;
        set => m_fov = MathHelper.Clamp(value, 20, 90);
    }

    private void ChangedLookAt()
    {
        m_lookAt = Matrix4.LookAt(Position, Position + m_direction, m_cameraUp);
        m_isLookAtChanged = false;
    }
}