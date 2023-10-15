using System.Numerics;
using OldTanks.Models;
using OldTanks.UI.ImGuiControls;
using OldTanks.UI.Services.EventArgs;

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
    }
    
    private void SpawnCubeBtnOnClick(ImGuiButton sender, EventArgs e)
    {
        //Not implemented
    }
    
    private void ClearObjectsSelectionButtonOnClick(ImGuiButton sender, EventArgs e)
    {
        m_worldObjectsListBox.ClearSelection();
    }
    
    private void DragFloat3TextBoxValueChanged(ImGuiFloatDragTextBoxBase<Vector3> sender, ValueChangedEventArgs<Vector3> e)
    {
        switch (sender.Name)
        {
            
        }
    }
}