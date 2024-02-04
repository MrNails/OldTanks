using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Buffer<T> : IDisposable
    where T: unmanaged
{
    private bool m_disposed;
    
    public unsafe Buffer(BufferTarget bufferTarget, BufferUsageHint usageHint, int maxElementsAmount, T[]? data = null)
    {
        BufferTarget = bufferTarget;
        UsageHint = usageHint;
        MaxElementsInBuffer = maxElementsAmount;
        
        Id = GL.GenBuffer();
        
        Log.Logger.Information("Buffer {BufferTarget} object: {Id}", BufferTarget, Id);
        
        Use();
        GL.BufferData(bufferTarget, maxElementsAmount * sizeof(T), data, usageHint);
    }
    
    public bool Disposed => m_disposed;
    
    public int Id { get; }
    public BufferTarget BufferTarget { get; }
    public BufferUsageHint UsageHint { get; }
    public int MaxElementsInBuffer { get; }
    
    public string? Name { get; set; }

    public void Use()
    {
        GL.BindBuffer(BufferTarget, Id);
    }

    public void ClearUsing()
    {
        GL.BindBuffer(BufferTarget, 0);
    }
    
    public unsafe void FillData(T[] data, int dataAmount, IntPtr offset = 0)
    {
        if (MaxElementsInBuffer < dataAmount)
            throw new InvalidOperationException($"Cannot fill buffer with data elements size {dataAmount}. Max buffer elements size is {MaxElementsInBuffer}");
        
        Use();
        GL.BufferSubData(BufferTarget, offset, dataAmount * sizeof(T), data);
    }

    private void ReleaseUnmanagedResources()
    {
        if (m_disposed)
            return;
        
        GL.BindBuffer(BufferTarget, 0);
        GL.DeleteBuffer(Id);
        
        m_disposed = true;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~Buffer()
    {
        Log.Logger.Fatal("Abandoned buffer {Name}: {Id}", Name ?? BufferTarget.ToString(), Id);
        ReleaseUnmanagedResources();
    }
}