using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.ImGuiControls;

public abstract class ImGuiControlContainer : ImGuiControl, IControlContainer
{
    private IControl? m_child;

    protected ImGuiControlContainer(string name) : base(name) { }

    public IControl? Child
    {
        get => m_child;
        set 
        {
            if (value != null)
                ControlHandler.Current!.RegisterLink(this, value);
            if (m_child != null)
                ControlHandler.Current!.UnRegisterLink(m_child);

            m_child = value; 
        }
    }
}