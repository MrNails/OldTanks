using System.Numerics;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow
{
    private void InitializeComponents()
    {
        var panel = new ImGuiPanel($"Panel: {Name}");

        m_textureImage = new ImGuiImage("Texture image")
        {
            Size = new Vector2(256, 256)
        };

        panel.Children.Add(m_textureImage);
        
        Child = panel;
    }

    private ImGuiImage m_textureImage;
}