using System.Globalization;
using System.Numerics;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// What one scene's side-by-side run measured: today's <see cref="OldTanks.Models.World"/> against CoolEngine.Physics
/// with <see cref="WorldSpaceDrive"/>, over every live body after every tick. Distances are Euclidean, computed in
/// double: <see cref="MaxPosition"/> in metres, <see cref="MaxVelocity"/> in m/s with today's local velocity turned into
/// world space by today's own frame (<c>H(ψ) · u</c>, the product today's <c>WorldObject.Move</c> applies).
/// <see cref="MaxDynamicVelocity"/> is <see cref="MaxVelocity"/> over the dynamic bodies only (a static body's frozen
/// velocity cannot be turned with its heading on the library side). <see cref="LastPosition"/> and
/// <see cref="LastVelocity"/> are the largest at the last tick. <see cref="BitEqual"/>: every position, world velocity,
/// world force and ground flag equal by bits on every tick.
/// </summary>
internal sealed record SceneDifference(
    string Name,
    int Ticks,
    double MaxPosition,
    double MaxVelocity,
    double MaxDynamicVelocity,
    double LastPosition,
    double LastVelocity,
    int GroundMismatches,
    int? FirstTickAboveMillimetre,
    bool BitEqual,
    string? FirstBitDifference,
    string? FirstAboveMicrometre,
    string? LibraryOnlyNonFinite);

/// <summary>
/// Runs a <see cref="SceneDescription"/> through today's code and the stage-2 library side by side and measures the
/// difference (stage 2 is not equal by bits by design: a quaternion rotation and a world-space velocity change the
/// arithmetic, plan §9).
/// </summary>
internal static class SceneComparison
{
    public const float TickTime = 1f / 60f;

    /// <summary>
    /// The largest |Δposition| against today's code a scene that is not bit-equal may show, over all bodies and ticks:
    /// the owner's acceptance bar for stage 2 (Denys, 2026-11-28: "1 mm, 190 of 200").
    /// </summary>
    public const float PositionBound = 0.001f;

    private const double Millimetre = 1e-3;
    private const double Micrometre = 1e-6;

    /// <summary>
    /// Runs the scene: before each tick its events on both sides, then one step of <paramref name="tickTime"/>'s time
    /// (default 1/60 s) on both, then the comparison. <paramref name="afterTick"/> checks a scene's preconditions on
    /// today's world. Fails the test if the two sides stop holding the same bodies in the same order (a harness defect).
    /// </summary>
    public static SceneDifference Run(SceneDescription scene, Func<int, float>? tickTime = null,
        Action<int, OldWorldRunner>? afterTick = null)
    {
        var old = new OldWorldRunner(scene);
        var library = new LibraryRunner(scene, OldWorldRunner.CubeShape());
        var events = scene.Events.ToLookup(tickEvent => tickEvent.Tick);
        var meter = new Meter();

        for (var tick = 0; tick < scene.Ticks; tick++)
        {
            foreach (var tickEvent in events[tick])
            {
                if (tickEvent.Add is { } spec)
                {
                    var handle = library.Add(spec);
                    old.Add(spec, handle.Slot);
                }
                else
                {
                    old.Apply(tickEvent);
                    library.Apply(tickEvent);
                }
            }

            var time = tickTime?.Invoke(tick) ?? TickTime;
            old.World.CollideObjects(time);
            library.RunTick(time);

            meter.Compare(scene, tick, tick == scene.Ticks - 1, old, library);
            afterTick?.Invoke(tick, old);
        }

        return meter.Result(scene);
    }

    /// <summary>
    /// Runs the scene, prints its largest |Δposition| and its count of ground-flag differences (printed only, never
    /// asserted), and asserts no NaN or infinity on the library side where today has none and a largest |Δposition| of
    /// at most <see cref="PositionBound"/>.
    /// </summary>
    public static SceneDifference AssertWithinPositionBound(SceneDescription scene, Func<int, float>? tickTime = null,
        Action<int, OldWorldRunner>? afterTick = null)
    {
        var difference = Run(scene, tickTime, afterTick);

        TestContext.Out.WriteLine(Summary(difference));
        Assert.Multiple(() =>
        {
            Assert.That(difference.LibraryOnlyNonFinite, Is.Null,
                $"{scene.Name}: a value not finite on the library side only");
            Assert.That(difference.MaxPosition, Is.LessThanOrEqualTo(PositionBound),
                $"{scene.Name}: largest |Δposition| against today's code ({difference.FirstAboveMicrometre})");
        });

        return difference;
    }

