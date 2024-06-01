using System.Numerics;
using ImGuiNET;
using OldTanks.UI.Services;
using OpenTK.Graphics.OpenGL4;

namespace OldTanks.UI.ImGuiControls;

public sealed class ImGuiImage : ImGuiControl
{
    private int m_textureId;
    private Vector2 m_size;

    public ImGuiImage(string name) : base(name) {}

    public int Texture
    {
        get => m_textureId;
        set => SetField(ref m_textureId, value);
    }

    public Vector2 Size
    {
        get => m_size;
        set
        {
            if (m_size == value) 
                return;

            if (m_size.X < 0 || m_size.Y < 0)
                throw new ArgumentException("Part of size or whole size cannot be less than 0");
            
            m_size = value;
            OnPropertyChanged();
        }
    }

    public override void Draw()
    {
        if (!IsVisible || 
            m_size.X == 0 || 
            m_size.Y == 0)
            return;
        
        ImGui.Image(m_textureId, m_size);
    }
}