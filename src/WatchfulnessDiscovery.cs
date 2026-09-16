using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace rfmechanics;

internal sealed partial class WatchfulnessRenderer
{
    private readonly WatchfulnessShapeRenderer shapes;
    private sealed class Glimpse
    {
        internal Sample Source = null!;
        internal double Born, FadeAt = -1;
        internal bool Traced, Submitted, PeakLogged;
    }
    private readonly List<Glimpse> glimpses = new(2);
    private bool glimpseRequested;
    private double previewDeadline;
    private double focusAt;
    private bool Zooming => api.Input.MouseGrabbed && self?.GetBehavior<RFElfZoomBehavior>()?.IsObserving == true;
    private bool Tracing => now <= traceUntil || RFMechanicsModSystem.Config?.WatchfulnessDiagnostics == true;
    private void StartTrace() { traceUntil = api.World.ElapsedMilliseconds / 1000.0 + 6; traceBudget = 160; }
    private void Trace(string message)
    {
        if (!Tracing || traceBudget-- <= 0) return;
        api.Logger.Notification("[rfmechanics] Watchfulness trace: {0}", message);
    }
    internal TextCommandResult RequestPreview(bool shape)
    {
        if (failed || !stance.Active) return TextCommandResult.Error("Enable elf Watchfulness first; renderer must be available.");
        StartTrace();
        previewDeadline = api.World.ElapsedMilliseconds / 1000.0 + 10;
        if (shape) glimpseRequested = true; else previewRequested = true;
        return TextCommandResult.Success(shape
            ? "GLIMPSE PREVIEW armed for 10s: close chat, aim centrally and hold racial zoom. Skips observation once; living/focus/terrain checks remain. Not normal discovery validation."
            : "AWARENESS PREVIEW armed for 10s: close chat without zoom for synthetic wisps at 10, 25 and 38 metres. Terrain still blocks; not a detection test.");
    }
    private void PreviewAwareness(float[] view)
    {
        cues.Clear();
        double[] distances = { 10, 25, 38 };
        for (int i = 0; i < distances.Length; i++)
        {
            double slope = (i-1) * 0.45 / projection[0];
            double z = -distances[i]/Math.Sqrt(1+slope*slope), x = slope * -z;
            var cue = new Cue { Born = now, Traced = true };
            cue.Position.Set(camera.X + view[0]*x + view[2]*z,
                camera.Y + view[4]*x + view[6]*z, camera.Z + view[8]*x + view[10]*z);
            cues.Add(cue);
            Trace($"synthetic awareness emitted slot={i+1} distance={distances[i]}m (not normal detection)");
        }
    }
    private double AwarenessHalfSize(Vec3d p, float[] view)
    {
        double z = view[2]*(p.X-origin.X)+view[6]*(p.Y-origin.Y)+view[10]*(p.Z-origin.Z)+view[14];
        // 1.3-block base diameter, at least 28 horizontal framebuffer pixels at normal range.
        return Math.Max(0.65, 28 * Math.Max(0.01, -z) / Math.Max(1, projection[0]*api.Render.FrameWidth));
    }
    private bool Attention(Vec3d p, float[] view, bool grace = false)
    {
        double x=p.X-origin.X, y=p.Y-origin.Y, z=p.Z-origin.Z;
        double vx=view[0]*x+view[4]*y+view[8]*z+view[12];
        double vy=view[1]*x+view[5]*y+view[9]*z+view[13];
        double vz=view[2]*x+view[6]*y+view[10]*z+view[14];
        double w=projection[11]*vz+projection[15];
        if (w <= 0) return false;
        double nx=projection[0]*vx/w/(grace ? 0.62 : 0.48);
        double ny=projection[5]*vy/w/(grace ? 0.68 : 0.55);
        return nx*nx+ny*ny <= 1;
    }
    private void TargetCentre(Sample sample)
    {
        Position(sample.Entity, point);
        point.Y += (sample.Entity.SelectionBox.Y1 + sample.Entity.SelectionBox.Y2) * 0.5;
    }
    private void SampleFocus(RFMechanicsConfig cfg, float[] view)
    {
        double dt = Math.Clamp(now-focusAt, 0, 0.15); focusAt = now;
        if(glimpseRequested && now>previewDeadline) {glimpseRequested=false;Trace("glimpse preview expired before zoom");}
        bool preview = glimpseRequested && Zooming && api.Input.MouseGrabbed;
        if(preview) {glimpseRequested=false;StartTrace();}
        int checkedTargets = 0, added = 0;
        double required = Setting(cfg.WatchfulnessObservationSeconds, 2, 0.5, 10);
        foreach (var sample in samples.Values)
        {
            if (!Zooming || !Valid(sample.Entity)) { sample.Observation = 0; continue; }
            if (glimpses.Exists(g => ReferenceEquals(g.Source, sample))) { sample.Observation = 0; continue; }
            TargetCentre(sample);
            bool focused = Attention(point, view) && point.SquareDistanceTo(body) <= radius*radius;
            if (!focused || checkedTargets >= 8)
            {
                // Two seconds of stored progress expires within one second away; no indefinite bank.
                sample.Observation = Math.Max(0, sample.Observation - dt*2);
                continue;
            }
            checkedTargets++;
            if (!ClearRay(point.X, point.Y, point.Z)) { sample.Observation = 0; continue; }
            sample.Observation += dt;
            if ((!preview && sample.Observation < required) || glimpses.Count >= 2) continue;
            sample.Observation = 0;
            if (!shapes.CanDraw(sample.Entity)) { Trace("discovery unavailable: renderer/mesh/animation unsupported or not ready"); continue; }
            if (!ShapeVisible(sample, out _, out _, out _)) { Trace("discovery withheld: solid viewing volume or occlusion budget"); continue; }
            var glimpse = new Glimpse { Source = sample, Born = now, Traced = Tracing };
            glimpses.Add(glimpse); added++;
            if (glimpse.Traced) Trace($"glimpse emitted preview={preview}, observation={required:0.0}s, distance={point.DistanceTo(body):0.0}m");
        }
        if (preview)
        {
            string result=$"GLIMPSE PREVIEW: {added} emitted, {checkedTargets} focused candidates checked; retained/submitted evidence is in client log. No normal observation validation.";
            api.ShowChatMessage(result); Trace(result);
        }
    }
    private bool ShapeVisible(Sample sample, out Vec3f clipMin, out Vec3f clipMax, out string reason)
    {
        var e=sample.Entity; var box=e.SelectionBox;
        // Shader clips geometry to this same box. This makes the occlusion proof independent
        // of animations, tails or modded meshes extending beyond their selection bounds.
        double minX=e.Pos.X+box.X1-0.25, maxX=e.Pos.X+box.X2+0.25;
        double minY=e.Pos.InternalY+box.Y1+0.06, maxY=e.Pos.InternalY+box.Y2+0.25;
        double minZ=e.Pos.Z+box.Z1-0.25, maxZ=e.Pos.Z+box.Z2+0.25;
        clipMin=new Vec3f((float)(minX-origin.X),(float)(minY-origin.Y),(float)(minZ-origin.Z));
        clipMax=new Vec3f((float)(maxX-origin.X),(float)(maxY-origin.Y),(float)(maxZ-origin.Z));
        return ClearVolume(minX-0.002,minY-0.002,minZ-0.002,maxX+0.002,maxY+0.002,maxZ+0.002, out reason);
    }
    private void DrawGlimpses(float[] view, float dt)
    {
        for (int i=glimpses.Count-1; i>=0; i--)
        {
            var g=glimpses[i]; var e=g.Source.Entity;
            TargetCentre(g.Source);
            string? reason = !Valid(e) ? "invalid/dead/despawned" : null;
            if (g.Source.Position.SquareDistanceTo(e.Pos.X,e.Pos.InternalY,e.Pos.Z)>4) reason="teleport";
            if (point.SquareDistanceTo(body)>radius*radius) reason="range";
            if (now-g.Born>=0.75) reason="expired";
            if (!Zooming || !Attention(point,view,true)) { if(g.FadeAt<0) g.FadeAt=now; }
            if (g.FadeAt>=0 && now-g.FadeAt>=0.12) reason="attention fade";
            if (!OnScreen(point,view)) reason="off-screen";
            Vec3f min=new(), max=new();
            if (reason == null && !ShapeVisible(g.Source,out min,out max,out string blocked)) reason=blocked;
            if (reason != null)
            {
                if(g.Traced) Trace($"glimpse retired={reason}, submitted={g.Submitted}, age={now-g.Born:0.000}s");
                glimpses.RemoveAt(i); g.Source.Observation=0; continue;
            }
            float alpha=(float)(0.65 * Math.Min(1,(now-g.Born)/0.08) * Math.Min(1,(0.75-(now-g.Born))/0.15));
            if(g.FadeAt>=0) alpha *= (float)Math.Clamp(1-(now-g.FadeAt)/0.12,0,1);
            bool submitted=shapes.Draw(e,view,projection,min,max,alpha);
            if(g.Traced && (!g.Submitted || (!g.PeakLogged && now-g.Born>=0.2)))
            {
                Trace($"glimpse retained -> submitted={submitted}, alpha={alpha:0.000}, {ProjectedBounds(min,max,view)}, geometry=animated entity mesh, framebuffer visibility unverified");
                if(now-g.Born>=0.2) g.PeakLogged=true;
            }
            g.Submitted |= submitted;
        }
    }
    private string ProjectedBounds(Vec3f min,Vec3f max,float[] view)
    {
        double left=1e9,right=-1e9,bottom=1e9,top=-1e9;
        for(int c=0;c<8;c++)
        {
            double x=(c&1)==0?min.X:max.X,y=(c&2)==0?min.Y:max.Y,z=(c&4)==0?min.Z:max.Z;
            double vx=view[0]*x+view[4]*y+view[8]*z+view[12];
            double vy=view[1]*x+view[5]*y+view[9]*z+view[13];
            double vz=view[2]*x+view[6]*y+view[10]*z+view[14];
            double w=projection[11]*vz+projection[15];
            if(w<=0) return "clip volume crosses camera";
            double nx=projection[0]*vx/w,ny=projection[5]*vy/w;
            left=Math.Min(left,nx);right=Math.Max(right,nx);bottom=Math.Min(bottom,ny);top=Math.Max(top,ny);
        }
        return $"clipCentreNdc=({(left+right)/2:0.000},{(bottom+top)/2:0.000}), clipSize=({(right-left)*api.Render.FrameWidth/2:0.0},{(top-bottom)*api.Render.FrameHeight/2:0.0})px";
    }
}
