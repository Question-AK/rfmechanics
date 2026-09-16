using System;
using OpenTK.Graphics.OpenGL4;
using Vintagestory.API.Client;

namespace rfmechanics;

// Capture before mesh upload/update as well as drawing. Restore actual incoming state,
// not assumed defaults; in particular the UI may need blending left enabled.
internal sealed class WatchfulnessRenderState : IDisposable
{
    private readonly bool blend = GL.IsEnabled(IndexedEnableCap.Blend, 0);
    private readonly bool depth = GL.IsEnabled(EnableCap.DepthTest);
    private readonly bool depthWrite = GL.GetBoolean(GetPName.DepthWritemask);
    private readonly int program = GL.GetInteger(GetPName.CurrentProgram);
    private readonly int vao = GL.GetInteger(GetPName.VertexArrayBinding);
    private readonly int buffer = GL.GetInteger(GetPName.ArrayBufferBinding);
    private readonly int elements = GL.GetInteger(GetPName.ElementArrayBufferBinding);
    private readonly int srcRgb = Indexed(0x80C9), dstRgb = Indexed(0x80C8);
    private readonly int srcAlpha = Indexed(0x80CB), dstAlpha = Indexed(0x80CA);
    private readonly int equationRgb = Indexed(0x8009), equationAlpha = Indexed(0x883D);
    private readonly bool cull = GL.IsEnabled(EnableCap.CullFace);
    private readonly int activeTexture = GL.GetInteger(GetPName.ActiveTexture);
    private readonly int texture0, sampler0;
    private readonly int uniformBuffer = GL.GetInteger(GetPName.UniformBufferBinding);
    private readonly int uniform0, uniformStart0, uniformSize0;

    private static int Indexed(int name)
    {
        GL.GetInteger((GetIndexedPName)name, 0, out int value);
        return value;
    }
    internal WatchfulnessRenderState(ICoreClientAPI api)
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        texture0=GL.GetInteger(GetPName.TextureBinding2D);
        sampler0=GL.GetInteger((GetPName)0x8919); // GL_SAMPLER_BINDING for active texture unit zero
        GL.GetInteger(GetIndexedPName.UniformBufferBinding,0,out uniform0);
        GL.GetInteger(GetIndexedPName.UniformBufferStart,0,out uniformStart0);
        GL.GetInteger(GetIndexedPName.UniformBufferSize,0,out uniformSize0);
        GL.ActiveTexture((TextureUnit)activeTexture);
    }
    internal static void BeginCue()
    {
        // Change only draw buffer zero; don't alter other buffers' blend state via the
        // engine's global GlToggleBlend helper (which also changes SSAO blend factors).
        GL.Enable(IndexedEnableCap.Blend, 0);
        GL.BlendEquationSeparate(0, BlendEquationMode.FuncAdd, BlendEquationMode.FuncAdd);
        GL.BlendFuncSeparate(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha,
            BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        GL.Disable(EnableCap.DepthTest); GL.DepthMask(false);
        GL.Disable(EnableCap.CullFace);
    }
    public void Dispose()
    {
        GL.UseProgram(program);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, elements);
        GL.BlendEquationSeparate(0, (BlendEquationMode)equationRgb, (BlendEquationMode)equationAlpha);
        GL.BlendFuncSeparate(0, (BlendingFactorSrc)srcRgb, (BlendingFactorDest)dstRgb,
            (BlendingFactorSrc)srcAlpha, (BlendingFactorDest)dstAlpha);
        if (blend) GL.Enable(IndexedEnableCap.Blend, 0); else GL.Disable(IndexedEnableCap.Blend, 0);
        if (depth) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(depthWrite);
        if(cull) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D,texture0);
        GL.BindSampler(0,sampler0); GL.ActiveTexture((TextureUnit)activeTexture);
        if(uniform0!=0 && uniformSize0>0)
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer,0,uniform0,(IntPtr)uniformStart0,uniformSize0);
        else GL.BindBufferBase(BufferRangeTarget.UniformBuffer,0,uniform0);
        GL.BindBuffer(BufferTarget.UniformBuffer,uniformBuffer);
    }
}
