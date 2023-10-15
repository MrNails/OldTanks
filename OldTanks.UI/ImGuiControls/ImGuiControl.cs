using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.ImGuiControls;

public abstract class ImGuiControl : IControl
{
    private string m_name;

    protected ImGuiControl(string name)
    {
        Name = name;
        IsVisible = true;
        ControlHandler.Current!.RegisterControl(this);
    }
    
    public bool IsVisible { get; set; }
    
    public string Name
    {
        get => m_name;
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Name cannot be empty.");

            m_name = value;
        }
    }

    public virtual void Draw() {}

    public bool Equals(IControl? x, IControl? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        
        return x.Name == y.Name && x.IsVisible == y.IsVisible;
    }

    public int GetHashCode(IControl obj)
    {
        return HashCode.Combine(obj.Name, obj.IsVisible);
    }
}