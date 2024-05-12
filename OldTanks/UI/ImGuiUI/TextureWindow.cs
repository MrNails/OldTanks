using System.Collections.ObjectModel;
using Common.Infrastructure.EventArgs;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using OldTanks.Services;
using OldTanks.UI.ImGuiControls;
using Serilog;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow : ImGuiWindow
{
    private readonly GameManager m_gameManager;
    private readonly Dictionary<Texture, string> m_texturesCache;

    private IDrawable? m_selectedDrawable;

    public TextureWindow(string name, GameManager gameManager) : base(name)
    {
        InitializeComponents();

        m_gameManager = gameManager;

        m_texturesListBox!.Items = m_gameManager.Textures;
        m_texturesListBox.SelectionChanged += TexturesListBoxOnSelectionChanged;
        
        m_texturedObjectData!.SelectionChanged += TexturedObjectDataOnSelectionChanged;
        m_texturedObjectData.BindingFunction += tObjInfo => nameof(TexturedObjectInfo);

        m_meshes!.SelectionChanged += MeshesOnSelectionChanged;
        m_meshes.BindingFunction += mesh => nameof(Mesh);
        
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

    //TODO: Change UI according to new changes for textures
    public IDrawable? SelectedDrawable
    {
        get => m_selectedDrawable;
        set
        {
            if (value == m_selectedDrawable)
                return;
            
            m_selectedDrawable = value;
            m_selectedItemNameTextBlock.Text = $"Item: {m_selectedDrawable?.Name}";
            
            if (m_selectedDrawable is not null)
            {
                m_texturedObjectData.Items = m_selectedDrawable.TexturedObjectInfos;
                m_texturedObjectData.SelectedIndex = 0;
            }

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
        if (sender.SelectedItem is null || SelectedDrawable is null ||
            m_texturedObjectData.SelectedItem is null || m_meshes.SelectedItem is null)
            return;

        var texture = GlobalCache<Texture>.Default.GetItemOrDefault(e.NewValue.Item!)!;
        var mesh = m_meshes.SelectedItem;
        var texturedObjectInfo = m_texturedObjectData.SelectedItem;
        
        texturedObjectInfo.ChangeTexture(mesh, texture);

        m_textureImage.Texture = texture.Handle;
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
    
    private void MeshesOnSelectionChanged(ImGuiListBox<Mesh> sender, ValueChangedEventArgs<SelectionChangedArgs<Mesh>> e)
    {
        if (sender.SelectedItem is null)
            return;
        
        var mesh = sender.SelectedItem;
        var texture = m_texturedObjectData.SelectedItem![mesh].Texture;
        
        if (texture != Texture.Empty &&
            m_texturesCache.TryGetValue(texture, out var textName))
            m_texturesListBox.SelectedItem = textName;
        else
            m_texturesListBox.SelectedItem = null;
        
        m_textureImage.Texture =  texture.Handle;
    }

    private void TexturedObjectDataOnSelectionChanged(ImGuiListBox<TexturedObjectInfo> sender, ValueChangedEventArgs<SelectionChangedArgs<TexturedObjectInfo>> e)
    {
        if (sender.SelectedItem is not null)
        {
            m_meshes.Items = new ObservableCollection<Mesh>(sender.SelectedItem.TexturedMeshes.Select(g => g.Key));
            m_meshes.SelectedItem = m_meshes.Items.FirstOrDefault();
        }
    }
}