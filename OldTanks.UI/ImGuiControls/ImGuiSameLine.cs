using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public sealed class ImGuiSameLine : ImGuiControl
{
    private float m_offsetX;
    private float m_spacing;

    public ImGuiSameLine(string name) : base(name) { }

    public float OffsetX
    {
        get => m_offsetX;
        set => SetField(ref m_offsetX, value);
    }

    public float Spacing
    {
        get => m_spacing;
        set => SetField(ref m_spacing, value);
    }

    public override void Draw()
    {
        if (!IsVisible)
            return;

        if (m_offsetX == 0)
            ImGui.SameLine();
        else
            ImGui.SameLine(m_offsetX, m_spacing);
    }
}