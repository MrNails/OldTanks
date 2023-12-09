using System.Buffers;
using System.Diagnostics;
using Common.Extensions;
using Common.Services;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using CoolEngine.Services.Renderers;
using OldTanks.DataModels;
using OldTanks.UI.Controls;
using OldTanks.Models;
using OldTanks.Services;
using OldTanks.UI.Services;
using OldTanks.UI.Services.ImGUI;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using EngineSettings = CoolEngine.Services.EngineSettings;

namespace OldTanks.Windows;

public partial class MainWindow : GameWindow
{
    private readonly List<Control> m_controls;
    private readonly SettingsService m_settingsService;
    private readonly ControlHandler m_controlHandler;
    private readonly UI.ImGuiUI.MainWindow m_imGuiMainWindow;
    private readonly Services.EngineSettings m_engineSettings;
    private readonly Settings m_userSettings;
    private readonly GameManager m_gameManager;
    private readonly LoggerService m_loggerService;

    private Shader? m_primitivesShader;

    private bool m_exit;
    private bool m_readyToHandle;

    private ImGuiController m_imGuiController;

    private Font m_font;

    private Thread m_interactionWorker;

    private World m_world;

    private double m_fps;

    private bool m_debugView;
    private bool m_drawNormals;
    private bool m_drawFaceNumber;
    private bool m_mouseDown;
    private bool m_freeCamMode;

    private IPhysicObject m_currentObject;

    private Vector2 m_lastMousePos;
    private bool m_firstMouseMove;

    private Vector3 m_rotation;

    public MainWindow(string caption, SettingsService settingsService, LoggerService logger)
        : this(800, 600, caption, settingsService, logger)
    {
    }

