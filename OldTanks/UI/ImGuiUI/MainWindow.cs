using System.Collections.Specialized;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Renderers;
using OldTanks.Models;
using OldTanks.Services;
using OldTanks.UI.ImGuiControls;
using OpenTK.Mathematics;

namespace OldTanks.UI.ImGuiUI;

public partial class MainWindow : ImGuiWindow
{
    private readonly GameManager m_gameManager;
    private TextureWindow? m_textureWindow;

    public MainWindow(string name, GameManager gameManager) : base(name)
    {
        Initialize();

        m_gameManager = gameManager;
        m_worldObjectsListBox!.Items = m_gameManager.World.WorldObjects;
        m_worldObjectsListBox.BindingFunction = w => w.Name ?? w.GetType().Name;

        m_gameManager.World.WorldObjects.CollectionChanged += WorldObjectsCollectionChanged;
        InitWorldObjectsBindings();

        m_gameManager.World.CurrentCamera.PropertyChanged += CurrentCameraPropertyChanged;
        m_gameManager.World.CurrentCamera.PropertyChanged += __CurrentCameraGeneratedBindingMethodToObject;
    }
    
    private void InitWorldObjectsBindings()
    {
        foreach (var worldObject in m_gameManager.World.WorldObjects)
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
        m_textureWindow ??= new TextureWindow("Texture window", m_gameManager) { Title = "Texture window" };
        m_textureWindow.Show();
    }
    
    private void DeleteObjectOnClick(ImGuiButton sender, EventArgs e)
    {
        var selectedItem = m_worldObjectsListBox.SelectedItem;
        
        if (selectedItem == null) 
            return;
        
        ObjectRenderer.RemoveDrawable(selectedItem);
        CollisionRenderer.RemoveCollision(selectedItem);
        m_gameManager.World.WorldObjects.Remove(selectedItem);
    }

    private void SpawnObjectOnClick(ImGuiButton sender, EventArgs e)
    {
        var cube = new Cube
        {
            Size = Vector3.One,
            RigidBody =
            {
                IsStatic = true
            }
        };
        cube.Collision = new Collision(cube, GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));

        m_gameManager.World.WorldObjects.Add(cube);
        CollisionRenderer.AddCollision(cube);
        ObjectRenderer.AddDrawable(cube, GlobalCache<Shader>.Default.GetItemOrDefault("DefaultShader"));
    }
}