using System.Drawing;
using GraphicalEngine.Services;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace OldTanks.Controls;

public class TextBlock : TextControl
{
    public TextBlock(string name) : base(name)
    { }

    public override void Draw()
    {
        DrawManager.DrawText2D(Font, Text, new Vector2(Position.X, GlobalSettings.GetWindowY(Position.Y)), 
            new Vector3(Color.A / 255.0f, Color.G / 255.0f, Color.B / 255.0f),
            new RectangleF(0, 0, Size.X, Size.Y));
    }
}