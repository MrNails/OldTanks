using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GraphicalEngine.Core;

public class Shader : IDisposable
{
    public Shader(string vertexShaderText, string fragmentShaderText, string name)
    {
        Name = name;

        var vertexShader = CreateShader(ShaderType.VertexShader, vertexShaderText);
        var fragmentShader = CreateShader(ShaderType.FragmentShader, fragmentShaderText);

        var shaderCompilingText = CompileShader(vertexShader);
        if (shaderCompilingText != string.Empty)
            Console.WriteLine($"Error compiling shader {name}.\n{shaderCompilingText}");

        shaderCompilingText = CompileShader(fragmentShader);
        if (shaderCompilingText != string.Empty)
            Console.WriteLine($"Error compiling shader {name}.\n{shaderCompilingText}");

        Handle = GL.CreateProgram();

        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);

        GL.LinkProgram(Handle);

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public int Handle { get; }
    public string Name { get; }
    
    public bool Disposed { get; private set; }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public void SetMatrix4(string name, Matrix4 matrix)
    {
        GL.UseProgram(Handle);
        GL.UniformMatrix4(GetUniformLocation(name), false, ref matrix);
    }
    
    public void SetMatrix3(string name, Matrix3 matrix)
    {
        GL.UseProgram(Handle);
        GL.UniformMatrix3(GetUniformLocation(name), false, ref matrix);
    }
    
    public void SetVector3(string name, Vector3 vector)
    {
        GL.UseProgram(Handle);
        GL.Uniform3(GetUniformLocation(name), ref vector);
    }
    
    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(Handle, attribName);
    }
    
    public int GetUniformLocation(string uniformName)
    {
        return GL.GetUniformLocation(Handle, uniformName);
    }
    
    private static int CreateShader(ShaderType shaderType, string source)
    {
        var shader = GL.CreateShader(shaderType);
        GL.ShaderSource(shader, source);

        return shader;
    }

    private static string CompileShader(int shader)
    {
        GL.CompileShader(shader);

        return GL.GetShaderInfoLog(shader);
    }

    private void ReleaseUnmanagedResources()
    {
        if (!Disposed)
        {
            GL.DeleteProgram(Handle);
            Disposed = true;
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~Shader()
    {
        ReleaseUnmanagedResources();
    }
}