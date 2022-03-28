namespace CoolEngine.GraphicalEngine.Core;

public class RigidBody
{
    private float m_speed;
    
    public float Acceleration { get; set; }
    public float AccelerationTime { get; set; }
    public float MaxSpeed { get; set; }
    public float MaxSpeedMultiplier { get; set; }
    public float Weight { get; set; }
    
    public float Speed
    {
        get => m_speed;
        set
        {
            if (value < m_speed && value >= 0 || 
                value <= MaxSpeed * MaxSpeedMultiplier)
                m_speed = value;
            else
                m_speed = MaxSpeed * MaxSpeedMultiplier;
        }
    }
}