namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Today's game rules around the library's phase calls, for one world: the forward damping and snap
/// (<c>RigidBody.OnTick</c>) before the acceleration and the forward clamp (the <c>RigidBody.Velocity</c> setter) after
/// it, on the velocity's X in the body's velocity frame; static bodies skipped. The same shape as the parity tests'
/// StageOneDrive; also used by the benchmarks (linked).
/// </summary>
internal sealed class TestDrive
{
    private Limits[] m_limits = new Limits[16];

    /// <summary>
    /// Sets one body's forward limits. Throws <see cref="ArgumentException"/> unless
    /// <c>maxSpeed × multiplier ≥ −maxBackSpeed</c>: outside that domain today's setter, which clamps up to three times
    /// per sub-step, is not idempotent and the drive's single clamp would differ.
    /// </summary>
    public void SetLimits(BodyHandle handle, float maxSpeed, float maxBackSpeed, float multiplier)
    {
        if (!(maxSpeed * multiplier >= -maxBackSpeed))
            throw new ArgumentException(
                $"The limits {maxSpeed} × {multiplier} < −{maxBackSpeed} are outside the domain the drive reproduces exactly.",
                nameof(maxSpeed));

        if (handle.Slot >= m_limits.Length)
            Array.Resize(ref m_limits, Math.Max(m_limits.Length * 2, handle.Slot + 1));

        m_limits[handle.Slot] = new Limits(handle, maxSpeed, maxBackSpeed, multiplier);
    }

    /// <summary>One tick: <see cref="RunSubStep"/> <c>SubStepCount</c> times with <c>timeStep / SubStepCount</c>.</summary>
    public void RunTick(PhysicsWorld world, float timeStep)
    {
        var subStepCount = world.Settings.SubStepCount;
        var subStepTime = timeStep / subStepCount;

        for (var subStep = 0; subStep < subStepCount; subStep++)
            RunSubStep(world, subStepTime, subStep == subStepCount - 1);
    }

    public void RunSubStep(PhysicsWorld world, float subStepTime, bool isLastSubStep)
    {
        foreach (var handle in world)
        {
            if (world.IsStatic(handle))
                continue;

            var velocity = world.GetVelocity(handle);
            velocity.X -= velocity.X * 0.6f * subStepTime;

            if (velocity.X is > -0.01f and < 0.01f)
                velocity.X = 0;

            world.SetVelocity(handle, velocity);
        }

        world.IntegrateVelocities(subStepTime);

        foreach (var handle in world)
        {
            if (world.IsStatic(handle))
                continue;

            var limits = GetLimits(handle);
            var velocity = world.GetVelocity(handle);

            if (velocity.X > limits.MaxSpeed * limits.Multiplier)
                velocity.X = limits.MaxSpeed * limits.Multiplier;
            else if (velocity.X < -limits.MaxBackSpeed)
                velocity.X = -limits.MaxBackSpeed;

            world.SetVelocity(handle, velocity);
        }

        world.ClampTerminalSpeed();

        if (isLastSubStep)
            world.ClearForces();

        world.IntegratePositions(subStepTime);
        world.ResolveContacts();
    }

    // A body without limits has today's RigidBody defaults: 0, 0 and 0.
    private Limits GetLimits(BodyHandle handle) =>
        handle.Slot < m_limits.Length && m_limits[handle.Slot].Handle == handle ? m_limits[handle.Slot] : default;

    private readonly record struct Limits(BodyHandle Handle, float MaxSpeed, float MaxBackSpeed, float Multiplier);
}
