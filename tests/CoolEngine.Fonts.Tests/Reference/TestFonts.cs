namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>A face of a test font: its file and index, shown in test names by <see cref="Name"/>.</summary>
public sealed record TestFace(string Name, string Path, int FaceIndex)
{
    public override string ToString() => Name;
}

/// <summary>
/// The test fonts: the game's two in the repository, six Windows fonts read in place (never copied; a missing one fails the
/// test), and Source Sans 3 (SIL OFL 1.1) under <c>tests\CoolEngine.Fonts.Tests\Assets\SourceSans3</c>, read from the source tree.
/// </summary>
internal static class TestFonts
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

    public static string WindowsFonts => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public static string SourceSansDirectory => Path.Combine(RepositoryRoot, "tests", "CoolEngine.Fonts.Tests", "Assets", "SourceSans3");

    /// <summary><c>OldTanks\Assets\Fonts\Arial.ttf</c>, the game's font.</summary>
    public static string RepositoryArial => Path.Combine(RepositoryRoot, "OldTanks", "Assets", "Fonts", "Arial.ttf");

    public static string RepositoryTimes => Path.Combine(RepositoryRoot, "OldTanks", "Assets", "Fonts", "TimesNewRoman.ttf");

    public static string Windows(string file) => Path.Combine(WindowsFonts, file);

    public static string SourceSans(string file) => Path.Combine(SourceSansDirectory, file);

    /// <summary>Every face with TrueType outlines: the outline, metric and bitmap tests run on these.</summary>
    public static IEnumerable<TestFace> TrueTypeFaces() =>
    [
        new("Arial (repository)", RepositoryArial, 0),
        new("Times New Roman (repository)", RepositoryTimes, 0),
        new("arial.ttf", Windows("arial.ttf"), 0),
        new("segoeui.ttf", Windows("segoeui.ttf"), 0),
        new("cambria.ttc #0", Windows("cambria.ttc"), 0),
        new("cambria.ttc #1", Windows("cambria.ttc"), 1),
        new("seguiemj.ttf", Windows("seguiemj.ttf"), 0),
        new("SitkaVF.ttf", Windows("SitkaVF.ttf"), 0),
        new("bahnschrift.ttf", Windows("bahnschrift.ttf"), 0),
        new("SourceSans3-Regular.ttf", SourceSans("SourceSans3-Regular.ttf"), 0),
        new("SourceSans3VF-Upright.ttf", SourceSans("SourceSans3VF-Upright.ttf"), 0),
    ];

    /// <summary>The faces with CFF or CFF2 outlines: loader, character map and property tests only until OT-4 WP2.</summary>
    public static IEnumerable<TestFace> CffFaces() =>
    [
        new("SourceSans3-Regular.otf", SourceSans("SourceSans3-Regular.otf"), 0),
        new("SourceSans3VF-Upright.otf", SourceSans("SourceSans3VF-Upright.otf"), 0),
    ];

    /// <summary>Every TrueType, CFF and collection face.</summary>
    public static IEnumerable<TestFace> AllFaces() => TrueTypeFaces().Concat(CffFaces());

    /// <summary>Every TrueType, CFF and collection file, once each.</summary>
    public static IEnumerable<string> AllFiles() => AllFaces().Select(face => face.Path).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>The WOFF copies of Source Sans 3.</summary>
    public static IEnumerable<string> WoffFiles() =>
        Directory.EnumerateFiles(SourceSansDirectory, "*.woff").Order(StringComparer.Ordinal);

    /// <summary>The WOFF2 copies of Source Sans 3.</summary>
    public static IEnumerable<string> Woff2Files() =>
        Directory.EnumerateFiles(SourceSansDirectory, "*.woff2").Order(StringComparer.Ordinal);

    /// <summary>The test sizes for every glyph: 9, 12, 16, 32 (the game's) and 72 pixels.</summary>
    public static readonly int[] EveryGlyphSizes = [9, 12, 16, 32, 72];

    /// <summary>The sizes for the game's characters: every size from 8 to 24 pixels.</summary>
    public static readonly int[] CharacterSizes = Enumerable.Range(8, 17).ToArray();

    /// <summary>The game's characters: ASCII (space to tilde) and Cyrillic А to я.</summary>
    public static IEnumerable<int> GameCharacters() =>
        Enumerable.Range(' ', '~' - ' ' + 1).Concat(Enumerable.Range('А', 'я' - 'А' + 1));
}
