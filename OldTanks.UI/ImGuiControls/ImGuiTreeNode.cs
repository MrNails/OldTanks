using ImGuiNET;

namespace OldTanks.UI.ImGuiControls;

public class ImGuiTreeNode : ImGuiControl
{
    private readonly List<ImGuiTreeNode> m_nodes;

    public ImGuiTreeNode(string name)
        : base(name)
    {
        m_nodes = new List<ImGuiTreeNode>();
    }

    public ImGuiTreeNode? Parent { get; set; }
    public List<ImGuiTreeNode> Nodes { get; set; }
    
    public bool IsExpanded { get; set; }

    public override void Draw() => DrawNode(this);

    private void DrawNode(ImGuiTreeNode node)
    {
        IsExpanded = ImGui.TreeNodeEx(Name);
        
        if (!node.IsExpanded)
            return;

        for (int i = 0; i < Children.Count; i++)
            Children[i].Draw();

        for (int i = 0; i < node.Nodes.Count; i++) 
            DrawNode(node.Nodes[i]);
        
        ImGui.TreePop();
    }

    // public class TreeNodeCollection : IList<TreeNode>
    // {
    //     private readonly List<TreeNode> m_nodes;
    //
    //     public TreeNodeCollection()
    //     {
    //         m_nodes = new List<TreeNode>();
    //     }
    //
    //     public IEnumerator<TreeNode> GetEnumerator() => m_nodes.GetEnumerator();
    //
    //     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    //
    //     public void Add(TreeNode item)
    //     {
    //         Control.CheckExistingControl(item.Name);
    //         throw new NotImplementedException();
    //     }
    //
    //     public void Clear()
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public bool Contains(TreeNode item)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public void CopyTo(TreeNode[] array, int arrayIndex)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public bool Remove(TreeNode item)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public int Count => _count;
    //
    //     public bool IsReadOnly => _isReadOnly;
    //
    //     public int IndexOf(TreeNode item)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public void Insert(int index, TreeNode item)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public void RemoveAt(int index)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public TreeNode this[int index]
    //     {
    //         get => throw new NotImplementedException();
    //         set => throw new NotImplementedException();
    //     }
    // }
}