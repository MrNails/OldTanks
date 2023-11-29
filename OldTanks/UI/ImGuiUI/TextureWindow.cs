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
    
    private readonly Dictionary<Texture, string> m_texturesCache;

    private Scene? m_selectedScene;

    public TextureWindow(string name, GameManager gameManager) : base(name)
    {
        InitializeComponents();

        m_gameManager = gameManager;

        m_texturesListBox!.Items = m_gameManager.Textures;
        m_texturesListBox.SelectionChanged += TexturesListBoxOnSelectionChanged;

        m_texturesCache = new Dictionary<Texture, string>();

        if (m_gameManager.Textures.Count == 0)
        {
            m_gameManager.TexturesLoaded += GameManagerTexturesLoaded;
        }
        else
        {
            foreach (var textName in m_gameManager.Textures)
            {
                AddNewTextureToCache(textName);
            }
        }
    }

    public Scene? SelectedScene
    {
        get => m_selectedScene;
        set
        {
            if (value == m_selectedScene)
                return;
            
            m_selectedScene = value;
            
            var mesh = value?.Meshes.FirstOrDefault();
            var textId = 0;
            if (mesh != null)
            {
                var texture = mesh.TextureData?.Texture ?? mesh.Texture;

                if (texture != null)
                {
                    textId = texture.Handle;
                    
                    if (m_texturesCache.TryGetValue(texture, out var textName))
                        m_texturesListBox.SelectedItem = textName;
                }
                else
                {
                    m_texturesListBox.SelectedItem = null;
                }
            }
            
            m_textureImage.Texture = textId;

            OnPropertyChanged();
        }
    }

    private void AddNewTextureToCache(string textName)
    {
        var texture = GlobalCache<Texture>.Default.GetItemOrDefault(textName);

        if (texture != null)
        {
            m_texturesCache[texture] = textName;
        }
    }
    
    private void TexturesListBoxOnSelectionChanged(ImGuiListBox<string> sender,
        ValueChangedEventArgs<SelectionChangedArgs<string>> e)
    {
        if (sender.SelectedItem == null || SelectedScene == null)
            return;

        var texture = GlobalCache<Texture>.Default.GetItemOrDefault(e.NewValue.Item!);
        var mesh = SelectedScene.Meshes.FirstOrDefault();

        if (mesh == null || mesh.TextureData.Texture == texture)
        {
            return;
        }
        
        mesh.TextureData.Texture = texture;

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
            
            AddNewTextureToCache(textName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "En error occured while loading texture {TextureName}.", textName);
        }
    }
    
    private void GameManagerTexturesLoaded(GameManager sender, EventArgs e)
    {
        for (int i = 0; i < sender.Textures.Count; i++)
        {
            var textName = sender.Textures[i];
            
            AddNewTextureToCache(textName);
        }
    }
}