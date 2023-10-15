using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiPanel : ImGuiControl, IControlsContainer
{
    public ImGuiPanel(string name) : base(name)
    {
        Children = new ControlCollection(this);
    }

    public ControlCollection Children { get; }

    public override void Draw()
    {
        if (!IsVisible)
            return;

        foreach (var child in Children)
            child.Draw();
    }
}