using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiTextBlock : ImGuiControl
{
    private string m_text;
    
    public ImGuiTextBlock(string name) : base(name) { }

    public string Text
    {
        get => m_text;
        set => SetField(ref m_text, value);
    }

    public override void Draw()
    {
        if (!IsVisible) 
            return;
        
        base.Draw();
        ImGui.Text(Text);
    }
}