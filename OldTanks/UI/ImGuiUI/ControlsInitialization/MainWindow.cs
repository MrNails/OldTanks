using System.Buffers;
using OldTanks.Models;
using OldTanks.UI.ImGuiControls;
using OldTanks.UI.SourceGenerators.Attributes;

namespace OldTanks.UI.ImGuiUI;

[BindableClass]
public partial class MainWindow
{
    partial void InitGeneratedData();

    private void Initialize()
    {
        var panel = new ImGuiPanel($"Panel: {Name}");

        m_spawnObject = new ImGuiButton("Spawn object");
        m_spawnObject.Click += SpawnObjectOnClick;
        m_deleteObject = new ImGuiButton("Delete selected object");
        m_deleteObject.Click += DeleteObjectOnClick;
        
        m_pushForceDragTextBox = new ImGuiFloat3DragTextBox("Push force");
        m_cameraPositionDragTextBox = new ImGuiFloat3DragTextBox("CPosition");
        m_cameraRotationDragTextBox = new ImGuiFloat3DragTextBox("CRotation");
        
        m_cameraFreeModeCheckBox = new ImGuiCheckBox("Camera free mode");
        m_textBlock1 = new ImGuiTextBlock("WorldObjectsTextBlock") { Text = "World objects"};
        
        m_worldObjectsListBox = new ImGuiListBox<WorldObject>("WorldObjectsListBox", ArrayPool<string>.Shared);
        m_clearObjectsSelectionButton = new ImGuiButton("Clear selection");
        m_clearObjectsSelectionButton.Click += ClearObjectsSelectionButtonOnClick;

        m_textBlock2 = new ImGuiTextBlock("SelectedWorldObjectDataTextBlock"){ Text = "Selected world object data"};

        m_selectedObjectPositionDragTextBox = new ImGuiFloat3DragTextBox("Position");
        m_selectedObjectRotationDragTextBox = new ImGuiFloat3DragTextBox("Rotation");
        m_selectedObjectSizeDragTextBox = new ImGuiFloat3DragTextBox("Size");
        m_selectedObjectCameraOffsetDragTextBox = new ImGuiFloat3DragTextBox("Camera offset");
        m_selectedObjectCameraOffsetAngleDragTextBox = new ImGuiFloat2DragTextBox("Camera offset angle");

        m_rigidBodyTreeNode = new ImGuiTreeNode("Rigid body");

        m_isStaticCheckBox = new ImGuiCheckBox("Is static");
        m_centerOfMassDragTextBox = new ImGuiFloat3DragTextBox("Center of mass");
        m_velocityDragTextBox = new ImGuiFloat3DragTextBox("Velocity");
        m_jumpForceDragTextBox = new ImGuiFloatDragTextBox("Jump force");
        m_maxSpeedDragTextBox = new ImGuiFloatDragTextBox("Max speed");
        m_maxBackSpeedDragTextBox = new ImGuiFloatDragTextBox("Max back speed");
        m_speedMultiplierDragTextBox = new ImGuiFloatDragTextBox("Speed multiplier");
        m_weightDragTextBox = new ImGuiFloatDragTextBox("Weight");
        
        m_showTextureWindow = new ImGuiButton("Show texture window");
        m_showTextureWindow.Click += ShowTextureWindowOnClick;

        m_rigidBodyTreeNode.Children.Add(m_isStaticCheckBox);
        m_rigidBodyTreeNode.Children.Add(m_centerOfMassDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_velocityDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_jumpForceDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_maxSpeedDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_maxBackSpeedDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_speedMultiplierDragTextBox);
        m_rigidBodyTreeNode.Children.Add(m_weightDragTextBox);

        var newLine1 = new ImGuiNewLine();
        
        m_columnsControl = new ImGuiColumnsControl($"ColumnsControl: {Name}") { ColumnsAmount = 2 };
        m_columnsControl.Children.Add(m_textBlock1);
        m_columnsControl.Children.Add(m_worldObjectsListBox);
        m_columnsControl.Children.Add(m_clearObjectsSelectionButton);
        m_columnsControl.Children.Add(m_textBlock2);
        m_columnsControl.Children.Add(m_selectedObjectPositionDragTextBox);
        m_columnsControl.Children.Add(m_selectedObjectRotationDragTextBox);
        m_columnsControl.Children.Add(m_selectedObjectSizeDragTextBox);
        m_columnsControl.Children.Add(m_selectedObjectCameraOffsetDragTextBox);
        m_columnsControl.Children.Add(m_selectedObjectCameraOffsetAngleDragTextBox);
        m_columnsControl.Children.Add(newLine1);
        m_columnsControl.Children.Add(m_rigidBodyTreeNode);
        m_columnsControl.SetColumn(m_textBlock1, 0);
        m_columnsControl.SetColumn(m_worldObjectsListBox, 0);
        m_columnsControl.SetColumn(m_clearObjectsSelectionButton, 0);
        m_columnsControl.SetColumn(m_textBlock2, 1);
        m_columnsControl.SetColumn(m_selectedObjectPositionDragTextBox, 1);
        m_columnsControl.SetColumn(m_selectedObjectRotationDragTextBox, 1);
        m_columnsControl.SetColumn(m_selectedObjectSizeDragTextBox, 1);
        m_columnsControl.SetColumn(m_selectedObjectCameraOffsetDragTextBox, 1);
        m_columnsControl.SetColumn(m_selectedObjectCameraOffsetAngleDragTextBox, 1);
        m_columnsControl.SetColumn(newLine1, 1);
        m_columnsControl.SetColumn(m_rigidBodyTreeNode, 1);
        
        panel.Children.Add(new ImGuiNewLine());
        panel.Children.Add(m_showTextureWindow);
        panel.Children.Add(m_spawnObject);
        panel.Children.Add(new ImGuiSameLine("SameLine0"));
        panel.Children.Add(m_deleteObject);
        panel.Children.Add(m_pushForceDragTextBox);
        panel.Children.Add(new ImGuiNewLine());
        
        panel.Children.Add(new ImGuiTextBlock("CameraData") { Text = "Camera data"});
        panel.Children.Add(m_cameraPositionDragTextBox);
        panel.Children.Add(m_cameraRotationDragTextBox);
        panel.Children.Add(m_cameraFreeModeCheckBox);
        panel.Children.Add(new ImGuiNewLine());
        panel.Children.Add(m_columnsControl);

        Child = panel;

        InitGeneratedData();
    }

