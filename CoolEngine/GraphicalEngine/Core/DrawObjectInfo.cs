namespace CoolEngine.GraphicalEngine.Core;

public class DrawObjectInfo
{
    private int m_vertexArrayObject;
    private int m_vertexBufferObject;
    private int m_elementsBufferObject;

    public DrawObjectInfo(int vertexArrayObject, int vertexBufferObject, int elementsBufferObject)
    {
        m_elementsBufferObject = elementsBufferObject;
        m_vertexBufferObject = vertexBufferObject;
        m_vertexArrayObject = vertexArrayObject;
    }

    public int VertexArrayObject => m_vertexArrayObject;
    public int VertexBufferObject => m_vertexBufferObject;
    public int ElementsBufferObject => m_elementsBufferObject;
}