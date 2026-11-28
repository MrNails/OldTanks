using System.Numerics;
using CoolEngine.Physics;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Today's game rules around CoolEngine.Physics' phase calls (stage 1, transitional until OldTanks.Simulation exists):
/// the forward damping and snap of <c>RigidBody.OnTick</c> before the acceleration and the forward clamp of the
/// <c>RigidBody.Velocity</c> setter after it, on the velocity's X in the body's velocity frame; static bodies skipped.
/// </summary>
internal sealed class StageOneDrive
{
    private Limits[] m_limits = new Limits[16];

    /// <summary>
    /// Sets one body's limits (today's MaxSpeed, MaxBackSpeed, MaxSpeedMultiplier). Throws
    /// <see cref="ArgumentException"/> unless <c>maxSpeed × multiplier ≥ −maxBackSpeed</c>: outside that domain today's
    /// setter, which clamps up to three times per sub-step, is not idempotent, and this drive's single clamp would
    /// differ.
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

    /// <summary>Today's <c>RigidBody.Velocity</c> setter's clamp of X to [−maxBackSpeed, maxSpeed × multiplier].</summary>
    public static Vector3 ClampForward(Vector3 velocity, float maxSpeed, float maxBackSpeed, float multiplier)
    {
        if (velocity.X > maxSpeed * multiplier)
            velocity.X = maxSpeed * multiplier;
        else if (velocity.X < -maxBackSpeed)
            velocity.X = -maxBackSpeed;

        return velocity;
    }

    /// <summary>One tick: <c>SubStepCount</c> sub-steps of <c>timeStep / SubStepCount</c>, the force cleared on the last.</summary>
    public void RunTick(PhysicsWorld world, float timeStep)
    {
        var subStepCount = world.Settings.SubStepCount;
        var subStepTime = timeStep / subStepCount;

        for (var subStep = 0; subStep < subStepCount; subStep++)
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
                world.SetVelocity(handle,
                    ClampForward(world.GetVelocity(handle), limits.MaxSpeed, limits.MaxBackSpeed, limits.Multiplier));
            }

            world.ClampTerminalSpeed();

            if (subStep == subStepCount - 1)
                world.ClearForces();

            world.IntegratePositions(subStepTime);
            world.ResolveContacts();
        }
    }

    // A body without limits has today's RigidBody defaults: 0, 0 and 0.
    private Limits GetLimits(BodyHandle handle) =>
        handle.Slot < m_limits.Length && m_limits[handle.Slot].Handle == handle ? m_limits[handle.Slot] : default;

    private readonly record struct Limits(BodyHandle Handle, float MaxSpeed, float MaxBackSpeed, float Multiplier);
}
