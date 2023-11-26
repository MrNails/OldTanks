using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services;
using OldTanks.Services;
using OldTanks.UI.ImGuiControls;
using OldTanks.UI.Services.EventArgs;
using Serilog;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow : ImGuiWindow
{
    private readonly GameManager m_gameManager;
    
    private Scene? m_selectedScene;

    public TextureWindow(string name, GameManager gameManager) : base(name)
    {
        InitializeComponents();

        m_gameManager = gameManager;

        m_texturesListBox!.Items = m_gameManager.Textures;
        m_texturesListBox.SelectionChanged += TexturesListBoxOnSelectionChanged;
    }

    public Scene? SelectedScene
    {
        get => m_selectedScene;
        set
        {
            if (value == m_selectedScene) 
                return;

            var mesh = value?.Meshes.FirstOrDefault();
            var tex = 0;
            if (mesh != null)
            {
                var texture = mesh.TextureData?.Texture ?? mesh.Texture;

                if (texture != null)
                {
                    tex = texture.Handle;
                }
            }

            m_textureImage.Texture = tex;
            m_selectedScene = value;

            OnPropertyChanged();
        }
    }
    
    private void TexturesListBoxOnSelectionChanged(ImGuiListBox<string> sender, ValueChangedEventArgs<SelectionChangedArgs<string>> e)
    {
        if (sender.SelectedItem == null || SelectedScene == null)
            return;

        var texture = GlobalCache<Texture>.Default.GetItemOrDefault(e.NewValue.Item!);

        if (SelectedScene != null)
        {
            SelectedScene.Meshes.FirstOrDefault().TextureData.Texture = texture;
        }

        m_textureImage.Texture = texture?.Handle ?? 0;
    }
    
    private async void AddNewTextureButtonOnClick(ImGuiButton sender, EventArgs e)
    {
        if (!File.Exists(m_assetPathTextBox.Text))
        {
            Log.Error("Path '{TexturePath}' is not valid.", m_assetPathTextBox.Text);
            return;
        }

        var textName = Path.GetFileNameWithoutExtension(m_assetPathTextBox.Text);

        if (m_gameManager.Textures.Contains(textName))
        {
            Log.Error("Texture {TextureName} is exists.", textName);
            return;
        }
        
        var textureLoader = m_gameManager.GetTextureLoader();

        try
        {
            await textureLoader.LoadAsset(m_assetPathTextBox.Text);
            m_gameManager.Textures.Add(textName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "En error occured while loading texture {TextureName}.", textName);
        }

    }
}