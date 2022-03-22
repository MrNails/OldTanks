using OpenTK.Mathematics;

namespace OldTanks.Services;

public class Settings
{
    private float m_sensitivity;
    
    public float Sensitivity
    {
        get => m_sensitivity;
        set => m_sensitivity = MathHelper.Clamp(value, 0, 10);
    }
}