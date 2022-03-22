using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GraphicalEngine.Core;

public class Texture
{
    public int Handle { get; }
    
    public float Width { get; }
    public float Height { get; }

    private Texture(int handle, float width, float height)
    {
        Handle = handle;
        Width = width;
        Height = height;
    }

    public void Use(TextureUnit unit)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }
    
    public static Texture CreateTexture(string path)
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        var img = Image.Load<Rgba32>(path);
        img.Mutate(x => x.Flip(FlipMode.Vertical));
        
        var pixelsAmount = 4 * img.Width * img.Height;
        var pixelIndex = 0;
        var pixels = new byte[pixelsAmount];
        
        for (int i = 0; i < img.Height; i++)
        {
            var row = img.DangerousGetPixelRowMemory(i).Span;
        
            for (int j = 0; j < img.Width; j++)
            {
                var currPixels = row[j];
        
                pixels[pixelIndex++] = currPixels.R;
                pixels[pixelIndex++] = currPixels.G;
                pixels[pixelIndex++] = currPixels.B;
                pixels[pixelIndex++] = currPixels.A;
            }
        }
        
        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            img.Width,
            img.Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return new Texture(handle, img.Width, img.Height);
    }
    
    public static Texture CreateFontTexture(IntPtr textureData, int width, int height, TextureWrapMode wrapMode)
    {
        var handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            PixelInternalFormat.CompressedRed,
            width,
            height,
            0,
            PixelFormat.Red,
            PixelType.UnsignedByte,
            textureData);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int) TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
            (int) TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) wrapMode);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return new Texture(handle, width, height);
    }
}