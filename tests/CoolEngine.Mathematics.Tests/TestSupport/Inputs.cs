namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>
/// Seeded random inputs for the oracle tests, which loop over <see cref="Count"/> inputs inside one test.
/// A test takes its own generator from <see cref="CreateRandom"/>, so every run and every test sees the same sequence
/// (a seeded <see cref="Random"/> is stable across runs and .NET versions). The ranges are the plan's: components and
/// translations in [-100, 100], angles in [-2π, 2π], scale factors in [0.5, 2], uniformly distributed unit quaternions.
/// The generators of the library types (vectors, quaternions, TRS matrices) are added in the stage that adds the
/// type, built from these.
/// </summary>
internal static class Inputs
{
    /// <summary>The number of inputs every oracle test loops over.</summary>
    public const int Count = 256;

    /// <summary>The seed of every oracle test unless it needs a second, independent sequence.</summary>
    public const int Seed = 20261125;

    private const double ComponentLimit = 100.0;
    private const double AngleLimit = 2.0 * Math.PI;
    private const double MinScaleFactor = 0.5;
    private const double MaxScaleFactor = 2.0;

    public static Random CreateRandom(int seed = Seed)
    {
        return new Random(seed);
    }

    /// <summary>The context string that names input <paramref name="index"/> in a failure message.</summary>
    public static string Label(int index)
    {
        return $"input {index}";
    }

    /// <summary>A vector component or a translation in [-100, 100].</summary>
    public static float NextComponent(this Random random)
    {
        return (float)random.NextComponentD();
    }

    /// <inheritdoc cref="NextComponent"/>
    public static double NextComponentD(this Random random)
    {
        return NextInRange(random, -ComponentLimit, ComponentLimit);
    }

    /// <summary>An angle in radians in [-2π, 2π].</summary>
    public static float NextAngle(this Random random)
    {
        return (float)random.NextAngleD();
    }

    /// <inheritdoc cref="NextAngle"/>
    public static double NextAngleD(this Random random)
    {
        return NextInRange(random, -AngleLimit, AngleLimit);
    }

    /// <summary>A per-axis scale factor of a TRS matrix in [0.5, 2].</summary>
    public static float NextScaleFactor(this Random random)
    {
        return (float)random.NextScaleFactorD();
    }

    /// <inheritdoc cref="NextScaleFactor"/>
    public static double NextScaleFactorD(this Random random)
    {
        return NextInRange(random, MinScaleFactor, MaxScaleFactor);
    }

    /// <summary>
    /// The components of a unit quaternion (vector part, then scalar), uniform over all rotations; computed in double
    /// and rounded to float, so its length is 1 within a few float ulps.
    /// </summary>
    public static (float X, float Y, float Z, float W) NextUnitQuaternion(this Random random)
    {
        var (x, y, z, w) = random.NextUnitQuaternionD();
        return ((float)x, (float)y, (float)z, (float)w);
    }

    /// <summary>The components of a unit quaternion (vector part, then scalar), uniform over all rotations.</summary>
    public static (double X, double Y, double Z, double W) NextUnitQuaternionD(this Random random)
    {
        // Shoemake, "Uniform random rotations" (Graphics Gems III): uniform on the unit 3-sphere.
        var u1 = random.NextDouble();
        var u2 = random.NextDouble();
        var u3 = random.NextDouble();
        var a = Math.Sqrt(1.0 - u1);
        var b = Math.Sqrt(u1);

        return (a * Math.Sin(2.0 * Math.PI * u2), a * Math.Cos(2.0 * Math.PI * u2),
            b * Math.Sin(2.0 * Math.PI * u3), b * Math.Cos(2.0 * Math.PI * u3));
    }

    private static double NextInRange(Random random, double min, double max)
    {
        return min + (max - min) * random.NextDouble();
    }
}
