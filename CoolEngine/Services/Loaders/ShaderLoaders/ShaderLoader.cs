using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;
using OpenTK.Windowing.Common;
using Serilog;

namespace CoolEngine.Services.Loaders.ShaderLoaders;

public sealed class ShaderLoader : IAssetLoader
{
    private readonly ILogger m_logger;

    public ShaderLoader(ILogger logger)
    {
        m_logger = logger;
    }

    public async Task LoadAsset(string assetPath)
    {
        if (!Directory.Exists(assetPath))
        {
            m_logger.Error("Shader directory '{DirectoryPath}' is not exists", assetPath);
            return;
        }

        var shaderName = Path.GetFileName(assetPath);
        var vertShaderText =
            await File.ReadAllTextAsync(Path.Combine(assetPath, $"{Path.GetFileName(assetPath)}.vert"));
        var fragShaderText =
            await File.ReadAllTextAsync(Path.Combine(assetPath, $"{Path.GetFileName(assetPath)}.frag"));
        
        var shader = Shader.Create(vertShaderText, fragShaderText, shaderName, m_logger);

        
        GlobalCache<Shader>.Default.AddOrUpdateItem(shaderName, shader);
    }
}