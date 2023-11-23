using CoolEngine.GraphicalEngine.Core;
using OldTanks.Services;
using OldTanks.UI.ImGuiControls;
using OldTanks.UI.Services;

namespace OldTanks.UI.ImGuiUI;

public sealed partial class TextureWindow : ImGuiWindow
{
    private Scene? m_selectedScene;

    public TextureWindow(string name) : base(name)
    {
        InitializeComponents();
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
}