using GraphicalEngine.Core;
using OldTanks.Services;

namespace OldTanks.Models;

public class World
{
    private readonly List<WorldObject> m_objects;
    private readonly Camera m_camera;

    public World()
    {
        m_objects = new List<WorldObject>();
        m_camera = new Camera();
    }

    public List<WorldObject> WorldObjects => m_objects;

    public Camera Camera => m_camera;
}