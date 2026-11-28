using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Today's game rules around CoolEngine.Physics' phase calls with a world-space velocity (stage 2; test code until
/// OldTanks.Simulation exists, then moved there). Each body's heading <c>ψ</c> (today's Rotation.Y) gives the frame
/// <c>H(ψ) = Matrix3.CreateRotationY(ψ).Transposed()</c> that today's <c>WorldObject.Move</c> applies to the local
/// velocity, and its forward axis <c>f = H(ψ).Column0</c>. Today's rules act on the forward component <c>s = v · f</c>:
/// the damping and snap of <c>RigidBody.OnTick</c> before <see cref="PhysicsWorld.IntegrateVelocities"/>, the forward
/// clamp of the <c>RigidBody.Velocity</c> setter after it and before <see cref="PhysicsWorld.ClampTerminalSpeed"/>;
/// static bodies skipped. A force or a velocity given in today's local terms is turned into world space with
/// <c>H(ψ)</c>, and a heading change turns the stored velocity: <c>v ← H(ψ₁) · (H(ψ₀)ᵀ · v)</c>. Each of these equals
/// today's rule in exact arithmetic (the plan's §9), not by bits.
/// </summary>
internal sealed class WorldSpaceDrive
{
    private Body[] m_bodies = new Body[16];

    /// <summary>
    /// The game's conversion of today's Euler angles (X yaw, Y pitch, Z roll, radians) to the orientation:
    /// <c>qZ(roll) × (qY(−pitch) × qX(yaw))</c>, today's rotation Z × rotation Y (−pitch) × rotation X.
    /// </summary>
    public static Quaternion ToOrientation(Vector3 rotation) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation.Z) *
        (Quaternion.CreateFromAxisAngle(Vector3.UnitY, -rotation.Y) *
         Quaternion.CreateFromAxisAngle(Vector3.UnitX, rotation.X));

    /// <summary>The half-extents of today's size: <c>size / 2</c>, today's scale (Width / 2, Height / 2, Length / 2).</summary>
    public static Vector3 ToHalfExtents(Vector3 size) => size / 2;

    /// <summary>Today's <c>WorldObject.Move</c> frame for heading <paramref name="heading"/>: local velocity to world.</summary>
    public static Matrix3 Frame(float heading) => Matrix3.CreateRotationY(heading).Transposed();

    /// <summary>Today's <c>RigidBody.Velocity</c> setter's clamp of the local X to [−maxBackSpeed, maxSpeed × multiplier].</summary>
    public static Vector3 ClampForward(Vector3 velocity, float maxSpeed, float maxBackSpeed, float multiplier)
    {
        if (velocity.X > maxSpeed * multiplier)
            velocity.X = maxSpeed * multiplier;
        else if (velocity.X < -maxBackSpeed)
            velocity.X = -maxBackSpeed;

        return velocity;
    }

    /// <summary>
    /// Registers a body that was just added (or replaces what its slot held): its limits (today's MaxSpeed,
    /// MaxBackSpeed, MaxSpeedMultiplier) and heading, no pending force. Throws <see cref="ArgumentException"/> unless
    /// <c>maxSpeed × multiplier ≥ −maxBackSpeed</c>: outside that domain today's setter, which clamps up to three times
    /// per sub-step, is not idempotent, and this drive's single clamp is not today's rule even in exact arithmetic.
    /// </summary>
    public void Register(BodyHandle handle, float maxSpeed, float maxBackSpeed, float multiplier, float heading)
    {
        RequireDomain(maxSpeed, maxBackSpeed, multiplier);

        Store(new Body(handle, maxSpeed, maxBackSpeed, multiplier, heading, Frame(heading), Vector3.Zero, false));
    }

    /// <summary>Changes a registered body's limits; the same domain rule as <see cref="Register"/>.</summary>
    public void SetLimits(BodyHandle handle, float maxSpeed, float maxBackSpeed, float multiplier)
    {
        RequireDomain(maxSpeed, maxBackSpeed, multiplier);
        var body = GetBody(handle);

        Store(body with { MaxSpeed = maxSpeed, MaxBackSpeed = maxBackSpeed, Multiplier = multiplier });
    }

    /// <summary>
    /// A velocity written in today's local terms (the debug window, a spawn): today's setter clamps the local X with the
    /// body's limits; the result is turned into world space, <c>v = H(ψ) · u</c>. Ignored for a static body, as today's
    /// setter ignores it.
    /// </summary>
    public void SetLocalVelocity(PhysicsWorld world, BodyHandle handle, Vector3 velocity)
    {
        if (world.IsStatic(handle))
            return;

        world.SetVelocity(handle, ToWorldVelocity(handle, velocity));
    }

    /// <summary>A velocity in today's local terms as the world velocity the library stores: <c>H(ψ) · clamp(u)</c>.</summary>
    public Vector3 ToWorldVelocity(BodyHandle handle, Vector3 velocity)
    {
        var body = GetBody(handle);

        return ToWorldVelocity(velocity, body.MaxSpeed, body.MaxBackSpeed, body.Multiplier, body.Heading);
    }

    /// <summary>
    /// The same conversion for a body not registered yet (a spawn's initial velocity): today's setter clamps the local X
    /// with the given limits, and the result is turned into world space with the heading, <c>H(ψ) · clamp(u)</c>.
    /// </summary>
    public static Vector3 ToWorldVelocity(Vector3 velocity, float maxSpeed, float maxBackSpeed, float multiplier,
        float heading) =>
        Frame(heading) * ClampForward(velocity, maxSpeed, maxBackSpeed, multiplier);

    /// <summary>
    /// A force in today's local terms (<c>Force += …</c>): summed in local terms as today does, and turned into world
    /// space with the heading the next tick integrates it under (today's force is local, so a heading change before the
    /// tick turns it too). Ignored for a static body, as today's setter ignores it; a sum pending when the body is made
    /// static waits until it is dynamic again, as today's frozen force does.
    /// </summary>
    public void AddLocalForce(PhysicsWorld world, BodyHandle handle, Vector3 force)
    {
        if (world.IsStatic(handle))
            return;

        var body = GetBody(handle);
        Store(body with { PendingForce = body.PendingForce + force, HasPendingForce = true });
    }

    /// <summary>
    /// A new heading (today's Rotation.Y): the stored velocity of a dynamic body is turned with it,
    /// <c>v ← H(ψ₁) · (H(ψ₀)ᵀ · v)</c>; nothing happens when the heading is unchanged. A static body's velocity cannot be
    /// set, so it stays in the old frame.
    /// </summary>
    public void SetHeading(PhysicsWorld world, BodyHandle handle, float heading)
    {
        var body = GetBody(handle);

        if (BitConverter.SingleToUInt32Bits(heading) == BitConverter.SingleToUInt32Bits(body.Heading))
            return;

        var frame = Frame(heading);

        if (!world.IsStatic(handle))
            world.SetVelocity(handle, frame * (body.Frame.Transposed() * world.GetVelocity(handle)));

        Store(body with { Heading = heading, Frame = frame });
    }

    /// <summary>
    /// One tick: the pending local forces turned into world forces (<c>H(ψ) · F</c>) on the dynamic bodies, then
    /// <c>SubStepCount</c> sub-steps of <c>timeStep / SubStepCount</c>: the damping, <c>IntegrateVelocities</c>, the
    /// forward clamp, <c>ClampTerminalSpeed</c>, on the last sub-step <c>ClearForces</c>, <c>IntegratePositions</c>,
    /// <c>ResolveContacts</c>.
    /// </summary>
    public void RunTick(PhysicsWorld world, float timeStep)
    {
        foreach (var handle in world)
        {
            var body = GetBody(handle);

            if (!body.HasPendingForce || world.IsStatic(handle))
                continue;

            world.AddForce(handle, body.Frame * body.PendingForce);
            Store(body with { PendingForce = Vector3.Zero, HasPendingForce = false });
        }

        var subStepCount = world.Settings.SubStepCount;
        var subStepTime = timeStep / subStepCount;

        for (var subStep = 0; subStep < subStepCount; subStep++)
        {
            foreach (var handle in world)
            {
                if (world.IsStatic(handle))
                    continue;

                var forward = GetBody(handle).Frame.Column0;
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
                var forward = body.Frame.Column0;
                var velocity = world.GetVelocity(handle);
                var speed = Vector3.Dot(velocity, forward);
                var clamped = speed > body.MaxSpeed * body.Multiplier
                    ? body.MaxSpeed * body.Multiplier
                    : speed < -body.MaxBackSpeed ? -body.MaxBackSpeed : speed;

                world.SetVelocity(handle, velocity + (clamped - speed) * forward);
            }

            world.ClampTerminalSpeed();

            if (subStep == subStepCount - 1)
                world.ClearForces();

            world.IntegratePositions(subStepTime);
            world.ResolveContacts();
        }
    }

    private static void RequireDomain(float maxSpeed, float maxBackSpeed, float multiplier)
    {
        if (!(maxSpeed * multiplier >= -maxBackSpeed))
            throw new ArgumentException(
                $"The limits {maxSpeed} × {multiplier} < −{maxBackSpeed} are outside the domain the drive reproduces.",
                nameof(maxSpeed));
    }

    private void Store(Body body)
    {
        if (body.Handle.Slot >= m_bodies.Length)
            Array.Resize(ref m_bodies, Math.Max(m_bodies.Length * 2, body.Handle.Slot + 1));

        m_bodies[body.Handle.Slot] = body;
    }

    // A body never registered has today's RigidBody defaults: limits 0, 0 and 0, heading 0.
    private Body GetBody(BodyHandle handle) =>
        handle.Slot < m_bodies.Length && m_bodies[handle.Slot].Handle == handle
            ? m_bodies[handle.Slot]
            : new Body(handle, 0, 0, 0, 0, Frame(0), Vector3.Zero, false);

    private readonly record struct Body(BodyHandle Handle, float MaxSpeed, float MaxBackSpeed, float Multiplier,
        float Heading, Matrix3 Frame, Vector3 PendingForce, bool HasPendingForce);
}
