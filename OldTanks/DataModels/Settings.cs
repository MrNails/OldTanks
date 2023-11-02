using Common.Models;
using OpenTK.Mathematics;

namespace OldTanks.DataModels;

public sealed class Settings : ObservableObject
{
    private string m_assetPath;
    private string m_texturesDirectory;
    private string m_modelsDirectory;
    private string m_skyBoxesDirectory;
    private string m_shadersDirectory;
    private string m_fontsDirectory;
    private bool m_fullScreen;
    private int m_width;
    private int m_height;
    private float m_sensitivity;

    public string AssetPath
    {
        get => m_assetPath;
        set => SetField(ref m_assetPath, value);
    }

    public string TexturesDirectory
    {
        get => m_texturesDirectory;
        set => SetField(ref m_texturesDirectory, value);
    }

    public string SkyBoxesDirectory
    {
        get => m_skyBoxesDirectory;
        set => SetField(ref m_skyBoxesDirectory, value);
    }
    
    public string ModelsDirectory
    {
        get => m_modelsDirectory;
        set => SetField(ref m_modelsDirectory, value);
    }

    public string ShadersDirectory
    {
        get => m_shadersDirectory;
        set => SetField(ref m_shadersDirectory, value);
    }

    public string FontsDirectory
    {
        get => m_fontsDirectory;
        set => SetField(ref m_fontsDirectory, value);
    }

    public bool FullScreen
    {
        get => m_fullScreen;
        set => SetField(ref m_fullScreen, value);
    }

    public int Width
    {
        get => m_width;
        set => SetField(ref m_width, value);
    }

    public int Height
    {
        get => m_height;
        set => SetField(ref m_height, value);
    }

    public float Sensitivity
    {
        get => m_sensitivity;
        set => SetField(ref m_sensitivity, MathHelper.Clamp(value, 0, 10));
    }
}