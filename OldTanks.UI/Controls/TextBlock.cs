using CoolEngine.Services;
using CoolEngine.Services.Renderers;
using OpenTK.Mathematics;

namespace OldTanks.UI.Controls;

public class TextBlock : TextControl
{
    public TextBlock(string name) : base(name)
    { }

    public override void Draw()
    {
        TextRenderer.DrawText2D(Text, Font, new Vector2(Position.X, GlobalSettings.GetWindowY(Position.Y)), Color, Rotation);
    }
}