using CoolEngine.Services;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class RigidBody
{
    public static readonly float MinDensity = 0.5f;    
    public static readonly float MaxDensity = 21.4f;

    private float m_speed;
    private bool m_onGround;
    private float m_density;

    public float Acceleration { get; set; }
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
    
    public float VerticalSpeed { get; set; }
    public float DefaultJumpForce { get; set; }

    public float BreakMultiplier { get; set; }

    /// <summary>
    /// Represent on how many degree rotate object per 1 second
    /// </summary>
    public float Rotation { get; set; }

    public float Weight { get; set; }

    public bool OnGround
    {
        get => m_onGround;
        set
        {
            m_onGround = value;

            if (value)
                VerticalSpeed = 0;
        }
    }

    public bool IsStatic { get; set; }

    public float Speed
    {
        get => m_speed;
        set
        {
            if (value >= MaxSpeed * MaxSpeedMultiplier)
                m_speed = MaxSpeed * MaxSpeedMultiplier;
            else if (value <= -MaxBackSpeed * MaxSpeedMultiplier)
                m_speed = -MaxBackSpeed * MaxSpeedMultiplier;
            else
                m_speed = value;
        }
    }

    public void OnTick(float timeDelta)
    {
        if (IsStatic)
            return;

        if (!GlobalSettings.PhysicsEnable && VerticalSpeed < PhysicsConstants.MaxFreeFallingSpeed)
            VerticalSpeed += PhysicsConstants.g * timeDelta;
    }
}