using System.Numerics;
using ImGuiNET;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.ImGuiUI;

/// <summary>
/// The "Startup failed" window: each frame it is pinned to a 10 px margin inside the game window's client
/// area (width from the supplied client-width callback) and wraps its text, so a long error line never runs
/// past the client area. It cannot be collapsed. GL thread only.
/// </summary>
public sealed class StartupErrorWindow : ImGuiWindow
{
    private const float Margin = 10f;
    private const float MinWidth = 100f;

    private readonly Func<int> m_clientWidth;

    public StartupErrorWindow(string name, string message, Func<int> clientWidth) : base(name)
    {
        m_clientWidth = clientWidth;
        Child = new WrappedTextBlock(name + "Text") { Text = message };
    }

    public override void Draw()
    {
        if (IsVisible)
        {
            ImGui.SetNextWindowPos(new Vector2(Margin, Margin), ImGuiCond.Always);
            ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
            // The game window's client width, which is also ImGui's DisplaySize.X.
            // Height 0: ImGui fits the height to the content.
            ImGui.SetNextWindowSize(
                new Vector2(Math.Max(m_clientWidth() - 2 * Margin, MinWidth), 0), ImGuiCond.Always);
        }

        base.Draw();
    }

    private sealed class WrappedTextBlock : ImGuiTextBlock
    {
        public WrappedTextBlock(string name) : base(name) { }

        public override void Draw()
        {
            // Wrap position 0 = the window's right content edge.
            ImGui.PushTextWrapPos(0f);
            base.Draw();
            ImGui.PopTextWrapPos();
        }
    }
}
