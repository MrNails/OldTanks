using System.Buffers;
using System.Runtime.InteropServices;
using CoolEngine.Services;
using FreeTypeSharp;
using FreeTypeSharp.Native;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core.Font;

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
    private static readonly int s_sidesPadding = 2;
    
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

                        charPosition += s_sidesPadding;

                        character = new CharacterInfo(face.GlyphMetricHorizontalAdvance,
                            new Vector2(face.GlyphBitmap.width, face.GlyphBitmap.rows),
                            new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop), charPosition);

                        charPosition += data.Width + s_sidesPadding;
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

            imgLength = (int)charsResult.Sum(p => (p.Value.Size.X + s_sidesPadding * 2) * maxHeight);
            
            var img = ArrayPool<byte>.Shared.Rent(imgLength);
            
            for (int row = 0; row < maxHeight; row ++)
            {
                for (int k = 0, imgOffset = 0; k < fontLoadDatas.Length; k++)
                {
                    var imgData = fontLoadDatas[k];

                    for (int paddingLeft = 0; paddingLeft < s_sidesPadding; paddingLeft++, imgOffset++)
                        img[row * (imgLength / maxHeight) + imgOffset] = 0; 

                    if (row < imgData.Height)
                        for (int column = 0; column < imgData.Width; column++, imgOffset++)
                            img[row * (imgLength / maxHeight) + imgOffset] = imgData.LoadedImage[row * imgData.Width + column];
                    else
                        for (int column = 0; column < imgData.Width; column++, imgOffset++)
                            img[row * (imgLength / maxHeight) + imgOffset] = 0;
                    
                    for (int paddingRight = 0; paddingRight < s_sidesPadding; paddingRight++, imgOffset++)
                        img[row * (imgLength / maxHeight) + imgOffset] = 0; 
                }
            }

            var texture = Texture.Texture.CreateFontTexture(img, imgLength / maxHeight, maxHeight, TextureWrapMode.ClampToEdge);

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