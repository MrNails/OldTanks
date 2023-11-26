using OldTanks.UI.Services.CustomExceptions;
using OldTanks.UI.Services.Interfaces;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace OldTanks.UI.Services;

public sealed class ControlHandler
{
    private static object CurrentInstanceLocker = new object();
    
    private readonly HashSet<IControl> m_controls;
    private readonly Dictionary<IControl, IControl> m_linkedControls;
    private readonly List<IControl> m_windows;

    public ControlHandler()
    {
        m_controls = new HashSet<IControl>();
        m_linkedControls = new Dictionary<IControl, IControl>();
        m_windows = new List<IControl>();
        
        lock (CurrentInstanceLocker)
        {
            if (Current != null)
                throw new InvalidOperationException("Cannot create ControlHandler. It already created.");

            Current = this;
        }
    }

    internal bool AddWindow(IControl window)
    {
        if (m_windows.Contains(window))
        {
            return false;
        }
        
        m_windows.Add(window);

        return true;
    }
    
    internal bool RemoveWindow(IControl window)
    {
        return m_windows.Remove(window);
    }
    
    internal void RegisterControl(IControl control)
    {
        if (!m_controls.Add(control)) 
            throw new InvalidOperationException($"Control with name {control.Name} already exists.");
    }

    internal bool UnRegisterControl(IControl control) => m_controls.Remove(control);

    internal void RegisterLink(IControl parent, IControl child)
    {
        if (m_linkedControls.ContainsKey(child) ||
            m_linkedControls.TryGetValue(parent, out var tmpChild) &&
            tmpChild == child)
            throw new ControlLinkedException($"Cannot link {child.Name} to {parent.Name}");
        
        m_linkedControls.Add(child, parent);
    }
    
    internal bool UnRegisterLink(IControl child)
    {
        return m_linkedControls.Remove(child);
    }

    public void HandleControls()
    {
        for (int i = 0; i < m_windows.Count; i++)
        {
            m_windows[i].Draw();
        }
    }

    public static ControlHandler? Current { get; private set; }
}