    /// <summary>One line: the scene's largest |Δposition| beside the bound, and its count of ground-flag differences.</summary>
    public static string Summary(SceneDifference difference) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{difference.Name}: largest |Δposition| {difference.MaxPosition:E2} m (bound {PositionBound} m); " +
            $"ground-flag differences {difference.GroundMismatches} body-ticks");

    /// <summary>Runs the scene and asserts that the stage-2 library still equals today's code by bits on every tick.</summary>
    public static void AssertBitEqual(SceneDescription scene, Func<int, float>? tickTime = null,
        Action<int, OldWorldRunner>? afterTick = null)
    {
        var difference = Run(scene, tickTime, afterTick);

        Assert.That(difference.BitEqual, Is.True, $"{scene.Name}: {difference.FirstBitDifference}");
    }

    public static bool SameBits(Vector3 a, Vector3 b) =>
        BitConverter.SingleToUInt32Bits(a.X) == BitConverter.SingleToUInt32Bits(b.X) &&
        BitConverter.SingleToUInt32Bits(a.Y) == BitConverter.SingleToUInt32Bits(b.Y) &&
        BitConverter.SingleToUInt32Bits(a.Z) == BitConverter.SingleToUInt32Bits(b.Z);

    public static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>|a − b| in double; 0 when both are not finite, +∞ when only one is.</summary>
    public static double Distance(Vector3 a, Vector3 b)
    {
        var aFinite = IsFinite(a);
        var bFinite = IsFinite(b);

        if (!aFinite || !bFinite)
            return aFinite == bFinite ? 0 : double.PositiveInfinity;

        var x = (double)a.X - b.X;
        var y = (double)a.Y - b.Y;
        var z = (double)a.Z - b.Z;

        return Math.Sqrt(x * x + y * y + z * z);
    }

    private static string Text(Vector3 v) =>
        string.Create(CultureInfo.InvariantCulture, $"({v.X:R}, {v.Y:R}, {v.Z:R})");

    private sealed class Meter
    {
        private double m_maxPosition;
        private double m_maxVelocity;
        private double m_maxDynamicVelocity;
        private double m_lastPosition;
        private double m_lastVelocity;
        private int m_groundMismatches;
        private int? m_firstTickAboveMillimetre;
        private string? m_firstBitDifference;
        private string? m_firstAboveMicrometre;
        private string? m_libraryOnlyNonFinite;

        public void Compare(SceneDescription scene, int tick, bool isLastTick, OldWorldRunner old, LibraryRunner library)
        {
            var slotCount = Math.Max(old.BySlot.Count, library.BySlot.Count);
            var listIndex = 0;

            for (var slot = 0; slot < slotCount; slot++)
            {
                var worldObject = slot < old.BySlot.Count ? old.BySlot[slot] : null;
                var handle = slot < library.BySlot.Count ? library.BySlot[slot] : null;
                var where = $"tick {tick}, slot {slot}";

                if ((worldObject == null) != (handle == null))
                    Assert.Fail($"{scene.Name}, {where}: today {(worldObject == null ? "empty" : "a body")}, " +
                                $"library {(handle == null ? "empty" : "a body")}");

                if (worldObject == null || handle == null)
                    continue;

                if (!ReferenceEquals(old.World.WorldObjects[listIndex++], worldObject))
                    Assert.Fail($"{scene.Name}, {where}: today's object list is not in slot order");

                var state = library.World.GetState(handle.Value);
                var frame = WorldSpaceDrive.Frame(worldObject.Rotation.Y);
                var todayPosition = worldObject.Position;
                var todayVelocity = frame * worldObject.RigidBody.Velocity;
                var todayForce = frame * worldObject.RigidBody.Force;
                var todayOnGround = worldObject.RigidBody.OnGround;

                var position = Distance(todayPosition, state.Position);
                var velocity = Distance(todayVelocity, state.Velocity);

                m_maxPosition = Math.Max(m_maxPosition, position);
                m_maxVelocity = Math.Max(m_maxVelocity, velocity);

                if (!state.IsStatic)
                    m_maxDynamicVelocity = Math.Max(m_maxDynamicVelocity, velocity);

                if (isLastTick)
                {
                    m_lastPosition = Math.Max(m_lastPosition, position);
                    m_lastVelocity = Math.Max(m_lastVelocity, velocity);
                }

                if (todayOnGround != state.OnGround)
                    m_groundMismatches++;

                if (position > Millimetre)
                    m_firstTickAboveMillimetre ??= tick;

                if (position > Micrometre)
                    m_firstAboveMicrometre ??= $"{where}: |Δp| {position:E2} m, today p {Text(todayPosition)} " +
                                               $"v {Text(todayVelocity)} ground {todayOnGround}, library p " +
                                               $"{Text(state.Position)} v {Text(state.Velocity)} ground {state.OnGround}";

                if (m_firstBitDifference == null)
                {
                    var field = !SameBits(todayPosition, state.Position) ? "position"
                        : !SameBits(todayVelocity, state.Velocity) ? "velocity"
                        : !SameBits(todayForce, state.Force) ? "force"
                        : todayOnGround != state.OnGround ? "ground flag"
                        : null;

                    if (field != null)
                        m_firstBitDifference = $"{where}, {field}: today p {Text(todayPosition)} v {Text(todayVelocity)} " +
                                               $"F {Text(todayForce)} ground {todayOnGround}; library p " +
                                               $"{Text(state.Position)} v {Text(state.Velocity)} F {Text(state.Force)} " +
                                               $"ground {state.OnGround}";
                }

                if (m_libraryOnlyNonFinite == null &&
                    ((!IsFinite(state.Position) && IsFinite(todayPosition)) ||
                     (!IsFinite(state.Velocity) && IsFinite(todayVelocity))))
                    m_libraryOnlyNonFinite = $"{where}: today p {Text(todayPosition)} v {Text(todayVelocity)}, " +
                                             $"library p {Text(state.Position)} v {Text(state.Velocity)}";
            }
        }

        public SceneDifference Result(SceneDescription scene) =>
            new SceneDifference(scene.Name, scene.Ticks, m_maxPosition, m_maxVelocity, m_maxDynamicVelocity,
                m_lastPosition, m_lastVelocity,
                m_groundMismatches, m_firstTickAboveMillimetre, m_firstBitDifference == null, m_firstBitDifference,
                m_firstAboveMicrometre, m_libraryOnlyNonFinite);
    }
}
