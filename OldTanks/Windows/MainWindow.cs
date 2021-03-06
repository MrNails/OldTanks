using System.Buffers;
using System.Diagnostics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Loaders;
using CoolEngine.Services.Misc;
using CoolEngine.Services.Renderers;
using ImGuiNET;
using OldTanks.Controls;
using OldTanks.Models;
using OldTanks.Services.ImGUI;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GlobalSettings = OldTanks.Services.GlobalSettings;
using GEGlobalSettings = CoolEngine.Services.GlobalSettings;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;

namespace OldTanks.Windows;

public partial class MainWindow : GameWindow
{
    private readonly List<Control> m_controls;
    private readonly List<Vector3> m_objSLots;

    private readonly int m_renderRadius;

    private bool m_exit;
    private bool m_haveCollision;

    private ImGuiController m_imGuiController;

    private readonly Thread m_generateObjectThread;

    private Font m_font;

    private Thread m_interactionWorker;

    private World m_world;

    private double m_fps;

    private int m_cubeCount;

    private bool m_debugView;
    private bool m_drawNormals;
    private bool m_drawFaceNumber;
    private bool m_mouseDown;
    private bool m_freeCamMode;
    private bool m_activeCamPhysics;
    private bool m_collisionCalculation;
    private bool m_renderingScene;

    private System.Numerics.Vector3 m_force;

    private IPhysicObject m_currentObject;
    private int m_selectedWorldObjectIndex;

    private Vector3 m_posDelta;

    private Vector2 m_lastMousePos;
    private bool m_firstMouseMove;

    private Vector3 m_rotation;

    private float m_camHeightDegree;

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

        m_freeCamMode = true;

        GlobalSettings.UserSettings.Sensitivity = 0.1f;

        GEGlobalSettings.CollisionIterations = 20;

        m_objSLots = new List<Vector3>(20);

        m_generateObjectThread = new Thread(GenerateObjects);

        m_rotation = new Vector3(0, 0, 0);

        m_selectedWorldObjectIndex = -1;

        m_interactionWorker = new Thread(WorldHandler) { IsBackground = true };
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
        var size = System.Numerics.Vector3.Zero;
        var velocity = System.Numerics.Vector3.Zero;
        var centerOfMass = System.Numerics.Vector3.Zero;
        var cameraOffset = System.Numerics.Vector3.Zero;
        var cameraOffsetAngle = System.Numerics.Vector2.Zero;
        var jumpForce = 0f;
        var maxSpeed = 0f;
        var maxBackSpeed = 0f;
        var speedMultiplier = 0f;
        var weight = 0f;
        var isStatic = false;

        WorldObject? lastObject =
            m_selectedWorldObjectIndex == -1 ? null : m_world.WorldObjects[m_selectedWorldObjectIndex];

        ImGui.Begin("Debug");
        var spawnCube = ImGui.Button("Spawn cube");

        ImGui.NewLine();
        ImGui.DragFloat3("Push force", ref m_force);
        ImGui.NewLine();

        if (spawnCube)
        {
            var cube = new Cube() { Name = $"Cube {m_cubeCount++}" };
            cube.Collision = new Collision(cube, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));

            foreach (var mesh in cube.Scene.Meshes)
                mesh.TextureData.Texture = GlobalCache<Texture>.GetItemOrDefault("wall-texture");

            ObjectRenderer.AddDrawable(cube, GlobalCache<Shader>.GetItemOrDefault("DefaultShader"));

