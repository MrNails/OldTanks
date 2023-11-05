using System.Buffers;
using System.Runtime.InteropServices;
using CoolEngine.Services;
using FreeTypeSharp;
using FreeTypeSharp.Native;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
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
        var lib = new FreeTypeLibrary();
        var pixelHeight = 32;

        var facePtr = IntPtr.Zero;

        try
        {
            UI.UIInvoke(() => GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1));

            var loadedData = await Task.Run(() =>
            {
                var newFaceResp = FT.FT_New_Face(lib.Native, path, 0, out facePtr);

                if (newFaceResp != FT_Error.FT_Err_Ok)
                {
                    logger.Warning("Error creating new face. {Error} ({ErrorCode:X})", 
                        newFaceResp, (int)newFaceResp);
                    return (null, null);
                }

                var face = new FreeTypeFaceFacade(lib, facePtr);

                var pixelSizeResp = FT.FT_Set_Pixel_Sizes(facePtr, 0, (uint)pixelHeight);

                if (pixelSizeResp != FT_Error.FT_Err_Ok)
                {
                    logger.Warning("Error setting pixel size. {Error} ({ErrorCode:X})", 
                        newFaceResp, (int)newFaceResp);
                    return (null, null);
                }

                var (fontLoadDatas, charsResult) = CreateFontDatas(facePtr, face, pixelHeight, logger, out var maxHeight);
                var imgLength = (int)charsResult.Sum(p => (p.Value.Size.X + s_sidesPadding * 2) * maxHeight);
            
                var img = CreateFontImage(imgLength, maxHeight, fontLoadDatas, out var width);
                var pixelDto = new Texture.Texture.PixelDto(PixelInternalFormat.CompressedRed, PixelFormat.Red, PixelType.UnsignedByte);
                
                var texture = UI.UIInvoke(() => 
                    Texture.Texture.CreateTexture2D(img, (width, maxHeight), ref pixelDto, TextureWrapMode.ClampToEdge));

                ArrayPool<byte>.Shared.Return(img);

                return (texture, charsResult);
            });

            UI.UIInvoke(() => GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4));

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
        finally
        {
            if (facePtr != IntPtr.Zero)
            {
                FT.FT_Done_Face(facePtr);
                lib.Dispose();
            }
        }

        return null;
    }
    private static (FontLoadData[], Dictionary<char, CharacterInfo>) CreateFontDatas(IntPtr facePtr, FreeTypeFaceFacade face, 
        int pixelHeight, ILogger logger, out int maxHeight)
    {
        var chars = new List<KeyValuePair<uint, uint>>
        {
            new KeyValuePair<uint, uint>(' ', '~'),
            new KeyValuePair<uint, uint>('А', 'я')
        };
        var charsResult = new Dictionary<char, CharacterInfo>();
        
        var fontLoadDatas = new FontLoadData[('~' - ' ') + ('я' - 'А') + 1];
        var imgLength = 0;
        maxHeight = 0;
        var charPosition = 0;
        var idx = 0;

        foreach (var charRange in chars)
        {
            for (uint i = charRange.Key; i <= charRange.Value; i++)
            {
                var loadCharResult = FT.FT_Load_Char(facePtr, i, FT.FT_LOAD_RENDER);
                if (loadCharResult != FT_Error.FT_Err_Ok)
                {
                    logger.Warning("Error loading character {Character} with error {Error} ({ErrorCode:X})", 
                        (char)i, loadCharResult, (int)loadCharResult);
                    continue;
                }

                CharacterInfo character;

                if (i != ' ')
                {
                    var data = new FontLoadData(face.GlyphBitmap.buffer, (int)face.GlyphBitmap.width,
                        (int)face.GlyphBitmap.rows);
                    fontLoadDatas[idx] = data;
                    idx++;

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
                {
                    character = new CharacterInfo(face.GlyphMetricHorizontalAdvance,
                        new Vector2(face.GlyphBitmap.width, face.GlyphBitmap.rows),
                        new Vector2(face.GlyphBitmapLeft, face.GlyphBitmapTop), -1);
                }

                if (maxHeight < face.GlyphBitmap.rows)
                    maxHeight = (int)face.GlyphBitmap.rows;

                charsResult.Add((char)i, character);
            }
        }

        return (fontLoadDatas, charsResult);
    }
    
    private static byte[] CreateFontImage(int imgLength, int maxHeight, FontLoadData[] fontLoadDatas, out int width)
    {
        var img = ArrayPool<byte>.Shared.Rent(imgLength);
        width = imgLength / maxHeight;

        for (int row = 0; row < maxHeight; row++)
        {
            for (int k = 0, imgOffset = 0; k < fontLoadDatas.Length; k++)
            {
                var imgData = fontLoadDatas[k];

                for (int paddingLeft = 0; paddingLeft < s_sidesPadding; paddingLeft++, imgOffset++)
                    img[row * width + imgOffset] = 0;

                if (row < imgData.Height)
                    for (int column = 0; column < imgData.Width; column++, imgOffset++)
                        img[row * width + imgOffset] = imgData.LoadedImage[row * imgData.Width + column];
                else
                    for (int column = 0; column < imgData.Width; column++, imgOffset++)
                        img[row * width + imgOffset] = 0;

                for (int paddingRight = 0; paddingRight < s_sidesPadding; paddingRight++, imgOffset++)
                    img[row * width + imgOffset] = 0;
            }
        }

        return img;
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