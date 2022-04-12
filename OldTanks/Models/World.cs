using System.Collections;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;

namespace OldTanks.Models;

public class World
{
    private readonly Camera m_defaultCamera;
    private readonly List<WorldObject> m_objects;
    private readonly CamerasCollection m_cameras;
    
    private readonly SkyBox m_skyBox;

    private Camera m_currentCamera;

    public World()
    {
        m_defaultCamera = new Camera();

        m_currentCamera = m_defaultCamera;
        
        m_objects = new List<WorldObject>();

        m_skyBox = new SkyBox();
        
        m_cameras = new CamerasCollection(this);
    }

    public List<WorldObject> WorldObjects => m_objects;
    public CamerasCollection Cameras => m_cameras;

    public Camera CurrentCamera => m_currentCamera;
    
    public WorldObject? Player { get; set; }

    public SkyBox SkyBox => m_skyBox;

    public void SetCurrentCamera(Camera camera)
    {
        if (camera == null)
            throw new ArgumentNullException(nameof(camera));

        m_currentCamera = camera;
    }

    public class CamerasCollection : IEnumerable<Camera>
    {
        private readonly List<Camera> m_cameras;
        private readonly World m_world;
        
        public CamerasCollection(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            
            m_cameras = new List<Camera>();
            m_world = world;
        }

        public IEnumerator<Camera> GetEnumerator() => m_cameras.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Camera camera) => m_cameras.Add(camera);
        public bool Remove(Camera camera) => m_cameras.Remove(camera);
        public bool Contains(Camera camera) => m_cameras.Contains(camera);

        public void Clear()
        {
            m_cameras.Clear();
            m_cameras.Add(m_world.m_defaultCamera);
        }
    }
}