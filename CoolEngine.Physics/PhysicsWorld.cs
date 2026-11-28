using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Detection;
using CoolEngine.Physics.Internal;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics;

/// <summary>
/// The bodies of one world and the sub-stepped step that moves them: today's integration, terminal speed, detection,
/// position correction, ground flag and vertical velocity cancelling, with today's arithmetic in today's order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threads:</b> constructed on any thread, then used by one thread at a time (the simulation thread). There is no
/// lock and concurrent use is not detected.
/// </para>
/// <para>
/// <b>Handles:</b> a <see cref="BodyHandle"/> is valid only for the world that returned it. Using it with another world
/// is not detected: it addresses whatever body that world holds in the same slot with the same generation.
/// </para>
/// <para>
/// <b>Storage:</b> one array per field, indexed by slot. A removed body's slot is reused by the next <see cref="Add"/>,
/// the last freed slot first, so after a removal and an add the new body takes the freed slot and the pair order
/// differs from an appended list. Every loop walks the slots in order.
/// </para>
/// <para>
/// <b>The step</b> is a sequence of public phase calls, which <see cref="Step"/> makes in order and a game can make
/// itself to run its own rules between them. One sub-step: <see cref="IntegrateVelocities"/>,
/// <see cref="ClampTerminalSpeed"/>, on the last sub-step <see cref="ClearForces"/>, <see cref="IntegratePositions"/>,
/// <see cref="ResolveContacts"/>. The order is enforced: a phase call in the wrong <see cref="Phase"/> throws
/// <see cref="InvalidOperationException"/>, and <see cref="AbortStep"/> returns to <see cref="StepPhase.Idle"/> after
/// an exception inside a sub-step. <see cref="SetVelocity"/>, <see cref="SetForce"/> and <see cref="AddForce"/> are
/// allowed until the positions are integrated; <see cref="ClearForces"/> and <see cref="AbortStep"/> in any phase; every
/// other mutator only in <see cref="StepPhase.Idle"/>.
/// </para>
/// <para>
/// Stage 1 (transitional): a body's pose is a position, today's linear part (rotation × scale) and size, and its
/// velocity and force are in its velocity frame (today's local frame), as in <see cref="BodyDescription"/>.
/// </para>
/// </remarks>
public sealed class PhysicsWorld
{
    private PhysicsSettings m_settings;
    private StepPhase m_phase;

    private int m_capacity;
    private int m_slotCount;
    private int m_count;
    private int m_version;

    private Vector3[] m_positions;
    private Vector3[] m_velocities;
    private Vector3[] m_forces;
    private Matrix3[] m_linears;
    private Matrix3[] m_velocityFrames;
    private Vector3[] m_sizes;
    private float[] m_masses;
    private bool[] m_isStatic;
    private bool[] m_onGround;
    private bool[] m_alive;
    private int[] m_generations;
    private Shape?[] m_shapes;
    private Vector3[] m_boundsMin;
    private Vector3[] m_boundsMax;

    // Each slot's world geometry, sized to the largest polygon the slot has held and kept for reuse after a removal.
    private Vector3[]?[] m_worldVertices;
    private Vector3[]?[] m_worldNormals;

    // A stack of free slots: the last freed is reused first.
    private int[] m_freeSlots;
    private int m_freeCount;

    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is negative.</exception>
    public PhysicsWorld(PhysicsSettings settings, int initialCapacity = 16)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        m_settings = settings;
        m_capacity = Math.Max(initialCapacity, 1);

