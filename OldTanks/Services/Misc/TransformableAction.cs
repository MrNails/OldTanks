using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.Services.Misc;

public readonly struct TransformableAction
{
    public TransformableAction(ITransformable transformable, Vector3 positionDelta, float timeDelta)
    {
        Transformable = transformable;
        PositionDelta = positionDelta;
        TimeDelta = timeDelta;
    }

    public ITransformable Transformable { get; }
    public Vector3 PositionDelta { get; }
    
    public float TimeDelta { get; }
}