using System.Buffers;
using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow
{
    private void InitializeComponents()
    {
        m_container = new ImGuiPanel($"Panel: {Name}");

        m_meshes = new ImGuiListBox<Mesh>("Meshes", ArrayPool<string>.Shared) { Width = 200 };
        m_texturedObjectData = new ImGuiListBox<TexturedObjectInfo>("Object textures", ArrayPool<string>.Shared) { Width = 200 };
        
        m_selectedItemNameTextBlock = new ImGuiTextBlock("SelectedItemTextBlock") { Text = "Item:" };
        m_texturesListBox = new ImGuiListBox<string>("Textures", ArrayPool<string>.Shared);
        
        m_textureImage = new ImGuiImage("Texture image")
        {
            Size = new Vector2(256, 256)
        };

        m_addNewTextureButton = new ImGuiButton("Add texture");
        m_addNewTextureButton.Click += AddNewTextureButtonOnClick;

        m_assetPathTextBox = new ImGuiTextBox("Asset path");

        m_container.Children.Add(m_selectedItemNameTextBlock);
        m_container.Children.Add(m_texturedObjectData);
        m_container.Children.Add(new ImGuiSameLine("SameLine3"));
        m_container.Children.Add(m_meshes);
        m_container.Children.Add(m_textureImage);
        m_container.Children.Add(new ImGuiSameLine("SameLine1"));
        m_container.Children.Add(m_texturesListBox);
        m_container.Children.Add(m_assetPathTextBox);
        m_container.Children.Add(new ImGuiSameLine("SameLine2"));
        m_container.Children.Add(m_addNewTextureButton);
        
        Child = m_container;
    }

    private ImGuiPanel m_container;
    private ImGuiTextBox m_assetPathTextBox;
    private ImGuiButton m_addNewTextureButton;
    private ImGuiTextBlock m_selectedItemNameTextBlock;
    private ImGuiListBox<string> m_texturesListBox;
    private ImGuiListBox<Mesh> m_meshes;
    private ImGuiListBox<TexturedObjectInfo> m_texturedObjectData;
    private ImGuiImage m_textureImage;
}