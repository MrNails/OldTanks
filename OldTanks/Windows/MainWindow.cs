using System.Buffers;
using System.Diagnostics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Loaders;
using CoolEngine.Services.Renderers;
using ImGuiNET;
using OldTanks.Controls;
using OldTanks.ImGuiControls;
using OldTanks.Models;
using OldTanks.Services;
using OldTanks.Services.ImGUI;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GlobalSettings = OldTanks.Services.GlobalSettings;
using GEGlobalSettings = CoolEngine.Services.GlobalSettings;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;
using Mesh = CoolEngine.GraphicalEngine.Core.Mesh;

namespace OldTanks.Windows;

public partial class MainWindow : GameWindow
{
    private readonly List<Control> m_controls;
    private readonly List<Vector3> m_objSLots;

    private readonly List<ICollisionable> m_collisionables = new List<ICollisionable>();

    private readonly int m_renderRadius;

    private bool m_exit;
    private bool m_haveCollision;

    private ImGuiController m_imGuiController;

    private readonly Thread m_generateObjectThread;
    private Thread m_interactionWorker;

    private World m_world;

    private double m_fps;

    private Cube m_testCube;

    private bool m_debugView;
    private bool m_activePhysic;
    private bool m_drawNormals;
    private bool m_drawFaceNumber;
    private bool m_mouseDown;
    private bool m_freeCamMode;
    private bool m_activeCamPhysics;

    private IPhysicObject m_currentObject;
    private int m_selectedWorldObjectIndex;

    // private string[] m_worldObjectsNames;

    private Vector3 m_posDelta;

    private Vector2 m_lastMousePos;
    private bool m_firstMouseMove;

    private Vector3 m_rotation;

    public MainWindow(string caption)
        : this(800, 600, caption)
    {
    }

    public MainWindow(int height, int width, string caption)
        : this(GameWindowSettings.Default,
            new NativeWindowSettings
            {
                Size = new Vector2i(height, width),
                Title = caption,
                APIVersion = new Version(4, 6)
            })
    {
    }

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        m_controls = new List<Control>();
        m_world = new World();

        // m_renderRadius = 20;

        m_freeCamMode = true;

        GlobalSettings.UserSettings.Sensitivity = 0.1f;

        m_objSLots = new List<Vector3>(20);

        m_generateObjectThread = new Thread(GenerateObjects);

        m_rotation = new Vector3(0, 0, 0);

        m_selectedWorldObjectIndex = -1;

