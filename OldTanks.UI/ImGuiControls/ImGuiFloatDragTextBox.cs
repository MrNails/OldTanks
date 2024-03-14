using System.Numerics;
using ImGuiNET;
using OldTanks.UI.Services;
using OldTanks.UI.Services.EventArgs;

namespace OldTanks.UI.ImGuiControls;

public abstract class ImGuiFloatDragTextBoxBase<TValue> : ImGuiControl
    where TValue : IEquatable<TValue>
{
    protected TValue m_value;
    private float m_dragDelta;

    public event EventHandler<ImGuiFloatDragTextBoxBase<TValue>, ValueChangedEventArgs<TValue>>? ValueChanged;

    protected ImGuiFloatDragTextBoxBase(string name) : base(name)
    {
        DragDelta = 1;
    }

    public TValue Value
    {
        get => m_value;
        set
        {
            if (m_value.Equals(value)) 
                return;
            
            var oldValue = m_value;
            m_value = value;
                
            ValueChanged?.Invoke(this, new ValueChangedEventArgs<TValue>(oldValue, value));
        }
    }

    public float DragDelta
    {
        get => m_dragDelta;
        set => SetField(ref m_dragDelta, value);
    }

    public override void Draw()
    {
        if (!IsVisible)
            return;

        var oldValue = m_value;
        
        DrawDragTextBox();
        
        if (!oldValue.Equals(m_value))
            ValueChanged?.Invoke(this, new ValueChangedEventArgs<TValue>(oldValue, m_value));
    }

    protected abstract void DrawDragTextBox();
}

public class ImGuiFloatDragTextBox : ImGuiFloatDragTextBoxBase<float>
{
    public ImGuiFloatDragTextBox(string name) : base(name)
    {
        m_value = 0;
    }

    protected override void DrawDragTextBox()
    {
        ImGui.DragFloat(Name, ref m_value, DragDelta);
    }
}

public class ImGuiFloat2DragTextBox : ImGuiFloatDragTextBoxBase<Vector2>
{
    public ImGuiFloat2DragTextBox(string name) : base(name)
    {
        m_value = new Vector2();
    }

    protected override void DrawDragTextBox()
    {
        ImGui.DragFloat2(Name, ref m_value, DragDelta);
    }
}

public class ImGuiFloat3DragTextBox : ImGuiFloatDragTextBoxBase<Vector3>
{
    public ImGuiFloat3DragTextBox(string name) : base(name)
    {
        m_value = new Vector3();
    }

    protected override void DrawDragTextBox()
    {
        ImGui.DragFloat3(Name, ref m_value, DragDelta);
    }
}

public class ImGuiFloat4DragTextBox : ImGuiFloatDragTextBoxBase<Vector4>
{
    public ImGuiFloat4DragTextBox(string name) : base(name)
    {
        m_value = new Vector4();
    }

    protected override void DrawDragTextBox()
    {
        ImGui.DragFloat4(Name, ref m_value, DragDelta);
    }
}