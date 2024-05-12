using System.Buffers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Common.Extensions;
using Common.Models;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Delegates;
using CoolEngine.Services.Extensions;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

using MeshTextureGroup = Dictionary<Texture, Dictionary<IDrawable, TextureData>>;

file static class ObjectRendererConstants
{
    public const int DefaultMaxInstancedElements_ = 32;
    public const int MaxPerInstanceElements_ = 15;
    
    public static readonly Vector4 DefaultEmptyTextureColor_ = new Vector4(0xAA, 0x55, 0x6F, 1);
}

//TODO: Add TextureChanged event to drawable and subscribe on it for texture changing
public sealed class ObjectRenderer<T> : ObservableObject, IRenderer<T>
    where T: IDrawable
{
    private readonly Dictionary<Scene, DrawSceneInfo> m_sceneBuffers;
    private readonly Dictionary<Scene, DrawSceneInfo> m_instancedSceneBuffers;
    private readonly Dictionary<Mesh, MeshTextureGroup> m_meshTextures;
    
    private bool m_isActive;

    private Shader? m_shader;
    private Shader? m_instancedShader;
    private ObservableCollection<T>? m_drawableItems;

    public ObjectRenderer()
    {
        m_sceneBuffers = new Dictionary<Scene, DrawSceneInfo>();
        m_instancedSceneBuffers = new Dictionary<Scene, DrawSceneInfo>();
        m_meshTextures = new Dictionary<Mesh, MeshTextureGroup>();
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
        //Traverse via scene buffers for finding drawables list drawables count per texture > MaxPerInstanceElements_
        foreach (var sceneBuffer in m_sceneBuffers)
        {
            foreach (var (texture, group) in sceneBuffer.Value.Drawables)
            {
                if (group.Count <= ObjectRendererConstants.MaxPerInstanceElements_) 
                    continue;

                m_instancedSceneBuffers.TryGetAndAddIfNotExists(sceneBuffer.Key)
                    .Drawables.Add(texture, group);
            }
        }
        
        foreach (var instancedSceneBuffer in m_instancedSceneBuffers)
        {
            if (!m_sceneBuffers.TryGetValue(instancedSceneBuffer.Key, out var drawSceneInfo)) 
                continue;
            
            foreach (var keyValuePair in instancedSceneBuffer.Value.Drawables)
            {
                drawSceneInfo.Drawables.Remove(keyValuePair.Key);
            }

            if (drawSceneInfo.Drawables.Count != 0) 
                continue;
                
            m_sceneBuffers.Remove(instancedSceneBuffer.Key);
            drawSceneInfo.TransformationsBuffer?.Dispose();
        }
    }
    
    private bool AddDrawable(IDrawable drawable)
    {
        var drawableScene = drawable.Scene;

        var fromInstancedBuffer = m_instancedSceneBuffers.TryGetValue(drawableScene, out var instancedDrawSceneInfo);
        var fromSceneBuffer = m_sceneBuffers.TryGetValue(drawableScene, out var drawSceneInfo);

        if (!fromInstancedBuffer && !fromSceneBuffer)
        {
            drawSceneInfo = new DrawSceneInfo();
            m_sceneBuffers.Add(drawableScene, drawSceneInfo);
        }
        
        AddDrawableToInstancedGroup(drawable, drawSceneInfo, instancedDrawSceneInfo, drawableScene);
        
        AddTextureGroup(drawable);
        
        foreach (var drawableTexturedObjectInfo in drawable.TexturedObjectInfos)
        {
            drawableTexturedObjectInfo.TextureChanged += DrawableTextureChanged;
        }

        return true;
    }

    private void AddDrawableToInstancedGroup(IDrawable drawable, DrawSceneInfo? drawSceneInfo,
        DrawSceneInfo? instancedDrawSceneInfo, Scene drawableScene)
    {
        if (InstancedShader is null) 
            return;
        
        foreach (var texture in drawable.GetUniqueTexturesFromDrawable())
        {
            List<IDrawable>? instancedDrawables = null;
            List<IDrawable>? uniqueDrawables = null;
            var haveDrawables = drawSceneInfo?.Drawables.TryGetValue(texture, out uniqueDrawables) ?? false;
            var haveInstancedDrawables = instancedDrawSceneInfo?.Drawables.TryGetValue(texture, out instancedDrawables) ?? false;
                
            if (!haveDrawables && !haveInstancedDrawables && drawSceneInfo is not null)
            {
                uniqueDrawables = new List<IDrawable> { drawable };
                drawSceneInfo.Drawables.Add(texture, uniqueDrawables);
            }
            else if (!haveDrawables && haveInstancedDrawables)
            {
                instancedDrawables!.Add(drawable);
            }
            else if (haveDrawables && !haveInstancedDrawables &&
                     uniqueDrawables!.Count > ObjectRendererConstants.MaxPerInstanceElements_)
            {
                if (instancedDrawSceneInfo is null)
                {
                    instancedDrawSceneInfo = new DrawSceneInfo();
                    m_instancedSceneBuffers.Add(drawableScene, instancedDrawSceneInfo);
                }
                    
                uniqueDrawables.Add(drawable);
                instancedDrawSceneInfo.Drawables.Add(texture, uniqueDrawables);
                drawSceneInfo!.Drawables.Remove(texture);

                if (drawSceneInfo.Drawables.Count != 0) 
                    continue;
                    
                drawSceneInfo.TransformationsBuffer?.Dispose();
                m_sceneBuffers.Remove(drawableScene);
            }
            else
            {
                uniqueDrawables?.Add(drawable);
            }
        }
    }

    private void AddDrawables(IList<T> drawables)
    {
        for (int i = 0; i < drawables.Count; i++)
        {
            var drawable = drawables[i];

            AddDrawable(drawable);
        }
    }

    private void AddTextureGroup(IDrawable drawable)
    {
        for (var i = 0; i < drawable.Scene.Meshes.Length; i++)
        {
            var mesh = drawable.Scene.Meshes[i];
            
            //Group by texture every texture data
            var groupedTextures = drawable.TexturedObjectInfos
                .GroupBy(key => key.TexturedMeshes[mesh].Texture,
                         v => v.TexturedMeshes[mesh]);
            
            if (m_meshTextures.TryGetValue(mesh, out var textureGroup))
            {
                foreach (var groupedTexture in groupedTextures)
                {
                    if (textureGroup.TryGetValue(groupedTexture.Key, out var textureDatas))
                    {
                        foreach (var textureData in groupedTexture)
                        {
                            textureDatas.Add(drawable, textureData);
                        }
                    }
                    else
                    {
                        textureGroup[groupedTexture.Key] = groupedTexture.ToDictionary(_ => drawable);
                    }
                }
            }
            else
            {
                textureGroup = groupedTextures.ToDictionary(k => k.Key, 
                                                            v => v.ToDictionary(_ => drawable));
                m_meshTextures.Add(mesh, textureGroup);
            }
        }
    }
    
    private bool RemoveDrawable(IDrawable drawable)
    {
        var existsInSceneBuffer = m_sceneBuffers.TryGetValue(drawable.Scene, out var drawSceneInfo);
        var existsInInstancedSceneBuffer = m_instancedSceneBuffers.TryGetValue(drawable.Scene, out var drawInstancedSceneInfo);

        if (!existsInSceneBuffer && !existsInInstancedSceneBuffer)
        {
            return false;
        }
        
        var removed = false;
        foreach (var texture in drawable.GetUniqueTexturesFromDrawable())
        {
            if (drawSceneInfo?.Drawables.TryGetValue(texture, out var uniqueDrawables) == true)
            {
                removed |= uniqueDrawables.Remove(drawable);
            }
            
            if (drawInstancedSceneInfo?.Drawables.TryGetValue(texture, out var uniqueInstancedDrawables) == true)
            {
                removed |= uniqueInstancedDrawables.Remove(drawable);
            }
        }
        
        foreach (var drawableTexturedObjectInfo in drawable.TexturedObjectInfos)
        {
            drawableTexturedObjectInfo.TextureChanged -= DrawableTextureChanged;
        }

        return removed;
    }

    private void RenderInternal(Dictionary<Scene, DrawSceneInfo> sceneInfos, Shader shader, Camera camera, ref Matrix4 projection, bool isInstanceRendering)
    {
        if (sceneInfos.Count == 0)
            return;
        
        if (!shader.IsCurrentShaderInUsing)
            shader.Use();

        shader.SetMatrix4("projection", projection);
        shader.SetMatrix4("view", camera.LookAt);

        ActionRef<RenderDto> renderExecute = isInstanceRendering 
                ? RenderInstanced 
                : RenderPerInstance;
        
        foreach (var elemPair in sceneInfos)
        {
            var drawSceneInfo = elemPair.Value;
            var meshes = elemPair.Key.Meshes;

            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                var textureGroup = m_meshTextures[mesh];
                
                foreach (var (texture, group) in textureGroup)
                {
                    texture.Use(TextureUnit.Texture0);
                    
                    //Find existing draw mesh info and if it doesn't exist - create it
                    var key = (mesh, shader);
                    if (!drawSceneInfo.MeshDrawInfos.TryGetValue(key, out var drawObjectInfo))
                    {
                        drawObjectInfo = CreateDrawMeshInfo(mesh, shader);

                        drawSceneInfo.MeshDrawInfos.Add(key, drawObjectInfo);
                    }

                    GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

                    var renderDto = new RenderDto(mesh, shader, texture, group, drawObjectInfo, drawSceneInfo);
                    
                    renderExecute(ref renderDto);
                }
            }
        }
        
        ClearBindState();
    }

    private void RenderPerInstance(ref RenderDto renderDto)
    {
        if (!renderDto.DrawSceneInfo.Drawables.TryGetValue(renderDto.Texture, out var sceneDrawables))
        {
            return;
        }
        
        var drawables = renderDto.Drawables;
        var mesh = renderDto.Mesh;
        var verticesLength = renderDto.DrawObjectInfo.VerticesLength;
        
        for (int j = 0; j < sceneDrawables.Count; j++)
        {
            var element = sceneDrawables[j];

            if (!element.Visible)
                continue;

            var textureData = drawables[element];

            Shader!.SetMatrix4("model", element.Transformation);
            Shader.SetVector4("color", Colors.White);

            Shader.SetMatrix2("textureTransform",
                Matrix2.CreateScale(textureData.Scale.X, textureData.Scale.Y) *
                Matrix2.CreateRotation(MathHelper.DegreesToRadians(textureData.RotationAngle)));

            if (textureData.Texture.IsEmpty || !mesh.HasTextureCoords)
            {
                Shader.SetVector4("color", ObjectRendererConstants.DefaultEmptyTextureColor_);
                Shader.SetBool("hasTexture", false);
            }
            else
                Shader.SetBool("hasTexture", true);
            
            GL.DrawArrays(PrimitiveType.Triangles, 0, verticesLength);
        }
    }
    
    private void RenderInstanced(ref RenderDto renderDto)
    {
        if (!renderDto.DrawSceneInfo.Drawables.TryGetValue(renderDto.Texture, out var sceneDrawables))
        {
            return;
        }
        
        var drawables = renderDto.Drawables;
        var mesh = renderDto.Mesh;
        var drawObjectInfo = renderDto.DrawObjectInfo;
        var drawSceneInfo = renderDto.DrawSceneInfo;
        
        var buffer = drawSceneInfo.TransformationsBuffer;
        var needBufferRecreate = buffer == null || buffer.MaxElementsInBuffer < sceneDrawables.Count;
        var maxElements = GetMaxBufferElements(sceneDrawables.Count, buffer);

        var modelsData = ArrayPool<ModelData>.Shared.Rent(maxElements);
        
        var hiddenElementsAmount = 0;
        
        for (int j = 0; j < sceneDrawables.Count; j++)
        {
            var element = sceneDrawables[j];

            if (!element.Visible)
            {
                hiddenElementsAmount++;
                continue;
            }

            var textureData = drawables[element];
            var haveTexture = !textureData.Texture.IsEmpty && mesh.HasTextureCoords;
            var textTransform = Matrix2.CreateScale(textureData.Scale.X, textureData.Scale.Y) *
                                Matrix2.CreateRotation(MathHelper.DegreesToRadians(textureData.RotationAngle));

            modelsData[j] = new ModelData(element.Transformation, 
                haveTexture ? Colors.White : ObjectRendererConstants.DefaultEmptyTextureColor_, 
                textTransform, haveTexture ? 1 : 0);
        }
        
        if (hiddenElementsAmount == sceneDrawables.Count)
            return;

        if (needBufferRecreate)
        {
            buffer?.Dispose();
            buffer = RecreateBuffer(drawObjectInfo, renderDto.Shader, maxElements);
            
            drawSceneInfo.TransformationsBuffer = buffer;
        }

        var drawablesToDraw = sceneDrawables.Count - hiddenElementsAmount;
        
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
    
    private void DrawableTextureChanged(TexturedObjectInfo sender, TexturedObjectInfo.TextureChangedArg e)
    {
        var drawable = e.Drawable;
        var existsInSceneBuffer = m_sceneBuffers.TryGetValue(drawable.Scene, out var drawSceneInfo);
        var existsInInstancedSceneBuffer = m_instancedSceneBuffers.TryGetValue(drawable.Scene, out var drawInstancedSceneInfo);

        if (!existsInSceneBuffer && !existsInInstancedSceneBuffer)
        {
            return;
        }

        if (m_meshTextures.TryGetValue(e.Mesh, out var textureGroup) &&
            textureGroup.TryGetValue(e.OldTexture, out var oldTextureGroupDrawables))
        {
            oldTextureGroupDrawables.Remove(drawable);
            var newTextureGroupDrawables = textureGroup.TryGetAndAddIfNotExists(e.NewTexture);
            newTextureGroupDrawables[drawable] = e.TextureData;
        }

        UpdateTextureForSceneInfo(e, existsInSceneBuffer ? drawSceneInfo! : drawInstancedSceneInfo!, drawable);
    }

    private static void UpdateTextureForSceneInfo(in TexturedObjectInfo.TextureChangedArg e, DrawSceneInfo drawSceneInfo, IDrawable drawable)
    {
        if (drawSceneInfo.Drawables.TryGetValue(e.OldTexture, out var oldDrawablesList))
        {
            oldDrawablesList.Remove(drawable);
        }

        drawSceneInfo.Drawables.TryGetAndAddIfNotExists(e.NewTexture)
            .Add(drawable);
    }

    private static int GetMaxBufferElements(int currentDrawablesAmount, Buffer<ModelData>? buffer)
    {
        var newElementsAmount = (int)Math.Pow(2, Math.Ceiling(Math.Log(currentDrawablesAmount) / Math.Log(2)));
        newElementsAmount = Math.Max(newElementsAmount, ObjectRendererConstants.DefaultMaxInstancedElements_);
        
        if (buffer is null)
        {
            return newElementsAmount;
        }

        if (buffer.MaxElementsInBuffer > currentDrawablesAmount)
        {
            return buffer.MaxElementsInBuffer;
        }

        if (buffer.MaxElementsInBuffer * 2 > currentDrawablesAmount)
        {
            return buffer.MaxElementsInBuffer * 2;
        }

        return newElementsAmount;
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
        var verticesLength = mesh.Faces.Sum(f => f.Indices.Length);
        var vertexRentArr = ArrayPool<Vertex>.Shared.Rent(verticesLength);
        
        for (int i = 0, vIdx = 0; i < mesh.Faces.Length; i++)
        {
            var face = mesh.Faces[i];
        
            for (int j = 0; j < face.Indices.Length; j++, vIdx++)
            {
                vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                    face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                    face.HasTextureIndices ? mesh.TextureCoords[face.TextureIndices[j]] : Vector2.Zero);
            }
        }
        
        var vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verticesLength * Vertex.SizeInBytes, vertexRentArr, BufferUsageHint.StaticDraw);

        ArrayPool<Vertex>.Shared.Return(vertexRentArr);

        var vao = GL.GenVertexArray();
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

    private readonly record struct RenderDto(
        Mesh Mesh,
        Shader Shader,
        Texture Texture,
        Dictionary<IDrawable, TextureData> Drawables,
        DrawObjectInfo DrawObjectInfo,
        DrawSceneInfo DrawSceneInfo);
    
    private sealed class DrawSceneInfo
    {
        public Dictionary<Texture, List<IDrawable>> Drawables { get; } = new();
        public Dictionary<(Mesh, Shader), DrawObjectInfo> MeshDrawInfos { get; } = new();
        
        public Buffer<ModelData>? TransformationsBuffer { get; set; }
    }
}