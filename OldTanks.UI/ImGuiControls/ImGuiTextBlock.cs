using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiTextBlock : ImGuiControl
{
    public ImGuiTextBlock(string name) : base(name) { }

    public override void Draw()
    {
        if (IsVisible) ImGui.Text(Name);
    }
}