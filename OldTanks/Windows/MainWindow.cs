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

    private World m_world;

    private double m_fps;

    private Cube m_testCube;

    private bool m_debugView;
    private bool m_activePhysic;
    private bool m_drawNormals;
    private bool m_drawFaceNumber;
    private bool m_mouseDown;

    private Vector3 m_camPosDelta;

    private Vector2 m_lastMousePos;
    private bool m_firstMouseMove;

    private Vector3 m_rotation;

    private readonly Dictionary<string, byte[]> m_imGUITextBoxDatas;

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
        m_world = new World();
        m_controls = new List<Control>();

        // m_renderRadius = 20;

        GlobalSettings.UserSettings.Sensitivity = 0.1f;

        m_objSLots = new List<Vector3>(20);

        m_generateObjectThread = new Thread(GenerateObjects);

        m_imGUITextBoxDatas = new Dictionary<string, byte[]>();

        m_rotation = new Vector3(0, 0, 0);
    }

    private void InitCameraPhysics()
    {
        var rBody = new RigidBody();

        rBody.MaxSpeed = 2;
        rBody.MaxBackSpeed = 2;
        rBody.MaxSpeedMultiplier = 1;
        rBody.Rotation = MathHelper.DegreesToRadians(30);
        rBody.Acceleration = 10;
        rBody.DefaultJumpForce = 1;
        rBody.BreakMultiplier = 1.5f;

        m_world.Camera.RigidBody = rBody;
    }
    
    private void HandleImGUI()
    {
        var testCubePos = VectorExtensions.GLToSystemVector3(m_testCube.Position);
        var testCubeRotation = VectorExtensions.GLToSystemVector3(m_testCube.Direction);
        var testCubeSize = VectorExtensions.GLToSystemVector3(m_testCube.Size);

        var defCubePos = VectorExtensions.GLToSystemVector3(m_world.WorldObjects[0].Position);
        var defCubeDirection = VectorExtensions.GLToSystemVector3(m_world.WorldObjects[0].Direction);
        var defCubeSize = VectorExtensions.GLToSystemVector3(m_world.WorldObjects[0].Size);

        ImGui.Begin("Debug");

        ImGui.Columns(2);

        ImGui.Text("Test cube data");

        ImGui.DragFloat3("Position", ref testCubePos);
        ImGui.DragFloat3("Rotation", ref testCubeRotation);
        ImGui.DragFloat3("Size", ref testCubeSize);


        for (int i = 0; i < m_testCube.Scene.Meshes.Count; i++)
        {
            var mesh = m_testCube.Scene.Meshes[i];
            var expanded = ImGui.TreeNodeEx($"Mesh {i}");
            var scale = VectorExtensions.GLToSystemVector2(mesh.TextureData.Scale);
            var angle = mesh.TextureData.RotationAngle;

            if (expanded)
            {
                ImGui.DragFloat2($"Mesh texture scale {i}", ref scale);
                ImGui.DragFloat($"Mesh texture angle {i}", ref angle);
                ImGui.TreePop();
            }

            mesh.TextureData.Scale = VectorExtensions.SystemToGLVector2(scale);
            mesh.TextureData.RotationAngle = angle;
        }

        ImGui.NextColumn();

        m_testCube.Position = VectorExtensions.SystemToGLVector3(testCubePos);
        m_testCube.Direction = VectorExtensions.SystemToGLVector3(testCubeRotation);
        m_testCube.Size = VectorExtensions.SystemToGLVector3(testCubeSize);

        ImGui.Text("Def cube data");

        ImGui.DragFloat3("Position 1", ref defCubePos);
        ImGui.DragFloat3("Rotation 1", ref defCubeDirection);
        ImGui.DragFloat3("Size 1", ref defCubeSize);

        m_world.WorldObjects[0].Position = VectorExtensions.SystemToGLVector3(defCubePos);
        m_world.WorldObjects[0].Direction = VectorExtensions.SystemToGLVector3(defCubeDirection);
        m_world.WorldObjects[0].Size = VectorExtensions.SystemToGLVector3(defCubeSize);

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
                    GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"), CollisionType.Polygon);
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
        GEGlobalSettings.GlobalLock.ExitWriteLock();
    }

    private void ApplyGLSettings()
    {
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void InitDefaultObjects()
    {
        var textures = GlobalCache<List<string>>.GetItemOrDefault("Textures");

        var defCube = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10), Visible = true };
        m_world.WorldObjects.Add(defCube);

        var sphere = new Sphere { Size = new Vector3(2, 2, 2), Position = new Vector3(0, 0, -1), Visible = true };
        m_world.WorldObjects.Add(sphere);
        
        // var robot = new Robot { Size = new Vector3(10, 10, 10), Position = new Vector3(0, 10, 0), Visible = true };
        // m_world.WorldObjects.Add(robot);


        m_testCube = new Cube { Size = new Vector3(5, 1, 10), Position = new Vector3(0, 5, 0), Visible = true };
        m_world.WorldObjects.Add(m_testCube);

        m_world.SkyBox.Texture = GlobalCache<Texture>.GetItemOrDefault("SkyBox2");

        m_world.Camera.Collision = new Collision(m_world.Camera,
            GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"), CollisionType.Polygon);
        m_world.Camera.Size = new Vector3(1f);
        m_world.Camera.Yaw = 45;
        m_world.Camera.Collision.IsActive = true;
        m_collisionables.Add(m_world.Camera);

        defCube.Collision =
            new Collision(defCube, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"), CollisionType.Polygon);
        defCube.Collision.IsActive = true;

        m_testCube.Collision =
            new Collision(m_testCube, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"),
                CollisionType.Polygon);
        m_testCube.Collision.IsActive = true;

        foreach (var wObject in m_world.WorldObjects)
        foreach (var mesh in wObject.Scene.Meshes)
            mesh.TextureData.Texture = GlobalCache<Texture>.GetItemOrDefault(textures[Random.Shared.Next(0, textures.Count)]);
        
        var cubeRBody = new RigidBody();

        cubeRBody.MaxSpeed = 0.1f;
        cubeRBody.MaxBackSpeed = 2;
        cubeRBody.MaxSpeedMultiplier = 1;
        cubeRBody.Rotation = MathHelper.DegreesToRadians(30);
        cubeRBody.Acceleration = 10;
        cubeRBody.DefaultJumpForce = 1;
        cubeRBody.BreakMultiplier = 1.5f;

        m_testCube.RigidBody = cubeRBody;
    }

    private void HandleMove(in FrameEventArgs args)
    {
        var timeDelta = (float)args.Time;

        var camPos = m_world.Camera.Position;
        var camRBody = m_world.Camera.RigidBody;
        var moveDirection = new Vector3();

        if (KeyboardState.IsKeyDown(Keys.D))
        {
            camPos += Vector3.Normalize(Vector3.Cross(m_world.Camera.Direction, m_world.Camera.CameraUp)) *
                      Math.Abs(camRBody.Speed);
            camRBody.Speed += camRBody.Acceleration * timeDelta;
            moveDirection.X = 1;
        }
        else if (KeyboardState.IsKeyDown(Keys.A))
        {
            camPos -= Vector3.Normalize(Vector3.Cross(m_world.Camera.Direction, m_world.Camera.CameraUp)) *
                      Math.Abs(camRBody.Speed);
            camRBody.Speed += camRBody.Acceleration * timeDelta;
            moveDirection.X = -1;
        }

        if (KeyboardState.IsKeyDown(Keys.W))
        {
            camRBody.Speed += camRBody.Acceleration * timeDelta * (camRBody.Speed < 0 ? 5 : 1);
            moveDirection.Z = 1;
        }
        else if (KeyboardState.IsKeyDown(Keys.S))
        {
            camRBody.Speed -= camRBody.Acceleration * timeDelta * (camRBody.Speed > 0 ? 5 : 1);
            moveDirection.Z = -1;
        }

        if (KeyboardState.IsKeyDown(Keys.Space))
            camRBody.VerticalForce = camRBody.DefaultJumpForce;

        if (m_activePhysic)
            moveDirection.Y = -1;

        camPos += m_world.Camera.Direction * camRBody.Speed +
                  m_world.Camera.CameraUp * camRBody.VerticalForce;

        var camMoved = moveDirection != Vector3.Zero;

        if (m_activePhysic)
            camRBody.VerticalForce += PhysicsConstants.g * timeDelta;

        if (!m_activePhysic && camRBody.Speed != 0 &&
            !camMoved && m_camPosDelta != Vector3.Zero)
            if (camRBody.Speed > 0.1f || camRBody.Speed < -0.1f)
                camRBody.Speed += camRBody.Acceleration * (camRBody.Speed > 0 ? -1 : 1) * timeDelta;
            else
                camRBody.Speed = 0;

        m_camPosDelta = camPos - m_world.Camera.Position;

        if (camRBody.VerticalForce > 2 * PhysicsConstants.g)
            camRBody.VerticalForce =
                MathHelper.InverseSqrtFast(camRBody.VerticalForce * camRBody.VerticalForce - 2 * PhysicsConstants.g);
        else
            camRBody.VerticalForce = 0;

        m_world.Camera.Position = camPos;

        m_world.Camera.Collision.CurrentObject.AcceptTransform();

        Vector3 normal = Vector3.Zero;
        float depth = 0;

        foreach (var worldObject in m_world.WorldObjects)
        {
            if (worldObject.Collision == null)
                continue;
            m_haveCollision = worldObject.Collision.CheckCollision(m_world.Camera, out normal, out depth);

            if (m_haveCollision)
                break;
        }

        if (m_haveCollision)
            m_world.Camera.Position += normal * depth;

        var testCubeMoving = new Vector3();

        if (KeyboardState.IsKeyDown(Keys.Up))
            testCubeMoving.Z = 1;
        else if (KeyboardState.IsKeyDown(Keys.Down))
            testCubeMoving.Z = -1;

        if (KeyboardState.IsKeyDown(Keys.Left))
            testCubeMoving.X = 1;
        else if (KeyboardState.IsKeyDown(Keys.Right))
            testCubeMoving.X = -1;

        if (KeyboardState.IsKeyDown(Keys.KeyPad7))
            testCubeMoving.Y = 1;
        else if (KeyboardState.IsKeyDown(Keys.KeyPad9))
            testCubeMoving.Y = -1;


        if (testCubeMoving != Vector3.Zero)
            m_testCube.RigidBody.Speed += m_testCube.RigidBody.Acceleration * timeDelta;

        if (m_testCube.RigidBody.Speed != 0 && testCubeMoving == Vector3.Zero && m_camPosDelta != Vector3.Zero)
            if (m_testCube.RigidBody.Speed > 0.1f || m_testCube.RigidBody.Speed < -0.1f)
                m_testCube.RigidBody.Speed -= m_testCube.RigidBody.Acceleration * timeDelta;
            else
                m_testCube.RigidBody.Speed = 0;

        m_testCube.Position += testCubeMoving * m_testCube.RigidBody.Speed;

        foreach (var worldObject in m_world.WorldObjects)
        {
            if (worldObject.Collision == null)
                continue;
            if (worldObject != m_testCube)
                m_haveCollision = worldObject.Collision.CheckCollision(m_testCube, out normal, out depth);
            else
                continue;

            if (m_haveCollision)
                break;
        }

        if (m_haveCollision)
            m_testCube.Position += normal * depth;
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
                m_world.Camera.Yaw += deltaX * GlobalSettings.UserSettings.Sensitivity;
                m_world.Camera.Pitch +=
                    -deltaY * GlobalSettings.UserSettings
                        .Sensitivity; // Reversed since y-coordinates range from bottom to top
            }
        }
    }

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
        
        InitCameraPhysics();

        LoadModels().Wait();


        ObjectRenderer.AddDrawables(m_world.WorldObjects, GlobalCache<Shader>.GetItemOrDefault("DefaultShader"));
        CollisionRenderer.AddCollisions(m_world.WorldObjects);

        // m_generateObjectThread.Start();
        // }
        // catch (Exception e)
        // {
        //     Console.WriteLine(e);
        // }

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.Camera.FOV),
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
        m_world.Camera.FOV += -e.OffsetY;

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.Camera.FOV),
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
            m_world.Camera.RigidBody.MaxSpeedMultiplier = 3;


        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.D0:
                m_debugView = !m_debugView;
                break;
            case Keys.LeftControl:
                m_world.Camera.RigidBody.MaxSpeedMultiplier = 1;
                break;
            case Keys.F:
                m_activePhysic = !m_activePhysic;
                break;
            case Keys.N:
                m_drawNormals = !m_drawNormals;
                break;
            case Keys.R:
                m_rotation = new Vector3();
                m_world.Camera.Position = new Vector3();
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
        ObjectRenderer.DrawSkyBox(m_world.SkyBox, m_world.Camera);
        GL.DepthFunc(DepthFunction.Less);

        GEGlobalSettings.GlobalLock.EnterReadLock();

        if (!m_debugView)
            ObjectRenderer.DrawElements(m_world.Camera, m_drawFaceNumber, m_drawNormals);
        else
            CollisionRenderer.DrawElementsCollision(m_world.Camera);

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

        HandleMove(args);
        HandleMouseMove();
        m_imGuiController.Update(this, (float)args.Time);

        // m_world.SkyBox.Rotation = new Vector3(0, (m_world.SkyBox.Rotation.Y + 0.05f), 0);

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbCamRotation.Text = m_world.Camera.Direction.ToString();
        m_tbX.Text = m_world.Camera.Position.X.ToString();
        m_tbY.Text = m_world.Camera.Position.Y.ToString();
        m_tbZ.Text = m_world.Camera.Position.Z.ToString();
        m_tbRotation.Text = m_rotation.ToString();
        m_tbHaveCollision.Text = m_haveCollision.ToString();
        m_tbCurrentSpeed.Text = m_world.Camera.RigidBody.Speed.ToString();

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
            MathHelper.DegreesToRadians(m_world.Camera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, GlobalSettings.MaxDepthLength);

        m_imGuiController.WindowResized(Size.X, Size.Y);

        VSync = VSyncMode.On;
    }

    protected override void Dispose(bool disposing)
    {
        m_exit = true;
        if (m_generateObjectThread.IsAlive)
            m_generateObjectThread.Join();

        base.Dispose(disposing);
    }
}