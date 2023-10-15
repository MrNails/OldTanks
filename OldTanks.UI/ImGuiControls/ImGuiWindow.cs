using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiWindow : ImGuiControlContainer
{
    public ImGuiWindow(string name) : base(name)
    {
    }

    public string Title { get; set; }

    public override void Draw()
    {
        if (!IsVisible) return;

        var open = IsVisible;

        ImGui.Begin(Title, ref open);

        IsVisible = open;

        if (open)
            Child?.Draw();

        ImGui.End();
    }
}