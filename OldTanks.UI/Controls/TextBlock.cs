﻿using CoolEngine.Services.Renderers;
using OpenTK.Mathematics;

namespace OldTanks.UI.Controls;

public class TextBlock : TextControl
{
    public TextBlock(string name) : base(name)
    { }

    public override void Draw()
    {
        TextRenderer.DrawText2D(Text, Font, new Vector2(Position.X, CoolEngine.Services.EngineSettings.Current.GetWindowY(Position.Y)), Color, Rotation);
    }
}