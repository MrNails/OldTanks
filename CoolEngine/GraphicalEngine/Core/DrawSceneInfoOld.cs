using CoolEngine.Services.Interfaces;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class DrawSceneInfoOld
{
    private readonly Dictionary<int, DrawObjectInfo> m_buffers;
    private readonly List<IDrawable> m_drawables;
    private readonly Shader m_shader;

    public DrawSceneInfoOld()
    {
        m_drawables = new List<IDrawable>();
        m_buffers = new Dictionary<int, DrawObjectInfo>();
    }
    
    public DrawSceneInfoOld(Shader shader)
    {
        m_shader = shader ?? throw new ArgumentNullException(nameof(shader));

        m_drawables = new List<IDrawable>();
        m_buffers = new Dictionary<int, DrawObjectInfo>();
    }

    public Shader Shader => m_shader;
    public Dictionary<int, DrawObjectInfo> Buffers => m_buffers;

    public List<IDrawable> Drawables => m_drawables;

    /// <summary>
    /// Represent vertices and indices buffers
    /// </summary>
    public DrawObjectInfo? DrawObjectInfo { get; set; }
    
    public int TexturesVBO { get; set; }
}