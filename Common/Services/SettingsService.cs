using System.Text.Json;

namespace Common.Services;

public sealed class SettingsService
{
    private static readonly string DefaultSettingsName = "DEFAULT";
    
    public readonly Dictionary<string, object> m_settings;

    public SettingsService()
    {
        m_settings = new Dictionary<string, object>();
    }

    public T? TryGetRuntimeSettings<T>(string settingsName)
    {
        m_settings.TryGetValue(settingsName, out var settings);

        return (T?)settings;
    }

    public void SetRuntimeSettings(string settingsName, object settings)
    {
        m_settings.Add(settingsName, settings);
    }

    public T GetDefaultSettings<T>()
    {
        return TryGetRuntimeSettings<T>(DefaultSettingsName)!;
    }

    public void SetDefaultSettings(object settings)
    {
        SetRuntimeSettings(DefaultSettingsName, settings);
    }

    public T? LoadSettings<T>(string path)
    {
        return File.Exists(path) 
            ? JsonSerializer.Deserialize<T>(File.ReadAllBytes(path)) 
            : default;
    }
    
    public void SaveSettings<T>(string path, T settings)
    {
        var serializedSettings = JsonSerializer.Serialize(settings);
        
        File.WriteAllText(path, serializedSettings);
    }
}