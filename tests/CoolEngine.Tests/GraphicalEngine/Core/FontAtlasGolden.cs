using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoolEngine.GraphicalEngine.Core.Font;

namespace CoolEngine.Tests.GraphicalEngine.Core;

/// <summary>
/// The text form of a font atlas used by the golden files in <c>Assets\Fonts</c>: the atlas size, the SHA-256 of its
/// <c>Width × Height</c> bytes (row-major, as uploaded) and one line per character.
/// </summary>
internal static class FontAtlasGolden
{
    public const int PixelHeight = 32;

    private const string SolutionFileName = "OldTanks.slnx";

    /// <summary><c>&lt;root&gt;\OldTanks\Assets\Fonts</c>.</summary>
    public static string GameFontsDirectory => Path.Combine(RepositoryRoot, "OldTanks", "Assets", "Fonts");

    /// <summary><c>&lt;root&gt;\tests\CoolEngine.Tests\Assets\Fonts</c>.</summary>
    public static string GoldenDirectory => Path.Combine(RepositoryRoot, "tests", "CoolEngine.Tests", "Assets", "Fonts");

    public static string GoldenPath(string fontName) => Path.Combine(GoldenDirectory, $"{fontName}-{PixelHeight}.golden.txt");

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

    public static string[] Format(FontAtlas atlas)
    {
        var lines = new List<string>
        {
            $"width {atlas.Width}",
            $"height {atlas.Height}",
            $"sha256 {Convert.ToHexString(SHA256.HashData(atlas.Pixels.AsSpan(0, atlas.Width * atlas.Height)))}"
        };

        foreach (var (character, info) in atlas.Characters.OrderBy(p => p.Key))
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"U+{(int)character:X4} advance={info.Advance} size={info.Size.X},{info.Size.Y} bearing={info.Bearing.X},{info.Bearing.Y} position={info.Position}"));
        }

        return lines.ToArray();
    }

    public static string[] ReadGolden(string fontName) =>
        File.ReadAllLines(GoldenPath(fontName), new UTF8Encoding(false));
}
