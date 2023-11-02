using Common.Services;
using CoolEngine.Services;
using OldTanks.DataModels;
using OldTanks.Services;
using OldTanks.Windows;
using Serilog;

namespace OldTanks;

public static class Program
{
    private static Application s_app;
    
    public static int Main(string[] args)
    {
        var loggerService = new LoggerService();
        Log.Logger = loggerService.CreateLogger();
        
        s_app = new Application();

        
        Log.Logger.Information("Thread Id {ThreadId}", Thread.CurrentThread.ManagedThreadId);

        using var mainWindow = new MainWindow("Test", SetUpSettings(), loggerService);

        mainWindow.Run();

        return 0;
    }

    private static SettingsService SetUpSettings()
    {
        var settingsService = new SettingsService();
        var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var projectDirectory = Path.Combine(userProfileFolder, "OldTanksProject");
        var settingsPath = Path.Combine(projectDirectory, "AppSettings.json");

        if (!Directory.Exists(projectDirectory))
            Directory.CreateDirectory(projectDirectory);

        var defaultSettings = settingsService.LoadSettings<Settings>(settingsPath)
                              ?? CreateDefaultSettings();
        settingsService.SetDefaultSettings(defaultSettings);
        settingsService.SaveSettings(settingsPath, defaultSettings);

        return settingsService;
    }

    private static Settings CreateDefaultSettings()
    {
        return new Settings
        {
            Sensitivity = 0.1f,
            Height = 600,
            Width = 800,
            FullScreen = false,
            AssetPath = "Assets",
            FontsDirectory = "Fonts",
            ModelsDirectory = "Models",
            ShadersDirectory = "Shaders",
            TexturesDirectory = "Textures",
            SkyBoxesDirectory = @"Textures\SkyBoxes"
        };
    }
    
    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.IsTerminating)
        {
            Log.Logger.Fatal(e.ExceptionObject as Exception, "Fatal error.");
        }
        else
        {
            Log.Logger.Error(e.ExceptionObject as Exception, "Unexpected error occured.");
        }
    }
}
