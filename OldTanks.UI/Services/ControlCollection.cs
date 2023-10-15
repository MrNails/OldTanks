using System.Collections;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.Services;

public sealed class ControlCollection : ICollection<IControl>
{
    private readonly HashSet<IControl> m_controls;
    private readonly IControl m_parent;

    public ControlCollection(IControl parent)
    {
        if (ControlHandler.Current == null)
            throw new InvalidOperationException("Cannot create ControlCollection due to ControlHandler.Current is null");
        
        m_controls = new HashSet<IControl>();
        m_parent = parent;
    }
    
    public int Count => m_controls.Count;

    public bool IsReadOnly => false;

    public IEnumerator<IControl> GetEnumerator() => m_controls.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public void Add(IControl item)
    {
        ControlHandler.Current!.RegisterLink(m_parent, item);
        m_controls.Add(item);
    }
    
    public bool Remove(IControl item)
    {
        var removed = m_controls.Remove(item);
        
        if (removed)
            ControlHandler.Current!.UnRegisterLink(item);

        return removed;
    }

    public void Clear()
    {
        foreach (var control in m_controls)
            ControlHandler.Current!.UnRegisterLink(control);

        m_controls.Clear();
    }

    public bool Contains(IControl item) => m_controls.Contains(item);

    public void CopyTo(IControl[] array, int arrayIndex) => throw new NotSupportedException();
}