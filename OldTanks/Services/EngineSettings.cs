using Common.Models;
using OpenTK.Mathematics;

namespace OldTanks.Services;

public sealed class EngineSettings : ObservableObject
{
    private int m_maxDepthLength;
    private Vector3 m_movementDirectionUnit;

    public EngineSettings()
    {
        MaxDepthLength = 2000;
        MovementDirectionUnit = Vector3.UnitX;
    }
    
    public int MaxDepthLength
    {
        get => m_maxDepthLength;
        set => SetField(ref m_maxDepthLength, value);
    }

    public Vector3 MovementDirectionUnit
    {
        get => m_movementDirectionUnit;
        set => SetField(ref m_movementDirectionUnit, value);
    }
}