﻿using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using Common.Extensions;
using Common.Services;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Models;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using CoolEngine.Services.Renderers;
using OldTanks.DataModels;
using OldTanks.Infrastructure;
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

    private readonly object m_locker = new object();

    private readonly ObjectRenderer<WorldObject> m_objectRenderer;

    private Shader? m_primitivesShader;

    private bool m_exit;
    private bool m_readyToHandle;

    private ImGuiController m_imGuiController;

    private Font m_font;

    private Thread m_interactionWorker;
    private Thread m_testThread;

    private Ray m_ray;
    private bool m_rayIntersected;
    private Vector3 m_rayIntersectionPoint;

    private World m_world;

    private double m_fps;

    private bool m_debugView;
    private bool m_drawNormals;
    private bool m_drawFaceNumber;
    private bool m_mouseDown;
    private bool m_freeCamMode;

    private IMovable m_currentObject;

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
        m_objectRenderer = new ObjectRenderer<WorldObject>();

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

        EngineSettings.Current.CollisionIterations = 5;

        m_rotation = new Vector3(0, 0, 0);

        m_interactionWorker = new Thread(WorldHandler) { IsBackground = true };
        m_testThread = new Thread(SpawnObjects) { IsBackground = true };

        m_controlHandler = new ControlHandler();
        m_imGuiMainWindow = new UI.ImGuiUI.MainWindow("DebugWindow", m_gameManager)
            { Title = "Debug window", IsVisible = true };
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

                ObjectRendererOld.RegisterScene(typeof(SkyBox),
                    GlobalCache<Shader>.Default.GetItemOrDefault("SkyBoxShader")!);

                m_currentObject = m_world.CurrentCamera;

                InitDefaultObjects();

                m_objectRenderer.Shader = GlobalCache<Shader>.Default.GetItemOrDefault("DefaultShader")!;
                m_objectRenderer.InstancedShader = GlobalCache<Shader>.Default.GetItemOrDefault("DefaultShader2")!;
                m_objectRenderer.DrawableItems = m_world.WorldObjects;

                ObjectRendererOld.AddDrawables(m_world.WorldObjects, m_objectRenderer.Shader);
                CollisionRenderer.AddCollisions(m_world.WorldObjects);

                m_readyToHandle = true;
                
                m_testThread.Start();
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
        if (e.Key == Keys.LeftControl && m_currentObject is IPhysicObject physObj)
            physObj.RigidBody.MaxSpeedMultiplier = 3;

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.I:
                m_world.Stop = false;
                break;
            case Keys.U:
                m_world.IsActive = !m_world.IsActive;
                break;
            case Keys.D0:
                m_debugView = !m_debugView;
                break;
            case Keys.D1:
                m_freeCamMode = m_world.Player == null || !m_freeCamMode;

                ChangeCameraMode();
                break;
            case Keys.LeftControl:
                if (m_currentObject is IPhysicObject physObj)
                    physObj.RigidBody.MaxSpeedMultiplier = 1;
                break;
            case Keys.F:
                EngineSettings.Current.PhysicsEnable = !EngineSettings.Current.PhysicsEnable;
                break;
            case Keys.N:
                m_drawNormals = !m_drawNormals;
                break;
            case Keys.R:
                m_rotation = new Vector3();
                m_currentObject.Position = Vector3.Zero;
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

        lock (m_locker)
        {
            m_fps = 1.0 / args.Time;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit |
                     ClearBufferMask.StencilBufferBit);

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GL.DepthFunc(DepthFunction.Lequal);
            ObjectRendererOld.DrawSkyBox(m_world.SkyBox, m_world.CurrentCamera);
            GL.DepthFunc(DepthFunction.Less);

            var projection = EngineSettings.Current.Projection;
            if (!m_debugView)
                m_objectRenderer.Render(m_world.CurrentCamera, ref projection);
            // ObjectRendererOld.DrawElements(m_world.CurrentCamera);
            else
                CollisionRenderer.DrawElementsCollision(m_world.CurrentCamera, m_font, drawVerticesPositions: true);

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
        }

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

        var cam = m_world.CurrentCamera;
        
        m_imGuiController.Update(this, (float)args.Time);

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbSubIterationAmount.Text = EngineSettings.Current.CollisionIterations.ToString();
        m_tbCamRotation.Text = m_currentObject.Rotation.ToString();
        m_tbPosition.Text = m_currentObject.Position.ToString();
        m_tbRotation.Text = m_rotation.ToString();

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

    private void SpawnObjects()
    {
        return;
        
        var objAmount = 10000;
        var rand = Random.Shared;
        var textures = new [] { "wall-texture", "Brick", "awesomeface", "Container", "FloorTile" };

        for (int i = 0; i < objAmount; i++)
        {
            var cube = new Cube
            {
                Size = new Vector3(1),
                Position = new Vector3(rand.Next(-75, 75), rand.Next(-75, 75), rand.Next(-75, 75)),
            };
            
            FillObject(cube, GlobalCache<Texture>.Default.GetItemOrDefault(textures[rand.Next(0, textures.Length)]));

            lock (m_locker)
            {
                m_world.WorldObjects.Add(cube);
            }
        }
    }

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
        var textureObjectInfo = new TexturedObjectInfo(drawable);
        
        foreach (var mesh in drawable.Scene.Meshes)
        {
            textureObjectInfo[mesh] = new TextureData { Texture = texture };
        }
        
        drawable.TexturedObjectInfos.Add(textureObjectInfo);
    }

    private void InitDefaultObjects()
    {
        var tempObjects = new List<WorldObject>();
        Sphere sphere = null;

        #region Static objects

        var wall = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10), Name = "Wall 1" };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName));
        tempObjects.Add(wall);
        
        FillObject(wall, GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture"));

        wall = new Cube
        {
            Size = new Vector3(1, 3, 20), 
            Position = new Vector3(9, 0, 0), 
            Rotation =new Vector3(),
            Name = "Wall 2"
        };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName));
        tempObjects.Add(wall);
        
        FillObject(wall, GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture"));

        var floor = new Cube { Size = new Vector3(20, 1, 20), Position = new Vector3(0, -2, 0), Name = "Floor 1" };
        floor.Collision = new Collision(floor, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName));
        tempObjects.Add(floor);
        
        FillObject(floor, GlobalCache<Texture>.Default.GetItemOrDefault("FloorTile"));

        sphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) };
        sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.SphereCollisionName));
        tempObjects.Add(sphere);
        
        FillObject(sphere, GlobalCache<Texture>.Default.GetItemOrDefault("Brick"));

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
            Size = new Vector3(1),
            Position = new Vector3(0, 2, 1.5f),
            Rotation = new Vector3(MathHelper.DegreesToRadians(45), 0, 0),
            Name = "Dynamic Cube 1"
        };
        dynamicCube.Collision =
            new Collision(dynamicCube, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName));
        tempObjects.Add(dynamicCube);

        sphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) };
        sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.SphereCollisionName));
        tempObjects.Add(sphere);

        foreach (var wObject in tempObjects)
        {
            wObject.RigidBody.MaxSpeed = 2;
            wObject.RigidBody.MaxBackSpeed = 2;
            wObject.RigidBody.MaxSpeedMultiplier = 1;
            wObject.RigidBody.Restitution = 0.4f;
            wObject.RigidBody.DefaultJumpForce = -250;

            FillObject(wObject, GlobalCache<Texture>.Default.GetItemOrDefault("wall-texture"));
        }

        m_world.WorldObjects.AddRange(tempObjects);

        #endregion

        m_world.CurrentCamera.FOV = 45;

        m_interactionWorker.Start();
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

            // if (KeyboardState.IsKeyDown(Keys.C))
            // {
            //     m_world.Player.Camera.Pitch = m_world.Player.Pitch;
            //
            //     m_world.Player.CameraOffset = new Vector3(-1, 1, 0);
            // }
        }
    }

    private void WorldHandler()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var dict = new Dictionary<double, string>();
        
        while (!m_exit)
        {
            try
            {
                while (!m_exit)
                {
                    Thread.Sleep(5);

                    var elapsedTime = (float)stopWatch.Elapsed.TotalSeconds;
                    var elapsedTime2 = (float)stopWatch.Elapsed.TotalSeconds;
                    //var elapsedTime2 = (float)0.001;
                    stopWatch.Restart();

                    HandleKeyboardInputs(elapsedTime);

                    //Collisions per second
                    var cps = Math.Round(1 / elapsedTime, MidpointRounding.ToEven);
                    var cpsString = "1000";
                    
                    if (cps < 1000 && !dict.TryGetValue(cps, out cpsString))
                    {
                        cpsString = cps.ToString(CultureInfo.InvariantCulture);
                        dict.Add(cps, cpsString);
                    }
                    
                    m_tbCollidingPS.Text = cpsString;

                    if (m_freeCamMode)
                    {
                        var cam = m_world.CurrentCamera;
                        cam.Move(elapsedTime, KeyboardState);
                        m_ray = new Ray(new Vector3(cam.Position.X, cam.Position.Y - 1, cam.Position.Z), cam.Position + cam.Rotation * 100);
                    }

                    HandleObjectMove(elapsedTime2);

                    HandleMouseMove();

                    // m_collisionCalculation = true;

                    m_world.CollideObjects(elapsedTime2);

                    var previousLength = float.MaxValue;
                    var tmpIntersectedPoint = Vector3.Zero;
                    m_rayIntersected = false;
                    for (var i = 0; i < m_world.WorldObjects.Count; i++)
                    {
                        var worldObject = m_world.WorldObjects[i];

                        if (worldObject.Collision == null)
                            continue;
                        
                        var isRayIntersected = worldObject.Collision.IntersectRay(m_ray, out tmpIntersectedPoint);

                        var lengthToRayStart = Math.Abs((tmpIntersectedPoint - m_ray.Start).Length);
                        if (isRayIntersected && previousLength > lengthToRayStart)
                        {
                            m_rayIntersected = true;
                            previousLength = lengthToRayStart;
                            m_rayIntersectionPoint = tmpIntersectedPoint;
                        }
                    }

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
        const int leftMargin = 10;
        const int rightMargin = 27;
        const int topMargin = 20;
        const int bottomMargin = 50;
        const int defaultDeltaX = 3;
        const int defaultDeltaY = 2;
        
        if (m_firstMouseMove)
        {
            m_lastMousePos = MousePosition;
            m_firstMouseMove = false;
        }
        else
        {
            var posX = Math.Min(Math.Max(MousePosition.X, leftMargin), Size.X - rightMargin);
            var posY = Math.Min(Math.Max(MousePosition.Y, topMargin), Size.Y - bottomMargin);
            
            var deltaX = 0f;
            var deltaY = 0f;

            if (posX == leftMargin)
                deltaX = -defaultDeltaX;
            else if (posX == Size.X - rightMargin)
                deltaX = defaultDeltaX;
            else 
                deltaX = MousePosition.X - m_lastMousePos.X;
            
            if (posY == topMargin)
                deltaY = -defaultDeltaY;
            else if (posY == Size.Y - bottomMargin)
                deltaY = defaultDeltaY;
            else
                deltaY = MousePosition.Y - m_lastMousePos.Y;
            
            m_lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);

            if (m_mouseDown)
            {
                var sensitivity = m_userSettings.Sensitivity;
                
                m_world.CurrentCamera.Rotate(deltaX * sensitivity, deltaY * sensitivity);
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

        ObjectRendererOld.DrawPrimitives(PrimitiveType.Points, shader, arr, drawLength);

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
                var rotation = Matrix3.CreateRotationX(MathHelper.DegreesToRadians(worldObj.Rotation.X)) *
                               Matrix3.CreateRotationY(MathHelper.DegreesToRadians(-worldObj.Rotation.Y)) *
                               Matrix3.CreateRotationZ(MathHelper.DegreesToRadians(worldObj.Rotation.Z));

                for (int meshIdx = 0; meshIdx < worldObj.Scene.Meshes.Length; meshIdx++)
                {
                    var mesh = worldObj.Scene.Meshes[meshIdx];

                    for (int k = 0; k < mesh.Faces.Length; k++)
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
        
        ObjectRendererOld.DrawPrimitives(PrimitiveType.Lines, shader, arr, drawLength);
        
        ArrayPool<Vector3>.Shared.Return(arr);
    }

    private void DrawRay(Shader shader)
    {
        var arr = ArrayPool<Vector3>.Shared.Rent(2);

        arr[0] = m_ray.Start;
        arr[1] = m_rayIntersected ? m_rayIntersectionPoint : m_ray.End;
        
        ObjectRendererOld.DrawPrimitives(PrimitiveType.Lines, shader, arr, 2);

        arr[0] = arr[1];
        
        GL.PointSize(30);
        
        ObjectRendererOld.DrawPrimitives(PrimitiveType.Points, shader, arr, 1);
        
        GL.PointSize(1);
        
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

        // DrawPositionsAndCentersOfMass(m_primitivesShader);

        if (m_drawNormals)
            DrawNormals(m_primitivesShader);

        DrawRay(m_primitivesShader);
    }
}