using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class DrawObjectInfo : IDisposable
{
    private int m_vertexArrayObject;
    private int m_vertexBufferObject;
    private int m_elementsBufferObject;
    private int m_verticesLength;
    private int m_indicesLength;
    private bool m_disposed;

    public DrawObjectInfo(int vertexArrayObject, int vertexBufferObject, int elementsBufferObject)
    {
        m_elementsBufferObject = elementsBufferObject;
        m_vertexBufferObject = vertexBufferObject;
        m_vertexArrayObject = vertexArrayObject;
    }

    public int VertexArrayObject => m_vertexArrayObject;

    /// <summary>
    /// Model vertices buffer
    /// </summary>
    public int VertexBufferObject => m_vertexBufferObject;

    /// <summary>
    /// Indices buffer
    /// </summary>
    public int ElementsBufferObject => m_elementsBufferObject;

    public bool Disposed => m_disposed;
    
    public int VerticesLength
    {
        get => m_verticesLength;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            m_verticesLength = value;
        }
    }

    public int IndicesLength
    {
        get => m_indicesLength;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            m_indicesLength = value;
        }
    }

    private void ReleaseUnmanagedResources()
    {
        if (m_disposed)
            return;
        
        if (m_vertexArrayObject != 0)
        {
            GL.DeleteBuffer(m_vertexArrayObject);
            m_vertexArrayObject = 0;
        }

        if (m_vertexBufferObject != 0)
        {
            GL.DeleteBuffer(m_vertexBufferObject);
            m_vertexBufferObject = 0;
        }

        if (m_elementsBufferObject != 0)
        {
            GL.DeleteBuffer(m_elementsBufferObject);
            m_elementsBufferObject = 0;
        }

        VerticesLength = 0;
        IndicesLength = 0;

        m_disposed = true;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~DrawObjectInfo()
    {
        ReleaseUnmanagedResources();
    }
}