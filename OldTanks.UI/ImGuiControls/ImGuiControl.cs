namespace OldTanks.UI.ImGuiControls;

public abstract class ImGuiControl
{
    private static readonly HashSet<string> s_controls = new HashSet<string>();

    private readonly List<ImGuiControl> m_children;
    private string m_name;

    protected ImGuiControl(string name)
    {
        m_children = new List<ImGuiControl>();
        Name = name;
    }

    public List<ImGuiControl> Children { get; }

    public string Name
    {
        get => m_name;
        set
        {
            if (value == null)
                value = string.Empty;

            if (value == m_name)
                return;

            if (s_controls.Contains(value))
                throw new ArgumentException($"Control with name {value} already exists.");

            s_controls.Remove(m_name);

            m_name = value;

            s_controls.Add(m_name);
        }
    }

    /// <summary>
    /// Drawing children
    /// </summary>
    public virtual void Draw()
    {
        for (int i = 0; i < Children.Count; i++)
            Children[i].Draw();
    }

    protected static bool CheckExistingControl(string name) => s_controls.Contains(name);
}