using System.Globalization;
using System.Numerics;
using System.Text;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Prints the table of stage 2's differences from today's code (quaternion orientation, world-space velocity, the game's
/// rules in <see cref="WorldSpaceDrive"/>) over every scene of the side-by-side tests. It asserts nothing, on purpose:
/// the acceptance bar (the scenes with no turned body equal by bits, the other named scenes within 1 mm on every tick,
/// at least 190 of the 200 random scenes within 1 mm) is asserted by <see cref="PhysicsParityTests"/> and
/// <see cref="GoldenSceneParityTests"/>. It is an ordinary test, not an explicit one, so that the table is printed on
/// every run.
/// </summary>
[TestFixture]
public sealed class Stage2DifferenceTests
{
    // The scene names Cube's and Sphere's constructors read from the cache; the game's loader puts the real models there.
    private const string CubeSceneName = "Cube_Cube.005";
    private const string SphereSceneName = "Sphere";

    [SetUp]
    public void SetUp()
    {
        // Other fixtures of this assembly change it; today's game sets 5.
        EngineSettings.Current.CollisionIterations = 5;
        GlobalCache<Scene>.Default.AddOrUpdateItem(CubeSceneName,
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
        GlobalCache<Scene>.Default.AddOrUpdateItem(SphereSceneName,
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
    }

    [TearDown]
    public void TearDown()
    {
        GlobalCache<Scene>.Default.RemoveItem(CubeSceneName);
        GlobalCache<Scene>.Default.RemoveItem(SphereSceneName);
    }

    [Test]
    public void Stage2Difference_AllScenes_Report()
    {
        var rows = new List<SceneDifference>
        {
            SceneComparison.Run(Scenes.Default()),
            SceneComparison.Run(Scenes.TerminalSpeed()),
            SceneComparison.Run(Scenes.ForcesHeadingsAndJump()),
            SceneComparison.Run(Scenes.DynamicAgainstDynamic()),
            SceneComparison.Run(Scenes.StaticToggled()),
            SceneComparison.Run(Scenes.RemoveAndAdd()),
            SceneComparison.Run(Scenes.NegativeZeroAndZeroTimeStep(), Scenes.NegativeZeroTickTime),
            SceneComparison.Run(Scenes.ZeroLimitsBackwardForce()),
            SceneComparison.Run(Scenes.BodyHitWhileStatic()),
            SceneComparison.Run(Scenes.ForceAndHeadingChangeBeforeTheSameTick()),
            SceneComparison.Run(Scenes.VelocityWrittenAfterAHeadingChange()),
            SceneComparison.Run(GoldenSceneParityTests.DefaultScene()),
            SceneComparison.Run(GoldenSceneParityTests.TerminalDrop(false)),
            SceneComparison.Run(GoldenSceneParityTests.TerminalDrop(true)),
            SceneComparison.Run(GoldenSceneParityTests.UpwardFlip()),
            SceneComparison.Run(GoldenSceneParityTests.CubeOnStaticSphere()),
            SceneComparison.Run(GoldenSceneParityTests.Stack()),
            SceneComparison.Run(GoldenSceneParityTests.SpawnedStatics()),
            SceneComparison.Run(GoldenSceneParityTests.Drive()),
            SceneComparison.Run(GoldenSceneParityTests.RestingHop()),
            SceneComparison.Run(GoldenSceneParityTests.OverlappingStatics()),
        };

        // The seeds of PhysicsParityTests.Step_SeededRandomScenes_AtLeast190Of200StayWithinOneMillimetre: seed × 1000 +
        // scene, seeds 1 … 4, scenes 0 … 49.
        var random = new List<SceneDifference>();
        for (var seed = 1; seed <= 4; seed++)
        {
            for (var scene = 0; scene < 50; scene++)
                random.Add(SceneComparison.Run(Scenes.Random(seed * 1000 + scene)));
        }

        TestContext.Out.Write(Report(rows, random));
    }

    private static string Report(List<SceneDifference> rows, List<SceneDifference> random)
    {
        var text = new StringBuilder();
        const string format = "{0,-46} {1,6} {2,10} {3,10} {4,10} {5,10} {6,10} {7,8} {8,10} {9,9}";

        text.AppendLine("Stage 2 against today's World. |Δp| in m, |Δv| in m/s (today's local velocity turned into world " +
                        "space by today's frame); max over all bodies and ticks, dyn = the same over dynamic bodies " +
                        "only, last = max over bodies at the last tick; ground = body-ticks whose ground flag differs; " +
                        "first > 1 mm = first tick with |Δp| > 1e-3 m; bit-equal = every position, world velocity, world " +
                        "force and ground flag equal by bits on every tick.");
        text.AppendLine(Line(format, "scene", "ticks", "max |Δp|", "max |Δv|", "dyn |Δv|", "last |Δp|", "last |Δv|",
            "ground", "first>1mm", "bit-equal"));

        foreach (var row in rows)
        {
            text.AppendLine(Line(format, row.Name, row.Ticks.ToString(CultureInfo.InvariantCulture),
                Number(row.MaxPosition), Number(row.MaxVelocity), Number(row.MaxDynamicVelocity),
                Number(row.LastPosition), Number(row.LastVelocity),
                row.GroundMismatches.ToString(CultureInfo.InvariantCulture),
                row.FirstTickAboveMillimetre?.ToString(CultureInfo.InvariantCulture) ?? "never",
                row.BitEqual ? "yes" : "no"));
        }

        foreach (var (label, statistic) in new (string, Func<IEnumerable<double>, double>)[]
                 {
                     ("random 200 scenes: median", Median), ("random 200 scenes: 90th percentile", Percentile90),
                     ("random 200 scenes: maximum", values => values.Max())
                 })
        {
            text.AppendLine(Line(format, label, "300", Number(statistic(random.Select(row => row.MaxPosition))),
                Number(statistic(random.Select(row => row.MaxVelocity))),
                Number(statistic(random.Select(row => row.MaxDynamicVelocity))),
                Number(statistic(random.Select(row => row.LastPosition))),
                Number(statistic(random.Select(row => row.LastVelocity))),
                statistic(random.Select(row => (double)row.GroundMismatches)).ToString("0.#", CultureInfo.InvariantCulture),
                "-", "-"));
        }

        var aboveMillimetre = random.Where(row => row.FirstTickAboveMillimetre != null)
            .Select(row => (double)row.FirstTickAboveMillimetre!.Value).ToArray();
        var firstTicks = aboveMillimetre.Length == 0
            ? "."
            : FormattableString.Invariant(
                $" (first tick: earliest {aboveMillimetre.Min()}, median {Median(aboveMillimetre)}).");
        var withGroundMismatch = random.Count(row => row.GroundMismatches > 0);
        var bitEqual = random.Count(row => row.BitEqual);
        text.AppendLine(FormattableString.Invariant($"Random scenes: {random.Count}; with a ground-flag mismatch: ") +
                        FormattableString.Invariant($"{withGroundMismatch}; bit-equal: {bitEqual}; with |Δp| > 1e-3 m: ") +
                        FormattableString.Invariant($"{aboveMillimetre.Length}") + firstTicks);
        text.AppendLine("Statistics of the per-scene values: median = mean of the two middle values; 90th percentile = " +
                        "nearest rank (the 180th of 200 in ascending order).");

        text.AppendLine();
        text.AppendLine("First differences (bits, then |Δp| > 1e-6 m) of each scene that is not bit-equal:");
        var worst = random.MaxBy(row => row.MaxPosition)!;
        foreach (var row in rows.Where(row => !row.BitEqual).Append(worst))
        {
            text.AppendLine($"  {row.Name}: bits at {row.FirstBitDifference}");
            text.AppendLine($"  {row.Name}: |Δp| > 1e-6 m at {row.FirstAboveMicrometre ?? "never"}");
        }

        return text.ToString();
    }

    private static string Line(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    private static string Number(double value) =>
        value == 0 ? "0" : value.ToString("0.00E+00", CultureInfo.InvariantCulture);

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;

        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static double Percentile90(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();

        return sorted[(int)Math.Ceiling(0.9 * sorted.Length) - 1];
    }
}
