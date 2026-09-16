using System;
using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics;

// Borrow the renderer's current GPU mesh and pose read-only. Do not retessellate,
// call entity render callbacks a second time, or mutate shared materials/animation state.
internal sealed class WatchfulnessShapeRenderer : IDisposable
{
    private const int JointCap = 230;
    private static readonly FieldInfo? MeshField = typeof(EntityShapeRenderer).GetField("meshRefOpaque",BindingFlags.Instance|BindingFlags.NonPublic);
    private readonly ICoreClientAPI api;
    private IShaderProgram? shader;
    private int animationBuffer;
    private bool ready;
    internal WatchfulnessShapeRenderer(ICoreClientAPI api)
    {
        this.api=api; api.Event.ReloadShader+=Load; Load();
    }
    private bool Load()
    {
        using var state=new WatchfulnessRenderState(api);
        shader?.Dispose();
        shader=api.Shader.NewShaderProgram();
        shader.VertexShader=api.Shader.NewShader(EnumShaderType.VertexShader);
        shader.FragmentShader=api.Shader.NewShader(EnumShaderType.FragmentShader);
        shader.AssetDomain="rfmechanics";
        api.Shader.RegisterFileShaderProgram("rfwatchglimpse",shader);
        ready=shader.Compile();
        if(!ready) return false;
        if(animationBuffer==0) animationBuffer=GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer,animationBuffer);
        GL.BufferData(BufferTarget.UniformBuffer,JointCap*16*4,IntPtr.Zero,BufferUsageHint.DynamicDraw);
        int block=GL.GetUniformBlockIndex(shader.ProgramId,"WatchAnimation");
        if(block<0) {ready=false;return false;}
        GL.UniformBlockBinding(shader.ProgramId,block,0);
        return true;
    }
    internal bool CanDraw(Entity e)
    {
        var animator=e.AnimManager?.Animator;
        return ready && e.IsRendered && e.Properties.Client.Renderer is EntityShapeRenderer renderer
            && MeshField?.GetValue(renderer) is MultiTextureMeshRef mesh && mesh.Initialized && !mesh.Disposed
            && animator!=null && animator.MaxJointId>0 && animator.MaxJointId<=JointCap
            && animator.Matrices.Length>=animator.MaxJointId*16;
    }
    internal bool Draw(Entity e,float[] view,float[] projection,Vec3f min,Vec3f max,float alpha)
    {
        if(!CanDraw(e) || shader==null) return false;
        var renderer=(EntityShapeRenderer)e.Properties.Client.Renderer;
        var mesh=(MultiTextureMeshRef)MeshField!.GetValue(renderer)!;
        var animator=e.AnimManager.Animator;
        using var state=new WatchfulnessRenderState(api);
        var previous=api.Render.CurrentActiveShader;
        previous?.Stop();
        try
        {
            shader.Use();
            WatchfulnessRenderState.BeginCue();
            GL.Disable(EnableCap.CullFace);
            GL.BindSampler(0,0);
            GL.BindBuffer(BufferTarget.UniformBuffer,animationBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer,IntPtr.Zero,animator.MaxJointId*16*4,animator.Matrices);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer,0,animationBuffer);
            shader.UniformMatrix("projectionMatrix",projection);
            shader.UniformMatrix("viewMatrix",view);
            shader.UniformMatrix("modelMatrix",renderer.ModelMat);
            shader.Uniform("clipMin",min);shader.Uniform("clipMax",max);
            shader.Uniform("glimpseAlpha",alpha);
            shader.Uniform("jointCount",animator.MaxJointId);
            for(int i=0;i<mesh.meshrefs.Length;i++)
            {
                if(mesh.meshrefs[i].Disposed || !mesh.meshrefs[i].Initialized) continue;
                shader.BindTexture2D("entityTex",mesh.textureids[i],0);
                api.Render.RenderMesh(mesh.meshrefs[i]);
            }
            return true;
        }
        finally {shader.Stop();previous?.Use();}
    }
    public void Dispose()
    {
        api.Event.ReloadShader-=Load;shader?.Dispose();
        if(animationBuffer!=0) GL.DeleteBuffer(animationBuffer);
        animationBuffer=0;ready=false;
    }
}
