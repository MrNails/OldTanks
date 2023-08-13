using OpenTK.Mathematics;

namespace OldTanks.UI.Controls;

public abstract class Control
{
    private string m_name;

    protected Control(string name)
    {
        Name = name;
        Size = Vector2.Zero;
    }

    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    
    public Vector2 Rotation { get; set; }
    
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

    public abstract void Draw();
}