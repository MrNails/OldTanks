using CoolEngine.Services.Exceptions;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Shader : IDisposable, IEquatable<Shader>
{
    private static readonly int MaxShaderAttributeSize_ = 4;
    
    private readonly ILogger m_logger;
    private readonly Dictionary<string, int> m_uniforms;
    private readonly Dictionary<string, int> m_uniformBlocks;
    private readonly Dictionary<string, int> m_attributes;
    private readonly HashSet<(string Kind, string Name)> m_reportedUnknownNames;

    /// <summary>
    /// Wraps a linked program and reads its active uniforms, uniform blocks and attributes. GL thread only.
    /// </summary>
    /// <param name="name">Shader name, used in log messages.</param>
    /// <param name="handle">Handle of a successfully linked program.</param>
    /// <param name="logger">Logger for the once-per-name reports of unknown names.</param>
    public Shader(string name, int handle, ILogger logger)
    {
        Name = name;
        Handle = handle;
        m_logger = logger;

        m_uniforms = new Dictionary<string, int>();
        m_uniformBlocks = new Dictionary<string, int>();
        m_attributes = new Dictionary<string, int>();
        m_reportedUnknownNames = new HashSet<(string Kind, string Name)>();
        LoadShaderUniforms();
        LoadShaderUniformBlocks();
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

    /// <summary>
    /// Returns the location of an active vertex attribute.
    /// </summary>
    /// <param name="attribName">Attribute name in the shader.</param>
    /// <returns>The attribute location, or -1 for a name the linked program does not have active
    /// (not declared, or optimised away); such a name is logged once per name.</returns>
    public int GetAttribLocation(string attribName)
    {
        if (m_attributes.TryGetValue(attribName, out int location))
            return location;

        ReportUnknownName("attribute", attribName);

        return -1;
    }

    /// <summary>
    /// Returns the location of an active uniform.
    /// </summary>
    /// <param name="uniformName">Uniform name in the shader.</param>
    /// <returns>The uniform location, or -1 for a name the linked program does not have active
    /// (not declared, or optimised away); such a name is logged once per name. GL ignores uploads to -1.
    /// A member of a uniform block has no location and is reported and returned the same way.</returns>
    public int GetUniformLocation(string uniformName)
    {
        if (m_uniforms.TryGetValue(uniformName, out int location))
            return location;

        ReportUnknownName("uniform", uniformName);

        return -1;
    }
    
    /// <summary>
    /// Returns the index of an active uniform block.
    /// </summary>
    /// <param name="uniformName">Uniform block name in the shader.</param>
    /// <returns>The uniform block index, or -1 for a name the linked program does not have active
    /// (not declared, or optimised away); such a name is logged once per name.</returns>
    public int GetUniformBlockIndex(string uniformName)
    {
        if (m_uniformBlocks.TryGetValue(uniformName, out int index))
            return index;

        ReportUnknownName("uniform block", uniformName);

        return -1;
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

        if (attribLocation == -1)
            return;

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
        var index = GetUniformBlockIndex(uniformName);

        if (index == -1)
            return;

        GL.UniformBlockBinding(Handle, index, bindingValue);
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

            var location = GL.GetUniformLocation(Handle, key);

            // A member of a uniform block has no location; leaving it out reports it once like an unknown name.
            if (location == -1)
                continue;

            m_uniforms[key] = location;
        }
    }

    private void LoadShaderUniformBlocks()
    {
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniformBlocks, out int count);

        if (count == 0)
            return;

        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniformBlockMaxNameLength, out int maxNameLength);

        for (int i = 0; i < count; i++)
        {
            GL.GetActiveUniformBlockName(Handle, i, maxNameLength, out _, out string blockName);

            m_uniformBlocks[blockName] = i;
        }
    }

    private void ReportUnknownName(string nameKind, string name)
    {
        if (m_reportedUnknownNames.Add((nameKind, name)))
            m_logger.Warning("Shader {Shader} has no active {NameKind} named {Name}; -1 is used, so GL ignores it",
                Name, nameKind, name);
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

    /// <summary>
    /// Compiles both stages, links them into a program and wraps it. GL thread only.
    /// Each stage's compile status and log are logged (Error when it fails, Warning when it compiles with a log),
    /// and so are the link status and log.
    /// </summary>
    /// <param name="vertexShaderText">Vertex stage source.</param>
    /// <param name="fragmentShaderText">Fragment stage source.</param>
    /// <param name="name">Shader name, used in log messages and in the exception.</param>
    /// <param name="logger">Logger for compile and link results; the shader keeps it for unknown-name reports.</param>
    /// <returns>The linked shader.</returns>
    /// <exception cref="ShaderException">The program fails to link; the program is deleted and the
    /// message carries the link log.</exception>
    public static Shader Create(string vertexShaderText, string fragmentShaderText, string name, ILogger logger)
    {
        var vertexShader = CreateShader(ShaderType.VertexShader, vertexShaderText);
        var fragmentShader = CreateShader(ShaderType.FragmentShader, fragmentShaderText);

        var shaderLog = CompileShader(vertexShader);
        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vertexCompiled);
        if (vertexCompiled == 0)
            logger.Error("Error compiling {ShaderStage} shader {Shader}.\n{ShaderLog}", "vertex", name, shaderLog);
        else if (!string.IsNullOrWhiteSpace(shaderLog))
            logger.Warning("Vertex part of shader {Shader} compiled with warnings.\n{ShaderLog}", name, shaderLog);
        else
            logger.Information("Vertex part of shader {Shader} compiled successfully", name);

        shaderLog = CompileShader(fragmentShader);
        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragmentCompiled);
        if (fragmentCompiled == 0)
            logger.Error("Error compiling {ShaderStage} shader {Shader}.\n{ShaderLog}", "fragment", name, shaderLog);
        else if (!string.IsNullOrWhiteSpace(shaderLog))
            logger.Warning("Fragment part of shader {Shader} compiled with warnings.\n{ShaderLog}", name, shaderLog);
        else
            logger.Information("Fragment part of shader {Shader} compiled successfully", name);

        var handle = GL.CreateProgram();

        GL.AttachShader(handle, vertexShader);
        GL.AttachShader(handle, fragmentShader);

        GL.LinkProgram(handle);

        GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out int linked);
        var linkLog = GL.GetProgramInfoLog(handle);

        if (linked == 0)
            logger.Error("Shader {Shader} failed to link.\n{LinkLog}", name, linkLog);
        else if (!string.IsNullOrWhiteSpace(linkLog))
            logger.Warning("Shader {Shader} linked with warnings.\n{LinkLog}", name, linkLog);
        else
            logger.Information("Shader {Shader} linked successfully", name);

        GL.DetachShader(handle, vertexShader);
        GL.DetachShader(handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        if (linked == 0)
        {
            GL.DeleteProgram(handle);

            throw new ShaderException($"Shader {name} failed to link.\n{linkLog}");
        }

        return new Shader(name, handle, logger);
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