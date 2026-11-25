using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Imaging;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using Serilog;

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
            var image = await ImageDecoder.LoadAsync(Path.Combine(assetPath, Parts[p]));

            skyBoxPixels.Add((image.Pixels, image.Width, image.Height));
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