using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.Services;
using OpenTK.Mathematics;

namespace OldTanks.UI.Controls;

public enum VerticalTextAlignment
{
    Center,
    Left,
    Right
}

public enum HorizontalTextAlignment
{
    Center,
    Top,
    Bottom
}

public abstract class TextControl : Control
{
    private string? m_text;
    private Font m_font;

    protected TextControl(string name) : base(name)
    {
        Color = Colors.White;
        Text = string.Empty;
    }

    public VerticalTextAlignment VerticalTextAlignment { get; set; }
    public HorizontalTextAlignment HorizontalTextAlignment { get; set; }

    public Vector4 Color { get; set; }

    public Font? Font
    {
        get => m_font;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            m_font = value;
            
            EvaluateSize();
        }
    }

    public string? Text
    {
        get => m_text;
        set
        {
            if (value == null)
                value = string.Empty;

            m_text = value;

            EvaluateSize();
        }
    }

    private void EvaluateSize()
    {
        if (Text == null || Font == null)
            return;
        
        Size = new Vector2(
            Text.Length * Font.FontSize * (m_font.FontSize / m_font.FontInformation.OriginalFontSize),
            Font.FontSize + 3);
    }
}