using System.Numerics;
using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public class FloatDragTextBox : ImGuiControl
{
    private float m_value;

    public FloatDragTextBox(string name)
        : base(name)
    {}
    
    public ref float Value => ref m_value;
    
    public void Draw() => ImGui.DragFloat(Name, ref m_value);
}

public class Float2DragTextBox : ImGuiControl
{
    private Vector2 m_vector2;

    public Float2DragTextBox(string name)
        : base(name)
    {
        m_vector2 = new Vector2();
    }
    
    public ref Vector2 Vector2 => ref m_vector2;
    
    public void Draw() => ImGui.DragFloat2(Name, ref Vector2);
}

public class Float3DragTextBox : ImGuiControl
{
    private Vector3 m_vector3;

    public Float3DragTextBox(string name)
        : base(name)
    {
        m_vector3 = new Vector3();
    }
    
    public ref Vector3 Vector3 => ref m_vector3;
    
    public void Draw() => ImGui.DragFloat3(Name, ref Vector3);
}

public class Float4DragTextBox : ImGuiControl
{
    private Vector4 m_vector4;

    public Float4DragTextBox(string name)
        : base(name)
    {
        m_vector4 = new Vector4();
    }
    
    public ref Vector4 Vector4 => ref m_vector4;
    
    public void Draw() => ImGui.DragFloat4(Name, ref Vector4);
}