using GraphicalEngine.Core;
using GraphicalEngine.Services.Exceptions;
using GraphicalEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public abstract class WorldObject : IDrawable
{
    private Scene m_scene;
    private Vector3 m_size;
    private Vector3 m_position;
    private Vector3 m_direction;
    
    protected Matrix4 m_transform;

    protected WorldObject(Scene scene)
    {
        var currType = GetType();
        
        m_scene = scene ??
                  throw new ObjectException(currType, $"Cannot create {currType.Name}. Scene is not exists"); 
    }
    
    public Scene Scene => m_scene;

    public Vector3 Size
    {
        get => m_size;
        set => m_size = value;
    }

    public Vector3 Position
    {
        get => m_position;
        set => m_position = value;
    }

    public Vector3 Direction
    {
        get => m_direction;
        set => m_direction = value;
    }

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

    public float Pitch
    {
        get => m_direction.X;
        set => m_direction.X = value;
    }

    public float Yaw
    {
        get => m_direction.Y;
        set => m_direction.Y = value;
    }

    public float Roll
    {
        get => m_direction.Z;
        set => m_direction.Z = value;
    }

    public Matrix4 Transform => m_transform;

    public virtual void AcceptTransform()
    {
        m_transform = Matrix4.CreateScale(Width / 2, Height / 2, Length / 2) *
                      Matrix4.CreateRotationX(MathHelper.DegreesToRadians(Pitch)) *
                      Matrix4.CreateRotationY(MathHelper.DegreesToRadians(Yaw)) *
                      Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Roll)) *
                      Matrix4.CreateTranslation(Position);
    }
}