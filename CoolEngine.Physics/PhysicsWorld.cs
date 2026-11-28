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
/// <b>Bodies:</b> a body's pose is a position, a unit-quaternion orientation and half-extents; its velocity and force
/// are in world space. The world knows no forward axis, speed limit or damping: those are the game's rules, run
/// between the phase calls.
/// </para>
/// </remarks>
public sealed class PhysicsWorld
{
    // |‖q‖² − 1| above this rejects an orientation as not unit.
    private const float UnitQuaternionTolerance = 1e-5f;

    private PhysicsSettings m_settings;
    private StepPhase m_phase;

    private int m_capacity;
    private int m_slotCount;
    private int m_count;
    private int m_version;

    private Vector3[] m_positions;
    private Vector3[] m_velocities;
    private Vector3[] m_forces;
    private Quaternion[] m_orientations;
    private Vector3[] m_halfExtents;
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
        m_orientations = new Quaternion[m_capacity];
        m_halfExtents = new Vector3[m_capacity];
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
    /// (<c>ParamName "description"</c>) The shape is null; a component of the position, orientation, half-extents,
    /// velocity or force is not finite; the orientation is not a unit quaternion (<c>|‖q‖² − 1| &gt; 1e-5</c>); the
    /// mass is not finite or not positive; or the shape is a sphere and its three half-extents are not equal.
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
        m_orientations[slot] = description.Orientation;
        m_halfExtents[slot] = description.HalfExtents;
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

    /// <summary>The orientation, a unit quaternion.</summary>
    public Quaternion GetOrientation(BodyHandle handle) => m_orientations[Slot(handle)];

    /// <summary>
    /// Turns the body and recomputes its world geometry at once, stored exactly as given (not normalized). The velocity
    /// is not turned: a game that keeps a velocity relative to the heading turns it itself.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is not finite, or the quaternion is not unit (<c>|‖q‖² − 1| &gt; 1e-5</c>).
    /// </exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetOrientation(BodyHandle handle, Quaternion orientation)
    {
        RequireIdle(nameof(SetOrientation));
        var slot = Slot(handle);
        RequireUnit(orientation, nameof(orientation));

        m_orientations[slot] = orientation;
        UpdateGeometry(slot);
    }

    /// <summary>Half the body's size along its own axes; a sphere's three are equal, its radius.</summary>
    public Vector3 GetHalfExtents(BodyHandle handle) => m_halfExtents[Slot(handle)];

    /// <summary>
    /// Sets the half-extents and recomputes the world geometry at once. Zero and negative components are allowed, as
    /// today's sizes are (negative mirrors; zero gives NaN normals for the faces it collapses).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is not finite, or the body is a sphere and the three components are not equal.
    /// </exception>
    /// <exception cref="InvalidOperationException">Called outside <see cref="StepPhase.Idle"/>.</exception>
    public void SetHalfExtents(BodyHandle handle, Vector3 halfExtents)
    {
        RequireIdle(nameof(SetHalfExtents));
        var slot = Slot(handle);
        RequireFinite(halfExtents, nameof(halfExtents));

        if (m_shapes[slot] is SphereShape && !AreEqual(halfExtents))
            throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents,
                "A sphere's three half-extents must be equal.");

        m_halfExtents[slot] = halfExtents;
        UpdateGeometry(slot);
    }

    /// <summary>
    /// The velocity in world space. A static body keeps the velocity it had when it was made static, and that value
    /// enters the contact rule's relative velocity (today's behaviour).
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

    /// <summary>The force in world space; cleared by <see cref="ClearForces"/> (the last sub-step of a step).</summary>
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

        return new BodyState(handle, m_positions[slot], m_orientations[slot], m_halfExtents[slot], m_velocities[slot],
            m_forces[slot], m_masses[slot], m_isStatic[slot], m_onGround[slot]);
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
    /// Moves every dynamic body by <c>velocity × subStepTime</c>, clears its ground flag and rebuilds
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

            StorePosition(slot, m_positions[slot] + m_velocities[slot] * subStepTime);
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
                isHit = PhysicsQueries.IntersectRay(m_positions[slot], Geometry.SphereWidth(m_halfExtents[slot]) / 2,
                    ray, out point);

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
            Geometry.SphereWidth(m_halfExtents[slot]), m_boundsMin[slot], m_boundsMax[slot]);

    private ReadOnlySpan<Vector3> WorldVertices(int slot) =>
        m_shapes[slot] is PolygonShape polygon
            ? m_worldVertices[slot].AsSpan(0, polygon.Vertices.Length)
            : ReadOnlySpan<Vector3>.Empty;

    private ReadOnlySpan<Vector3> WorldNormals(int slot) =>
        m_shapes[slot] is PolygonShape polygon
            ? m_worldNormals[slot].AsSpan(0, polygon.FaceCount)
            : ReadOnlySpan<Vector3>.Empty;

    // One body's world geometry and bounds from its position, orientation and half-extents.
    private void UpdateGeometry(int slot)
    {
        var position = m_positions[slot];
        var halfExtents = m_halfExtents[slot];
        var sphereWidth = Geometry.SphereWidth(halfExtents);

        if (m_shapes[slot] is PolygonShape polygon)
        {
            var vertices = m_worldVertices[slot].AsSpan(0, polygon.Vertices.Length);
            var rotation = Matrix3.CreateFromQuaternion(m_orientations[slot]);
            Geometry.ComputeVertices(polygon, position, rotation, halfExtents, vertices);
            Geometry.ComputeNormals(polygon, rotation, halfExtents, m_worldNormals[slot].AsSpan(0, polygon.FaceCount));
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
        Array.Resize(ref m_orientations, capacity);
        Array.Resize(ref m_halfExtents, capacity);
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

        if (!IsFinite(description.Position) || !IsFinite(description.Orientation) ||
            !IsFinite(description.HalfExtents) || !IsFinite(description.Velocity) || !IsFinite(description.Force))
            throw new ArgumentException("A component of the position, orientation, half-extents, velocity or force is " +
                                        "not finite.", name);

        if (!IsUnit(description.Orientation))
            throw new ArgumentException("The orientation is not a unit quaternion.", name);

        if (!float.IsFinite(description.Mass) || description.Mass <= 0)
            throw new ArgumentException("The mass must be finite and positive.", name);

        if (description.Shape is SphereShape && !AreEqual(description.HalfExtents))
            throw new ArgumentException("A sphere's three half-extents must be equal.", name);
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

    private static void RequireUnit(Quaternion value, string name)
    {
        if (!IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "Every component must be finite.");

        if (!IsUnit(value))
            throw new ArgumentOutOfRangeException(name, value, "The quaternion must be unit: |‖q‖² − 1| ≤ 1e-5.");
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Quaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);

    // For a finite quaternion.
    private static bool IsUnit(Quaternion value) =>
        MathF.Abs(value.LengthSquared() - 1) <= UnitQuaternionTolerance;

    // A sphere's half-extents: IEEE equality, so +0 equals −0.
    private static bool AreEqual(Vector3 halfExtents) =>
        halfExtents.X == halfExtents.Y && halfExtents.X == halfExtents.Z;

    #endregion
}
