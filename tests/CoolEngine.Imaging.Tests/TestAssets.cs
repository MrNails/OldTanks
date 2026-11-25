namespace CoolEngine.Imaging.Tests;

/// <summary>The game's texture assets, found from the test output folder by walking up to the checkout root.</summary>
internal static class TestAssets
{
    private const string SolutionFileName = "OldTanks.slnx";

    /// <summary>The folder holding <c>OldTanks.slnx</c>, the nearest one above the test output folder.</summary>
    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException($"No folder holding {SolutionFileName} above {TestContext.CurrentContext.TestDirectory}.");
        }
    }

    /// <summary><c>&lt;root&gt;\OldTanks\Assets\Textures</c>.</summary>
    public static string TexturesDirectory => Path.Combine(RepositoryRoot, "OldTanks", "Assets", "Textures");

    /// <summary>Every <c>*.png</c> under <see cref="TexturesDirectory"/>, relative to it, sorted ordinally.</summary>
    public static IEnumerable<string> PngTextures() => Textures("*.png");

    /// <summary>Every <c>*.jpg</c> under <see cref="TexturesDirectory"/>, relative to it, sorted ordinally.</summary>
    public static IEnumerable<string> JpegTextures() => Textures("*.jpg");

    /// <param name="relativePath">A path relative to <see cref="TexturesDirectory"/>.</param>
    public static byte[] Read(string relativePath) => File.ReadAllBytes(Path.Combine(TexturesDirectory, relativePath));

    private static IEnumerable<string> Textures(string searchPattern)
    {
        var root = TexturesDirectory;

        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('/', '\\'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
