using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class RigidBody
{
    public static readonly float MinDensity = 0.5f;    
    public static readonly float MaxDensity = 21.4f;

    private float m_speed;
    private bool m_onGround;
    private float m_density;
    private float m_verticalSpeed;
    private Vector3 m_velocity;

    private Vector3 m_force;
    private float m_weight;

    public RigidBody()
    {
        Weight = 1;
        Restitution = float.MaxValue;
    }
    
    public float MaxSpeed { get; set; }
    public float MaxBackSpeed { get; set; }
    public float MaxSpeedMultiplier { get; set; }

    //Плотность
    public float Density
    {
        get => m_density;
        set => m_density = Math.Clamp(value, MinDensity, MaxDensity);
    }

    //Восстановление
    public float Restitution { get; set; }

    public Vector3 Velocity
    {
        get => m_velocity;
        set
        {
            if (IsStatic)
                return;

            if (value.X > MaxSpeed * MaxSpeedMultiplier)
                value.X = MaxSpeed * MaxSpeedMultiplier;
            else if (value.X < -MaxBackSpeed)
                value.X = -MaxBackSpeed;
            
            m_velocity = value;
        }
    }

    public Vector3 Force
    {
        get => m_force;
        set
        {
            if (IsStatic)
                return;
            
            m_force = value;
        }
    }

    public float DefaultJumpForce { get; set; }
    
    public Vector3 CenterOfMass { get; set; }

    public float Weight
    {
        get => m_weight;
        set
        {
            if (value <= 0)
                throw new ArgumentException("Weight cannot be less or equal 0");
            
            m_weight = value;
        }
    }

    public bool OnGround
    {
        get => m_onGround;
        set => m_onGround = value;
    }

    public bool IsStatic { get; set; }
    
    public virtual void OnTick(float timeDelta, int collisionIteration = 1)
    {
        if (IsStatic)
            return;

        Velocity += (Force / Weight + PhysicsConstants.GravityDirection * PhysicsConstants.FreeFallingAcceleration) * timeDelta;

        if (collisionIteration == -1 || collisionIteration == GlobalSettings.CollisionIterations - 1)
        {
            Force = Vector3.Zero;
        }
    }
}