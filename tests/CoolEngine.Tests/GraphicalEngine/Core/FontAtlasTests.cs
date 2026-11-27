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
