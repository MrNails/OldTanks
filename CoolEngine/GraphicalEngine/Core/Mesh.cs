﻿using CoolEngine.Core.Primitives;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Mesh
{
    private int m_meshId;
    private Vertex[] m_vertices;
    private uint[] m_indices;

    public Mesh(int meshId) : this(meshId, Array.Empty<Vertex>(), Array.Empty<uint>()) { }
    
    public Mesh(int meshId, Vertex[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
        m_meshId = meshId;
    }

    /// <summary>
    /// Represent mesh id related to specified scene.
    /// </summary>
    public int MeshId => m_meshId;
    
    public Vertex[] Vertices
    {
        get => m_vertices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            m_vertices = value;
        }
    }

    public uint[] Indices
    {
        get => m_indices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            m_indices = value;
        }
    }

    public Vector3 Normal { get; set; }

    public Texture Texture { get; set; }

    public Mesh Copy()
    {
        return new Mesh(MeshId)
        {
            Vertices = m_vertices,
            Indices = m_indices,
            Texture = Texture,
            Normal = Normal
        };
    }
}