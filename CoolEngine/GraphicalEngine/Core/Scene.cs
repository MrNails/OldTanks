namespace CoolEngine.GraphicalEngine.Core;

public class Scene
{
    private List<Mesh> m_meshes;

    public Scene() : this(10, 10, 10)
    { }
    
    public Scene(float width, float height, float length)
    {
        m_meshes = new List<Mesh>();
    }

    public List<Mesh> Meshes => m_meshes;

    public Scene Copy()
    {
        var newScene = new Scene();

        foreach (var mesh in m_meshes)
            newScene.Meshes.Add(mesh.Copy());
        
        return newScene;
    }
}