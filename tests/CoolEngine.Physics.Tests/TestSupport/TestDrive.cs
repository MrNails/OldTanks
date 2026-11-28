using System.Numerics;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Today's game rules around the library's phase calls, for one world, on the forward component of the world velocity
/// (the forward axis <c>f</c> is Column0 of <see cref="TestBodies.Frame"/> for the body's heading): the forward damping
/// and snap (<c>RigidBody.OnTick</c>) before the acceleration and the forward clamp (the <c>RigidBody.Velocity</c>
/// setter) after it; static bodies skipped. The same rules as the parity tests' WorldSpaceDrive, without heading
/// changes and local forces; also used by the benchmarks (linked).
/// </summary>
internal sealed class TestDrive
{
    private static readonly Vector3 s_defaultForward = TestBodies.Frame(0).Column0;

    private Body[] m_bodies = new Body[16];

    /// <summary>
    /// Sets one body's forward limits and heading. Throws <see cref="ArgumentException"/> unless
    /// <c>maxSpeed × multiplier ≥ −maxBackSpeed</c>: outside that domain today's setter, which clamps up to three times
    /// per sub-step, is not idempotent and the drive's single clamp would differ.
    /// </summary>
    public void SetLimits(BodyHandle handle, float maxSpeed, float maxBackSpeed, float multiplier, float heading = 0)
    {
        if (!(maxSpeed * multiplier >= -maxBackSpeed))
            throw new ArgumentException(
                $"The limits {maxSpeed} × {multiplier} < −{maxBackSpeed} are outside the domain the drive reproduces exactly.",
                nameof(maxSpeed));

        if (handle.Slot >= m_bodies.Length)
            Array.Resize(ref m_bodies, Math.Max(m_bodies.Length * 2, handle.Slot + 1));

        m_bodies[handle.Slot] = new Body(handle, maxSpeed, maxBackSpeed, multiplier, TestBodies.Frame(heading).Column0);
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

            var forward = GetBody(handle).Forward;
            var velocity = world.GetVelocity(handle);
            var speed = Vector3.Dot(velocity, forward);
            var damped = speed - speed * 0.6f * subStepTime;

            if (damped is > -0.01f and < 0.01f)
                damped = 0;

            world.SetVelocity(handle, velocity + (damped - speed) * forward);
        }

        world.IntegrateVelocities(subStepTime);

        foreach (var handle in world)
        {
            if (world.IsStatic(handle))
                continue;

            var body = GetBody(handle);
            var velocity = world.GetVelocity(handle);
            var speed = Vector3.Dot(velocity, body.Forward);
            var clamped = speed > body.MaxSpeed * body.Multiplier
                ? body.MaxSpeed * body.Multiplier
                : speed < -body.MaxBackSpeed ? -body.MaxBackSpeed : speed;

            world.SetVelocity(handle, velocity + (clamped - speed) * body.Forward);
        }

        world.ClampTerminalSpeed();

        if (isLastSubStep)
            world.ClearForces();

        world.IntegratePositions(subStepTime);
        world.ResolveContacts();
    }

    // A body without limits has today's RigidBody defaults (0, 0 and 0) and heading 0.
    private Body GetBody(BodyHandle handle) =>
        handle.Slot < m_bodies.Length && m_bodies[handle.Slot].Handle == handle
            ? m_bodies[handle.Slot]
            : new Body(handle, 0, 0, 0, s_defaultForward);

    private readonly record struct Body(BodyHandle Handle, float MaxSpeed, float MaxBackSpeed, float Multiplier,
        Vector3 Forward);
}