    private ImGuiButton m_spawnObject;
    private ImGuiButton m_deleteObject;
    
    private ImGuiFloat3DragTextBox m_pushForceDragTextBox;

    [BindableElement("Value", "Position",
        "m_gameManager.World.CurrentCamera", "CoolEngine.GraphicalEngine.Core.Camera", "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)",
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_cameraPositionDragTextBox;

    private ImGuiFloat3DragTextBox m_cameraRotationDragTextBox;

    private ImGuiCheckBox m_cameraFreeModeCheckBox;
    private ImGuiColumnsControl m_columnsControl;
    private ImGuiTextBlock m_textBlock1;
    
    [TriggerUpdateOn("SelectionChanged", 
        "OldTanks.UI.ImGuiControls.ImGuiListBox<OldTanks.Models.WorldObject> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<OldTanks.UI.ImGuiControls.SelectionChangedArgs<OldTanks.Models.WorldObject>> e", 
        "m_worldObjectsListBox.SelectedItem")]
    [BindableElement("SelectedItem.Scene", "SelectedScene", 
        "m_textureWindow", "CoolEngine.GraphicalEngine.Core.Scene",
        "SelectionChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiListBox<OldTanks.Models.WorldObject> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<OldTanks.UI.ImGuiControls.SelectionChangedArgs<OldTanks.Models.WorldObject>> e)",
        bindingWay: BindingWay.OneWayToSource)]
    private ImGuiListBox<WorldObject> m_worldObjectsListBox;
    
    private ImGuiButton m_clearObjectsSelectionButton;
    private ImGuiTextBlock m_textBlock2;
    
    [BindableElement("Value", "Position",
        "m_worldObjectsListBox.SelectedItem", "OldTanks.Models.WorldObject",  "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_selectedObjectPositionDragTextBox;
    
    
    // [BindableElement("Value", "Rotation",
    //     "m_worldObjectsListBox.SelectedItem", "OldTanks.Models.WorldObject",  "ValueChanged",
    //     "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
    //     "OldTanks.Helpers.UIVectorHelper.FromSystemVector3DegreesToGLVector3Radians", "OldTanks.Helpers.UIVectorHelper.FromGLVector3RadiansToSystemVector3Degrees")]
    private ImGuiFloat3DragTextBox m_selectedObjectRotationDragTextBox;
    
    [BindableElement("Value", "Size",
        "m_worldObjectsListBox.SelectedItem", "OldTanks.Models.WorldObject",  "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_selectedObjectSizeDragTextBox;
    
    [BindableElement("Value", "CameraOffset",
        "m_worldObjectsListBox.SelectedItem", "OldTanks.Models.WorldObject",  "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_selectedObjectCameraOffsetDragTextBox;
    
    [BindableElement("Value", "CameraOffsetAngle",
        "m_worldObjectsListBox.SelectedItem", "OldTanks.Models.WorldObject",  "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector2> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector2> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector2", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector2")]
    private ImGuiFloat2DragTextBox m_selectedObjectCameraOffsetAngleDragTextBox;
    
    private ImGuiTreeNode m_rigidBodyTreeNode;
    
    [BindableElement("IsChecked", "IsStatic",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "Checked",
        "(OldTanks.UI.ImGuiControls.ImGuiCheckBox sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<bool> e)")]
    private ImGuiCheckBox m_isStaticCheckBox;
    
    [BindableElement("Value", "CenterOfMass",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody",  "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_centerOfMassDragTextBox;
    
    [BindableElement("Value", "Velocity",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody",  
        "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<System.Numerics.Vector3> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<System.Numerics.Vector3> e)", 
        "CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3", "CoolEngine.Services.Extensions.VectorExtensions.ToSystemVector3")]
    private ImGuiFloat3DragTextBox m_velocityDragTextBox;
    
    [BindableElement("Value", "DefaultJumpForce",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<float> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<float> e)")]
    private ImGuiFloatDragTextBox m_jumpForceDragTextBox;
    
    [BindableElement("Value", "MaxSpeed",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "ValueChanged",
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<float> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<float> e)")]
    private ImGuiFloatDragTextBox m_maxSpeedDragTextBox;
    
    [BindableElement("Value", "MaxBackSpeed",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<float> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<float> e)")]
    private ImGuiFloatDragTextBox m_maxBackSpeedDragTextBox;
    
    [BindableElement("Value", "MaxSpeedMultiplier",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<float> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<float> e)")]
    private ImGuiFloatDragTextBox m_speedMultiplierDragTextBox;
    
    [BindableElement("Value", "Weight",
        "m_worldObjectsListBox.SelectedItem.RigidBody", "CoolEngine.PhysicEngine.Core.RigidBody", 
        "ValueChanged", 
        "(OldTanks.UI.ImGuiControls.ImGuiFloatDragTextBoxBase<float> sender, OldTanks.UI.Services.EventArgs.ValueChangedEventArgs<float> e)")]
    private ImGuiFloatDragTextBox m_weightDragTextBox;

    private ImGuiButton m_showTextureWindow;
}