        m_positions = new Vector3[m_capacity];
        m_velocities = new Vector3[m_capacity];
        m_forces = new Vector3[m_capacity];
        m_linears = new Matrix3[m_capacity];
        m_velocityFrames = new Matrix3[m_capacity];
        m_sizes = new Vector3[m_capacity];
        m_masses = new float[m_capacity];
        m_isStatic = new bool[m_capacity];
        m_onGround = new bool[m_capacity];
        m_alive = new bool[m_capacity];
        m_generations = new int[m_capacity];
        m_shapes = new Shape?[m_capacity];
        m_boundsMin = new Vector3[m_capacity];
        m_boundsMax = new Vector3[m_capacity];
        m_worldVertices = new Vector3[]?[m_capacity];
        m_worldNormals = new Vector3[]?[m_capacity];
        m_freeSlots = new int[m_capacity];
    }

    /// <summary>The constants of the step. The setter needs <see cref="StepPhase.Idle"/>; the next sub-step uses the new value.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">Set outside <see cref="StepPhase.Idle"/>.</exception>
    public PhysicsSettings Settings
    {
        get => m_settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireIdle(nameof(Settings));

            m_settings = value;
        }
    }

    /// <summary>Where the world is inside one sub-step; <see cref="StepPhase.Idle"/> between sub-steps.</summary>
    public StepPhase Phase => m_phase;

    /// <summary>The live bodies.</summary>
    public int Count => m_count;

    /// <summary>One past the highest slot ever used; slots 0 … SlotCount − 1, some of them free. Never shrinks.</summary>
    public int SlotCount => m_slotCount;

    #region Bodies

    /// <summary>
    /// Adds a body: in the last freed slot, else the next new one (every field array doubles when full), and computes
    /// its world geometry. Allocates only when growing or when the slot's geometry caches are smaller than the shape.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// (<c>ParamName "description"</c>) The shape is null, a component of the position, linear part, size, velocity
    /// frame, velocity or force is not finite, or the mass is not finite or not positive.
    /// </exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public BodyHandle Add(in BodyDescription description)
    {
        RequireIdle(nameof(Add));
        Validate(description);

        int slot;
        if (m_freeCount > 0)
        {
            slot = m_freeSlots[--m_freeCount];
        }
        else
        {
            if (m_slotCount == m_capacity)
                Grow();

            slot = m_slotCount++;
            m_generations[slot] = 1;
        }

        m_shapes[slot] = description.Shape;
        m_positions[slot] = description.Position;
        m_linears[slot] = description.Linear;
        m_sizes[slot] = description.Size;
        m_velocityFrames[slot] = description.VelocityFrame;
        m_velocities[slot] = description.Velocity;
        m_forces[slot] = description.Force;
        m_masses[slot] = description.Mass;
        m_isStatic[slot] = description.IsStatic;
        m_onGround[slot] = false;
        m_alive[slot] = true;

        if (description.Shape is PolygonShape polygon)
        {
            if ((m_worldVertices[slot]?.Length ?? -1) < polygon.Vertices.Length)
                m_worldVertices[slot] = new Vector3[polygon.Vertices.Length];

            if ((m_worldNormals[slot]?.Length ?? -1) < polygon.FaceCount)
                m_worldNormals[slot] = new Vector3[polygon.FaceCount];
        }

        m_count++;
        m_version++;

        UpdateGeometry(slot);

        return new BodyHandle(slot, m_generations[slot]);
    }

    /// <summary>
    /// Removes a body: its slot's generation goes up by one (from <c>int.MaxValue</c> to 1, never 0), so the handle is
    /// rejected from now on; the slot keeps its geometry caches for reuse. Never allocates.
    /// </summary>
    /// <exception cref="ArgumentException">The handle is not a live body of this world.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void Remove(BodyHandle handle)
    {
        RequireIdle(nameof(Remove));
        var slot = Slot(handle);

        m_alive[slot] = false;
        m_shapes[slot] = null;
        m_generations[slot] = m_generations[slot] == int.MaxValue ? 1 : m_generations[slot] + 1;
        m_freeSlots[m_freeCount++] = slot;
        m_count--;
        m_version++;
    }

    /// <summary>Whether the handle is a live body of this world. Never throws.</summary>
    public bool Contains(BodyHandle handle) =>
        (uint)handle.Slot < (uint)m_slotCount && m_alive[handle.Slot] &&
        m_generations[handle.Slot] == handle.Generation;

    /// <summary>The live body in <paramref name="slot"/>; false for a free slot or a slot outside 0 … SlotCount − 1.</summary>
    public bool TryGetHandle(int slot, out BodyHandle handle)
    {
        if ((uint)slot < (uint)m_slotCount && m_alive[slot])
        {
            handle = new BodyHandle(slot, m_generations[slot]);
            return true;
        }

        handle = default;
        return false;
    }

    /// <summary>The live bodies in slot order, without allocating (<c>foreach</c>).</summary>
    public BodyEnumerator GetEnumerator() => new BodyEnumerator(this);

    /// <summary>
    /// Enumerates the live bodies in slot order. <see cref="MoveNext"/> throws <see cref="InvalidOperationException"/>
    /// once a body was added or removed after the enumerator was made.
    /// </summary>
    public struct BodyEnumerator
    {
        private readonly PhysicsWorld? m_world;
        private readonly int m_version;
        private int m_slot;

        internal BodyEnumerator(PhysicsWorld world)
        {
            m_world = world;
            m_version = world.m_version;
            m_slot = -1;
        }

        /// <summary>The current body; <c>default</c> before the first <see cref="MoveNext"/> and after the end.</summary>
        public readonly BodyHandle Current =>
            m_world == null || m_slot < 0 || m_slot >= m_world.m_slotCount
                ? default
                : new BodyHandle(m_slot, m_world.m_generations[m_slot]);

        public bool MoveNext()
        {
            if (m_world == null)
                return false;

            if (m_world.m_version != m_version)
                throw new InvalidOperationException("A body was added or removed during the enumeration.");

            while (++m_slot < m_world.m_slotCount)
            {
                if (m_world.m_alive[m_slot])
                    return true;
            }

            m_slot = m_world.m_slotCount;
            return false;
        }
    }

    #endregion

    #region Body state

    // The ! is safe: a live slot always holds its shape (Add sets it, only Remove clears it).
    public Shape GetShape(BodyHandle handle) => m_shapes[Slot(handle)]!;

    public Vector3 GetPosition(BodyHandle handle) => m_positions[Slot(handle)];

    /// <summary>Moves the body and recomputes its world geometry at once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetPosition(BodyHandle handle, Vector3 position)
    {
        RequireIdle(nameof(SetPosition));
        var slot = Slot(handle);
        RequireFinite(position, nameof(position));

        m_positions[slot] = position;
        UpdateGeometry(slot);
    }

    /// <summary>Stage 1 (transitional): today's rotation × scale.</summary>
    public Matrix3 GetLinear(BodyHandle handle) => m_linears[Slot(handle)];

    /// <summary>Stage 1 (transitional): sets today's rotation × scale and recomputes the world geometry at once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An element is not finite.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetLinear(BodyHandle handle, Matrix3 linear)
    {
        RequireIdle(nameof(SetLinear));
        var slot = Slot(handle);
        RequireFinite(linear, nameof(linear));

        m_linears[slot] = linear;
        UpdateGeometry(slot);
    }

    /// <summary>Stage 1 (transitional): today's size; a sphere's width is its X.</summary>
    public Vector3 GetSize(BodyHandle handle) => m_sizes[Slot(handle)];

    /// <summary>
    /// Stage 1 (transitional): sets today's size and recomputes the world geometry at once. Zero and negative
    /// components are allowed, as today (negative mirrors; zero gives NaN normals).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetSize(BodyHandle handle, Vector3 size)
    {
        RequireIdle(nameof(SetSize));
        var slot = Slot(handle);
        RequireFinite(size, nameof(size));

        m_sizes[slot] = size;
        UpdateGeometry(slot);
    }

    /// <summary>Stage 1 (transitional): the frame that turns the stored velocity into world space when positions move.</summary>
    public Matrix3 GetVelocityFrame(BodyHandle handle) => m_velocityFrames[Slot(handle)];

    /// <summary>Stage 1 (transitional): sets the velocity frame (<c>Matrix3.CreateRotationY(heading).Transposed()</c>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">An element is not finite.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetVelocityFrame(BodyHandle handle, Matrix3 velocityFrame)
    {
        RequireIdle(nameof(SetVelocityFrame));
        var slot = Slot(handle);
        RequireFinite(velocityFrame, nameof(velocityFrame));

        m_velocityFrames[slot] = velocityFrame;
    }

    /// <summary>
    /// The velocity in the body's velocity frame. A static body keeps the velocity it had when it was made static, and
    /// that value enters the contact rule's relative velocity (today's behaviour).
    /// </summary>
    public Vector3 GetVelocity(BodyHandle handle) => m_velocities[Slot(handle)];

    /// <summary>Sets the velocity, stored exactly as given. Allowed until the positions are integrated.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite.</exception>
    /// <exception cref="InvalidOperationException">The body is static, or the phase is <see cref="StepPhase.PositionsIntegrated"/>.</exception>
    public void SetVelocity(BodyHandle handle, Vector3 velocity)
    {
        RequireBeforePositions(nameof(SetVelocity));
        var slot = DynamicSlot(handle, nameof(SetVelocity));
        RequireFinite(velocity, nameof(velocity));

        m_velocities[slot] = velocity;
    }

    /// <summary>The force in the body's velocity frame; cleared by <see cref="ClearForces"/> (the last sub-step of a step).</summary>
    public Vector3 GetForce(BodyHandle handle) => m_forces[Slot(handle)];

    /// <summary>Sets the force. Allowed until the positions are integrated.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite.</exception>
    /// <exception cref="InvalidOperationException">The body is static, or the phase is <see cref="StepPhase.PositionsIntegrated"/>.</exception>
    public void SetForce(BodyHandle handle, Vector3 force)
    {
        RequireBeforePositions(nameof(SetForce));
        var slot = DynamicSlot(handle, nameof(SetForce));
        RequireFinite(force, nameof(force));

        m_forces[slot] = force;
    }

    /// <summary>Adds to the force: <c>force_old + force</c> (today's <c>Force += …</c>). Allowed until the positions are integrated.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A component of the argument or of the sum is not finite.</exception>
    /// <exception cref="InvalidOperationException">The body is static, or the phase is <see cref="StepPhase.PositionsIntegrated"/>.</exception>
    public void AddForce(BodyHandle handle, Vector3 force)
    {
        RequireBeforePositions(nameof(AddForce));
        var slot = DynamicSlot(handle, nameof(AddForce));
        RequireFinite(force, nameof(force));
        var sum = m_forces[slot] + force;
        RequireFinite(sum, nameof(force));

        m_forces[slot] = sum;
    }

    /// <summary>Today's <c>RigidBody.Weight</c>, used only as <c>Force / Mass</c>.</summary>
    public float GetMass(BodyHandle handle) => m_masses[Slot(handle)];

    /// <exception cref="ArgumentOutOfRangeException">The mass is not finite or not positive.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetMass(BodyHandle handle, float mass)
    {
        RequireIdle(nameof(SetMass));
        var slot = Slot(handle);

        if (!float.IsFinite(mass) || mass <= 0)
            throw new ArgumentOutOfRangeException(nameof(mass), mass, "The mass must be finite and positive.");

        m_masses[slot] = mass;
    }

    public bool IsStatic(BodyHandle handle) => m_isStatic[Slot(handle)];

    /// <summary>Makes the body static or dynamic; its velocity, force and ground flag stay as they are (today's behaviour).</summary>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetStatic(BodyHandle handle, bool isStatic)
    {
        RequireIdle(nameof(SetStatic));
        var slot = Slot(handle);

        m_isStatic[slot] = isStatic;
    }

    /// <summary>
    /// The last sub-step's ground flag: cleared at every sub-step for a dynamic body and set by a contact from the
    /// direction between the centres (today's rule); a static body keeps whatever it had. Read-only.
    /// </summary>
    public bool IsOnGround(BodyHandle handle) => m_onGround[Slot(handle)];

    public BodyState GetState(BodyHandle handle)
    {
        var slot = Slot(handle);

        return new BodyState(handle, m_positions[slot], m_linears[slot], m_sizes[slot], m_velocityFrames[slot],
            m_velocities[slot], m_forces[slot], m_masses[slot], m_isStatic[slot], m_onGround[slot]);
    }

    /// <summary>The cached world vertices (empty for a sphere); valid until the next mutation or sub-step.</summary>
    public ReadOnlySpan<Vector3> GetWorldVertices(BodyHandle handle) => WorldVertices(Slot(handle));

    /// <summary>The cached world face normals (empty for a sphere); valid until the next mutation or sub-step.</summary>
    public ReadOnlySpan<Vector3> GetWorldNormals(BodyHandle handle) => WorldNormals(Slot(handle));

    #endregion

    #region Step

    /// <summary>
    /// One step with no game rule: <c>subStepTime = timeStep / Settings.SubStepCount</c>, then per sub-step
    /// <see cref="IntegrateVelocities"/>, <see cref="ClampTerminalSpeed"/>, on the last sub-step
    /// <see cref="ClearForces"/>, <see cref="IntegratePositions"/> and <see cref="ResolveContacts"/>. Allocates nothing.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeStep"/> is not finite or negative.</exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void Step(float timeStep)
    {
        RequireTime(timeStep, nameof(timeStep));
        RequireIdle(nameof(Step));

        var subStepCount = m_settings.SubStepCount;
        var subStepTime = timeStep / subStepCount;

        for (var subStep = 0; subStep < subStepCount; subStep++)
        {
            IntegrateVelocities(subStepTime);
            ClampTerminalSpeed();

            if (subStep == subStepCount - 1)
                ClearForces();

            IntegratePositions(subStepTime);
            ResolveContacts();
        }
    }

    /// <summary>
    /// Adds force and gravity to every dynamic body's velocity, in slot order:
    /// <c>v + (force / mass + gravityDirection × gravityAcceleration) × subStepTime</c>. Needs
    /// <see cref="StepPhase.Idle"/>; leaves <see cref="StepPhase.VelocitiesIntegrated"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="subStepTime"/> is not finite or negative.</exception>
    /// <exception cref="InvalidOperationException">The phase is not <see cref="StepPhase.Idle"/>.</exception>
    public void IntegrateVelocities(float subStepTime)
    {
        RequireTime(subStepTime, nameof(subStepTime));
        RequirePhase(StepPhase.Idle, nameof(IntegrateVelocities));

        var gravityDirection = m_settings.GravityDirection;
        var gravityAcceleration = m_settings.GravityAcceleration;

        for (var slot = 0; slot < m_slotCount; slot++)
        {
            if (!m_alive[slot] || m_isStatic[slot])
                continue;

            m_velocities[slot] = m_velocities[slot] +
                                 (m_forces[slot] / m_masses[slot] + gravityDirection * gravityAcceleration) * subStepTime;
        }

        m_phase = StepPhase.VelocitiesIntegrated;
    }

    /// <summary>
    /// Today's terminal speed: a dynamic body whose speed along the gravity axis exceeds the terminal speed in either
    /// direction gets that speed falling (each nonzero component of <c>terminalSpeed × gravityDirection</c> replaces
    /// the velocity's), so a fast upward speed is flipped to falling. Needs <see cref="StepPhase.VelocitiesIntegrated"/>;
    /// leaves <see cref="StepPhase.TerminalSpeedClamped"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The phase is not <see cref="StepPhase.VelocitiesIntegrated"/>.</exception>
    public void ClampTerminalSpeed()
    {
        RequirePhase(StepPhase.VelocitiesIntegrated, nameof(ClampTerminalSpeed));

        var gravityDirection = m_settings.GravityDirection;
        var terminalSpeed = m_settings.TerminalSpeed;

        for (var slot = 0; slot < m_slotCount; slot++)
        {
            if (!m_alive[slot] || m_isStatic[slot])
                continue;

            var gravitySpeed = Vector3.Dot(m_velocities[slot], gravityDirection);

            if (Math.Abs(gravitySpeed) > terminalSpeed)
                m_velocities[slot] = LegacyMath.ReplaceComponent(m_velocities[slot], terminalSpeed * gravityDirection);
        }

        m_phase = StepPhase.TerminalSpeedClamped;
    }

    /// <summary>
    /// Sets every dynamic body's force to zero; a static body keeps its force (today's setter ignores it). Any phase;
    /// <see cref="Step"/> calls it on the last sub-step after the terminal speed.
    /// </summary>
    public void ClearForces()
    {
        for (var slot = 0; slot < m_slotCount; slot++)
        {
            if (m_alive[slot] && !m_isStatic[slot])
                m_forces[slot] = Vector3.Zero;
        }
    }

    /// <summary>
    /// Moves every dynamic body by <c>(velocityFrame × velocity) × subStepTime</c>, clears its ground flag and rebuilds
    /// its world geometry. Needs <see cref="StepPhase.TerminalSpeedClamped"/>; leaves
    /// <see cref="StepPhase.PositionsIntegrated"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="subStepTime"/> is not finite or negative.</exception>
    /// <exception cref="InvalidOperationException">The phase is not <see cref="StepPhase.TerminalSpeedClamped"/>.</exception>
    public void IntegratePositions(float subStepTime)
    {
        RequireTime(subStepTime, nameof(subStepTime));
        RequirePhase(StepPhase.TerminalSpeedClamped, nameof(IntegratePositions));

        for (var slot = 0; slot < m_slotCount; slot++)
        {
            if (!m_alive[slot] || m_isStatic[slot])
                continue;

            StorePosition(slot, m_positions[slot] + m_velocityFrames[slot] * m_velocities[slot] * subStepTime);
            m_onGround[slot] = false;
            UpdateGeometry(slot);
        }

        m_phase = StepPhase.PositionsIntegrated;
    }

    /// <summary>
    /// Tests every pair of live bodies once (slots i &lt; j, static pairs skipped) and resolves each contact at once:
    /// pushes the dynamic body by the depth along the normal (each by half when both are dynamic), sets the ground flag
    /// from the direction between the centres, cancels the gravity-axis part of the relative velocity for a body on
    /// the ground, and rebuilds the moved bodies' geometry before the next pair. Needs
    /// <see cref="StepPhase.PositionsIntegrated"/>; leaves <see cref="StepPhase.Idle"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The phase is not <see cref="StepPhase.PositionsIntegrated"/>.</exception>
    public void ResolveContacts()
    {
        RequirePhase(StepPhase.PositionsIntegrated, nameof(ResolveContacts));

        for (var first = 0; first < m_slotCount - 1; first++)
        {
            if (!m_alive[first])
                continue;

            for (var second = first + 1; second < m_slotCount; second++)
            {
                if (!m_alive[second] || (m_isStatic[first] && m_isStatic[second]))
                    continue;

                // The bounds test CheckCollision starts with, run first so a separated pair builds no views.
                if (!NarrowPhase.BoundsOverlap(m_boundsMin[first], m_boundsMax[first], m_boundsMin[second],
                        m_boundsMax[second]))
                    continue;

                if (!NarrowPhase.CheckCollision(View(first), View(second), out var normal, out var depth))
                    continue;

                ResolveContact(first, second, normal, depth);
            }
        }

        m_phase = StepPhase.Idle;
    }

    /// <summary>Returns to <see cref="StepPhase.Idle"/> from any phase and changes nothing else: the way out after an exception inside a sub-step.</summary>
    public void AbortStep() => m_phase = StepPhase.Idle;

    /// <summary>
    /// Today's nearest ray hit over the live bodies in slot order, on the cached geometry: inside one polygon the first
    /// face in list order that the ray passes wins; between bodies the strictly nearer hit wins, so the lower slot wins a
    /// tie. Any phase; allocates nothing.
    /// </summary>
    public bool RayCast(in Ray ray, out RayHit hit)
    {
        var nearest = new PhysicsQueries.NearestHit(ray);

        for (var slot = 0; slot < m_slotCount; slot++)
        {
            if (!m_alive[slot])
                continue;

            bool isHit;
            Vector3 point;

            if (m_shapes[slot] is PolygonShape polygon)
                isHit = PhysicsQueries.IntersectRayWithPolygon(polygon, WorldVertices(slot), WorldNormals(slot), ray,
                    out point);
            else
                isHit = PhysicsQueries.IntersectRay(m_positions[slot], m_sizes[slot].X / 2, ray, out point);

            nearest.Consider(slot, isHit, point);
        }

        hit = nearest.Found
            ? new RayHit(new BodyHandle(nearest.Index, m_generations[nearest.Index]), nearest.Point, nearest.Distance)
            : default;

        return nearest.Found;
    }

    #endregion

    #region Private

    // Today's World.CollideObjects contact branch and World.ResolveColliding, on one detected pair.
    private void ResolveContact(int first, int second, Vector3 normal, float depth)
    {
        var gravityDirection = m_settings.GravityDirection;
        var firstIsStatic = m_isStatic[first];
        var secondIsStatic = m_isStatic[second];

        var directionMove = Vector3.Normalize(m_positions[first] - m_positions[second]);
        var directionType = Vector3.Dot(directionMove, normal);
        var onGroundValue = Vector3.Dot(directionMove, gravityDirection);

        if (firstIsStatic)
        {
            StorePosition(second, m_positions[second] + normal * depth * (directionType < 0 ? 1 : -1));
            m_onGround[second] = onGroundValue > 0;
        }
        else if (secondIsStatic)
        {
            StorePosition(first, m_positions[first] + normal * depth * (directionType > 0 ? 1 : -1));
            m_onGround[first] = onGroundValue < 0;
        }
        else
        {
            var transformedNormal = normal * depth / 2;
            StorePosition(first, m_positions[first] + transformedNormal * (directionType > 0 ? 1 : -1));
            StorePosition(second, m_positions[second] + transformedNormal * (directionType < 0 ? 1 : -1));

            m_onGround[first] = onGroundValue < 0;
            m_onGround[second] = onGroundValue > 0;
        }

        // A static body's stored velocity takes part, as today; only a dynamic body's velocity changes. Today's setter
        // also clamps the forward speed here, a no-op on a speed the drive has already clamped.
        var relativeSpeed = m_velocities[first] - m_velocities[second];

        if (m_onGround[first] && !firstIsStatic)
            m_velocities[first] = m_velocities[first] + relativeSpeed * gravityDirection;
        if (m_onGround[second] && !secondIsStatic)
            m_velocities[second] = m_velocities[second] - relativeSpeed * gravityDirection;

        // A static body did not move, so its geometry is current.
        if (!firstIsStatic)
            UpdateGeometry(first);
        if (!secondIsStatic)
            UpdateGeometry(second);
    }

    // Today's WorldObject.Position setter stores through ObservableObject.SetField, which skips a value equal to the old
    // one by EqualityComparer<Vector3>.Default: +0 against −0 counts as equal, so an old −0 stays.
    private void StorePosition(int slot, Vector3 position)
    {
        if (!EqualityComparer<Vector3>.Default.Equals(m_positions[slot], position))
            m_positions[slot] = position;
    }

    private BodyGeometry View(int slot) =>
        new BodyGeometry(m_shapes[slot] as PolygonShape, WorldVertices(slot), WorldNormals(slot), m_positions[slot],
            m_sizes[slot].X, m_boundsMin[slot], m_boundsMax[slot]);

    private ReadOnlySpan<Vector3> WorldVertices(int slot) =>
        m_shapes[slot] is PolygonShape polygon
            ? m_worldVertices[slot].AsSpan(0, polygon.Vertices.Length)
            : ReadOnlySpan<Vector3>.Empty;

    private ReadOnlySpan<Vector3> WorldNormals(int slot) =>
        m_shapes[slot] is PolygonShape polygon
            ? m_worldNormals[slot].AsSpan(0, polygon.FaceCount)
            : ReadOnlySpan<Vector3>.Empty;

    // Today's WorldObject.ApplyTransformation and Collision.UpdateCollision for one body.
    private void UpdateGeometry(int slot)
    {
        var position = m_positions[slot];
        var sphereWidth = m_sizes[slot].X;

        if (m_shapes[slot] is PolygonShape polygon)
        {
            var vertices = m_worldVertices[slot].AsSpan(0, polygon.Vertices.Length);
            var transformation = Geometry.ComputeMatrix(position, m_linears[slot]);
            Geometry.ComputeVertices(polygon, transformation, vertices);
            Geometry.ComputeNormals(polygon, transformation, m_worldNormals[slot].AsSpan(0, polygon.FaceCount));
            Geometry.ComputeBounds(polygon, vertices, position, sphereWidth, out m_boundsMin[slot],
                out m_boundsMax[slot]);
        }
        else
        {
            Geometry.ComputeBounds(null, ReadOnlySpan<Vector3>.Empty, position, sphereWidth, out m_boundsMin[slot],
                out m_boundsMax[slot]);
        }
    }

    private void Grow()
    {
        var capacity = m_capacity * 2;

        Array.Resize(ref m_positions, capacity);
        Array.Resize(ref m_velocities, capacity);
        Array.Resize(ref m_forces, capacity);
        Array.Resize(ref m_linears, capacity);
        Array.Resize(ref m_velocityFrames, capacity);
        Array.Resize(ref m_sizes, capacity);
        Array.Resize(ref m_masses, capacity);
        Array.Resize(ref m_isStatic, capacity);
        Array.Resize(ref m_onGround, capacity);
        Array.Resize(ref m_alive, capacity);
        Array.Resize(ref m_generations, capacity);
        Array.Resize(ref m_shapes, capacity);
        Array.Resize(ref m_boundsMin, capacity);
        Array.Resize(ref m_boundsMax, capacity);
        Array.Resize(ref m_worldVertices, capacity);
        Array.Resize(ref m_worldNormals, capacity);
        Array.Resize(ref m_freeSlots, capacity);

        m_capacity = capacity;
    }

    private int Slot(BodyHandle handle)
    {
        if (!Contains(handle))
            throw new ArgumentException($"{handle} is not a live body of this world.", nameof(handle));

        return handle.Slot;
    }

    private int DynamicSlot(BodyHandle handle, string call)
    {
        var slot = Slot(handle);

        if (m_isStatic[slot])
            throw new InvalidOperationException($"{call} on the static {handle}: a static body keeps its velocity and force.");

        return slot;
    }

    private static void Validate(in BodyDescription description)
    {
        const string name = "description";

        if (description.Shape is null)
            throw new ArgumentException("The shape is null.", name);

        if (!IsFinite(description.Position) || !IsFinite(description.Linear) || !IsFinite(description.Size) ||
            !IsFinite(description.VelocityFrame) || !IsFinite(description.Velocity) || !IsFinite(description.Force))
            throw new ArgumentException("A component of the position, linear part, size, velocity frame, velocity or " +
                                        "force is not finite.", name);

        if (!float.IsFinite(description.Mass) || description.Mass <= 0)
            throw new ArgumentException("The mass must be finite and positive.", name);
    }

    private void RequireIdle(string call) => RequirePhase(StepPhase.Idle, call);

    private void RequirePhase(StepPhase expected, string call)
    {
        if (m_phase != expected)
            throw new InvalidOperationException(
                $"{call} needs the phase {expected}, but the world is in {m_phase}; call {NextCall(m_phase)} next.");
    }

    private void RequireBeforePositions(string call)
    {
        if (m_phase == StepPhase.PositionsIntegrated)
            throw new InvalidOperationException(
                $"{call} is not allowed after the positions are integrated (phase {m_phase}); call {NextCall(m_phase)} next.");
    }

    private static string NextCall(StepPhase phase) => phase switch
    {
        StepPhase.Idle => nameof(IntegrateVelocities),
        StepPhase.VelocitiesIntegrated => nameof(ClampTerminalSpeed),
        StepPhase.TerminalSpeedClamped => nameof(IntegratePositions),
        _ => nameof(ResolveContacts)
    };

    private static void RequireTime(float time, string name)
    {
        if (!float.IsFinite(time) || time < 0)
            throw new ArgumentOutOfRangeException(name, time, "The time must be finite and not negative.");
    }

    private static void RequireFinite(Vector3 value, string name)
    {
        if (!IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "Every component must be finite.");
    }

    private static void RequireFinite(Matrix3 value, string name)
    {
        if (!IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "Every element must be finite.");
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Matrix3 value) =>
        IsFinite(value.Column0) && IsFinite(value.Column1) && IsFinite(value.Column2);

    #endregion
}
