using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Shader : IDisposable
{
    private Dictionary<string, int> m_uniforms;
    private Dictionary<string, int> m_attributes;

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

        m_uniforms = new Dictionary<string, int>();
        m_attributes = new Dictionary<string, int>();
        LoadShaderUniforms();
        LoadShaderAttributes();
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
    
    public void SetBool(string name, bool boolean)
    {
        GL.UseProgram(Handle);
        GL.Uniform1(GetUniformLocation(name), boolean ? 1 : 0);
    }
    
    public int GetAttribLocation(string attribName)
    {
        m_attributes.TryGetValue(attribName, out int location);

        return location;
    }
    
    public int GetUniformLocation(string uniformName)
    {
        m_uniforms.TryGetValue(uniformName, out int location);
        
        return location;
    }

    private void LoadShaderUniforms()
    {
        int output = 0;
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out output);

        for (int i = 0; i < output; i++)
        {
            var size = 0;
            var ut = ActiveUniformType.FloatMat4;

            var key = GL.GetActiveUniform(Handle, i, out size, out ut);

            m_uniforms[key] = GL.GetUniformLocation(Handle, key);
        }
    }
    
    private void LoadShaderAttributes()
    {
        int output = 0;
        GL.GetProgram(Handle, GetProgramParameterName.ActiveAttributes, out output);

        for (int i = 0; i < output; i++)
        {
            var size = 0;
            var attribType = ActiveAttribType.FloatMat4;

            var key = GL.GetActiveAttrib(Handle, i, out size, out attribType);

            m_attributes[key] = GL.GetAttribLocation(Handle, key);
        }
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