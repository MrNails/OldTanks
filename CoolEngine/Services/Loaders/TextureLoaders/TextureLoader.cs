using System.Buffers;
using CoolEngine.GraphicalEngine.Core.Texture;
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
        
        var img = await Image.LoadAsync<Rgba32>(assetPath);
        img.Mutate(x => x.Flip(FlipMode.Vertical));
        
        var pixels = ArrayPool<Rgba32>.Shared.Rent(img.Width * img.Height);

        for (int i = 0; i < img.Height; i++)
        for (int j = 0; j < img.Width; j++)
            pixels[i * img.Width + j] = img[j, i];

        var pixelDto = new Texture.PixelDto(PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
        
        GlobalCache<Texture>.Default.AddOrUpdateItem(tName, UI.UIInvoke(() => Texture.CreateTexture2D(pixels, (img.Width, img.Height), ref pixelDto)));
        
        img.Dispose();
        ArrayPool<Rgba32>.Shared.Return(pixels);
    }
}