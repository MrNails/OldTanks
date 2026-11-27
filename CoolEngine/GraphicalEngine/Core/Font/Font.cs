using System.Buffers;
using CoolEngine.Fonts;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using OpenTK.Graphics.OpenGL4;
using System.Numerics;
using Serilog;

namespace CoolEngine.GraphicalEngine.Core.Font;

public readonly struct CharacterInfo
{
    public CharacterInfo(int advance, Vector2 size, Vector2 bearing, int position)
    {
        Advance = advance;
        Size = size;
        Bearing = bearing;
        Position = position;
    }

    /// <summary>
    /// X position in font texture
    /// </summary>
    public int Position { get; }

    public Vector2 Size { get; }

    public Vector2 Bearing { get; }


    /// <summary>
    /// The horizontal advance e.g. the horizontal distance (in whole pixels) from the origin to the origin of the next glyph
    /// </summary>
    public int Advance { get; }

    public override string ToString()
    {
        return $"Size: {Size.ToDisplayString()}, Bearing: {Bearing.ToDisplayString()}, Advance: {Advance}";
    }
}

public class Font
{
    private static readonly int s_sidesPadding = 2;

    public Font(string name, float size, FontInformation fontInformation)
    {
        Name = name;
        FontSize = size;
        FontInformation = fontInformation;
    }

    public string Name { get; }

    public float FontSize { get; }

    public FontInformation FontInformation { get; }

    public static async Task<FontInformation?> CreateFont(string path, ILogger logger)
    {
        var pixelHeight = 32;

        try
        {
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            var loadedData = await Task.Run(async () =>
            {
                var atlas = await BuildAtlasAsync(path, pixelHeight, logger).ConfigureAwait(false);

                if (atlas == null)
                {
                    return (null, null);
                }

                var img = atlas.Pixels;
                var width = atlas.Width;
                var maxHeight = atlas.Height;
                var charsResult = atlas.Characters;
                var pixelDto = new Texture.Texture.PixelDto(PixelInternalFormat.CompressedRed, PixelFormat.Red, PixelType.UnsignedByte);

                var texture = UI.UIInvoke(() => Texture.Texture.CreateTexture2D(img, (width, maxHeight), ref pixelDto, TextureWrapMode.ClampToEdge));
                texture.Name = Path.GetFileNameWithoutExtension(path);

                ArrayPool<byte>.Shared.Return(img);

                return (texture, charsResult);
            });

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

            if (loadedData.texture == null)
            {
                return null;
            }

            return new FontInformation(pixelHeight, Path.GetFileNameWithoutExtension(path), loadedData.charsResult!, loadedData.texture);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error loading font {FontName}", Path.GetFileNameWithoutExtension(path));
        }

        return null;
    }

