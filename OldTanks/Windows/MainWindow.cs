using System.Drawing;
using GraphicalEngine.Core;
using GraphicalEngine.Core.Font;
using GraphicalEngine.Services;
using OldTanks.Controls;
using OldTanks.Models;
using OldTanks.Services;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GlobalSettings = OldTanks.Services.GlobalSettings;
using GEGlobalSettings = GraphicalEngine.Services.GlobalSettings;

namespace OldTanks.Windows;

public partial class MainWindow : GameWindow
{
    private readonly List<Control> m_controls;

    private World m_world;

    private double m_fps;
    private bool m_condition;

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
                Title = caption
            })
    {
    }

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        m_world = new World();
        m_controls = new List<Control>();

        GlobalSettings.UserSettings.Sensitivity = 0.1f;

        m_rotation = new Vector3(180, 90, 0);

        GenerateObjects();
    }

    private void GenerateObjects()
    {
        var rand = new Random();
        var objAmount = rand.Next(3, 5);

        var defCube = new Cube() { Size = new Vector3(1, 1, 1) };
        m_world.WorldObjects.Add(defCube);

        for (int i = 0; i < objAmount; i++)
        {
            var cube = new Cube()
            {
                Height = 1,
                Width = 1,
                Length = 1,
                Position = new Vector3(rand.Next(3, 5), rand.Next(1, 3), rand.Next(1, 7))
            };

            m_world.WorldObjects.Add(cube);
        }
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

        foreach (var textureFile in Directory.GetFiles(shaderDirPath))
        {
            GlobalCache<Texture>.AddOrUpdateItem(Path.GetFileNameWithoutExtension(textureFile),
                Texture.CreateTexture(textureFile));
        }
    }

    private void LoadFonts()
    {
        var fontsDirPath = Path.Combine(Environment.CurrentDirectory, @"Assets\Fonts");
        // var chars = new List<char>('~' - ' ');

        float[] characterVert =
        {
            //x     y         z        tX(u)        tY(v)
            1.0f, 1.0f, 0.0f, 1.0f, 1.0f, //top right
            1.0f, -1.0f, 0.0f, 1.0f, 0.0f, //bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, //bottom left
            -1.0f, 1.0f, 0.0f, 0.0f, 1.0f, //top left
        };

        uint[] characterVertIndices =
        {
            0, 1, 3,
            1, 2, 3
        };

        var scene = new Scene();
        scene.Meshes.Add(new Mesh(0, characterVert, characterVertIndices));

        var dynamicFontScene = new Scene();
        dynamicFontScene.Meshes.Add(new Mesh(1));

        GlobalCache<Scene>.AddOrUpdateItem("FontScene", scene);
        GlobalCache<Scene>.AddOrUpdateItem("DynamicFontScene", dynamicFontScene);

        // for (int i = ' '; i < '~'; i++)
        //     chars.Add((char)i);

        foreach (var fontPath in Directory.GetFiles(fontsDirPath))
        {
            var font = Font.RegisterFont(fontPath);

            if (!font)
                Console.WriteLine($"Error loading font: {Path.GetFileNameWithoutExtension(fontPath)}");
        }
    }

    protected override void OnLoad()
    {
        VSync = VSyncMode.On;
        var rand = new Random();

        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

        CursorVisible = false;
        CursorGrabbed = true;

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        try
        {
            LoadShaders();
            LoadTextures();
            LoadFonts();
            InitControls();

            DrawManager.RegisterScene(typeof(Cube),
                GlobalCache<Shader>.GetItemOrDefault("DefaultShader"));

            DrawManager.RegisterScene(typeof(string),
                GlobalCache<Shader>.GetItemOrDefault("FontShader"));

            var textures = new string[] { "Container", "Brick" };

            foreach (var worldObject in m_world.WorldObjects)
            {
                var texture = GlobalCache<Texture>.GetItemOrDefault(textures[rand.Next(0, 8) % 2]);
                Console.WriteLine(texture.Handle);

                foreach (var mesh in worldObject.Scene.Meshes)
                    mesh.Texture = texture;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.Camera.FOV),
            (float)Size.X / Size.Y, 0.1f, 1000.0f);

        base.OnLoad();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Size.X == 0 || Size.Y == 0)
            return;

        var aspect = (float)Size.X / Size.Y;
        m_world.Camera.FOV += -e.OffsetY;

        GEGlobalSettings.Projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(m_world.Camera.FOV),
            aspect >= 1 ? aspect : 1, 0.1f, 1000.0f);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        if (e.Key == Keys.B)
            m_condition = !m_condition;

        if (e.Alt == true && e.Key == Keys.Enter)
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Fullscreen;
            else
                WindowState = WindowState.Normal;

        base.OnKeyUp(e);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        m_fps = 1.0 / args.Time;

        GL.Enable(EnableCap.DepthTest);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.CullFace);

        DrawManager.DrawElements(m_world.WorldObjects, m_world.Camera, true);

        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        foreach (var control in m_controls)
            control.Draw();

        Context.SwapBuffers();

        base.OnRenderFrame(args);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        var speedMultiplier = 1.0f;
        var timeDelta = (float)args.Time;

        if (KeyboardState.IsKeyDown(Keys.LeftControl))
            speedMultiplier = 3f;

        if (KeyboardState.IsKeyDown(Keys.D))
            m_world.Camera.Position +=
                Vector3.Normalize(Vector3.Cross(m_world.Camera.Direction, m_world.Camera.CameraUp)) *
                m_world.Camera.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.A))
            m_world.Camera.Position -=
                Vector3.Normalize(Vector3.Cross(m_world.Camera.Direction, m_world.Camera.CameraUp)) *
                m_world.Camera.Speed * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.W))
            m_world.Camera.Position += m_world.Camera.Direction * m_world.Camera.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.S))
            m_world.Camera.Position -= m_world.Camera.Direction * m_world.Camera.Speed * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.Space))
            m_world.Camera.Position += m_world.Camera.CameraUp * m_world.Camera.Speed * timeDelta * speedMultiplier;
        else if (KeyboardState.IsKeyDown(Keys.LeftShift))
            m_world.Camera.Position -= m_world.Camera.CameraUp * m_world.Camera.Speed * timeDelta * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.Up))
            m_rotation.Y += 1;
        else if (KeyboardState.IsKeyDown(Keys.Down))
            m_rotation.Y -= 1;

        if (KeyboardState.IsKeyDown(Keys.Left))
            m_rotation.X += 1;
        else if (KeyboardState.IsKeyDown(Keys.Right))
            m_rotation.X -= 1;

        if (KeyboardState.IsKeyDown(Keys.KeyPad7))
            m_rotation.Z += 1;
        else if (KeyboardState.IsKeyDown(Keys.KeyPad8))
            m_rotation.Z -= 1;

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

            // Apply the camera pitch and yaw (we clamp the pitch in the camera class)
            m_world.Camera.Yaw += deltaX * GlobalSettings.UserSettings.Sensitivity;
            m_world.Camera.Pitch +=
                -deltaY * GlobalSettings.UserSettings
                    .Sensitivity; // Reversed since y-coordinates range from bottom to top
        }

        m_tbFPS.Text = Math.Round(m_fps, MidpointRounding.ToEven).ToString();
        m_tbCamRotation.Text = m_world.Camera.Direction.ToString();
        m_tbX.Text = m_world.Camera.Position.X.ToString();
        m_tbY.Text = m_world.Camera.Position.Y.ToString();
        m_tbZ.Text = m_world.Camera.Position.Z.ToString();

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
            aspect >= 1 ? aspect : 1, 0.1f, 1000.0f);
    }
}