using System.Buffers;
using System.Collections.ObjectModel;
using System.Numerics;
using ImGuiNET;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.Tests.ImGuiControls;

[TestFixture]
public sealed class ImGuiControlTests
{
    private IntPtr m_context;
    private float m_defaultItemWidth;

    private static string UniqueName() => $"{TestContext.CurrentContext.Test.Name}-{Guid.NewGuid():N}";

    [SetUp]
    public void CreateImGuiContext()
    {
        // The context reads and writes imgui.ini beside the test assembly; harmless, as the window size is set every frame.
        m_context = ImGui.CreateContext();
        ImGui.SetCurrentContext(m_context);

        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _);
        io.DisplaySize = new Vector2(800, 600);
        io.DeltaTime = 1f / 60f;

        ImGui.NewFrame();
        ImGui.SetNextWindowSize(new Vector2(400, 300));
        ImGui.Begin("ImGuiControlTests");

        m_defaultItemWidth = ImGui.CalcItemWidth();
        Assume.That(m_defaultItemWidth, Is.Not.EqualTo(200f));
    }

    [TearDown]
    public void DestroyImGuiContext()
    {
        ImGui.End();
        ImGui.EndFrame();
        ImGui.DestroyContext(m_context);
    }

    [Test]
    public void Draw_WithWidth_LeavesItemWidthOfLaterItemsUnchanged()
    {
        var textBox = new ImGuiTextBox("##" + UniqueName()) { Width = 200 };

        textBox.Draw();

        Assert.That(ImGui.CalcItemWidth(), Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    [Test]
    public void Draw_WithWidth_DrawsItsWidgetAtThatWidth()
    {
        var textBox = new ImGuiTextBox("##" + UniqueName()) { Width = 200 };

        textBox.Draw();

        Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(200f).Within(0.001f));
    }

    [Test]
    public void Draw_TwoListBoxesWithWidth_LeaveItemWidthUnchanged()
    {
        var name = UniqueName();
        var first = CreateListBox(name + "0");
        var second = CreateListBox(name + "1");

        try
        {
            first.Draw();
            second.Draw();

            Assert.That(ImGui.CalcItemWidth(), Is.EqualTo(m_defaultItemWidth).Within(0.001f));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    [Test]
    public void Draw_WithoutWidth_UsesTheWindowItemWidth()
    {
        var textBox = new ImGuiTextBox("##" + UniqueName());

        textBox.Draw();

        Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    [Test]
    public void Draw_Float3DragWithWidth_LeavesItemWidthUnchanged()
    {
        var drag = new ImGuiFloat3DragTextBox("##" + UniqueName()) { Width = 200 };

        drag.Draw();

        Assert.That(ImGui.CalcItemWidth(), Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    [Test]
    public void Draw_HiddenCheckBoxWithWidth_DoesNotSizeTheNextItem()
    {
        var name = UniqueName();
        var checkBox = new ImGuiCheckBox(name) { Width = 200, IsVisible = false };
        var next = new ImGuiTextBox("##" + name + "-next");

        checkBox.Draw();
        next.Draw();

        Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    [Test]
    public void Draw_CollapsedWindowWithWidthChild_LeavesNextItemWidthUnchanged()
    {
        var name = UniqueName();
        var window = new ImGuiWindow(name)
        {
            Title = "ImGuiControlTests.Collapsed",
            Child = new ImGuiTextBox("##" + name + "-child") { Width = 200 }
        };
        var next = new ImGuiTextBox("##" + name + "-next");

        try
        {
            window.Show();

            ImGui.SetNextWindowSize(new Vector2(300, 200));
            ImGui.SetNextWindowCollapsed(true, ImGuiCond.Always);
            window.Draw();
            next.Draw();

            Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(m_defaultItemWidth).Within(0.001f));
        }
        finally
        {
            window.Dispose();
        }
    }

    [Test]
    public void Draw_PanelWithWidth_DoesNotSizeItsChildren()
    {
        var name = UniqueName();
        var panel = new ImGuiPanel(name) { Width = 200 };
        var child = new ImGuiTextBox("##" + name + "-child");
        panel.Children.Add(child);

        panel.Draw();

        Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    [Test]
    public void Draw_TreeNodeWithWidth_DoesNotSizeItsChildren()
    {
        var name = UniqueName();
        var tree = new ImGuiTreeNode(name) { Width = 200 };
        var child = new ImGuiTextBox("##" + name + "-child");
        tree.Children.Add(child);

        ImGui.SetNextItemOpen(true);
        tree.Draw();

        Assert.That(ImGui.GetItemRectSize().X, Is.EqualTo(m_defaultItemWidth).Within(0.001f));
    }

    private static ImGuiListBox<string> CreateListBox(string name) =>
        new("##" + name, ArrayPool<string>.Shared)
        {
            Width = 200,
            Items = new ObservableCollection<string> { "a", "b" }
        };
}
