using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Renderers;
using OpenTK.Mathematics;

namespace CoolEngine.Tests.Services.Renderers;

[TestFixture]
public sealed class ObjectRendererTests
{
    #region Filing with an instanced shader

    [Test]
    public void CollectionAdd_SeventeenthWithInstancedShader_MovesGroupToInstanced()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 17);
        var renderer = new ObjectRenderer<FakeDrawable> { InstancedShader = CreateShaderWithoutGl() };
        var items = new ObservableCollection<FakeDrawable>(drawables.Take(16));
        renderer.DrawableItems = items;

        items.Add(drawables[16]);

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.Null);
        });
    }

    [Test]
    public void CollectionAdd_SixteenthWithInstancedShader_KeepsGroupPerInstance()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 16);
        var renderer = new ObjectRenderer<FakeDrawable> { InstancedShader = CreateShaderWithoutGl() };
        var items = new ObservableCollection<FakeDrawable>(drawables.Take(15));
        renderer.DrawableItems = items;

        items.Add(drawables[15]);

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.Null);
        });
    }

    #endregion

    #region Texture changes

    [Test]
    public void TextureChanged_PerInstanceDrawable_MovesItBetweenPerInstanceGroups()
    {
        var scene = CreateScene();
        var mesh = scene.Meshes[0];
        var tex7 = CreateTexture(7);
        var drawables = CreateDrawables(scene, Texture.Empty, 2);
        var renderer = new ObjectRenderer<FakeDrawable> { InstancedShader = CreateShaderWithoutGl() };
        renderer.DrawableItems = new ObservableCollection<FakeDrawable>(drawables);

        drawables[0].TexturedObjectInfos[0].ChangeTexture(mesh, tex7);

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EqualTo(new[] { drawables[1] }));
            Assert.That(renderer.GetPerInstanceGroup(scene, tex7), Is.EqualTo(new[] { drawables[0] }));
        });
    }

    #endregion

    #region Helpers

    private static Scene CreateScene()
    {
        return new Scene(new[] { new Mesh(new[] { Vector3.Zero, Vector3.One }, Array.Empty<Face>()) });
    }

    private static FakeDrawable CreateDrawable(Scene scene, Texture texture, Matrix4? transformation = null)
    {
        var drawable = new FakeDrawable
        {
            Scene = scene,
            Transformation = transformation ?? Matrix4.Identity
        };

        var texturedObjectInfo = new TexturedObjectInfo(drawable);
        foreach (var mesh in scene.Meshes)
            texturedObjectInfo.TexturedMeshes[mesh] = new TextureData { Texture = texture };

        drawable.TexturedObjectInfos.Add(texturedObjectInfo);

        return drawable;
    }

    private static List<FakeDrawable> CreateDrawables(Scene scene, Texture texture, int count)
    {
        var drawables = new List<FakeDrawable>(count);
        for (var i = 0; i < count; i++)
            drawables.Add(CreateDrawable(scene, texture));

        return drawables;
    }

    // Texture's constructor is private, and its finalizer calls GL.DeleteTexture (KI-017): no GL context here.
    private static Texture CreateTexture(int handle)
    {
        var constructor = typeof(Texture).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
            new[] { typeof(int), typeof(float), typeof(float) })!;
        var texture = (Texture)constructor.Invoke(new object[] { handle, 1f, 1f });
        GC.SuppressFinalize(texture);

        return texture;
    }

    // Shader's constructor and finalizer call OpenGL (KI-017); the renderer only stores the instanced shader while filing.
    private static Shader CreateShaderWithoutGl()
    {
        var shader = (Shader)RuntimeHelpers.GetUninitializedObject(typeof(Shader));
        GC.SuppressFinalize(shader);

        return shader;
    }

    private sealed class FakeDrawable : IDrawable
    {
        public required Scene Scene { get; set; }

        public ObservableCollection<TexturedObjectInfo> TexturedObjectInfos { get; } = new();

        public bool Visible { get; set; } = true;

        public string Name { get; set; } = string.Empty;

        public bool NeedTransformationApply => false;

        public Matrix4 Transformation { get; set; }

        public void ApplyTransformation()
        {
        }
    }

    #endregion
}
