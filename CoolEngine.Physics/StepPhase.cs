namespace CoolEngine.Physics;

/// <summary>Where a <see cref="PhysicsWorld"/> is inside one sub-step; <see cref="Idle"/> between sub-steps.</summary>
public enum StepPhase
{
    /// <summary>Between sub-steps; next: <see cref="PhysicsWorld.IntegrateVelocities"/>.</summary>
    Idle,

    /// <summary>Force and gravity added; next: <see cref="PhysicsWorld.ClampTerminalSpeed"/>.</summary>
    VelocitiesIntegrated,

    /// <summary>Terminal speed applied; next: <see cref="PhysicsWorld.IntegratePositions"/>.</summary>
    TerminalSpeedClamped,

    /// <summary>Bodies moved and their geometry rebuilt; next: <see cref="PhysicsWorld.ResolveContacts"/>.</summary>
    PositionsIntegrated
}
