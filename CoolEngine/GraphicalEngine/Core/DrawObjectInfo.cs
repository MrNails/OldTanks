using CoolEngine.GraphicalEngine.Services;
using OpenTK.Graphics.OpenGL4;
using Serilog;

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
            GL.DeleteVertexArray(m_vertexArrayObject);
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

    // Runs on the finalizer thread, which has no GL context: no GL call here; the GL thread deletes the names
    // (the queue ignores a 0 name, such as a missing element buffer).
    ~DrawObjectInfo()
    {
        Log.Warning("Draw object info (GL vertex array {VertexArrayObject}, vertex buffer {VertexBufferObject}, " +
                    "element buffer {ElementsBufferObject}) was never disposed; its deletion is queued for the GL thread",
            m_vertexArrayObject, m_vertexBufferObject, m_elementsBufferObject);

        if (!m_disposed)
        {
            GLDeletionQueue.Enqueue(GLObjectKind.VertexArray, m_vertexArrayObject);
            GLDeletionQueue.Enqueue(GLObjectKind.Buffer, m_vertexBufferObject);
            GLDeletionQueue.Enqueue(GLObjectKind.Buffer, m_elementsBufferObject);
        }
    }
}