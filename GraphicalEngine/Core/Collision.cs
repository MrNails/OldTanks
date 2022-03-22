using GraphicalEngine.Services.Interfaces;

namespace GraphicalEngine.Core;

public class Collision
{
    private List<Mesh> m_meshes;

    public Collision()
    {
        m_meshes = new List<Mesh>();
    }

    public List<Mesh> Meshes => m_meshes;

    public bool CheckCollision(ITransformable t1, ITransformable t2)
    {
        return false;
    }
}