using Common;
using Common.Infrastructure.Delegates;
using ImGuiNET;
using OldTanks.UI.Services;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiButton : ImGuiControl
{
    public event EventHandler<ImGuiButton, EventArgs>? Click;

    public ImGuiButton(string name) : base(name) {}
    
    public override void Draw()
    {
        if (!IsVisible)
            return;
        
        base.Draw();
        
        if (ImGui.Button(Name))
            Click?.Invoke(this, EventArgs.Empty);
    }
}