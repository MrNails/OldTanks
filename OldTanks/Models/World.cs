using GraphicalEngine.Core;
using OldTanks.Services;

namespace OldTanks.Models;

public class World
{
    private readonly List<WorldObject> m_objects;
    
    private readonly Camera m_camera;
    private readonly SkyBox m_skyBox;

    public World()
    {
        m_objects = new List<WorldObject>();
        m_camera = new Camera();
        m_skyBox = new SkyBox();
    }

    public List<WorldObject> WorldObjects => m_objects;

    public Camera Camera => m_camera;

    public SkyBox SkyBox => m_skyBox;
}