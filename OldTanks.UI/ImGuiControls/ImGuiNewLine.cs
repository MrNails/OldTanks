using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public sealed class ImGuiNewLine : ImGuiControl
{
    public ImGuiNewLine() : this($"NL: {Guid.NewGuid()}") {}
    public ImGuiNewLine(string name) : base(name)  { }

    public override void Draw()
    {
        if (IsVisible) ImGui.NewLine();
    }
}