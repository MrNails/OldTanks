using System.Collections.Specialized;
using OldTanks.Models;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.ImGuiUI;

public partial class MainWindow : ImGuiWindow
{
    private World m_world;

    public MainWindow(string name, World world) : base(name)
    {
        Initialize();

        m_world = world;
        m_worldObjectsListBox!.Items = m_world.WorldObjects;
        m_worldObjectsListBox.BindingFunction = w => w.Name ?? w.GetType().Name;

        m_world.WorldObjects.CollectionChanged += WorldObjectsCollectionChanged;
        InitWorldObjectsBindings();
    }

    private void InitWorldObjectsBindings()
    {
        foreach (var worldObject in m_world.WorldObjects)
        {
            worldObject.PropertyChanged += __SelectedItemGeneratedBindingMethodToObject;
            worldObject.RigidBody.PropertyChanged += __RigidBodyGeneratedBindingMethodToObject;
        }
    }

    private void ClearObjectsSelectionButtonOnClick(ImGuiButton sender, EventArgs e)
    {
        m_worldObjectsListBox.ClearSelection();
    }

    private void WorldObjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (WorldObject worldObject in e.NewItems!)
                {
                    worldObject.PropertyChanged += __SelectedItemGeneratedBindingMethodToObject;
                    worldObject.RigidBody.PropertyChanged += __RigidBodyGeneratedBindingMethodToObject;
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (WorldObject worldObject in e.OldItems!)
                {
                    worldObject.PropertyChanged -= __SelectedItemGeneratedBindingMethodToObject;
                    worldObject.RigidBody.PropertyChanged -= __RigidBodyGeneratedBindingMethodToObject;
                }
                break;
            default:
                break;
        }
    }
}