            m_world.WorldObjects.Add(cube);
        }

        position = VectorExtensions.GLToSystemVector3(m_world.CurrentCamera.Position);
        rotation.X = m_world.CurrentCamera.Yaw;
        rotation.Y = m_world.CurrentCamera.Pitch;
        rotation.Z = m_world.CurrentCamera.Roll;
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

        m_freeCamMode = m_world.Player == null || m_freeCamMode;

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

        ImGui.NextColumn();

        ImGui.Text("Selected world object data");

        position = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.Position);
        rotation = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.Direction);
        size = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.Size);
        velocity = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.RigidBody.Velocity);
        centerOfMass = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.RigidBody.CenterOfMass);
        cameraOffset = selectedWorldObject == null
            ? System.Numerics.Vector3.Zero
            : VectorExtensions.GLToSystemVector3(selectedWorldObject.CameraOffset);
        cameraOffsetAngle = selectedWorldObject == null
            ? System.Numerics.Vector2.Zero
            : VectorExtensions.GLToSystemVector2(selectedWorldObject.CameraOffsetAngle);

        maxSpeed = selectedWorldObject?.RigidBody.MaxSpeed ?? 0;
        maxBackSpeed = selectedWorldObject?.RigidBody.MaxBackSpeed ?? 0;
        speedMultiplier = selectedWorldObject?.RigidBody.MaxSpeedMultiplier ?? 0;
        weight = selectedWorldObject?.RigidBody.Weight ?? 0;
        jumpForce = selectedWorldObject?.RigidBody.DefaultJumpForce ?? 0;
        isStatic = selectedWorldObject?.RigidBody.IsStatic ?? false;

        ImGui.DragFloat3("Position", ref position);
        ImGui.DragFloat3("Rotation", ref rotation);
        ImGui.DragFloat3("Size", ref size);
        ImGui.DragFloat3("Camera offset", ref cameraOffset);
        ImGui.DragFloat2("Camera offset angle", ref cameraOffsetAngle);

        ImGui.NewLine();
        var rBodyOpened = ImGui.TreeNodeEx("Rigid body");

        if (rBodyOpened)
        {
            ImGui.Checkbox("IsStatic", ref isStatic);
            ImGui.DragFloat3("Center of mass", ref centerOfMass);
            ImGui.DragFloat3("Velocity", ref velocity);
            ImGui.DragFloat("Jump force", ref jumpForce);
            ImGui.DragFloat("Max speed", ref maxSpeed);
            ImGui.DragFloat("Max back speed", ref maxBackSpeed);
            ImGui.DragFloat("Speed multiplier", ref speedMultiplier);
            ImGui.DragFloat("Weight", ref weight);

            ImGui.TreePop();
        }

        if (selectedWorldObject != null)
        {
            selectedWorldObject.CameraOffset = VectorExtensions.SystemToGLVector3(cameraOffset);
            selectedWorldObject.CameraOffsetAngle = VectorExtensions.SystemToGLVector2(cameraOffsetAngle);
            
            selectedWorldObject.X = position.X;
            selectedWorldObject.Y = position.Y;
            selectedWorldObject.Z = position.Z;

            selectedWorldObject.Yaw = rotation.X;
            selectedWorldObject.Pitch = rotation.Y;
            selectedWorldObject.Roll = rotation.Z;

            selectedWorldObject.Size = VectorExtensions.SystemToGLVector3(size);

            selectedWorldObject.RigidBody.MaxSpeed = maxSpeed;
            selectedWorldObject.RigidBody.MaxBackSpeed = maxBackSpeed;
            selectedWorldObject.RigidBody.Velocity = VectorExtensions.SystemToGLVector3(velocity);
            selectedWorldObject.RigidBody.CenterOfMass = VectorExtensions.SystemToGLVector3(centerOfMass);
            selectedWorldObject.RigidBody.MaxSpeedMultiplier = speedMultiplier;
            selectedWorldObject.RigidBody.Weight = weight;
            selectedWorldObject.RigidBody.DefaultJumpForce = jumpForce;
            selectedWorldObject.RigidBody.IsStatic = isStatic;
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

        GEGlobalSettings.GlobalLock.ExitWriteLock();
    }

    #endregion

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
        var textures = GlobalCache<List<string>>.GetItemOrDefault("Textures");

        m_world.SkyBox.Texture = GlobalCache<Texture>.GetItemOrDefault("SkyBox2");

        var tempObjects = new List<WorldObject>();
        Sphere sphere = null;

        #region Static objects

        var wall = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10) };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(wall);

        FillObject(wall, GlobalCache<Texture>.GetItemOrDefault("wall-texture"));

        wall = new Cube
            { Size = new Vector3(20, 5, 0.1f), Position = new Vector3(9.95f, 1, 0), Direction = new Vector3(0, 90, 0) };
        wall.Collision = new Collision(wall, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(wall);

        FillObject(wall, GlobalCache<Texture>.GetItemOrDefault("wall-texture"));

        var floor = new Cube { Size = new Vector3(20, 1, 20), Position = new Vector3(0, -2, 0) };
        floor.Collision = new Collision(floor, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(floor);

        FillObject(floor, GlobalCache<Texture>.GetItemOrDefault("FloorTile"));

        sphere = new Sphere { Size = new Vector3(2, 2, 2), Position = new Vector3(0, 0, -1) };
        sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        tempObjects.Add(sphere);

        FillObject(sphere, GlobalCache<Texture>.GetItemOrDefault("Brick"));

        foreach (var wObject in tempObjects)
        {
            var rBody = new RigidBody();
            rBody.IsStatic = true;
            rBody.Restitution = 0.5f;

            wObject.RigidBody = rBody;
        }

        m_world.WorldObjects.AddRange(tempObjects);
        tempObjects.Clear();

        #endregion

        #region Dynamic objects

        var dynamicCube = new Cube
        {
            Size = new Vector3(5, 1, 10),
            Position = new Vector3(0, 5, 0),
            Name = $"Cube {m_cubeCount++}"
        };
        dynamicCube.Collision =
            new Collision(dynamicCube, GlobalCache<CollisionData>.GetItemOrDefault("CubeCollision"));
        tempObjects.Add(dynamicCube);

        // sphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(2, 0, -1) };
        // sphere.Collision = new Collision(sphere, GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));
        // tempObjects.Add(sphere);

        foreach (var wObject in tempObjects)
        {
            var cubeRBody = new RigidBody();

            cubeRBody.MaxSpeed = 2;
            cubeRBody.MaxBackSpeed = 2;
            cubeRBody.MaxSpeedMultiplier = 1;
            cubeRBody.Restitution = 0.4f;
            cubeRBody.DefaultJumpForce = -250;

            wObject.RigidBody = cubeRBody;

            foreach (var mesh in wObject.Scene.Meshes)
                mesh.TextureData.Texture =
                    GlobalCache<Texture>.GetItemOrDefault(textures[Random.Shared.Next(0, textures.Count)]);
        }

        m_world.WorldObjects.AddRange(tempObjects);

        #endregion

        m_world.CurrentCamera.FOV = 45;
        m_world.CurrentCamera.Size = new Vector3(1);
        m_world.CurrentCamera.Collision = new Collision(m_world.CurrentCamera,
            GlobalCache<CollisionData>.GetItemOrDefault("SphereCollision"));

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
                rigidBody.Force += GlobalSettings.MovementDirectionUnit * 15;
            else if (KeyboardState.IsKeyDown(Keys.S))
                rigidBody.Force -= GlobalSettings.MovementDirectionUnit * 15;

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
                m_world.CurrentCamera.Yaw += deltaX * GlobalSettings.UserSettings.Sensitivity;
                m_world.CurrentCamera.Pitch +=
                    -deltaY * GlobalSettings.UserSettings
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
                            arr[arrIdx] = worldObj.Position + mesh.Normals[mesh.Faces[k].NormalsIndices[l]] * scale * rotation;
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

        m_font = new Font("Arial", 16);

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
                m_freeCamMode = m_world.Player == null || !m_freeCamMode;

                ChangeCameraMode();
                break;
            case Keys.LeftControl:
                m_currentObject.RigidBody.MaxSpeedMultiplier = 1;
                break;
            case Keys.F:
                GEGlobalSettings.PhysicsEnable = !GEGlobalSettings.PhysicsEnable;
                break;
            case Keys.T:
                if (m_world.Player != null)
                    m_world.Player.RigidBody.Force += VectorExtensions.SystemToGLVector3(m_force);
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

        m_renderingScene = true;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.DepthTest);

        GL.Enable(EnableCap.CullFace);

        GL.DepthFunc(DepthFunction.Lequal);
        ObjectRenderer.DrawSkyBox(m_world.SkyBox, m_world.CurrentCamera);
        GL.DepthFunc(DepthFunction.Less);

        if (!m_debugView)
            ObjectRenderer.DrawElements(m_world.CurrentCamera);
        else
            CollisionRenderer.DrawElementsCollision(m_world.CurrentCamera, drawVerticesPositions: false);

        var primitivesShader = GlobalCache<Shader>.GetItemOrDefault("PrimitivesShader");

        if (primitivesShader != null)
        {
            primitivesShader.Use();
            
            primitivesShader.SetMatrix4("projection", GEGlobalSettings.Projection);
            primitivesShader.SetMatrix4("view", m_world.CurrentCamera.LookAt);
            primitivesShader.SetVector4("color", Colors.Red);

            DrawPositionsAndCentersOfMass(primitivesShader);

            if (m_drawNormals)
                DrawNormals(primitivesShader);
        }
        
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        foreach (var control in m_controls)
            control.Draw();

        foreach (var worldObject in m_world.WorldObjects)
            if (!string.IsNullOrEmpty(worldObject.Name))
                TextRenderer.DrawText3D(m_font, worldObject.Name, m_world.CurrentCamera,
                    new TextDrawInformation
                    {
                        Color = Colors.Red,
                        OriginPosition = worldObject.Position,
                        SelfPosition = new Vector3(0, worldObject.Size.Y / 2, 0),
                        Scale = 0.05f,
                    }, true);

        GEGlobalSettings.GlobalLock.EnterWriteLock();

        HandleImGUI();

        GEGlobalSettings.GlobalLock.ExitWriteLock();

        m_imGuiController.Render();

        m_renderingScene = false;

        Context.SwapBuffers();

        base.OnRenderFrame(args);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        m_imGuiController.Update(this, (float)args.Time);

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbSubIterationAmount.Text = GEGlobalSettings.CollisionIterations.ToString();
        m_tbCamRotation.Text = m_currentObject.Direction.ToString();
        m_tbPosition.Text = m_currentObject.Position.ToString();
        m_tbRotation.Text = m_rotation.ToString();
        m_tbHaveCollision.Text = m_haveCollision.ToString();
        m_tbCurrentSpeed.Text = m_currentObject.RigidBody.Velocity.ToString();

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