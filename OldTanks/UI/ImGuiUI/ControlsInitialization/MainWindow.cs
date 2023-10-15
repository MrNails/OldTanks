using System.Buffers;
using System.Numerics;
using OldTanks.Models;
using OldTanks.UI.ImGuiControls;
using OldTanks.UI.Services.EventArgs;

namespace OldTanks.UI.ImGuiUI;

public partial class MainWindow
{
    private void Initialize()
    {
        var panel = new ImGuiPanel($"Panel: {Name}");
        m_spawnCubeBtn = new ImGuiButton("Spawn cube");
        m_spawnCubeBtn.Click += SpawnCubeBtnOnClick;

        m_pushForceDragTextBox = new ImGuiFloat3DragTextBox("Push force");
        m_cameraPositionDragTextBox = new ImGuiFloat3DragTextBox("CPosition");
        m_cameraPositionDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_cameraRotationDragTextBox = new ImGuiFloat3DragTextBox("CRotation");
        m_cameraRotationDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_cameraSizeDragTextBox = new ImGuiFloat3DragTextBox("CSize");
        m_cameraSizeDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        
        m_cameraFreeModeCheckBox = new ImGuiCheckBox("Camera free mode");
        m_textBlock1 = new ImGuiTextBlock("World objects");
        
        m_worldObjectsListBox = new ImGuiListBox<WorldObject>("WorldObjectsListBox", ArrayPool<string>.Shared);
        m_clearObjectsSelectionButton = new ImGuiButton("Clear selection");
        m_clearObjectsSelectionButton.Click += ClearObjectsSelectionButtonOnClick;

        m_textBlock2 = new ImGuiTextBlock("Selected world object data");

        m_selectedObjectPositionDragTextBox = new ImGuiFloat3DragTextBox("Position");
        m_selectedObjectPositionDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_selectedObjectRotationDragTextBox = new ImGuiFloat3DragTextBox("Rotation");
        m_selectedObjectRotationDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_selectedObjectSizeDragTextBox = new ImGuiFloat3DragTextBox("Size");
        m_selectedObjectSizeDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_selectedObjectCameraOffsetDragTextBox = new ImGuiFloat3DragTextBox("Camera offset");
        m_selectedObjectCameraOffsetDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_selectedObjectCameraOffsetAngleDragTextBox = new ImGuiFloat3DragTextBox("Camera offset angle");
        m_selectedObjectCameraOffsetAngleDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;

        m_rigidBodyTreeNode = new ImGuiTreeNode("Rigid body");

        m_isStaticCheckBox = new ImGuiCheckBox("Is static");
        m_centerOfMassDragTextBox = new ImGuiFloat3DragTextBox("Center of mass");
        m_centerOfMassDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_velocityDragTextBox = new ImGuiFloat3DragTextBox("Velocity");
        m_velocityDragTextBox.ValueChanged += DragFloat3TextBoxValueChanged;
        m_jumpForceDragTextBox = new ImGuiFloatDragTextBox("Jump force");
        m_maxSpeedDragTextBox = new ImGuiFloatDragTextBox("Max speed");
        m_maxBackSpeedDragTextBox = new ImGuiFloatDragTextBox("Max back speed");
        m_speedMultiplierDragTextBox = new ImGuiFloatDragTextBox("Speed multiplier");
        m_weightDragTextBox = new ImGuiFloatDragTextBox("Weight");

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
        
        panel.Children.Add(m_spawnCubeBtn);
        panel.Children.Add(new ImGuiNewLine());
        panel.Children.Add(m_pushForceDragTextBox);
        panel.Children.Add(new ImGuiNewLine());
        
        panel.Children.Add(new ImGuiTextBlock("Camera data"));
        panel.Children.Add(m_cameraPositionDragTextBox);
        panel.Children.Add(m_cameraRotationDragTextBox);
        panel.Children.Add(m_cameraSizeDragTextBox);
        panel.Children.Add(m_cameraFreeModeCheckBox);
        panel.Children.Add(new ImGuiNewLine());
        panel.Children.Add(m_columnsControl);

        Child = panel;
    }

    private ImGuiButton m_spawnCubeBtn;
    private ImGuiFloat3DragTextBox m_pushForceDragTextBox;
    private ImGuiFloat3DragTextBox m_cameraPositionDragTextBox;
    private ImGuiFloat3DragTextBox m_cameraRotationDragTextBox;
    private ImGuiFloat3DragTextBox m_cameraSizeDragTextBox;
    private ImGuiCheckBox m_cameraFreeModeCheckBox;
    private ImGuiColumnsControl m_columnsControl;
    private ImGuiTextBlock m_textBlock1;
    private ImGuiListBox<WorldObject> m_worldObjectsListBox;
    private ImGuiButton m_clearObjectsSelectionButton;
    private ImGuiTextBlock m_textBlock2;
    private ImGuiFloat3DragTextBox m_selectedObjectPositionDragTextBox;
    private ImGuiFloat3DragTextBox m_selectedObjectRotationDragTextBox;
    private ImGuiFloat3DragTextBox m_selectedObjectSizeDragTextBox;
    private ImGuiFloat3DragTextBox m_selectedObjectCameraOffsetDragTextBox;
    private ImGuiFloat3DragTextBox m_selectedObjectCameraOffsetAngleDragTextBox;
    private ImGuiTreeNode m_rigidBodyTreeNode;
    private ImGuiCheckBox m_isStaticCheckBox;
    private ImGuiFloat3DragTextBox m_centerOfMassDragTextBox;
    private ImGuiFloat3DragTextBox m_velocityDragTextBox;
    private ImGuiFloatDragTextBox m_jumpForceDragTextBox;
    private ImGuiFloatDragTextBox m_maxSpeedDragTextBox;
    private ImGuiFloatDragTextBox m_maxBackSpeedDragTextBox;
    private ImGuiFloatDragTextBox m_speedMultiplierDragTextBox;
    private ImGuiFloatDragTextBox m_weightDragTextBox;
}