        m_interactionWorker = new Thread(HandleCollision);
    }

    private void InitCameraPhysics()
    {
        var rBody = new RigidBody();

        rBody.MaxSpeed = 5;
        rBody.MaxBackSpeed = 2;
        rBody.MaxSpeedMultiplier = 1;
        rBody.Rotation = MathHelper.DegreesToRadians(30);
        rBody.Acceleration = 10;
        rBody.DefaultJumpForce = 1;
        rBody.BreakMultiplier = 1.5f;
        rBody.Speed = 5;

        m_world.CurrentCamera.RigidBody = rBody;
    }

    private void ChangeCameraMode()
    {
        m_currentObject = m_freeCamMode ? m_world.CurrentCamera : m_world.Player;

        if (!m_freeCamMode && m_currentObject is IWatchable watchable)
            watchable.Camera = m_world.CurrentCamera;
    }

    private void HandleImGUI()
    {
        var position = System.Numerics.Vector3.Zero;
        var rotation = System.Numerics.Vector3.Zero;
        var size =  System.Numerics.Vector3.Zero;
        WorldObject? lastObject = m_selectedWorldObjectIndex == -1 ? null : m_world.WorldObjects[m_selectedWorldObjectIndex];

        ImGui.Begin("Debug");
        
        position = VectorExtensions.GLToSystemVector3(m_world.CurrentCamera.Position);
        rotation.X = m_world.CurrentCamera.Yaw ;
        rotation.Y = m_world.CurrentCamera.Pitch ;
        rotation.Z = m_world.CurrentCamera.Roll ;
        size = VectorExtensions.GLToSystemVector3(m_world.CurrentCamera.Size);
        
        ImGui.Text("Camera data");
        ImGui.DragFloat3("CPosition", ref position);
        ImGui.DragFloat3("CRotation", ref rotation);
        ImGui.DragFloat3("CSize", ref size);

        m_world.CurrentCamera.Position = VectorExtensions.SystemToGLVector3(position);
        m_world.CurrentCamera.Yaw = rotation.X;
        m_world.CurrentCamera.Pitch = rotation.Y;
        m_world.CurrentCamera.Roll = rotation.Z;
        m_world.CurrentCamera.Size = VectorExtensions.SystemToGLVector3(size);
        
        ImGui.Checkbox("Camera free mode", ref m_freeCamMode);
        
        m_currentObject = m_freeCamMode ? m_world.CurrentCamera : m_world.Player;

        ImGui.Columns(2);

        ImGui.Text("World objects");

        var arr = new string[m_world.WorldObjects.Count];
        
        for (int i = 0; i < m_world.WorldObjects.Count; i++)
            arr[i] = m_world.WorldObjects[i].GetType().Name;

        ImGui.ListBox(string.Empty, ref m_selectedWorldObjectIndex, arr, m_world.WorldObjects.Count);

        var pressed = ImGui.Button("Clear selection");

        if (pressed)
            m_selectedWorldObjectIndex = -1;

        WorldObject? selectedWorldObject;
        if (m_selectedWorldObjectIndex != -1)
            selectedWorldObject = m_world.WorldObjects[m_selectedWorldObjectIndex];
        else
            selectedWorldObject = null;

        if (selectedWorldObject != null)
        {
            if (lastObject != null)
                lastObject.Camera = null;
            
            m_world.Player = selectedWorldObject;
            
            ChangeCameraMode();
        }
        
        // if (!m_freeCamMode && selectedWorldObject != null && selectedWorldObject.Camera == null)
        // {
        //     m_world.Player = selectedWorldObject;
        //     selectedWorldObject.Camera = m_world.CurrentCamera;
        //
        //     if (lastObject != null)
        //         lastObject.Camera = null;
        // }

        // for (int i = 0; i < m_testCube.Scene.Meshes.Count; i++)
        // {
        //     var mesh = m_testCube.Scene.Meshes[i];
        //     var expanded = ImGui.TreeNodeEx($"Mesh {i}");
        //     var scale = VectorExtensions.GLToSystemVector2(mesh.TextureData.Scale);
        //     var angle = mesh.TextureData.RotationAngle;
        //
        //     if (expanded)
        //     {
        //         ImGui.DragFloat2($"Mesh texture scale {i}", ref scale);
        //         ImGui.DragFloat($"Mesh texture angle {i}", ref angle);
        //         ImGui.TreePop();
        //     }
        //
        //     mesh.TextureData.Scale = VectorExtensions.SystemToGLVector2(scale);
        //     mesh.TextureData.RotationAngle = angle;
        // }

        ImGui.NextColumn();

        ImGui.Text("Selected world object data");

        position = selectedWorldObject == null ? System.Numerics.Vector3.Zero : VectorExtensions.GLToSystemVector3(selectedWorldObject.Position);
        rotation = selectedWorldObject == null ? System.Numerics.Vector3.Zero : VectorExtensions.GLToSystemVector3(selectedWorldObject.Direction);
        size =  selectedWorldObject == null ? System.Numerics.Vector3.Zero : VectorExtensions.GLToSystemVector3(selectedWorldObject.Size);
        
        ImGui.DragFloat3("Position", ref position);
        ImGui.DragFloat3("Rotation", ref rotation);
        ImGui.DragFloat3("Size", ref size);

        if (selectedWorldObject != null)
        {
            selectedWorldObject.X = position.X;
            selectedWorldObject.Y = position.Y;
            selectedWorldObject.Z = position.Z;

            selectedWorldObject.Yaw = rotation.X;
            selectedWorldObject.Pitch = rotation.Y;
            selectedWorldObject.Roll = rotation.Z;

            selectedWorldObject.Size = VectorExtensions.SystemToGLVector3(size);
        }

        ImGui.End();
    }

    private void GenerateObjects()
    {
        var rand = new Random();

        GEGlobalSettings.GlobalLock.EnterWriteLock();

        var textures = new string[] { "Container", "Brick" };
        for (int i = 0; i < m_renderRadius; i++)
        for (int j = 0; j < m_renderRadius; j++)
        for (int k = 0; k < m_renderRadius; k += 2)
            m_objSLots.Add(new Vector3(i - m_renderRadius / 2, j - m_renderRadius / 2, k - m_renderRadius / 2));

        GEGlobalSettings.GlobalLock.ExitWriteLock();

        while (m_objSLots.Count != 0 && !m_exit)
        {
            var index = rand.Next(0, m_objSLots.Count);

            var cube = new Cube()
            {
                Size = new Vector3(rand.Next(1, 4), rand.Next(1, 6), rand.Next(1, 3)),
                Position = m_objSLots[index]
            };

            cube.Collision =
                new Collision(cube,
                    GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
            var texture = GlobalCache<Texture>.GetItemOrDefault(textures[rand.Next(0, 9) % 2]);

            foreach (var mesh in cube.Scene.Meshes)
                mesh.TextureData.Texture = texture;

            GEGlobalSettings.GlobalLock.EnterWriteLock();
            m_world.WorldObjects.Add(cube);

            m_objSLots.RemoveAt(index);
            GEGlobalSettings.GlobalLock.ExitWriteLock();

            // Thread.Sleep(100);
        }

        Console.WriteLine("Done!");
    }

    #region Loads methods
    
    private void LoadShaders()
    {
        var shaderDirPath = Path.Combine(Environment.CurrentDirectory, @"Assets\Shaders");

        foreach (var shaderDir in new DirectoryInfo(shaderDirPath).GetDirectories())
        {
            var vertShaderText = File.ReadAllText(Path.Combine(shaderDir.FullName, $"{shaderDir.Name}.vert"));
            var fragShaderText = File.ReadAllText(Path.Combine(shaderDir.FullName, $"{shaderDir.Name}.frag"));

            GlobalCache<Shader>.AddOrUpdateItem(shaderDir.Name,
                new Shader(vertShaderText, fragShaderText, shaderDir.Name));
        }
    }

    private void LoadTextures()
    {
        var shaderDirPath = Path.Combine(Environment.CurrentDirectory, @"Assets\Textures");
        var skyBoxesDir = Path.Combine(shaderDirPath, "SkyBoxes");
        var textures = new List<string>(20);

        foreach (var textureFile in Directory.GetFiles(shaderDirPath))
        {
            var tName = Path.GetFileNameWithoutExtension(textureFile);
            textures.Add(tName);

            GlobalCache<Texture>.AddOrUpdateItem(tName,
                Texture.CreateTexture(textureFile));
        }

        GlobalCache<List<string>>.AddOrUpdateItem("Textures", textures);

        // foreach (var textureFile in Directory.GetFiles(skyBoxesDir))
        // {
        //     GlobalCache<Texture>.AddOrUpdateItem(Path.GetFileNameWithoutExtension(textureFile),
        //         Texture.CreateSkyBoxTextureFromOneImg(textureFile));
        // }

        foreach (var skyboxDir in new DirectoryInfo(skyBoxesDir).GetDirectories())
        {
            GlobalCache<Texture>.AddOrUpdateItem(skyboxDir.Name,
                Texture.CreateSkyBoxTexture(skyboxDir.FullName));
        }
    }

    private void LoadFonts()
    {
        var fontsDirPath = Path.Combine(Environment.CurrentDirectory, @"Assets\Fonts");

        foreach (var fontPath in Directory.GetFiles(fontsDirPath))
        {
            var font = Font.CreateFont(fontPath);

            if (font == null)
                Console.WriteLine($"Error loading font: {Path.GetFileNameWithoutExtension(fontPath)}");
            else
                GlobalCache<FontInformation>.AddOrUpdateItem(font.FontName, font);
        }
    }

    private async Task LoadModelsFromDir(IModelLoader loader, string dirPath)
    {
        foreach (var fPath in Directory.GetFiles(dirPath))
        {
            try
            {
                var loadData = await loader.LoadAsync(fPath);
                var fName = Path.GetFileNameWithoutExtension(fPath);

                GlobalCache<Scene>.AddOrUpdateItem(fName, loadData.Scene);
                Console.WriteLine($"Loading model {fName} completed");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private async Task LoadModels()
    {
        var wfLoader = new WaveFrontLoader();
        var modelsPath = Path.Combine(Environment.CurrentDirectory, @"Assets\Models");

        await LoadModelsFromDir(wfLoader, modelsPath);

        foreach (var dirPath in Directory.GetDirectories(modelsPath))
        {
            await LoadModelsFromDir(wfLoader, dirPath);
        }

        GEGlobalSettings.GlobalLock.EnterWriteLock();
        InitDefaultObjects();

        InitCameraPhysics();
        GEGlobalSettings.GlobalLock.ExitWriteLock();
    }
    
    #endregion

    private void ApplyGLSettings()
    {
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void InitDefaultObjects()
    {
        var textures = GlobalCache<List<string>>.GetItemOrDefault("Textures");

        m_world.SkyBox.Texture = GlobalCache<Texture>.GetItemOrDefault("SkyBox2");

        var defCube = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10) };
        m_world.WorldObjects.Add(defCube);

        var sphere = new Sphere { Size = new Vector3(2, 2, 2), Position = new Vector3(0, 0, -1) };
        sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        m_world.WorldObjects.Add(sphere);

        m_testCube = new Cube { Size = new Vector3(5, 1, 10), Position = new Vector3(0, 5, 0) };
        m_world.WorldObjects.Add(m_testCube);

        m_world.Player = m_testCube;

        m_world.Player.Collision = new Collision(m_world.Player,
            GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
        m_collisionables.Add(m_world.Player);

        defCube.Collision =
            new Collision(defCube, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));

        foreach (var wObject in m_world.WorldObjects)
        {
            var cubeRBody = new RigidBody();

            cubeRBody.MaxSpeed = 2;
            cubeRBody.MaxBackSpeed = 2;
            cubeRBody.MaxSpeedMultiplier = 1;
            cubeRBody.Rotation = MathHelper.DegreesToRadians(30);
            cubeRBody.Acceleration = 10;
            cubeRBody.DefaultJumpForce = 1;
            cubeRBody.BreakMultiplier = 1.5f;

            wObject.RigidBody = cubeRBody;
            
            foreach (var mesh in wObject.Scene.Meshes)
                mesh.TextureData.Texture =
                    GlobalCache<Texture>.GetItemOrDefault(textures[Random.Shared.Next(0, textures.Count)]);
        }
        
        m_world.CurrentCamera.FOV = 45;
        m_world.CurrentCamera.Size = new Vector3(1);
        m_world.CurrentCamera.Collision = new Collision(m_world.CurrentCamera,
            GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        
        m_interactionWorker.Start();
    }

    private void HandleCameraMove(in FrameEventArgs args)
    {
        var timeDelta = (float)args.Time;
        var camRBody = m_world.CurrentCamera.RigidBody;

        var speedMultiplier = 1f;

        if (KeyboardState.IsKeyDown(Keys.LeftControl))
            speedMultiplier = 3f;

        if (KeyboardState.IsKeyDown(Keys.D))
            m_world.CurrentCamera.Position +=
                Vector3.Normalize(Vector3.Cross(m_world.CurrentCamera.Direction, m_world.CurrentCamera.CameraUp)) *
                camRBody.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.A))
            m_world.CurrentCamera.Position -=
                Vector3.Normalize(Vector3.Cross(m_world.CurrentCamera.Direction, m_world.CurrentCamera.CameraUp)) *
                camRBody.Speed * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.W))
            m_world.CurrentCamera.Position +=
                m_world.CurrentCamera.Direction * camRBody.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.S))
            m_world.CurrentCamera.Position -=
                m_world.CurrentCamera.Direction * camRBody.Speed * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.Space))
            m_world.CurrentCamera.Position +=
                m_world.CurrentCamera.CameraUp * camRBody.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.LeftShift))
            m_world.CurrentCamera.Position -=
                m_world.CurrentCamera.CameraUp * camRBody.Speed * timeDelta * speedMultiplier;

        m_world.CurrentCamera.AcceptTransform();

        if (m_world.CurrentCamera.Collision == null)
            return;

        Vector3 normal = Vector3.Zero;
        float depth = 0;

        // foreach (var worldObject in m_world.WorldObjects)
        // {
        //     if (worldObject.Collision == null)
        //         continue;
        //
        //     m_haveCollision = worldObject.Collision.CheckCollision(m_world.CurrentCamera, out normal, out depth);
        //
        //     if (m_haveCollision)
        //         break;
        // }

        if (m_haveCollision)
            m_world.CurrentCamera.Position += normal * depth;
    }

    private void HandleObjectMove(in FrameEventArgs args)
    {
        var timeDelta = (float)args.Time;
        
        if (m_world.Player == null)
            return;
        
        var position = m_world.Player.Position;
        var rigidBody = m_world.Player.RigidBody;
        var moved = KeyboardState.IsKeyDown(Keys.W) || KeyboardState.IsKeyDown(Keys.S);

        if (KeyboardState.IsKeyDown(Keys.D))
            m_world.Player.Pitch += 1;
        else if (KeyboardState.IsKeyDown(Keys.A))
            m_world.Player.Pitch -= 1;
        
        if (KeyboardState.IsKeyDown(Keys.W))
            rigidBody.Speed += rigidBody.Acceleration * timeDelta * (rigidBody.Speed < 0 ? 5 : 1);
        else if (KeyboardState.IsKeyDown(Keys.S))
            rigidBody.Speed -= rigidBody.Acceleration * timeDelta * (rigidBody.Speed > 0 ? 5 : 1);
        
        if (KeyboardState.IsKeyDown(Keys.Space))
            rigidBody.VerticalForce = rigidBody.DefaultJumpForce;

        position += (Matrix3.CreateRotationX(MathHelper.DegreesToRadians(m_world.Player.Direction.X)) * 
                     Matrix3.CreateRotationY(MathHelper.DegreesToRadians(m_world.Player.Direction.Y)) * 
                     Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(m_world.Player.Direction.Z)) * 
                     Vector3.UnitZ * rigidBody.Speed +
                     m_world.CurrentCamera.CameraUp * rigidBody.VerticalForce) * timeDelta;

        if (m_activePhysic)
            rigidBody.VerticalForce += PhysicsConstants.g * timeDelta;
        
        if (!m_activePhysic && rigidBody.Speed != 0 &&
            !moved && m_posDelta != Vector3.Zero)
            if (rigidBody.Speed > 0.1f || rigidBody.Speed < -0.1f)
                rigidBody.Speed += rigidBody.Acceleration * (rigidBody.Speed > 0 ? -1 : 1) * timeDelta;
            else
                rigidBody.Speed = 0;
        
        m_posDelta = position - m_world.Player.Position;
        
        if (rigidBody.VerticalForce > 2 * PhysicsConstants.g)
            rigidBody.VerticalForce =
                MathHelper.InverseSqrtFast(rigidBody.VerticalForce * rigidBody.VerticalForce - 2 * PhysicsConstants.g);
        else
            rigidBody.VerticalForce = 0;
        
        m_world.Player.Position = position;
    }

    //TODO: Fix multithreading collision resolution
    private void HandleCollision()
    {
        var normal = Vector3.Zero;
        var depth = 0f;
        var haveCollision = false;

        // var collisionWatch = new Stopwatch();
        // collisionWatch.Start();

        while (!m_exit)
        {
            try
            {
                while (!m_exit)
                {
                    for (int i = 0; i < m_world.WorldObjects.Count; i++)
                        m_world.WorldObjects[i].Collision.UpdateCollision();

                    // GEGlobalSettings.GlobalLock.EnterWriteLock();
                    for (int i = 0; i < m_world.WorldObjects.Count; i++)
                    {
                        for (int j = i + 1; j < m_world.WorldObjects.Count; j++)
                        {
                            haveCollision = m_world.WorldObjects[i].Collision.CheckCollision(m_world.WorldObjects[j].Collision, out normal, out depth);

                            if (haveCollision)
                            {
                                // m_world.WorldObjects[i].Position += -normal * depth;
                                m_world.WorldObjects[j].Position += normal * depth;
                                m_world.WorldObjects[j].Collision.UpdateCollision();
                            }
                        }
                    }
                    // GEGlobalSettings.GlobalLock.ExitWriteLock();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private void HandleMouseMove()
    {
        if (m_firstMouseMove)
        {
            m_lastMousePos = MousePosition;
            m_firstMouseMove = false;
        }
        else
        {
            var deltaX = MousePosition.X - m_lastMousePos.X;
            var deltaY = MousePosition.Y - m_lastMousePos.Y;
            m_lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);

            if (m_mouseDown)
            {
                // Apply the camera pitch and yaw (we clamp the pitch in the camera class)
                m_world.CurrentCamera.Yaw += deltaX * GlobalSettings.UserSettings.Sensitivity;
                m_world.CurrentCamera.Pitch +=
                    -deltaY * GlobalSettings.UserSettings
                        .Sensitivity; // Reversed since y-coordinates range from bottom to top
            }
        }
    }

    #region Overloads

    protected override void OnLoad()
    {
        VSync = VSyncMode.On;
        m_imGuiController = new ImGuiController(Size.X, Size.Y);

        Title += $". OpenGL: {GL.GetString(StringName.Version)}";

        ApplyGLSettings();

        // try
        // {
        LoadShaders();
        LoadTextures();
        LoadFonts();
        InitControls();

        ObjectRenderer.RegisterScene(typeof(SkyBox),
            GlobalCache<Shader>.GetItemOrDefault("SkyBoxShader"));

        CollisionRenderer.Shader = GlobalCache<Shader>.GetItemOrDefault("CollisionShader");

        TextRenderer.Shader = GlobalCache<Shader>.GetItemOrDefault("FontShader");

        LoadModels().Wait();

        m_currentObject = m_world.CurrentCamera;

        ObjectRenderer.AddDrawables(m_world.WorldObjects, GlobalCache<Shader>.GetItemOrDefault("DefaultShader"));
        CollisionRenderer.AddCollisions(m_world.WorldObjects);

        // m_generateObjectThread.Start();
        // }
        // catch (Exception e)
        // {
        //     Console.WriteLine(e);
        // }

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            (float)Size.X / Size.Y, 0.1f, GlobalSettings.MaxDepthLength);

        base.OnLoad();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        m_mouseDown = true;
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        m_mouseDown = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Size.X == 0 || Size.Y == 0)
            return;

        var aspect = (float)Size.X / Size.Y;
        m_world.CurrentCamera.FOV += -e.OffsetY;

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, GlobalSettings.MaxDepthLength);

        m_imGuiController.MouseScroll(e.Offset);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        m_imGuiController.PressChar((char)e.Unicode);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.Key == Keys.LeftControl)
            m_currentObject.RigidBody.MaxSpeedMultiplier = 3;


        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.D0:
                m_debugView = !m_debugView;
                break;
            case Keys.D1:
                m_freeCamMode = !m_freeCamMode;
                
                ChangeCameraMode();
                break;
            case Keys.LeftControl:
                m_currentObject.RigidBody.MaxSpeedMultiplier = 1;
                break;
            case Keys.F:
                m_activePhysic = !m_activePhysic;
                break;
            case Keys.N:
                m_drawNormals = !m_drawNormals;
                break;
            case Keys.R:
                m_rotation = new Vector3();
                m_currentObject.X = 0;
                m_currentObject.Y = 0;
                m_currentObject.Z = 0;
                // m_world.Camera.Roll = 0;
                // m_world.Camera.Yaw = 0;
                // m_world.Camera.Pitch = 0;
                break;
            case Keys.O:
                m_drawFaceNumber = !m_drawFaceNumber;
                break;
            default:
            {
                if (e.Alt && e.Key == Keys.Enter)
                    WindowState = WindowState == WindowState.Normal ? WindowState.Fullscreen : WindowState.Normal;

                break;
            }
        }

        base.OnKeyUp(e);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        m_fps = 1.0 / args.Time;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.DepthTest);

        GL.Enable(EnableCap.CullFace);

        GL.DepthFunc(DepthFunction.Lequal);
        ObjectRenderer.DrawSkyBox(m_world.SkyBox, m_world.CurrentCamera);
        GL.DepthFunc(DepthFunction.Less);

        GEGlobalSettings.GlobalLock.EnterReadLock();

        if (!m_debugView)
            ObjectRenderer.DrawElements(m_world.CurrentCamera, m_drawFaceNumber, m_drawNormals);
        else
            CollisionRenderer.DrawElementsCollision(m_world.CurrentCamera, drawVerticesPositions: false);

        // CollisionRenderer.DrawCollision(m_world.Camera, m_world.Camera, false);

        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        foreach (var control in m_controls)
            control.Draw();

        GEGlobalSettings.GlobalLock.ExitReadLock();

        HandleImGUI();
        m_imGuiController.Render();

        Context.SwapBuffers();

        base.OnRenderFrame(args);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();
        
        if (m_freeCamMode)
            HandleCameraMove(args);
        else
            HandleObjectMove(args);

        // HandleCollision();

        HandleMouseMove();
        m_imGuiController.Update(this, (float)args.Time);

        // m_world.SkyBox.Rotation = new Vector3(0, (m_world.SkyBox.Rotation.Y + 0.05f), 0);

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbCamRotation.Text = m_currentObject.Direction.ToString();
        m_tbX.Text = m_currentObject.Position.X.ToString();
        m_tbY.Text = m_currentObject.Position.Y.ToString();
        m_tbZ.Text = m_currentObject.Position.Z.ToString();
        m_tbRotation.Text = m_rotation.ToString();
        m_tbHaveCollision.Text = m_haveCollision.ToString();
        m_tbCurrentSpeed.Text = m_currentObject.RigidBody.Speed.ToString();

        base.OnUpdateFrame(args);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, Size.X, Size.Y);

        GEGlobalSettings.ScreenProjection = Matrix4.CreateOrthographicOffCenter(0, Size.X, 0, Size.Y, -1, 1);
        // GEGlobalSettings.ScreenProjection = Matrix4.CreateOrthographicOffCenter(0, Size.X, Size.Y, 0, -1, 1);

        GEGlobalSettings.WindowWidth = Size.X;
        GEGlobalSettings.WindowHeight = Size.Y;

        if (Size.X == 0 || Size.Y == 0)
            return;

        var aspect = (float)Size.X / Size.Y;

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, GlobalSettings.MaxDepthLength);

        m_imGuiController.WindowResized(Size.X, Size.Y);

        VSync = VSyncMode.On;
    }

    protected override void Dispose(bool disposing)
    {
        m_exit = true;
        if (m_generateObjectThread.IsAlive)
            m_generateObjectThread.Join();

        if (m_interactionWorker.IsAlive)
            m_interactionWorker.Join();

        base.Dispose(disposing);
    }

    #endregion
}