using Common.Extensions;
using ImGuiNET;
using OldTanks.UI.Services;
using OldTanks.UI.Services.Interfaces;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiColumnsControl : ImGuiControl, IControlsContainer
{
    private readonly Dictionary<int, HashSet<ImGuiControl>> _controlsColumn;
    private int m_columnsAmount;

    public ImGuiColumnsControl(string name) : base(name)
    {
        _controlsColumn = new Dictionary<int, HashSet<ImGuiControl>>();
        Children = new ControlCollection(this);
    }

    public ControlCollection Children { get; }

    public int ColumnsAmount
    {
        get => m_columnsAmount;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Amount of columns cannot be less than zero");
            
            m_columnsAmount = value;
        }
    }

    public void SetColumn(ImGuiControl control, int column)
    {
        if (!Children.Contains(control))
            throw new InvalidOperationException("Cannot set column to child that not belong to this control");

        column = Math.Clamp(column, 0, ColumnsAmount - 1);

        var oldColumn = GetColumn(control);
        if (oldColumn != -1)
            _controlsColumn[oldColumn].Remove(control);

        _controlsColumn.TryGetAndAddIfNotExists(column)
            .Add(control);
    }
    
    public int GetColumn(ImGuiControl control)
    {
        foreach (var columnAndControls in _controlsColumn)
        {
            if (columnAndControls.Value.Contains(control))
                return columnAndControls.Key;
        }

        return -1;
    }

    public override void Draw()
    {
        if (!IsVisible || m_columnsAmount == 0)
            return;
        
        ImGui.Columns(m_columnsAmount);

        foreach (var columnAndControls in _controlsColumn)
        {
            foreach (var control in columnAndControls.Value)
                control.Draw();

            ImGui.NextColumn();
        }
    }
}