    public MainWindow(int height, int width, string caption,
        SettingsService settingsService, LoggerService logger)
        : this(GameWindowSettings.Default,
            new NativeWindowSettings
            {
                Size = new Vector2i(height, width),
                Title = caption,
                APIVersion = new Version(4, 6),
                Flags = ContextFlags.ForwardCompatible
                        | ContextFlags.Debug
            },
            settingsService, logger)
    {
    }

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings,
        SettingsService settingsService, LoggerService loggerService)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        m_settingsService = settingsService;
        m_userSettings = m_settingsService.GetDefaultSettings<Settings>()!;
        
        Log.Logger.AddGLMessageHandling();
        
        if (m_userSettings.FullScreen)
        {
            WindowState = WindowState.Fullscreen;
        }

        m_controls = new List<Control>();
        m_world = new World();

        m_loggerService = loggerService;
        m_gameManager = new GameManager(loggerService, settingsService, m_world);

        m_freeCamMode = true;
        m_engineSettings = new Services.EngineSettings();
        m_settingsService.SetRuntimeSettings(nameof(Services.EngineSettings), m_settingsService);

        EngineSettings.Current.CollisionIterations = 20;

        m_rotation = new Vector3(0, 0, 0);

        m_interactionWorker = new Thread(WorldHandler) { IsBackground = true };
        m_controlHandler = new ControlHandler();
        m_imGuiMainWindow = new UI.ImGuiUI.MainWindow("DebugWindow", m_gameManager) { Title = "Debug window", IsVisible = true };
    }

    #region Overloads

    protected override void OnLoad()
    {
        VSync = VSyncMode.On;
        m_imGuiController = new ImGuiController(Size.X, Size.Y, m_loggerService.CreateLogger());

        Title += $". OpenGL: {GL.GetString(StringName.Version)}";

        ApplyGLSettings();

        m_gameManager.FontsLoaded += OnFontsLoaded;
        m_gameManager.ShadersLoaded += OnShadersLoaded;
        m_gameManager.SkyBoxesLoaded += OnSkyBoxesLoaded;

        var shadersTask = m_gameManager.LoadShaders();
        var fontsTask = m_gameManager.LoadFonts();
        var texturesTask = m_gameManager.LoadTextures();
        var skyBoxesTask = m_gameManager.LoadSkyBoxes();
        var modelsTask = m_gameManager.LoadModels();

        Task.WhenAll(shadersTask, fontsTask, texturesTask, skyBoxesTask, modelsTask)
            .ContinueWith(r =>
            {
                m_gameManager.FontsLoaded -= OnFontsLoaded;
                m_gameManager.ShadersLoaded -= OnShadersLoaded;
                m_gameManager.SkyBoxesLoaded -= OnSkyBoxesLoaded;

                ObjectRenderer.RegisterScene(typeof(SkyBox),
                    GlobalCache<Shader>.Default.GetItemOrDefault("SkyBoxShader")!);

                m_currentObject = m_world.CurrentCamera;

                InitDefaultObjects();

                ObjectRenderer.AddDrawables(m_world.WorldObjects,
                    GlobalCache<Shader>.Default.GetItemOrDefault("DefaultShader")!);
                CollisionRenderer.AddCollisions(m_world.WorldObjects);

                m_readyToHandle = true;
            });

        EngineSettings.Current.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            (float)Size.X / Size.Y, 0.1f, m_engineSettings.MaxDepthLength);

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

        EngineSettings.Current.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, m_engineSettings.MaxDepthLength);

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
                m_freeCamMode = m_world.Player == null || !m_freeCamMode;

                ChangeCameraMode();
                break;
            case Keys.LeftControl:
                m_currentObject.RigidBody.MaxSpeedMultiplier = 1;
                break;
            case Keys.F:
                EngineSettings.Current.PhysicsEnable = !EngineSettings.Current.PhysicsEnable;
                break;
            case Keys.N:
                m_drawNormals = !m_drawNormals;
                break;
            case Keys.R:
                m_rotation = new Vector3();
                m_currentObject.X = 0;
                m_currentObject.Y = 0;
                m_currentObject.Z = 0;
                break;
            case Keys.O:
                m_drawFaceNumber = !m_drawFaceNumber;
                break;
            default:
            {
                if (e is { Alt: true, Key: Keys.Enter })
                {
                    WindowState = WindowState != WindowState.Fullscreen ? WindowState.Fullscreen : WindowState.Normal;
                }
                
                break;
            }
        }

        base.OnKeyUp(e);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        if (!m_readyToHandle)
            return;

        m_fps = 1.0 / args.Time;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);

        GL.DepthFunc(DepthFunction.Lequal);
        ObjectRenderer.DrawSkyBox(m_world.SkyBox, m_world.CurrentCamera);
        GL.DepthFunc(DepthFunction.Less);

        if (!m_debugView)
            ObjectRenderer.DrawElements(m_world.CurrentCamera);
        else
            CollisionRenderer.DrawElementsCollision(m_world.CurrentCamera, m_font, drawVerticesPositions: false);

        OnRenderPrimitives();

        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        foreach (var control in m_controls)
            control.Draw();

        foreach (var worldObject in m_world.WorldObjects)
        {
            if (!string.IsNullOrEmpty(worldObject.Name))
            {
                TextRenderer.DrawText3D(m_font, worldObject.Name, m_world.CurrentCamera,
                    new TextDrawInformation
                    {
                        Color = Colors.Red,
                        OriginPosition = worldObject.Position,
                        SelfPosition = new Vector3(0, worldObject.Size.Y / 2, 0),
                        Scale = 0.05f,
                    }, true);
            }
        }

        EngineSettings.Current.GlobalLock.EnterWriteLock();
        
        m_controlHandler.HandleControls();
        
        EngineSettings.Current.GlobalLock.ExitWriteLock();

        m_imGuiController.Render();

        Context.SwapBuffers();

        base.OnRenderFrame(args);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        Application.Current.Dispatcher.HandleQueue();

        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        if (!m_readyToHandle)
            return;

        m_imGuiController.Update(this, (float)args.Time);

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbSubIterationAmount.Text = EngineSettings.Current.CollisionIterations.ToString();
        m_tbCamRotation.Text = m_currentObject.Direction.ToString();
        m_tbPosition.Text = m_currentObject.Position.ToString();
        m_tbRotation.Text = m_rotation.ToString();
        m_tbHaveCollision.Text = "<empty>";
        m_tbCurrentSpeed.Text = m_currentObject.RigidBody.Velocity.ToString();

        base.OnUpdateFrame(args);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, Size.X, Size.Y);

        EngineSettings.Current.ScreenProjection = Matrix4.CreateOrthographicOffCenter(0, Size.X, 0, Size.Y, -1, 1);

        EngineSettings.Current.WindowWidth = Size.X;
        EngineSettings.Current.WindowHeight = Size.Y;

        if (Size.X == 0 || Size.Y == 0)
            return;

        var aspect = (float)Size.X / Size.Y;

        EngineSettings.Current.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.CurrentCamera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, m_engineSettings.MaxDepthLength);

        m_imGuiController.WindowResized(Size.X, Size.Y);

        VSync = VSyncMode.On;
    }

    protected override void Dispose(bool disposing)
    {
        m_exit = true;

        if (m_interactionWorker.IsAlive)
            m_interactionWorker.Join();

        base.Dispose(disposing);
    }

    #endregion

    private void ChangeCameraMode()
    {
        m_currentObject = m_freeCamMode ? m_world.CurrentCamera : m_world.Player;

        if (!m_freeCamMode && m_currentObject is IWatchable watchable)
            watchable.Camera = m_world.CurrentCamera;
    }

    private void ApplyGLSettings()
    {
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void FillObject(IDrawable drawable, Texture texture)
    {
        foreach (var mesh in drawable.Scene.Meshes)
            mesh.TextureData.Texture = texture;
    }

    private void InitDefaultObjects()
    {
        var tempObjects = new List<WorldObject>();
        Sphere sphere = null;

        #region Static objects

        var wall = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10) };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(wall);

        FillObject(wall, GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture"));

        wall = new Cube
            { Size = new Vector3(20, 5, 2f), Position = new Vector3(9.95f, 1, 0), Direction = new Vector3(0, 90, 0) };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(wall);

        FillObject(wall, GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture"));

        var floor = new Cube { Size = new Vector3(20, 1, 20), Position = new Vector3(0, -2, 0) };
        floor.Collision = new Collision(floor, GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(floor);

        FillObject(floor, GlobalCache<Texture>.Default.GetItemOrDefault("FloorTile"));

        // sphere = new Sphere { Size = new Vector3(2, 2, 2), Position = new Vector3(0, 0, -1) };
        // sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        // tempObjects.Add(sphere);

        // FillObject(sphere, GlobalCache<Texture>.GetItemOrDefault("Brick"));

        foreach (var wObject in tempObjects)
        {
            wObject.RigidBody.IsStatic = true;
            wObject.RigidBody.Restitution = 0.5f;
        }

        m_world.WorldObjects.AddRange(tempObjects);

        tempObjects.Clear();

        #endregion

        #region Dynamic objects

        var dynamicCube = new Cube
        {
            Size = new Vector3(5, 1, 10),
            Position = new Vector3(0, 5, 0),
            Name = $"Cube 1"
        };
        dynamicCube.Collision =
            new Collision(dynamicCube, GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(dynamicCube);

        // sphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(2, 0, -1) };
        // sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        // tempObjects.Add(sphere);

        foreach (var wObject in tempObjects)
        {
            wObject.RigidBody.MaxSpeed = 2;
            wObject.RigidBody.MaxBackSpeed = 2;
            wObject.RigidBody.MaxSpeedMultiplier = 1;
            wObject.RigidBody.Restitution = 0.4f;
            wObject.RigidBody.DefaultJumpForce = -250;

            foreach (var mesh in wObject.Scene.Meshes)
                mesh.TextureData.Texture = GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture");
        }

        m_world.WorldObjects.AddRange(tempObjects);

        #endregion

        m_world.CurrentCamera.FOV = 45;
        m_world.CurrentCamera.Size = new Vector3(1);
        m_world.CurrentCamera.Collision = new Collision(m_world.CurrentCamera,
            GlobalCache<CollisionData>.Default.GetItemOrDefault("CubeCollision"));

        m_interactionWorker.Start();
    }

    private void HandleCameraMove(float timeDelta)
    {
        if (!m_freeCamMode)
            return;

        var camRBody = m_world.CurrentCamera.RigidBody;

        var speedMultiplier = 1f;

        if (KeyboardState.IsKeyDown(Keys.LeftControl))
            speedMultiplier = 3f;

        var posDelta = Vector3.Zero;
        if (KeyboardState.IsKeyDown(Keys.D))
            posDelta += Vector3.Normalize(
                            Vector3.Cross(m_world.CurrentCamera.Direction, m_world.CurrentCamera.CameraUp)) *
                        camRBody.Velocity.X * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.A))
            posDelta -= Vector3.Normalize(
                            Vector3.Cross(m_world.CurrentCamera.Direction, m_world.CurrentCamera.CameraUp)) *
                        camRBody.Velocity.X * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.W))
            posDelta += m_world.CurrentCamera.Direction * camRBody.Velocity.Z * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.S))
            posDelta -= m_world.CurrentCamera.Direction * camRBody.Velocity.Z * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.Space))
            posDelta += m_world.CurrentCamera.CameraUp * camRBody.Velocity.Y * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.LeftShift))
            posDelta -= m_world.CurrentCamera.CameraUp * camRBody.Velocity.Y * timeDelta * speedMultiplier;

        m_world.CurrentCamera.Position += posDelta;
    }

    private void HandleObjectMove(float timeDelta)
    {
        if (m_world.Player == null)
            return;
        var rigidBody = m_world.Player.RigidBody;
        var moved = !m_freeCamMode && (KeyboardState.IsKeyDown(Keys.W) || KeyboardState.IsKeyDown(Keys.S));

        if (!m_freeCamMode)
        {
            if (KeyboardState.IsKeyDown(Keys.D))
                m_world.Player.Pitch += 1;
            else if (KeyboardState.IsKeyDown(Keys.A))
                m_world.Player.Pitch -= 1;

            // if (KeyboardState.IsKeyDown(Keys.W))
            //     rigidBody.Force += rigidBody.Acceleration * timeDelta * (rigidBody.Speed < 0 ? 5 : 1);
            // else if (KeyboardState.IsKeyDown(Keys.S))
            //     rigidBody.Force -= rigidBody.Acceleration * timeDelta * (rigidBody.Speed > 0 ? 5 : 1);

            if (KeyboardState.IsKeyDown(Keys.W))
                rigidBody.Force += m_engineSettings.MovementDirectionUnit * 15;
            else if (KeyboardState.IsKeyDown(Keys.S))
                rigidBody.Force -= m_engineSettings.MovementDirectionUnit * 15;

            if (KeyboardState.IsKeyDown(Keys.Space) && rigidBody.OnGround)
            {
                rigidBody.Force += PhysicsConstants.GravityDirection * rigidBody.DefaultJumpForce;
                rigidBody.OnGround = false;
            }
        }

        // if (GEGlobalSettings.PhysicsEnable && rigidBody.Speed != 0 &&
        //     !moved && m_posDelta != Vector3.Zero)
        //     if (rigidBody.Speed > 0.1f || rigidBody.Speed < -0.1f)
        //         rigidBody.Speed += rigidBody.Acceleration * (rigidBody.Speed > 0 ? -1 : 1) * timeDelta;
        //     else
        //         rigidBody.Speed = 0;
    }

    private void HandleKeyboardInputs(float timeDelta)
    {
        if (!m_freeCamMode)
        {
            if (KeyboardState.IsKeyDown(Keys.E))
                m_world.Player.CameraOffsetAngle -= new Vector2(0, 1);

            if (KeyboardState.IsKeyDown(Keys.Q))
                m_world.Player.CameraOffsetAngle += new Vector2(0, 1);

            if (KeyboardState.IsKeyDown(Keys.Z))
                m_world.Player.CameraOffsetAngle -= new Vector2(1, 0);

            if (KeyboardState.IsKeyDown(Keys.X))
                m_world.Player.CameraOffsetAngle += new Vector2(1, 0);

            if (KeyboardState.IsKeyDown(Keys.C))
            {
                m_world.Player.Camera.Pitch = m_world.Player.Pitch;

                m_world.Player.CameraOffset = new Vector3(-1, 1, 0);
            }
        }
    }

    private void WorldHandler()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (!m_exit)
        {
            try
            {
                while (!m_exit)
                {
                    Thread.Sleep(5);

                    var elapsedTime = (float)stopWatch.Elapsed.TotalSeconds;
                    stopWatch.Restart();

                    HandleKeyboardInputs(elapsedTime);

                    m_tbCollidingPS.Text = Math.Round(1 / elapsedTime, MidpointRounding.ToEven).ToString();

                    if (m_freeCamMode)
                        HandleCameraMove(elapsedTime);

                    HandleObjectMove(elapsedTime);

                    HandleMouseMove();

                    // m_collisionCalculation = true;

                    m_world.CollideObjects(elapsedTime);

                    // m_collisionCalculation = false;
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
                m_world.CurrentCamera.Yaw += deltaX * m_userSettings.Sensitivity;
                m_world.CurrentCamera.Pitch +=
                    -deltaY * m_userSettings
                        .Sensitivity; // Reversed since y-coordinates range from bottom to top
            }
        }
    }

    private void DrawPositionsAndCentersOfMass(Shader shader)
    {
        var drawLength = m_world.WorldObjects.Count(w => w.Visible) * 2;
        var arr = ArrayPool<Vector3>.Shared.Rent(drawLength);

        for (int i = 0, arrIdx = 0; i < m_world.WorldObjects.Count; i++)
        {
            var worldObj = m_world.WorldObjects[i];

            if (worldObj.Visible)
            {
                arr[arrIdx++] = worldObj.Position;
                arr[arrIdx++] = worldObj.Position + worldObj.RigidBody.CenterOfMass;
            }
        }

        GL.PointSize(30);

        ObjectRenderer.DrawPrimitives(PrimitiveType.Points, shader, arr, drawLength);

        GL.PointSize(1);

        ArrayPool<Vector3>.Shared.Return(arr);
    }

    private void DrawNormals(Shader shader)
    {
        var drawLength = m_world.WorldObjects.Where(w => w.Visible)
            .Sum(w => w.Scene.Meshes.Sum(m => m.Faces.Sum(f => f.NormalsIndices.Length) * 2));

        var arr = ArrayPool<Vector3>.Shared.Rent(drawLength);

        for (int i = 0, arrIdx = 0; i < m_world.WorldObjects.Count; i++)
        {
            var worldObj = m_world.WorldObjects[i];

            if (worldObj.Visible)
            {
                var scale = worldObj.Size / 2;
                var rotation = Matrix3.CreateRotationX(MathHelper.DegreesToRadians(worldObj.Direction.X)) *
                               Matrix3.CreateRotationY(MathHelper.DegreesToRadians(-worldObj.Direction.Y)) *
                               Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(worldObj.Direction.Z));

                for (int meshIdx = 0; meshIdx < worldObj.Scene.Meshes.Count; meshIdx++)
                {
                    var mesh = worldObj.Scene.Meshes[meshIdx];

                    for (int k = 0; k < mesh.Faces.Count; k++)
                    {
                        for (int l = 0; l < mesh.Faces[k].NormalsIndices.Length; l++)
                        {
                            arr[arrIdx] = worldObj.Position +
                                          mesh.Normals[mesh.Faces[k].NormalsIndices[l]] * scale * rotation;
                            arr[arrIdx + 1] =
                                arr[arrIdx] + mesh.Normals[mesh.Faces[k].NormalsIndices[l]] * rotation;

                            arrIdx += 2;
                        }
                    }
                }
            }
        }

        GL.LineWidth(5);

        ObjectRenderer.DrawPrimitives(PrimitiveType.Lines, shader, arr, drawLength);

        GL.LineWidth(1);

        ArrayPool<Vector3>.Shared.Return(arr);
    }

    private void OnFontsLoaded(GameManager sender, EventArgs e)
    {
        InitControls();

        m_font = new Font("Arial", 16, GlobalCache<FontInformation>.Default.GetItemOrDefault("Arial")!);
    }

    private void OnShadersLoaded(GameManager sender, EventArgs e)
    {
        CollisionRenderer.Shader = GlobalCache<Shader>.Default.GetItemOrDefault("CollisionShader");
        TextRenderer.Shader = GlobalCache<Shader>.Default.GetItemOrDefault("FontShader")!;

        m_primitivesShader = GlobalCache<Shader>.Default.GetItemOrDefault("PrimitivesShader");
    }

    private void OnSkyBoxesLoaded(GameManager sender, EventArgs e)
    {
        m_world.SkyBox.Texture = GlobalCache<Texture>.Default.GetItemOrDefault("SkyBox2");
    }

    private void OnRenderPrimitives()
    {
        if (m_primitivesShader == null)
            return;

        m_primitivesShader.Use();

        m_primitivesShader.SetMatrix4("projection", EngineSettings.Current.Projection);
        m_primitivesShader.SetMatrix4("view", m_world.CurrentCamera.LookAt);
        m_primitivesShader.SetVector4("color", Colors.Red);

        DrawPositionsAndCentersOfMass(m_primitivesShader);

        if (m_drawNormals)
            DrawNormals(m_primitivesShader);
    }
}