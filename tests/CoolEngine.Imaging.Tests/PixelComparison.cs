namespace CoolEngine.Imaging.Tests;

/// <summary>
/// How far two images are apart over the R, G and B channels: the largest absolute difference, the mean one,
/// the 99.9th percentile, and whether every alpha value is identical.
/// </summary>
internal readonly record struct PixelDifference(int Max, double Mean, int Percentile999, bool AlphaEqual);

internal static class PixelComparison
{
    /// <exception cref="ArgumentException">The images have different pixel counts.</exception>
    public static PixelDifference Compare(ReadOnlySpan<Rgba32> actual, ReadOnlySpan<Rgba32> expected)
    {
        if (actual.Length != expected.Length)
            throw new ArgumentException($"Pixel counts differ: {actual.Length} vs {expected.Length}.", nameof(actual));

        // A histogram of the 256 possible differences gives the percentile without sorting.
        var histogram = new long[256];
        var alphaEqual = true;

        for (var i = 0; i < actual.Length; i++)
        {
            histogram[Math.Abs(actual[i].R - expected[i].R)]++;
            histogram[Math.Abs(actual[i].G - expected[i].G)]++;
            histogram[Math.Abs(actual[i].B - expected[i].B)]++;
            alphaEqual &= actual[i].A == expected[i].A;
        }

        long count = actual.Length * 3L;
        if (count == 0)
            return new PixelDifference(0, 0, 0, alphaEqual);

        var max = 0;
        long sum = 0;
        for (var difference = 0; difference < histogram.Length; difference++)
        {
            if (histogram[difference] == 0)
                continue;

            max = difference;
            sum += histogram[difference] * difference;
        }

        // The smallest difference that at least 99.9 % of the samples do not exceed.
        var rank = (long)Math.Ceiling(count * 0.999);
        var percentile = 0;
        long cumulative = 0;
        for (var difference = 0; difference < histogram.Length; difference++)
        {
            cumulative += histogram[difference];
            if (cumulative >= rank)
            {
                percentile = difference;
                break;
            }
        }

        return new PixelDifference(max, (double)sum / count, percentile, alphaEqual);
    }
}
