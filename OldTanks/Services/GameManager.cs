using Common.Services;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Loaders.ModelsLoaders;
using CoolEngine.Services.Loaders.ShaderLoaders;
using CoolEngine.Services.Loaders.TextureLoaders;
using OldTanks.DataModels;
using OldTanks.Models;
using OldTanks.UI.Services;
using Serilog;

namespace OldTanks.Services;

public sealed class GameManager
{
    private readonly ILogger m_logger;
    private readonly LoggerService m_loggerService;
    private readonly SettingsService m_settingsService;

    private readonly Dictionary<string, IAssetLoader> m_assetLoaders;

    public EventHandler<GameManager, EventArgs>? ShadersLoaded;
    public EventHandler<GameManager, EventArgs>? TexturesLoaded;
    public EventHandler<GameManager, EventArgs>? SkyBoxesLoaded;
    public EventHandler<GameManager, EventArgs>? ModelsLoaded;
    public EventHandler<GameManager, EventArgs>? FontsLoaded;

    public GameManager(LoggerService loggerService, SettingsService settingsService, World world)
    {
        m_loggerService = loggerService;
        m_logger = m_loggerService.CreateLogger();
        m_settingsService = settingsService;
        
        m_assetLoaders = new Dictionary<string, IAssetLoader>
        {
            { nameof(ShaderLoader), new ShaderLoader(m_loggerService.CreateLogger("ShaderLog.txt")) },
            { nameof(TextureLoader), new TextureLoader(m_loggerService.CreateLogger("TextureLog.txt")) },
            { nameof(WaveFrontLoader), new WaveFrontLoader(m_loggerService.CreateLogger("WaveFontLog.txt")) },
            { nameof(SkyBoxTextureLoader), new SkyBoxTextureLoader(m_loggerService.CreateLogger("TextureLog.txt")) },
        };

        World = world;
    }

    public World World { get; }
    
    public IAssetLoader GetShaderLoader() => m_assetLoaders[nameof(ShaderLoader)];
    public IAssetLoader GetTextureLoader() => m_assetLoaders[nameof(TextureLoader)];
    public IAssetLoader GetSkyBoxTextureLoader() => m_assetLoaders[nameof(SkyBoxTextureLoader)];
    public IAssetLoader GetWaveFrontModelLoader() => m_assetLoaders[nameof(WaveFrontLoader)];

    public async Task LoadShaders()
    {
        var loader = GetShaderLoader();
        var defaultSettings = m_settingsService.GetDefaultSettings<Settings>();

        var shaderDirPath = Path.Combine(Environment.CurrentDirectory, defaultSettings.AssetPath, defaultSettings.ShadersDirectory);

        foreach (var shaderDir in Directory.GetDirectories(shaderDirPath))
        {
            await loader.LoadAsset(shaderDir);
        }
        
        ShadersLoaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadTextures()
    {
        var loader = GetTextureLoader();
        var defaultSettings = m_settingsService.GetDefaultSettings<Settings>();

        var shaderDirPath = Path.Combine(Environment.CurrentDirectory, defaultSettings.AssetPath, defaultSettings.TexturesDirectory);

        foreach (var textureFile in Directory.GetFiles(shaderDirPath))
        {
            await loader.LoadAsset(textureFile);
        }
        
        TexturesLoaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadSkyBoxes()
    {
        var loader = GetSkyBoxTextureLoader();
        var defaultSettings = m_settingsService.GetDefaultSettings<Settings>();

        var skyBoxesDir = Path.Combine(Environment.CurrentDirectory, defaultSettings.AssetPath, defaultSettings.SkyBoxesDirectory);
        
        foreach (var skyboxDir in Directory.GetDirectories(skyBoxesDir))
        {
            await loader.LoadAsset(skyboxDir);
        }
        
        SkyBoxesLoaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadFonts()
    {
        var defaultSettings = m_settingsService.GetDefaultSettings<Settings>();

        var fontsDirPath = Path.Combine(Environment.CurrentDirectory, defaultSettings.AssetPath, defaultSettings.FontsDirectory);

        foreach (var fontPath in Directory.GetFiles(fontsDirPath))
        {
            var fontInformation = await Font.CreateFont(fontPath, m_logger);

            if (fontInformation != null)
                GlobalCache<FontInformation>.Default.AddOrUpdateItem(fontInformation.FontName, fontInformation);
        }
        
        FontsLoaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadModels()
    {
        var loader = GetWaveFrontModelLoader();
        var defaultSettings = m_settingsService.GetDefaultSettings<Settings>();

        var modelsDirPath = Path.Combine(Environment.CurrentDirectory, defaultSettings.AssetPath, defaultSettings.ModelsDirectory);

        await LoadModelsFromDir(modelsDirPath, loader);
        
        foreach (var modelsSubDirectory in Directory.GetDirectories(modelsDirPath))
        {
            await LoadModelsFromDir(modelsSubDirectory, loader);
        }
        
        ModelsLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void HandleCollisions()
    {
        
    }

    public void Draw()
    {
        
    }
    
    private async Task LoadModelsFromDir(string dirPath, IAssetLoader modelLoader)
    {
        foreach (var modelPath in Directory.GetFiles(dirPath))
        {
            var modelName = Path.GetFileNameWithoutExtension(modelPath);
            
            try
            {
                await modelLoader.LoadAsset(modelPath);

                m_logger.Information("Model {ModelName} loaded.", modelName);
            }
            catch (Exception e)
            {
                m_logger.Error(e, "Error loading model {ModelName}.", modelName);
            }
        }
    }
}