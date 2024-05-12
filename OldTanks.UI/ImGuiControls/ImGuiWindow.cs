using ImGuiNET;
using OldTanks.UI.Services;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiWindow : ImGuiControlContainer, IDisposable
{
    public ImGuiWindow(string name) : base(name)
    {
        ControlHandler.Current!.AddWindow(this);
        IsVisible = false;
    }

    public string Title { get; set; }

    public override void Draw()
    {
        if (!IsVisible) return;
        
        base.Draw();

        var open = IsVisible;

        ImGui.Begin(Title, ref open);

        IsVisible = open;

        if (open)
            Child?.Draw();

        ImGui.End();
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        ControlHandler.Current!.RemoveWindow(this);
    }
}