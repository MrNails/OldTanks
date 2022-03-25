using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using FreeTypeSharp.Native;
using GraphicalEngine.Services;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GraphicalEngine.Core.Font;

public class CharacterInfo
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
    /// The horizontal advance e.g. the horizontal distance (in 1/64th pixels) from the origin to the origin of the next glyph
    /// </summary>
    public int Advance { get; }

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

    public static FontInformation? CreateFont(string path)
    {
        var lib = new FreeTypeLibrary();
        var charsResult = new Dictionary<char, CharacterInfo>();
        var pixelHeight = 32;

        IntPtr facePtr = IntPtr.Zero;

        try
        {
            var newFaceResp = FT.FT_New_Face(lib.Native, path, 0, out facePtr);

            if (newFaceResp != FT_Error.FT_Err_Ok)
                return null;

            var face = new FreeTypeFaceFacade(lib, facePtr);

            var pixelSizeResp = FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)pixelHeight);

            if (pixelSizeResp != FT_Error.FT_Err_Ok)
                return null;

            var chars = new List<KeyValuePair<uint, uint>>
            {
                new KeyValuePair<uint, uint>(' ', '~'),
                new KeyValuePair<uint, uint>('А', 'я')
            };

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            var fontLoadDatas = new FontLoadData[('~' - ' ') + ('я' - 'А') + 1];
            var imgLength = 0;
            var maxHeight = 0;
            var charPosition = 0;
            var idx = 0;

            foreach (var charRange in chars)
            {
                for (uint i = charRange.Key; i <= charRange.Value; i++)
                {
                    if (FT.FT_Load_Char(facePtr, i, FT.FT_LOAD_RENDER) != FT_Error.FT_Err_Ok)
                        continue;

                    CharacterInfo character;

                    if (i != ' ')
                    {
                        var data = new FontLoadData(face.GlyphBitmap.buffer, (int)face.GlyphBitmap.width,
                            (int)face.GlyphBitmap.rows);
                        fontLoadDatas[idx++] = data;

                        var tmpImg = new byte[data.Width * pixelHeight];

                        for (int j = 0; j < data.Height; j++)
                        {
                            for (int k = 0; k < data.Width; k++)
                            {
                                var index = j * data.Width + k;
                                tmpImg[index] = Marshal.ReadByte(data.FontDataPtr, index);
                            }
                        }

                        for (int j = data.Height; j < pixelHeight; j++)
                        for (int k = 0; k < data.Width; k++)
                            tmpImg[j * data.Width + k] = 0;

                        data.LoadedImage = tmpImg;

                        character = new CharacterInfo(face.GlyphMetricHorizontalAdvance,
                            new Vector2(face.GlyphBitmap.width, face.GlyphBitmap.rows),
                            new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop), charPosition);

                        charPosition += data.Width;
                    }
                    else
                        character = new CharacterInfo(face.GlyphMetricHorizontalAdvance,
                            new Vector2(face.GlyphBitmap.width, face.GlyphBitmap.rows),
                            new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop), -1);



                    if (maxHeight < face.GlyphBitmap.rows)
                        maxHeight = (int)face.GlyphBitmap.rows;

                    charsResult.Add((char)i, character);
                }
            }

            imgLength = (int)charsResult.Sum(p => p.Value.Size.X * maxHeight);
            
            var img = ArrayPool<byte>.Shared.Rent(imgLength);
            
            for (int i = 0; i < maxHeight; i ++)
            {
                for (int k = 0, imgOffset = 0; k < fontLoadDatas.Length; k++)
                {
                    var imgData = fontLoadDatas[k];
            
                    if (i < imgData.Height)
                        for (int j = 0; j < imgData.Width; j++, imgOffset++)
                            img[i * (imgLength / maxHeight) + imgOffset] = imgData.LoadedImage[i * imgData.Width + j];
                    else
                        for (int j = 0; j < imgData.Width; j++, imgOffset++)
                            img[i * (imgLength / maxHeight) + imgOffset] = 0;
                }
            }

            var texture = Texture.CreateFontTexture(img, imgLength / maxHeight, maxHeight, TextureWrapMode.ClampToEdge);

            ArrayPool<byte>.Shared.Return(img);

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

            return new FontInformation(pixelHeight, Path.GetFileNameWithoutExtension(path), charsResult, texture);
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

        return null;
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