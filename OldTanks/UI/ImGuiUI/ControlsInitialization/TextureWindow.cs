using System.Buffers;
using System.Numerics;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow
{
    private void InitializeComponents()
    {
        var panel = new ImGuiPanel($"Panel: {Name}");

        m_selectedItemNameTextBlock = new ImGuiTextBlock("SelectedItemTextBlock") { Text = "Selected item" };
        m_texturesListBox = new ImGuiListBox<string>("Textures", ArrayPool<string>.Shared);
        
        m_textureImage = new ImGuiImage("Texture image")
        {
            Size = new Vector2(256, 256)
        };

        m_addNewTextureButton = new ImGuiButton("Add texture");
        m_addNewTextureButton.Click += AddNewTextureButtonOnClick;

        m_assetPathTextBox = new ImGuiTextBox("Asset path");

        panel.Children.Add(m_selectedItemNameTextBlock);
        panel.Children.Add(m_textureImage);
        panel.Children.Add(new ImGuiSameLine("SameLine1"));
        panel.Children.Add(m_texturesListBox);
        panel.Children.Add(m_assetPathTextBox);
        panel.Children.Add(new ImGuiSameLine("SameLine2"));
        panel.Children.Add(m_addNewTextureButton);
        
        Child = panel;
    }

    private ImGuiTextBox m_assetPathTextBox;
    private ImGuiButton m_addNewTextureButton;
    private ImGuiTextBlock m_selectedItemNameTextBlock;
    private ImGuiListBox<string> m_texturesListBox;
    private ImGuiImage m_textureImage;
}