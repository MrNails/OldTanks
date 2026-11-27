using System.Collections.ObjectModel;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Mathematics;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Renderers;
using CoolEngine.Tests.TestSupport;

namespace CoolEngine.Tests.Services.Renderers;

[TestFixture]
public sealed class ObjectRendererTests
{
    private const float Tolerance = 1e-6f;

    private static readonly Vector4 s_emptyTextureColor = new Vector4(0xAA / 255f, 0x55 / 255f, 0x6F / 255f, 1f);

    #region FillInstanceRecords

    [Test]
    public void FillInstanceRecords_MiddleDrawableHidden_PacksVisibleRecordsContiguously()
    {
        var scene = CreateScene();
        var drawables = CreateTranslatedDrawables(scene);
        drawables[1].Visible = false;
        var records = new ObjectRenderer<FakeDrawable>.ModelData[3];

        var count = ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), false, records);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(records[0].Transformation, Is.EqualTo(Matrix4.CreateTranslation(1, 0, 0)));
            Assert.That(records[1].Transformation, Is.EqualTo(Matrix4.CreateTranslation(3, 0, 0)));
        });
    }

    [Test]
    public void FillInstanceRecords_FirstDrawableHidden_PacksRemainingRecordsFromIndexZero()
    {
        var scene = CreateScene();
        var drawables = CreateTranslatedDrawables(scene);
        drawables[0].Visible = false;
        var records = new ObjectRenderer<FakeDrawable>.ModelData[3];

        var count = ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), false, records);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(records[0].Transformation, Is.EqualTo(Matrix4.CreateTranslation(2, 0, 0)));
            Assert.That(records[1].Transformation, Is.EqualTo(Matrix4.CreateTranslation(3, 0, 0)));
        });
    }

    [Test]
    public void FillInstanceRecords_AllVisible_PacksEveryRecordInOrder()
    {
        var scene = CreateScene();
        var drawables = CreateTranslatedDrawables(scene);
        var records = new ObjectRenderer<FakeDrawable>.ModelData[3];

        var count = ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), false, records);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(3));
            Assert.That(records[0].Transformation, Is.EqualTo(Matrix4.CreateTranslation(1, 0, 0)));
            Assert.That(records[1].Transformation, Is.EqualTo(Matrix4.CreateTranslation(2, 0, 0)));
            Assert.That(records[2].Transformation, Is.EqualTo(Matrix4.CreateTranslation(3, 0, 0)));
        });
    }

    [Test]
    public void FillInstanceRecords_AllHidden_ReturnsZero()
    {
        var scene = CreateScene();
        var drawables = CreateTranslatedDrawables(scene);
        foreach (var drawable in drawables)
            drawable.Visible = false;
        var records = new ObjectRenderer<FakeDrawable>.ModelData[3];

        var count = ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), false, records);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void FillInstanceRecords_UntexturedDrawable_UsesEmptyTextureColorInZeroToOneRange()
    {
        var scene = CreateScene();
        var drawables = new List<FakeDrawable> { CreateDrawable(scene, Texture.Empty) };
        var records = new ObjectRenderer<FakeDrawable>.ModelData[1];

        ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), true, records);

        AssertEmptyTextureRecord(records[0]);
    }

    [Test]
    public void FillInstanceRecords_TexturedDrawableOnMeshWithoutTextureCoords_UsesEmptyTextureColor()
    {
        var scene = CreateScene();
        var drawables = new List<FakeDrawable> { CreateDrawable(scene, CreateTexture(7)) };
        var records = new ObjectRenderer<FakeDrawable>.ModelData[1];

        ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), false, records);

        AssertEmptyTextureRecord(records[0]);
    }

    [Test]
    public void FillInstanceRecords_TexturedDrawableOnMeshWithTextureCoords_UsesWhite()
    {
        var scene = CreateScene();
        var drawables = new List<FakeDrawable> { CreateDrawable(scene, CreateTexture(7)) };
        var records = new ObjectRenderer<FakeDrawable>.ModelData[1];

        ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables),
            CreateTextureDatas(scene, drawables), true, records);

        Assert.Multiple(() =>
        {
            Assert.That(records[0].Color, Is.EqualTo(Colors.White));
            Assert.That(records[0].HaveTexture, Is.EqualTo(1f).Within(Tolerance));
        });
    }

    #endregion

    #region Golden values

    [Test]
    public void ModelData_TextureTransform_MatchesGolden()
    {
        var scene = CreateScene();
        var transformation = Matrix4.CreateTranslation(5f, -6f, 7f) *
                             (Matrix4.CreateRotationZ(1.1f) *
                              (Matrix4.CreateRotationY(-0.3f) *
                               (Matrix4.CreateRotationX(0.5f) * Matrix4.CreateScale(2f, 3f, 4f))));
        var drawables = new List<FakeDrawable> { CreateDrawable(scene, CreateTexture(7), transformation) };
        var textureDatas = CreateTextureDatas(scene, drawables);
        textureDatas[drawables[0]].Scale = new Vector2(2f, 0.5f);
        textureDatas[drawables[0]].RotationAngle = 30f;
        var records = new ObjectRenderer<FakeDrawable>.ModelData[1];

        ObjectRenderer<FakeDrawable>.FillInstanceRecords(AsDrawableList(drawables), textureDatas, true, records);

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        // The record as the GPU receives it: transformation (16), color (4), texture transformation (4), haveTexture.
        float[] expected =
        [
            0.8666738f, 1.7028059f, 0.59104043f, 0f,
            -2.5391207f, 0.81540537f, 1.3740382f, 0f,
            1.2385218f, -1.7943771f, 3.3535466f, 0f,
            5f, -6f, 7f, 1f,
            1f, 1f, 1f, 1f,
            1.7320508f, 1f, -0.25f, 0.4330127f,
            1f
        ];

        GoldenAssert.AreClose(expected, MemoryMarshal.Cast<ObjectRenderer<FakeDrawable>.ModelData, float>(records).ToArray(),
            "ModelData");
    }

    #endregion

    #region Filing without an instanced shader

    [Test]
    public void DrawableItems_WithoutInstancedShader_FilesDrawablesPerInstance()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 3);
        var renderer = new ObjectRenderer<FakeDrawable>();

        renderer.DrawableItems = new ObservableCollection<FakeDrawable>(drawables);

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.Null);
        });
    }

    [Test]
    public void CollectionAdd_WithoutInstancedShader_FilesDrawablePerInstance()
    {
        var scene = CreateScene();
        var drawable = CreateDrawable(scene, Texture.Empty);
        var renderer = new ObjectRenderer<FakeDrawable>();
        var items = new ObservableCollection<FakeDrawable>();
        renderer.DrawableItems = items;

        items.Add(drawable);

        Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EquivalentTo(new[] { drawable }));
    }

    [Test]
    public void CollectionAdd_SeventeenthWithoutInstancedShader_KeepsGroupPerInstance()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 17);
        var renderer = new ObjectRenderer<FakeDrawable>();
        var items = new ObservableCollection<FakeDrawable>(drawables.Take(16));
        renderer.DrawableItems = items;

        items.Add(drawables[16]);

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.Null);
        });
    }

    [Test]
    public void InstancedShader_SetAfterSixteenDrawables_MovesGroupToInstanced()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 16);
        var renderer = new ObjectRenderer<FakeDrawable>();
        renderer.DrawableItems = new ObservableCollection<FakeDrawable>(drawables);

        renderer.InstancedShader = CreateShaderWithoutGl();

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.Null);
        });
    }

    [Test]
    public void InstancedShader_SetAfterFifteenDrawables_KeepsGroupPerInstance()
    {
        var scene = CreateScene();
        var drawables = CreateDrawables(scene, Texture.Empty, 15);
        var renderer = new ObjectRenderer<FakeDrawable>();
        renderer.DrawableItems = new ObservableCollection<FakeDrawable>(drawables);

        renderer.InstancedShader = CreateShaderWithoutGl();

        Assert.Multiple(() =>
        {
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.EquivalentTo(drawables));
            Assert.That(renderer.GetInstancedGroup(scene, Texture.Empty), Is.Null);
        });
    }

    #endregion

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
    public void TextureChanged_InstancedDrawableWhileSceneHasPerInstanceGroups_LeavesOldInstancedGroup()
    {
        var scene = CreateScene();
        var mesh = scene.Meshes[0];
        var tex7 = CreateTexture(7);
        var renderer = new ObjectRenderer<FakeDrawable> { InstancedShader = CreateShaderWithoutGl() };
        var items = new ObservableCollection<FakeDrawable>();
        renderer.DrawableItems = items;
        var texturedDrawable = CreateDrawable(scene, tex7);
        items.Add(texturedDrawable);
        var untexturedDrawables = CreateDrawables(scene, Texture.Empty, 17);
        foreach (var drawable in untexturedDrawables)
            items.Add(drawable);
        var changed = untexturedDrawables[0];

        changed.TexturedObjectInfos[0].ChangeTexture(mesh, tex7);

        var oldInstancedGroup = renderer.GetInstancedGroup(scene, Texture.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(oldInstancedGroup, Has.Count.EqualTo(16));
            Assert.That(oldInstancedGroup, Does.Not.Contain(changed));
            Assert.That(renderer.GetInstancedGroup(scene, tex7), Does.Contain(changed));
            Assert.That(renderer.GetPerInstanceGroup(scene, tex7), Is.EqualTo(new[] { texturedDrawable }));
            Assert.That(renderer.GetPerInstanceGroup(scene, Texture.Empty), Is.Null);
        });
    }

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

    private static void AssertEmptyTextureRecord(ObjectRenderer<FakeDrawable>.ModelData record)
    {
        Assert.Multiple(() =>
        {
            Assert.That(record.Color.X, Is.EqualTo(s_emptyTextureColor.X).Within(Tolerance));
            Assert.That(record.Color.Y, Is.EqualTo(s_emptyTextureColor.Y).Within(Tolerance));
            Assert.That(record.Color.Z, Is.EqualTo(s_emptyTextureColor.Z).Within(Tolerance));
            Assert.That(record.Color.W, Is.EqualTo(s_emptyTextureColor.W).Within(Tolerance));
            Assert.That(record.HaveTexture, Is.EqualTo(0f).Within(Tolerance));
        });
    }

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

    private static List<FakeDrawable> CreateTranslatedDrawables(Scene scene)
    {
        return new List<FakeDrawable>
        {
            CreateDrawable(scene, Texture.Empty, Matrix4.CreateTranslation(1, 0, 0)),
            CreateDrawable(scene, Texture.Empty, Matrix4.CreateTranslation(2, 0, 0)),
            CreateDrawable(scene, Texture.Empty, Matrix4.CreateTranslation(3, 0, 0))
        };
    }

    private static List<IDrawable> AsDrawableList(List<FakeDrawable> drawables)
    {
        return new List<IDrawable>(drawables);
    }

    private static Dictionary<IDrawable, TextureData> CreateTextureDatas(Scene scene, List<FakeDrawable> drawables)
    {
        var mesh = scene.Meshes[0];
        var textureDatas = new Dictionary<IDrawable, TextureData>();
        foreach (var drawable in drawables)
            textureDatas.Add(drawable, drawable.TexturedObjectInfos[0][mesh]);

        return textureDatas;
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
