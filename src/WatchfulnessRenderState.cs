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

    private static int Indexed(int name)
    {
        GL.GetInteger((GetIndexedPName)name, 0, out int value);
        return value;
    }
    internal WatchfulnessRenderState(ICoreClientAPI api) { }
    internal static void BeginCue()
    {
        // Change only draw buffer zero; don't alter other buffers' blend state via the
        // engine's global GlToggleBlend helper (which also changes SSAO blend factors).
        GL.Enable(IndexedEnableCap.Blend, 0);
        GL.BlendEquationSeparate(0, BlendEquationMode.FuncAdd, BlendEquationMode.FuncAdd);
        GL.BlendFuncSeparate(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha,
            BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        GL.Disable(EnableCap.DepthTest); GL.DepthMask(false);
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
    }
}
