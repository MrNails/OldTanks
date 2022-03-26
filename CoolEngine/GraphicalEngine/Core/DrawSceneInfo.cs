using CoolEngine.Core;

namespace CoolEngine.GraphicalEngine.Core;

public class DrawSceneInfo
{
    private readonly Dictionary<int, DrawObjectInfo> m_buffers;
    private readonly Shader m_shader;

    public DrawSceneInfo(Shader shader, Dictionary<int, DrawObjectInfo> buffers)
    {
        m_shader = shader ?? throw new ArgumentNullException(nameof(shader));
        m_buffers = buffers ?? throw new ArgumentNullException(nameof(buffers));;
    }

    public Shader Shader => m_shader;
    public Dictionary<int, DrawObjectInfo> Buffers => m_buffers;
}