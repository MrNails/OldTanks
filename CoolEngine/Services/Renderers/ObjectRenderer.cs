using System.Buffers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Common.Models;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public sealed class ObjectRenderer<T> : ObservableObject, IRenderer<T>
    where T: IDrawable
{
    public static readonly int DEFAULT_MAX_INSTANCED_ELEMENTS = 32;
    public static readonly Vector4 DEFAULT_EMPTY_TEXTURE_COLOR = new Vector4(0xAA, 0x55, 0x6F, 1);
    public static readonly int MAX_PER_INSTANCE_ELEMENTS = 5;
    
    private readonly Dictionary<Scene, DrawSceneInfo> m_sceneBuffers;
    private readonly Dictionary<Scene, DrawSceneInfo> m_instancedSceneBuffers;

    private bool m_isActive;

    private Shader? m_shader;
    private Shader? m_instancedShader;
    private ObservableCollection<T>? m_drawableItems;

    public ObjectRenderer()
    {
        m_sceneBuffers = new Dictionary<Scene, DrawSceneInfo>();
        m_instancedSceneBuffers = new Dictionary<Scene, DrawSceneInfo>();
        IsActive = true;
    }
    
    public bool IsActive
    {
        get => m_isActive;
        set => SetField(ref m_isActive, value);
    }

    public ObservableCollection<T>? DrawableItems
    {
        get => m_drawableItems;
        set
        {
            if (Equals(value, m_drawableItems)) return;

            if (m_drawableItems != null)
            {
                m_drawableItems.CollectionChanged -= DrawablesOnCollectionChanged;
            }

            if (value != null)
            {
                value.CollectionChanged += DrawablesOnCollectionChanged;
                AddDrawables(value);
            }
            
            m_drawableItems = value;
            
            OnPropertyChanged();
        }
    }

    public Shader? Shader
    {
        get => m_shader;
        set
        {
            m_shader = value ?? throw new ArgumentNullException(nameof(Shader));
            
            OnPropertyChanged();
        }
    }
    
    public Shader? InstancedShader
    {
        get => m_instancedShader;
        set
        {
            m_instancedShader = value ?? throw new ArgumentNullException(nameof(InstancedShader));
            
            SortInstancedAndPerInstanceObjects();
            OnPropertyChanged();
        }
    }
    
    public void Render(Camera camera, ref Matrix4 projection)
    {
        if (!IsActive || 
            Shader == null || 
            DrawableItems == null || 
            DrawableItems.Count == 0)
            return;

        RenderInternal(m_sceneBuffers, Shader!, camera, ref projection, false);

        if (InstancedShader != null)
            RenderInternal(m_instancedSceneBuffers, InstancedShader, camera, ref projection, true);
    }

    private void SortInstancedAndPerInstanceObjects()
    {
        foreach (var sceneBuffer in m_sceneBuffers)
        {
            if (sceneBuffer.Value.Drawables.Count > MAX_PER_INSTANCE_ELEMENTS)
                m_instancedSceneBuffers.Add(sceneBuffer.Key, sceneBuffer.Value);
        }
        
        foreach (var sceneBuffer in m_instancedSceneBuffers)
            m_sceneBuffers.Remove(sceneBuffer.Key);
    }
    
    private bool AddDrawable(IDrawable drawable)
    {
        var drawableScene = drawable.Scene;
        
        if (!m_instancedSceneBuffers.TryGetValue(drawableScene, out var drawSceneInfo) && 
            !m_sceneBuffers.TryGetValue(drawableScene, out drawSceneInfo))
        {
            drawSceneInfo = new DrawSceneInfo();
            m_sceneBuffers.Add(drawableScene, drawSceneInfo);
        } 
        else if (InstancedShader != null && 
                 !m_instancedSceneBuffers.ContainsKey(drawableScene) && 
                 drawSceneInfo.Drawables.Count > MAX_PER_INSTANCE_ELEMENTS)
        {
            m_sceneBuffers.Remove(drawableScene);
            m_instancedSceneBuffers[drawableScene] = drawSceneInfo;
        }
        
        drawSceneInfo.Drawables.Add(drawable);

        return true;
    }

    private void AddDrawables(IList<T> drawables)
    {
        for (int i = 0; i < drawables.Count; i++)
        {
            var drawable = drawables[i];

            AddDrawable(drawable);
        }
    }

    private bool RemoveDrawable(IDrawable drawable)
    {
        return (m_sceneBuffers.TryGetValue(drawable.Scene, out var drawSceneInfo) || 
                m_instancedSceneBuffers.TryGetValue(drawable.Scene, out drawSceneInfo)) &&
               drawSceneInfo.Drawables.Remove(drawable);
    }

    private void RenderInternal(Dictionary<Scene, DrawSceneInfo> sceneInfos, Shader shader, Camera camera, ref Matrix4 projection, bool isInstanceRendering)
    {
        if (sceneInfos.Count == 0)
            return;
        
        if (!shader.IsCurrentShaderInUsing)
            shader.Use();

        shader.SetMatrix4("projection", projection);
        shader.SetMatrix4("view", camera.LookAt);

        Action<DrawSceneInfo, Mesh, DrawObjectInfo, Shader> renderExecute = isInstanceRendering 
                ? RenderInstanced 
                : RenderPerInstance;
        
        foreach (var elemPair in sceneInfos)
        {
            var drawSceneInfo = elemPair.Value;
            var meshes = elemPair.Key.Meshes;

            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                
                //Find existing draw mesh info and if it don't exists - create it
                var key = (mesh, shader);
                if (!drawSceneInfo.MeshDrawInfos.TryGetValue(key, out var drawObjectInfo))
                {
                    drawObjectInfo = CreateDrawMeshInfo(mesh, shader);

                    drawSceneInfo.MeshDrawInfos.Add(key, drawObjectInfo);
                }

                GL.BindVertexArray(drawObjectInfo.VertexArrayObject);
                
                renderExecute(drawSceneInfo, mesh, drawObjectInfo, shader);
            }
        }
        
        ClearBindState();
    }

    private void RenderPerInstance(DrawSceneInfo drawSceneInfo, Mesh mesh, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        for (int j = 0; j < drawSceneInfo.Drawables.Count; j++)
        {
            var element = drawSceneInfo.Drawables[j];

            if (!element.Visible)
                continue;

            Shader!.SetMatrix4("model", element.Transformation);
            Shader.SetVector4("color", Colors.White);

            Shader.SetMatrix2("textureTransform",
                Matrix2.CreateScale(mesh.TextureData.Scale.X, mesh.TextureData.Scale.Y) *
                Matrix2.CreateRotation(MathHelper.DegreesToRadians(mesh.TextureData.RotationAngle)));

            if (mesh.TextureData.Texture.Handle == 0 || !mesh.HasTextureCoords)
            {
                Shader.SetVector4("color", DEFAULT_EMPTY_TEXTURE_COLOR);
                Shader.SetBool("hasTexture", false);
            }
            else
                Shader.SetBool("hasTexture", true);

            mesh.TextureData.Texture.Use(TextureUnit.Texture0);
            
            GL.DrawArrays(PrimitiveType.Triangles, 0, drawObjectInfo.VerticesLength);
        }
    }
    
    private void RenderInstanced(DrawSceneInfo drawSceneInfo, Mesh mesh, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        var buffer = drawSceneInfo.TransformationsBuffer;
        var needBufferRecreate = buffer == null || buffer.MaxElementsInBuffer < drawSceneInfo.Drawables.Count;
        var maxElements = GetMaxBufferElements(drawSceneInfo, needBufferRecreate, buffer);

        var modelsData = ArrayPool<ModelData>.Shared.Rent(maxElements);
        mesh.TextureData.Texture.Use(TextureUnit.Texture0);
        
        var haveTexture = mesh.TextureData.Texture != Texture.Empty && mesh.HasTextureCoords;
        var textTransform = Matrix2.CreateScale(mesh.TextureData.Scale.X, mesh.TextureData.Scale.Y) *
                            Matrix2.CreateRotation(MathHelper.DegreesToRadians(mesh.TextureData.RotationAngle));
        var hiddenElementsAmount = 0;
        
        for (int j = 0; j < drawSceneInfo.Drawables.Count; j++)
        {
            var element = drawSceneInfo.Drawables[j];

            if (!element.Visible)
            {
                hiddenElementsAmount++;
                continue;
            }

            modelsData[j] = new ModelData(element.Transformation, 
                haveTexture ? Colors.White : DEFAULT_EMPTY_TEXTURE_COLOR, 
                textTransform, haveTexture ? 1 : 0);
        }
        
        if (hiddenElementsAmount == drawSceneInfo.Drawables.Count)
            return;

        if (needBufferRecreate)
        {
            buffer?.Dispose();
            buffer = RecreateBuffer(drawObjectInfo, shader, maxElements);
            
            drawSceneInfo.TransformationsBuffer = buffer;
        }

        var drawablesToDraw = drawSceneInfo.Drawables.Count - hiddenElementsAmount;
        
        buffer!.FillData(modelsData, drawablesToDraw);
        
        GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, drawObjectInfo.VerticesLength, drawablesToDraw);
        
        ArrayPool<ModelData>.Shared.Return(modelsData);
    }
    
    private void DrawablesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (IDrawable drawable in e.NewItems!)
                    AddDrawable(drawable);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (IDrawable drawable in e.OldItems!)
                    RemoveDrawable(drawable);
                break;
        }
    }
    
    private static int GetMaxBufferElements(DrawSceneInfo drawSceneInfo, bool needBufferRecreate, Buffer<ModelData>? buffer)
    {
        var elementsAmount = drawSceneInfo.Drawables.Count;
        var newElementsAmount = DEFAULT_MAX_INSTANCED_ELEMENTS;
        
        while (elementsAmount > newElementsAmount)
        {
            newElementsAmount *= 2;
        }
        
        return needBufferRecreate 
            ? buffer?.MaxElementsInBuffer * 2 ?? newElementsAmount 
            : buffer!.MaxElementsInBuffer;
    }
    
    private static void ClearBindState()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        GL.BindVertexArray(0);
    }
    
    private static Buffer<ModelData> RecreateBuffer(DrawObjectInfo drawObjectInfo, Shader shader, int maxElements)
    {
        var buffer = new Buffer<ModelData>(BufferTarget.ArrayBuffer,
            BufferUsageHint.DynamicDraw, maxElements) { Name = "Instanced rendering ArrayBuffer" };

        GL.BindVertexArray(drawObjectInfo.VertexArrayObject);
        
        shader.SetAttributeData("iModel", 4 * 4 + 4 * 2 + 1,
            VertexAttribPointerType.Float, ModelData.SizeInBytes, 0, vertexAttbDivisor: 1);
        
        return buffer;
    }
    
    private static DrawObjectInfo CreateDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0;

        var verticesLength = mesh.Faces.Sum(f => f.Indices.Length);
        var vertexRentArr = ArrayPool<Vertex>.Shared.Rent(verticesLength);
        
        for (int i = 0, vIdx = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];
        
            for (int j = 0; j < face.Indices.Length; j++, vIdx++)
            {
                vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                    face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                    face.HasTextureIndices ? mesh.TextureCoords[face.TextureIndices[j]] : Vector2.Zero);
            }
        }
        
        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verticesLength * Vertex.SizeInBytes, vertexRentArr, BufferUsageHint.StaticDraw);

        ArrayPool<Vertex>.Shared.Return(vertexRentArr);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        shader.SetAttributeData("iPos", 3, VertexAttribPointerType.Float, Vertex.SizeInBytes, 0);
        shader.SetAttributeData("iTextureCoord", 2, VertexAttribPointerType.Float, Vertex.SizeInBytes, Vector3.SizeInBytes * 2);

        return new DrawObjectInfo(vao, vbo, 0)
        {
            VerticesLength = verticesLength
        };
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ModelData
    {
        public readonly Matrix4 Transformation;
        public readonly Vector4 Color;
        public readonly Matrix2 TextureTransformation;
        public readonly float HaveTexture;
        
        public ModelData(Matrix4 transformation, Vector4 color, Matrix2 textureTransformation, float haveTexture)
        {
            Transformation = transformation;
            Color = color;
            TextureTransformation = textureTransformation;
            HaveTexture = haveTexture;
        }

        public static unsafe int SizeInBytes => sizeof(ModelData);
    }
    
    private sealed class DrawSceneInfo
    {
        public List<IDrawable> Drawables { get; } = new();
        public Dictionary<(Mesh, Shader), DrawObjectInfo> MeshDrawInfos { get; } = new();
        
        public Buffer<ModelData>? TransformationsBuffer { get; set; }
    }
}