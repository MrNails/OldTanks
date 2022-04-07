using System.Buffers;
using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.GraphicalEngine.Services;

public readonly struct GLSettings
{
    public int ProgramId { get; init; }
    public int TextureId { get; init; }
        
    public int[] ScissorBox { get; init; }
        
    public bool BlendIsActive { get; init; }
    public bool ScissorIsActive { get; init; }
    public bool CullFaceIsActive { get; init; }
    public bool DepthTestIsActive { get; init; }
    public bool StencilTestIsActive { get; init; }
        
    public BlendEquationMode BlendEquation { get; init; }
    public BlendingFactor BlendFactorSrcAlpha { get; init; }
    public BlendingFactor BlendFactorDstAlpha { get; init; }

    public static GLSettings GetCurrentGLSettings()
    {
        var scissorBox = ArrayPool<int>.Shared.Rent(4);

        GL.GetInteger(GetPName.ActiveTexture, out var activeTexture);
        GL.GetInteger(GetPName.CurrentProgram, out var activeProgram);
        GL.GetInteger(GetPName.ScissorBox, scissorBox);
        GL.GetInteger(GetPName.BlendSrcAlpha, out var srcAlpha);
        GL.GetInteger(GetPName.BlendDstAlpha, out var dstAlpha);
        GL.GetInteger(GetPName.BlendEquationAlpha, out var blendEquation);

        return new GLSettings
        {
            ProgramId = activeProgram,
            TextureId = activeTexture,
            ScissorBox = scissorBox,
            BlendIsActive = GL.IsEnabled(EnableCap.Blend),
            ScissorIsActive = GL.IsEnabled(EnableCap.ScissorTest),
            CullFaceIsActive = GL.IsEnabled(EnableCap.CullFace),
            DepthTestIsActive = GL.IsEnabled(EnableCap.DepthTest),
            StencilTestIsActive = GL.IsEnabled(EnableCap.StencilTest),
            BlendEquation = (BlendEquationMode)blendEquation,
            BlendFactorSrcAlpha = (BlendingFactor)srcAlpha,
            BlendFactorDstAlpha = (BlendingFactor)dstAlpha
        };
    }

    public static void RestoreGLSettings(in GLSettings settings)
    {
        GL.UseProgram(settings.ProgramId);
        GL.BindTexture(TextureTarget.Texture2D, settings.TextureId);
        GL.BlendFunc(settings.BlendFactorSrcAlpha, settings.BlendFactorDstAlpha);
        GL.BlendEquation(settings.BlendEquation);
        
        GL.Scissor(settings.ScissorBox[0], settings.ScissorBox[1], settings.ScissorBox[2], settings.ScissorBox[3]);
        
        if (settings.BlendIsActive) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
        if (settings.ScissorIsActive) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);
        if (settings.CullFaceIsActive) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        if (settings.DepthTestIsActive) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
        if (settings.StencilTestIsActive) GL.Enable(EnableCap.StencilTest); else GL.Disable(EnableCap.StencilTest);
    }
}