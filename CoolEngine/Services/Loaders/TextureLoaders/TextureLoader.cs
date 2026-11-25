using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Imaging;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace CoolEngine.Services.Loaders.TextureLoaders;

public sealed class TextureLoader : IAssetLoader
{
    private readonly ILogger m_logger;

    public TextureLoader(ILogger logger)
    {
        m_logger = logger;
    }

    public async Task LoadAsset(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            m_logger.Error("Texture '{TexturePath}' is not exists", assetPath);
            return;
        }

        var tName = Path.GetFileNameWithoutExtension(assetPath);
        
        var image = await ImageDecoder.LoadAsync(assetPath, flipVertically: true);

        var pixelDto = new Texture.PixelDto(PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
        var texture = Texture.CreateTexture2D(image.Pixels, (image.Width, image.Height), ref pixelDto);
        texture.Name = tName;

        GlobalCache<Texture>.Default.AddOrUpdateItem(tName, texture);
    }
}