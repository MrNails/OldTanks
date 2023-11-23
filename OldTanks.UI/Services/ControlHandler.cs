using OldTanks.UI.Services.CustomExceptions;
using OldTanks.UI.Services.Interfaces;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace OldTanks.UI.Services;

public sealed class ControlHandler
{
    private static object CurrentInstanceLocker = new object();
    
    private HashSet<IControl> _controls;
    private Dictionary<IControl, IControl> _linkedControls;

    public ControlHandler()
    {
        _controls = new HashSet<IControl>();
        _linkedControls = new Dictionary<IControl, IControl>();
        Windows = new HashSet<IControl>();
        
        lock (CurrentInstanceLocker)
        {
            if (Current != null)
                throw new InvalidOperationException("Cannot create ControlHandler. It already created.");

            Current = this;
        }
    }
    
    internal HashSet<IControl> Windows { get; }

    internal void RegisterControl(IControl control)
    {
        if (!_controls.Add(control)) 
            throw new InvalidOperationException($"Control with name {control.Name} already exists.");
    }

    internal bool UnRegisterControl(IControl control) => _controls.Remove(control);

    internal void RegisterLink(IControl parent, IControl child)
    {
        if (_linkedControls.ContainsKey(child) ||
            _linkedControls.TryGetValue(parent, out var tmpChild) &&
            tmpChild == child)
            throw new ControlLinkedException($"Cannot link {child.Name} to {parent.Name}");
        
        _linkedControls.Add(child, parent);
    }
    
    internal bool UnRegisterLink(IControl child)
    {
        return _linkedControls.Remove(child);
    }

    public void HandleControls()
    {
        foreach (var window in Windows)
        {
            window.Draw();
        }
    }

    public static ControlHandler? Current { get; private set; }
}