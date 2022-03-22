using System.Collections.ObjectModel;
using FreeTypeSharp;
using FreeTypeSharp.Native;
using GraphicalEngine.Services;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GraphicalEngine.Core.Font;

public readonly struct CharacterInfo
{
    public CharacterInfo(int advance, Texture texture, Vector2 size, Vector2 bearing)
    {
        Advance = advance;
        Texture = texture;
        Size = size;
        Bearing = bearing;
    }

    public Vector2 Size { get; }
    public Vector2 Bearing { get; }


    /// <summary>
    /// The horizontal advance e.g. the horizontal distance (in 1/64th pixels) from the origin to the origin of the next glyph
    /// </summary>
    public int Advance { get; }

    public Texture Texture { get; }

    public override string ToString()
    {
        return $"Size: {Size.ToString()}, Bearing: {Bearing.ToString()}, Advance: {Advance}";
    }
}

public class Font
{
    private string m_name;

    private float m_fontSize;

    public Font(string name, float size)
    {
        m_name = name;
        m_fontSize = size;

        FontInformation = GlobalCache<FontInformation>.GetItemOrDefault(name) ??
                          throw new ArgumentException($"Unregistered font {name}", nameof(name));
    }

    public string Name => m_name;
    public float FontSize => m_fontSize;
    public FontInformation FontInformation { get; }

    public static bool RegisterFont(string path)
    {
        var lib = new FreeTypeLibrary();
        var charsResult = new Dictionary<char, CharacterInfo>();
        
        IntPtr facePtr = IntPtr.Zero;

        try
        {
            var newFaceResp = FT.FT_New_Face(lib.Native, path, 0, out facePtr);

            if (newFaceResp != FT_Error.FT_Err_Ok)
                return false;

            var face = new FreeTypeFaceFacade(lib, facePtr);

            var pixelSizeResp = FT.FT_Set_Pixel_Sizes(facePtr, 0, 32);

            if (pixelSizeResp != FT_Error.FT_Err_Ok)
                return false;

            var chars = new List<KeyValuePair<uint, uint>>
            {
                new KeyValuePair<uint, uint>(' ', '~'),
                new KeyValuePair<uint, uint>('А', 'я')
            };

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            foreach (var charRange in chars)
            {
                for (uint i = charRange.Key; i < charRange.Value; i++)
                {
                    if (FT.FT_Load_Char(facePtr, i, FT.FT_LOAD_RENDER) != FT_Error.FT_Err_Ok)
                        continue;

                    var texture = Core.Texture.CreateFontTexture(face.GlyphBitmap.buffer, (int)face.GlyphBitmap.width,
                        (int)face.GlyphBitmap.rows, TextureWrapMode.ClampToEdge);

                    var character = new CharacterInfo(face.GlyphMetricHorizontalAdvance, texture,
                        new Vector2(face.GlyphBitmap.width, face.GlyphBitmap.rows),
                        new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop));

                    charsResult.Add((char)i, character);
                }
            }

            var fontName = Path.GetFileNameWithoutExtension(path);

            var fontInfo = new FontInformation(32, fontName, charsResult);

            GlobalCache<FontInformation>.AddOrUpdateItem(fontName, fontInfo);

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            if (facePtr != IntPtr.Zero)
            {
                FT.FT_Done_Face(facePtr);
                lib?.Dispose();
            }
        }

        return false;
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