    /// <summary>
    /// The CPU part of <see cref="CreateFont"/>, with no OpenGL call: loads the font file with <see cref="FontLoader"/>, renders
    /// U+0020..U+007E and U+0410..U+044F of face 0 at <paramref name="pixelHeight"/> pixels (hinted, TrueType interpreter v40)
    /// on one <see cref="ScaledFont"/>, in that order, and lays the glyph bitmaps side by side in one atlas.
    /// </summary>
    /// <remarks>
    /// Every glyph has <see cref="s_sidesPadding"/> empty columns on each side, and the space has no glyph in the atlas
    /// (<see cref="CharacterInfo.Position"/> -1) but its padding columns are counted in the width, at the end. The returned
    /// <see cref="FontAtlas.Pixels"/> is rented from <see cref="ArrayPool{T}.Shared"/> and zeroed, so every pixel never
    /// drawn is 0; the caller returns it to the pool it was rented from (<paramref name="pool"/>, the shared pool when null). A character whose glyph fails to load is logged and left out.
    /// A face FreeType would have auto-hinted (<see cref="FontFace.WouldBeAutoHintedByFreeType"/>) is rendered unhinted and
    /// logged as a warning. Returns <see langword="null"/> when the file has no face.
    /// </remarks>
    /// <exception cref="InvalidDataException">The file is not a valid font.</exception>
    /// <exception cref="NotSupportedException">The file's format or a feature of it is not supported.</exception>
    internal static async Task<FontAtlas?> BuildAtlasAsync(string path, int pixelHeight, ILogger logger, ArrayPool<byte>? pool = null)
    {
        var fontName = Path.GetFileNameWithoutExtension(path);
        var file = await FontLoader.LoadAsync(path).ConfigureAwait(false);

        if (file.Faces.Count == 0)
        {
            logger.Warning("Font {FontName} has no face", fontName);
            return null;
        }

        var face = file.Faces[0];

        if (face.WouldBeAutoHintedByFreeType)
        {
            logger.Warning("Font {FontName} has no hinting instructions: FreeType would have auto-hinted it, CoolEngine.Fonts renders it unhinted",
                fontName);
        }

        var scaledFont = face.CreateScaledFont(new ScaledFontOptions(pixelHeight)
        {
            Hinting = HintingMode.Native,
            Interpreter = TrueTypeInterpreterVersion.V40
        });

        var chars = new List<KeyValuePair<uint, uint>>
        {
            new KeyValuePair<uint, uint>(' ', '~'),
            new KeyValuePair<uint, uint>('А', 'я')
        };
        var charsResult = new Dictionary<char, CharacterInfo>();
        var bitmaps = new List<GlyphBitmap>();
        var maxHeight = 0;
        var width = 0;
        var charPosition = 0;

        foreach (var charRange in chars)
        {
            for (var i = charRange.Key; i <= charRange.Value; i++)
            {
                GlyphBitmap bitmap;
                GlyphMetrics metrics;

                try
                {
                    // An unmapped character is glyph 0, as FT_Load_Char loads it.
                    var glyphIndex = face.GetGlyphIndex((int)i);
                    bitmap = scaledFont.Render(glyphIndex);
                    metrics = scaledFont.GetMetrics(glyphIndex);
                }
                catch (Exception e) when (e is InvalidDataException or NotSupportedException)
                {
                    logger.Warning(e, "Error loading character {Character} of font {FontName}", (char)i, fontName);
                    continue;
                }

                // CharacterInfo.Advance is in whole pixels (the hinted 26.6 advance, shifted down by 6 bits).
                var advance = metrics.Advance >> 6;
                var size = new Vector2(bitmap.Width, bitmap.Height);
                var bearing = new Vector2(bitmap.Left, bitmap.Top);
                CharacterInfo character;

                if (i != ' ')
                {
                    bitmaps.Add(bitmap);

                    charPosition += s_sidesPadding;
                    character = new CharacterInfo(advance, size, bearing, charPosition);
                    charPosition += bitmap.Width + s_sidesPadding;
                }
                else
                {
                    character = new CharacterInfo(advance, size, bearing, -1);
                }

                if (maxHeight < bitmap.Height)
                    maxHeight = bitmap.Height;

                width += bitmap.Width + s_sidesPadding * 2;
                charsResult.Add((char)i, character);
            }
        }

        if (maxHeight == 0)
        {
            throw new InvalidDataException($"Font {fontName}: every glyph failed to load or has no bitmap (see the warnings above).");
        }

        var img = (pool ?? ArrayPool<byte>.Shared).Rent(width * maxHeight);
        Array.Clear(img, 0, width * maxHeight);

        var x = 0;

        foreach (var bitmap in bitmaps)
        {
            x += s_sidesPadding;

            for (var row = 0; row < bitmap.Height; row++)
                Array.Copy(bitmap.Pixels, row * bitmap.Width, img, row * width + x, bitmap.Width);

            x += bitmap.Width + s_sidesPadding;
        }

        return new FontAtlas(img, width, maxHeight, charsResult);
    }
}

// private void InitCharacters(IEnumerable<char> characters)
// {
//     IList<char> localChars;
//
//     if (characters is IList<char> chars)
//         localChars = chars;
//     else
//         localChars = characters.ToList();
//
//     m_characters = new Dictionary<char, CharacterInfo>(localChars.Count);
//     m_readOnlyCharacters = new ReadOnlyDictionary<char, CharacterInfo>(m_characters);
//
//     var charSize = 1 / 16.0f;
//
//     foreach (var _char in localChars)
//     {
//         int x = _char % m_characterRowAmount;
//         int y = _char / m_characterRowAmount;
//
//         var left = x * charSize;
//         var right = left + charSize;
//         var top = 1 - y * charSize;
//         var bottom = top - charSize;
//
//         var charInfo = new CharacterInfo(left, right, top, bottom, GlobalSettings.DefaultCharacterSize);
//
//         Console.WriteLine($"Char: {_char}; X: {x}; Y: {y}; CharInfo: {charInfo.ToString()}");
//
//         m_characters[_char] = charInfo;
//     }
// }
