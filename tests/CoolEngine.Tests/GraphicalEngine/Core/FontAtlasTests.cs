using System.Buffers;
using FontType = CoolEngine.GraphicalEngine.Core.Font.Font;

namespace CoolEngine.Tests.GraphicalEngine.Core;

/// <summary>
/// <see cref="FontType.BuildAtlasAsync"/> (the CPU part of <c>Font.CreateFont</c>, no OpenGL) against golden files recorded
/// from the FreeType path it replaced (OT-4 WP7): <c>Assets\Fonts\&lt;name&gt;-32.golden.txt</c>. The golden atlas has its
/// never-drawn tail columns zeroed. The goldens are not regenerated to make a test pass.
/// </summary>
[TestFixture]
public class FontAtlasTests
{
    [TestCase("Arial")]
    [TestCase("TimesNewRoman")]
    public async Task BuildAtlasAsync_GameFont_EqualsGoldenFromFreeType(string name)
    {
        var path = Path.Combine(FontAtlasGolden.GameFontsDirectory, name + ".ttf");

        var atlas = await FontType.BuildAtlasAsync(path, FontAtlasGolden.PixelHeight, Serilog.Core.Logger.None);

        Assert.That(atlas, Is.Not.Null);

        var expected = FontAtlasGolden.ReadGolden(name);
        var actual = FontAtlasGolden.Format(atlas!);

        // Atlas size and hash first (lines 0-2), then every character.
        Assert.Multiple(() =>
        {
            Assert.That(actual.Take(3), Is.EqualTo(expected.Take(3)), "atlas width, height and SHA-256");
            Assert.That(actual, Is.EqualTo(expected), "every line");
        });
    }

    [Test]
    public async Task BuildAtlasAsync_PoolHandsOutDirtyArrays_StillEqualsGolden()
    {
        var path = Path.Combine(FontAtlasGolden.GameFontsDirectory, "Arial.ttf");
        var pool = new DirtyPool();

        var atlas = await FontType.BuildAtlasAsync(path, FontAtlasGolden.PixelHeight, Serilog.Core.Logger.None, pool);

        Assert.That(atlas, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(pool.Rented, Is.EqualTo(1), "the atlas is rented from the given pool");
            Assert.That(FontAtlasGolden.Format(atlas!), Is.EqualTo(FontAtlasGolden.ReadGolden("Arial")));
        });
    }

    /// <summary>
    /// A CFF font (Source Sans 3, OpenType CFF, from the fonts library's test assets) builds an atlas with the CFF hinting of
    /// OT-4 WP6: every character of both ranges present, each drawn character with a bitmap and a positive advance. No golden
    /// data: the hinted glyphs themselves are compared with FreeType in the fonts library's tests.
    /// </summary>
    [Test]
    public async Task BuildAtlasAsync_CffFont_BuildsEveryCharacter()
    {
        var path = Path.Combine(FontAtlasGolden.RepositoryRoot, "tests", "CoolEngine.Fonts.Tests", "Assets", "SourceSans3",
            "SourceSans3-Regular.otf");
        var expected = Enumerable.Range(' ', '~' - ' ' + 1).Concat(Enumerable.Range('А', 'я' - 'А' + 1)).Select(c => (char)c).ToArray();

        var atlas = await FontType.BuildAtlasAsync(path, FontAtlasGolden.PixelHeight, Serilog.Core.Logger.None);

        Assert.That(atlas, Is.Not.Null);
        var drawn = atlas!.Characters.Where(pair => pair.Key != ' ').Select(pair => pair.Value).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(atlas.Characters.Keys.Order(), Is.EqualTo(expected), "every character of both ranges");
            Assert.That(atlas.Width, Is.Positive);
            Assert.That(atlas.Height, Is.Positive);
            Assert.That(atlas.Pixels.AsSpan(0, atlas.Width * atlas.Height).ToArray(), Has.Some.Not.Zero, "the atlas has ink");
            Assert.That(drawn.Select(info => info.Size.X * info.Size.Y), Is.All.Positive, "every drawn character has a bitmap");
            Assert.That(drawn.Select(info => info.Position), Is.All.GreaterThanOrEqualTo(0), "every drawn character is placed");
            Assert.That(atlas.Characters.Values.Select(info => info.Advance), Is.All.Positive, "every advance is positive");
        });
    }

    /// <summary>A pool whose arrays come back full of 0xFF, as a reused pooled array can hold anything.</summary>
    private sealed class DirtyPool : ArrayPool<byte>
    {
        public int Rented { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Rented++;
            var array = new byte[minimumLength];
            Array.Fill(array, (byte)0xFF);

            return array;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
        }
    }
}
