namespace GraphicalEngine.Core;

public class Scene
{
    private List<Mesh> m_meshes;

    public Scene() : this(10, 10, 10)
    { }
    
    public Scene(float width, float height, float length)
    {
        m_meshes = new List<Mesh>();
        Width = width;
        Height = height;
        Length = length;
    }

    public List<Mesh> Meshes => m_meshes;
    
    public float Width { get; set; }
    public float Height { get; set; }
    public float Length { get; set; }

    public Scene Copy()
    {
        var newScene = new Scene(Width, Height, Length);

        foreach (var mesh in m_meshes)
            newScene.Meshes.Add(mesh.Copy());
        
        return newScene;
    }
}