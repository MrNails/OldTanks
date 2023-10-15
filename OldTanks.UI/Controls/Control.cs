using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;
using OpenTK.Mathematics;

namespace OldTanks.UI.Controls;

public abstract class Control : IControl
{
    private string m_name;

    protected Control(string name)
    {
        Name = name;
        Size = Vector2.Zero;
        ControlHandler.Current!.RegisterControl(this);
    }

    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public Vector2 Rotation { get; set; }

    public bool IsVisible { get; set; }

    public string Name
    {
        get => m_name;
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Name cannot be null.");
            
            m_name = value;
        }
    }

    public virtual void Draw() {  }

    public bool Equals(IControl? x, IControl? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        
        return x.Name == y.Name;
    }

    public int GetHashCode(IControl obj)
    {
        return obj.Name.GetHashCode();
    }
}