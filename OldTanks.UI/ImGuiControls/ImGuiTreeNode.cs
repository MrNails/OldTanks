using ImGuiNET;
using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiTreeNode : ImGuiControl, IControlsContainer
{
    public ImGuiTreeNode(string name)
        : base(name)
    {
        Children = new ControlCollection(this);
    }

    public ControlCollection Children { get; }

    public bool IsExpanded { get; set; }

    public override void Draw()
    {
        IsExpanded = ImGui.TreeNodeEx(Name);

        if (!IsExpanded || !IsVisible)
            return;
        
        base.Draw();

        foreach (var child in Children)
            child.Draw();

        ImGui.TreePop();
    }
}