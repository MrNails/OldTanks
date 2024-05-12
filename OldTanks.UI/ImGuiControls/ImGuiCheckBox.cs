using Common;
using Common.Infrastructure.Delegates;
using Common.Infrastructure.EventArgs;
using ImGuiNET;
using OldTanks.UI.Services;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiCheckBox : ImGuiControl
{
    private bool m_isChecked;

    public event EventHandler<ImGuiCheckBox, ValueChangedEventArgs<bool>>? Checked;

    public ImGuiCheckBox(string name) : base(name) {}

    public bool IsChecked
    {
        get => m_isChecked;
        set => m_isChecked = value;
    }

    public override void Draw()
    {
        base.Draw();
        
        if (!IsVisible)
            return;

        var oldIsChecked = m_isChecked;
        ImGui.Checkbox(Name, ref m_isChecked);
        
        if (oldIsChecked != m_isChecked)
            Checked?.Invoke(this, new ValueChangedEventArgs<bool>(oldIsChecked, m_isChecked));
    }
}