using System.Collections.Specialized;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Extensions;
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

        m_world.CurrentCamera.PropertyChanged += CurrentCameraPropertyChanged;
        m_world.CurrentCamera.PropertyChanged += __CurrentCameraGeneratedBindingMethodToObject;
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

    private void CurrentCameraPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var cam = sender as Camera;

        if (cam == null)
            return;

        if (e.PropertyName == nameof(Camera.Direction))
        {
            m_cameraRotationDragTextBox.Value = cam.Direction.ToSystemVector3();
        }
    }
    
    private void ShowTextureWindowOnClick(ImGuiButton sender, EventArgs e)
    {
        m_textureWindow.Show();
    }
}