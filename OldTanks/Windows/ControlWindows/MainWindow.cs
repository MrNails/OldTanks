using OldTanks.Controls;
using OpenTK.Mathematics;

namespace OldTanks.Windows;

public partial class MainWindow
{
    private TextBlock m_tbX;
    private TextBlock m_tbY;
    private TextBlock m_tbZ;
    private TextBlock m_tbFPS;
    private TextBlock m_tbCamRotation;
    private TextBlock m_tbRotation;
    private TextBlock m_tbHaveCollision;
    private TextBlock m_tbCurrentSpeed;
    
    private void InitControls()
    {
        var textBlock1 = new TextBlock("TextBlock1")
        {
            Position = new Vector2(0, 15),
            Text = "FPS: ",
        };

        var textBlock2 = new TextBlock("TextBlock2")
        {
            Position = new Vector2(0, textBlock1.Position.Y + textBlock1.Font.FontSize),
            Text = "X: ",
        };

        var textBlock3 = new TextBlock("TextBlock3")
        {
            Position = new Vector2(0, textBlock2.Position.Y + textBlock2.Font.FontSize),
            Text = "Y: ",
        };
        
        var textBlock4 = new TextBlock("TextBlock4")
        {
            Position = new Vector2(0, textBlock3.Position.Y + textBlock3.Font.FontSize),
            Text = "Z: ",
        };
        
        var textBlock5 = new TextBlock("TextBlock5")
        {
            Position = new Vector2(0, textBlock4.Position.Y + textBlock4.Font.FontSize),
            Text = "Camera rotation: ",
        };

        var textBlock6 = new TextBlock("TextBlock6")
        {
            Position = new Vector2(0, textBlock5.Position.Y + textBlock5.Font.FontSize),
            Text = "Default cube rotation: ",
        };
        
        var textBlock7 = new TextBlock("TextBlock7")
        {
            Position = new Vector2(0, textBlock6.Position.Y + textBlock6.Font.FontSize),
            Text = "Have collision: ",
        };
        
        var textBlock8 = new TextBlock("TextBlock8")
        {
            Position = new Vector2(0, textBlock7.Position.Y + textBlock7.Font.FontSize),
            Text = "Speed: ",
        };

        
        m_tbFPS = new TextBlock("TB_FPS")
        {
            Position = new Vector2(textBlock1.Position.X + textBlock1.Size.X, textBlock1.Position.Y),
        };
        
        m_tbX = new TextBlock("TB_X")
        {
            Position = new Vector2(textBlock2.Position.X + textBlock2.Size.X, textBlock2.Position.Y),
            Size = new Vector2(30, 15)
        };

        m_tbY = new TextBlock("TB_Y")
        {
            Position = new Vector2(textBlock3.Position.X + textBlock3.Size.X, textBlock3.Position.Y),
        };

        m_tbZ = new TextBlock("TB_Z")
        {
            Position = new Vector2(textBlock4.Position.X + textBlock4.Size.X, textBlock4.Position.Y),
        };
        
        m_tbCamRotation = new TextBlock("TB_CamRotation")
        {
            Position = new Vector2(textBlock5.Position.X + textBlock5.Size.X, textBlock5.Position.Y),
        };
        
        m_tbRotation = new TextBlock("TB_Rotation")
        {
            Position = new Vector2(textBlock6.Position.X + textBlock6.Size.X, textBlock6.Position.Y),
            Rotation = new Vector2(0, 0)
        };

        m_tbHaveCollision = new TextBlock("TB_HaveCollision")
        {
            Position = new Vector2(textBlock7.Position.X + textBlock7.Size.X, textBlock7.Position.Y),
        };
            
        m_tbCurrentSpeed = new TextBlock("TB_CurrentSpeed")
        {
            Position = new Vector2(textBlock8.Position.X + textBlock8.Size.X + 3, textBlock8.Position.Y),
        };
        
        m_controls.Add(textBlock1);
        m_controls.Add(textBlock2);
        m_controls.Add(textBlock3);
        m_controls.Add(textBlock4);
        m_controls.Add(textBlock5);
        m_controls.Add(textBlock6);
        m_controls.Add(textBlock7);
        m_controls.Add(textBlock8);
        m_controls.Add(m_tbFPS);
        m_controls.Add(m_tbX);
        m_controls.Add(m_tbY);
        m_controls.Add(m_tbZ);
        m_controls.Add(m_tbCamRotation);
        m_controls.Add(m_tbRotation);
        m_controls.Add(m_tbHaveCollision);
        m_controls.Add(m_tbCurrentSpeed);
    }
}