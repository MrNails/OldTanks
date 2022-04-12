using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class RigidBody
{
    private float m_speed;
    
    public float Acceleration { get; set; }
    public float MaxSpeed { get; set; }
    public float MaxBackSpeed { get; set; }
    public float MaxSpeedMultiplier { get; set; }
    
    public float VerticalForce { get; set; }
    public float DefaultJumpForce { get; set; }
    
    public float BreakMultiplier { get; set; }

    /// <summary>
    /// Represent on how many degree rotate object per 1 second
    /// </summary>
    public float Rotation { get; set; }

    public float Weight { get; set; }
    
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
}