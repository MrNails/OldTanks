using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CoolEngine.Services.Loaders.TextureLoaders;

public sealed class SkyBoxTextureLoader : IAssetLoader
{
    private static readonly string[] Parts =
        { "right.jpg", "left.jpg", "top.jpg", "bottom.jpg", "front.jpg", "back.jpg" };
    
    private readonly ILogger m_logger;

    public SkyBoxTextureLoader(ILogger logger)
    {
        m_logger = logger;
    }

    public async Task LoadAsset(string assetPath)
    {
        if (!Directory.Exists(assetPath))
        {
            m_logger.Error("SkyBox textures directory '{SkyBoxTexturesPath}' is not exists", assetPath);
            return;
        }

        var skyBoxPixels = new List<(Rgba32[] pixels, int width, int height)>(Parts.Length);
        var pixelDto = new Texture.PixelDto(PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
        for (int p = 0; p < Parts.Length; p++)
        {
            var img = await Image.LoadAsync<Rgba32>(Path.Combine(assetPath, Parts[p]));
            var tmpPixels = new Rgba32[img.Width * img.Height];

            skyBoxPixels.Add((tmpPixels, img.Width, img.Height));
            
            for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                tmpPixels[i * img.Width + j] = img[j, i];
        
            img.Dispose();
        }

        var tName = Path.GetFileNameWithoutExtension(assetPath);
        var texture = Texture.CreateCubeSkyBoxTexture((Func<int, (Rgba32[], int, int, Texture.PixelDto)>)PartHandler);
        texture.Name = tName;
        
        GlobalCache<Texture>.Default.AddOrUpdateItem(tName, texture);
        
        return;

        (Rgba32[], int, int, Texture.PixelDto) PartHandler(int partIdx)
        {
            var part = skyBoxPixels[partIdx];

            return (part.pixels, part.width, part.height, pixelDto);
        }
    }
}