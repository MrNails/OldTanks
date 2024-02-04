using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Shader : IDisposable, IEquatable<Shader>
{
    private static readonly int MaxShaderAttributeSize_ = 4;
    
    private readonly Dictionary<string, (int uniformLocation, int uniformBlockIndex)> m_uniforms;
    private readonly Dictionary<string, int> m_attributes;

    public Shader(string name, int handle)
    {
        Name = name;
        Handle = handle;

        m_uniforms = new Dictionary<string, (int uniformLocation, int uniformBlockIndex)>();
        m_attributes = new Dictionary<string, int>();
        LoadShaderUniforms();
        LoadShaderAttributes();
    }

    public bool IsCurrentShaderInUsing => CurrentUsingShader == this;

    public int Handle { get; }
    public string Name { get; }

    public bool Disposed { get; private set; }

    public Shader CurrentUsingShader { get; private set; }

    public void Use()
    {
        GL.UseProgram(Handle);

        CurrentUsingShader = this;
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

    public void SetMatrix2(string name, Matrix2 matrix)
    {
        GL.UseProgram(Handle);
        GL.UniformMatrix2(GetUniformLocation(name), false, ref matrix);
    }

    public void SetVector3(string name, Vector3 vector)
    {
        GL.UseProgram(Handle);
        GL.Uniform3(GetUniformLocation(name), ref vector);
    }

    public void SetVector4(string name, Vector4 vector)
    {
        GL.UseProgram(Handle);
        GL.Uniform4(GetUniformLocation(name), ref vector);
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
        m_uniforms.TryGetValue(uniformName, out var uniformData);

        return uniformData.uniformLocation;
    }
    
    public int GetUniformBlockIndex(string uniformName)
    {
        m_uniforms.TryGetValue(uniformName, out var uniformData);

        return uniformData.uniformBlockIndex;
    }
    
    /// <summary>
    /// Set attribute data to specific shader attribute 
    /// </summary>
    /// <param name="attribName">Attribute name in shader</param>
    /// <param name="size">Specifies the number of components per generic vertex attribute.
    /// Must be 1, 2, 3, 4. Additionally, the symbolic constant GL_BGRA is accepted by glVertexAttribPointer.
    /// If value greater than 4 - than attribute locations will be split on multiple with size up to 4.</param>
    /// <param name="ptrType">Specifies the data type of each component in the array.</param>
    /// <param name="stride">Specifies the byte offset between consecutive generic vertex attributes. If stride is 0, the generic vertex attributes are understood to be tightly packed in the array.</param>
    /// <param name="offset">Specifies the byte offset between consecutive components in generic vertex.</param>
    /// <param name="normalized">For glVertexAttribPointer, specifies whether fixed-point data values should be normalized (<see cref="true"/>) or converted directly as fixed-point values (<see cref="false"/>) when they are accessed</param>
    /// <param name="enable">Enable vertex attribute array</param>
    /// <param name="vertexAttbDivisor">Modifies the rate at which generic vertex attributes advance when rendering multiple instances of primitives in a single draw call.
    /// If divisor is zero, the attribute at slot index advances once per vertex.
    /// If divisor is non-zero, the attribute advances once per divisor instances of the set(s) of vertices being rendered.</param>
    public void SetAttributeData(string attribName, int size, VertexAttribPointerType ptrType, int stride, int offset,
        bool normalized = false, bool enable = true, int vertexAttbDivisor = 0)
    {
        if (size < 1)
            throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be less then 1.");
        
        if (vertexAttbDivisor < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexAttbDivisor), "VertexAttbDivisor cannot be less then 0.");
        
        var attribLocation = GetAttribLocation(attribName);
        var tmpSize = size;
        var localOffset = offset;

        while (tmpSize != 0)
        {
            var s = MaxShaderAttributeSize_;
            
            if (tmpSize - MaxShaderAttributeSize_ <= 0)
                s = tmpSize;
            
            GL.VertexAttribPointer(attribLocation, s, ptrType, normalized, stride, localOffset);
            
            if (enable)
                GL.EnableVertexAttribArray(attribLocation);

            if (vertexAttbDivisor != 0)
                GL.VertexAttribDivisor(attribLocation, vertexAttbDivisor);
            
            localOffset += s * 4; //Multiplying s by 4 gives us size in bytes per attribute
            tmpSize -= s;
            attribLocation++;
        }
    }

    public void BindUniformBlock(string uniformName, int bindingValue = 0)
    {
        GL.UniformBlockBinding(Handle, GetUniformBlockIndex(uniformName), bindingValue);
    }
    
    public bool Equals(Shader? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Handle == other.Handle;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Shader);
    }

    public override int GetHashCode()
    {
        return Handle;
    }

    private void LoadShaderUniforms()
    {
        int output = 0;
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out output);

        for (int i = 0; i < output; i++)
        {
            var size = 0;
            var uniformType = ActiveUniformType.FloatMat4;

            var key = GL.GetActiveUniform(Handle, i, out size, out uniformType);

            m_uniforms[key] = (GL.GetUniformLocation(Handle, key), GL.GetUniformBlockIndex(Handle, key));
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
        Log.Fatal("Abandoned Shader {ShaderName} ({ShaderId})", Name, Handle);
        ReleaseUnmanagedResources();
    }

    public static Shader Create(string vertexShaderText, string fragmentShaderText, string name, ILogger logger)
    {
        var vertexShader = CreateShader(ShaderType.VertexShader, vertexShaderText);
        var fragmentShader = CreateShader(ShaderType.FragmentShader, fragmentShaderText);

        var shaderCompilingText = CompileShader(vertexShader);
        if (shaderCompilingText != string.Empty)
            logger.Error("Error compiling shader {Shader}.\n{ShaderCompilingText}", name, shaderCompilingText);
        else
            logger.Information("Vertex part of shader {Shader} compiled successfully", name);

        shaderCompilingText = CompileShader(fragmentShader);
        if (shaderCompilingText != string.Empty)
            logger.Error("Error compiling shader {Shader}.\n{ShaderCompilingText}", name, shaderCompilingText);
        else
            logger.Information("Fragment part of shader {Shader} compiled successfully", name);

        var handle = GL.CreateProgram();

        GL.AttachShader(handle, vertexShader);
        GL.AttachShader(handle, fragmentShader);

        GL.LinkProgram(handle);

        GL.DetachShader(handle, vertexShader);
        GL.DetachShader(handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return new Shader(name, handle);
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

    public static bool operator ==(Shader? left, Shader? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Shader? left, Shader? right)
    {
        return !Equals(left, right);
    }
}