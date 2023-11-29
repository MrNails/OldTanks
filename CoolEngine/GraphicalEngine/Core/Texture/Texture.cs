using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public class Texture : IDisposable, IEquatable<Texture>
{
    public static Texture Empty { get; } = new Texture(0, -1, -1) { Name = "Empty" };

    private Texture(int handle, float width, float height)
    {
        Handle = handle;
        Width = width;
        Height = height;
    }

    public int Handle { get; }
    public string? Name { get; set; }
    public float Width { get; }
    public float Height { get; }
    
    public bool Disposed { get; private set; }
    
    public override string ToString()
    {
        return $"Id: {Handle}; Size: [{Width};{Height}]";
    }

    public override int GetHashCode()
    {
        return Handle;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Texture);
    }
    
    public bool Equals(Texture? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return Handle == other.Handle;
    }

    public void Use(TextureUnit unit, TextureTarget textureTarget = TextureTarget.Texture2D)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(textureTarget, Handle);
    }

    private void ReleaseUnmanagedResources()
    {
        if (!Disposed)
        {
            GL.DeleteTexture(Handle);
            Disposed = true;
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~Texture()
    {
        Log.Fatal("Abandoned Shader {ShaderName} ({ShaderId})", Name, Handle);
        ReleaseUnmanagedResources();
    }
    
    public static Texture CreateTexture2D<T>(T[] pixels, (int Width, int Height) imgSize,
        ref PixelDto pixelDto,
        TextureWrapMode textureWrapMode = TextureWrapMode.Repeat,
        bool generateMipMap = true)
        where T : unmanaged
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            pixelDto.PixelInternalFormat,
            imgSize.Width,
            imgSize.Height,
            0,
            pixelDto.PixelFormat,
            pixelDto.PixelType,
            pixels);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)textureWrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)textureWrapMode);

        if (generateMipMap)
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return new Texture(handle, imgSize.Width, imgSize.Height);
    }

    public static Texture CreateCubeSkyBoxTexture<T>(
        Func<int, (T[] Pixels, int Width, int Height, PixelDto pixelDto)> getSkyBoxPart)
        where T : unmanaged
    {
        int handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.TextureCubeMap, handle);

        for (var idx = 0; idx < 6; idx++)
        {
            var part = getSkyBoxPart(idx);
            var pixelDto = part.pixelDto;

            GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + idx,
                0,
                pixelDto.PixelInternalFormat,
                part.Width,
                part.Height,
                0,
                pixelDto.PixelFormat,
                pixelDto.PixelType,
                part.Pixels);
        }

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter,
            (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);

        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR,
            (int)TextureWrapMode.ClampToEdge);

        return new Texture(handle, -1, -1);
    }

    public static Texture CreateTexture2D(IntPtr ptr, (int Width, int Height) imgSize,
        ref PixelDto pixelDto,
        TextureWrapMode textureWrapMode = TextureWrapMode.Repeat,
        bool generateMipMap = true)
    {
        var handle = GL.GenTexture();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            pixelDto.PixelInternalFormat,
            imgSize.Width,
            imgSize.Height,
            0,
            pixelDto.PixelFormat,
            pixelDto.PixelType,
            ptr);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)textureWrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)textureWrapMode);

        if (generateMipMap)
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        return new Texture(handle, imgSize.Width, imgSize.Height);
    }

    public static void Use(int handle, TextureUnit unit, TextureTarget textureTarget = TextureTarget.Texture2D)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(textureTarget, handle);
    }

    public static bool operator ==(Texture? left, Texture? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Texture? left, Texture? right)
    {
        return !Equals(left, right);
    }
    
    public readonly struct PixelDto
    {
        public PixelDto(PixelInternalFormat pixelInternalFormat, PixelFormat pixelFormat, PixelType pixelType)
        {
            PixelInternalFormat = pixelInternalFormat;
            PixelFormat = pixelFormat;
            PixelType = pixelType;
        }

        public PixelInternalFormat PixelInternalFormat { get; }
        public PixelFormat PixelFormat { get; }
        public PixelType PixelType { get; }
    }
}