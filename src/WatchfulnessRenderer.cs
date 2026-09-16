using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics;

internal sealed class WatchfulnessRenderer : IRenderer
{
    private const int CandidateCap = 64, QueryBudget = 256, CueCap = 8;
    private const double Life = 0.8, HalfSize = 0.22;
    private sealed class Sample
    {
        internal Entity Entity = null!;
        internal readonly Vec3d Position = new();
        internal double Time, NextCue;
        internal int Generation;
        internal bool WasOnScreen;
    }
    private sealed class Cue
    {
        internal Sample Source = null!;
        internal readonly Vec3d Position = new();
        internal double Born;
    }
    private readonly ICoreClientAPI api;
    private readonly ElfWatchfulnessModSystem stance;
    private readonly EntityPartitioning partitions;
    private readonly Dictionary<long, Sample> samples = new(CandidateCap);
    private readonly List<long> remove = new(CandidateCap);
    private readonly List<Cue> cues = new(CueCap);
    private readonly MeshData mesh = new(CandidateCap * 4, CandidateCap * 6, false, true, true, false);
    private readonly Vec3d origin = new(), camera = new(), body = new(), lastBody = new(), point = new();
    private readonly BlockPos blockPos = new(0);
    private readonly Vintagestory.API.Common.ActionConsumable<Entity> visitor;
    private MeshRef? meshRef;
    private IShaderProgram? shader;
    private bool failed, haveBody;
    private bool testRequested;
    private int dimension, generation, visited, cursor, checks, emitted;
    private double now, sampleAt, refreshAt, logAt, radius;
    private EntityPlayer? self;
    public double RenderOrder => 0.07;
    public int RenderRange => 64;

    internal WatchfulnessRenderer(ICoreClientAPI api, ElfWatchfulnessModSystem stance)
    {
        this.api = api; this.stance = stance;
        partitions = api.ModLoader.GetModSystem<EntityPartitioning>();
        visitor = Visit;
        api.Event.ReloadShader += LoadShader;
        LoadShader();
        api.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, "rfwatchfulness");
    }
    private bool LoadShader()
    {
        shader?.Dispose();
        shader = api.Shader.NewShaderProgram();
        shader.VertexShader = api.Shader.NewShader(EnumShaderType.VertexShader);
        shader.FragmentShader = api.Shader.NewShader(EnumShaderType.FragmentShader);
        shader.AssetDomain = "rfmechanics";
        // Reuse only the soft radial billboard shader, never scent's moving particle lifecycle.
        api.Shader.RegisterFileShaderProgram("rfwatchfulness", shader);
        failed = !shader.Compile();
        return !failed;
    }
    internal void Clear()
    {
        testRequested = false;
        samples.Clear(); cues.Clear(); remove.Clear(); haveBody = false;
        sampleAt = refreshAt = 0;
    }
    internal TextCommandResult RequestTest()
    {
        if (failed) return TextCommandResult.Error("Watchfulness renderer unavailable; check the client log.");
        if (!stance.Active || api.World.Player?.Entity?.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Elf)
            return TextCommandResult.Error("Enable elf Watchfulness first (Ctrl+H or your stance binding).");
        testRequested = true;
        return TextCommandResult.Success("Testing on the next movement sample: up to 64 tracked visible targets, 5–40 blocks at default range. Cooldowns bypassed once.");
    }
    private static double Setting(double value, double fallback, double min, double max)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private static void Position(Entity e, Vec3d to) => to.Set(e.Pos.X, e.Pos.InternalY, e.Pos.Z);
    private bool Valid(Entity e) => e != self && e.Alive && !e.ShouldDespawn
        && e.Pos.Dimension == dimension && api.World.LoadedEntities.TryGetValue(e.EntityId, out var loaded)
        && ReferenceEquals(e, loaded);
    private bool Visit(Entity e)
    {
        visited++;
        if (Valid(e) && (e is EntityPlayer || OrcSmellClassifier.IsSmellableFauna(e)))
        {
            Position(e, point);
            if (point.SquareDistanceTo(body) <= radius * radius)
            {
                if (!samples.TryGetValue(e.EntityId, out var sample) && samples.Count < CandidateCap)
                {
                    sample = new Sample { Entity = e, Time = now, NextCue = now + api.World.Rand.NextDouble() * 1.2 };
                    sample.Position.Set(point);
                    samples.Add(e.EntityId, sample);
                }
                if (sample != null) sample.Generation = generation;
            }
        }
        return visited < QueryBudget;
    }
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (failed) return;
        try
        {
            var cfg = RFMechanicsModSystem.Config;
            self = api.World.Player?.Entity;
            if (cfg?.EnableElfWatchfulness != true || !stance.Active || self?.Alive != true
                || self.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Elf || api.IsGamePaused)
            { Clear(); return; }
            now = api.World.ElapsedMilliseconds / 1000.0;
            Position(self, body);
            if (haveBody && (dimension != self.Pos.Dimension || body.SquareDistanceTo(lastBody) > 4 || deltaTime > 0.5)) Clear();
            dimension = self.Pos.Dimension; lastBody.Set(body); haveBody = true;
            radius = Setting(cfg.WatchfulnessRadius, 40, 10, 64);
            origin.Set(self.CameraPos);
            float[] view = api.Render.CameraMatrixOriginf;
            camera.Set(origin.X - (view[0]*view[12] + view[1]*view[13] + view[2]*view[14]),
                origin.Y - (view[4]*view[12] + view[5]*view[13] + view[6]*view[14]),
                origin.Z - (view[8]*view[12] + view[9]*view[13] + view[10]*view[14]));
            if (now >= refreshAt)
            {
                refreshAt = now + 1; generation++; visited = 0;
                // The no-range-test overload invokes our callback for EVERY visited creature,
                // not just matches. Returning false bounds query traversal itself at 256.
                // Radius <=64 also bounds empty partition visits. Reuse the game's spatial index.
                partitions.WalkEntities(body.X, body.Y, body.Z, radius, visitor, null, EnumEntitySearchType.Creatures);
                remove.Clear();
                foreach (var pair in samples)
                    if (pair.Value.Generation != generation || !Valid(pair.Value.Entity)) remove.Add(pair.Key);
                foreach (long id in remove) samples.Remove(id);
            }
            if (now >= sampleAt)
            {
                sampleAt = now + 0.1;
                SampleMovement(cfg, view);
            }
            // Retire, don't hide: leaving the frustum or losing visibility must never queue a cue.
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                Cue cue = cues[i];
                Position(cue.Source.Entity, point);
                if (now - cue.Born >= Life || !Valid(cue.Source.Entity)
                    || point.SquareDistanceTo(cue.Source.Position) > 4
                    || !OnScreen(cue.Position, view) || !Visible(cue.Position, view)) cues.RemoveAt(i);
            }
            Draw(view);
            if (cfg.WatchfulnessDiagnostics && now >= logAt)
            {
                logAt = now + 5;
                api.Logger.Notification("[rfmechanics] Watchfulness candidates={0}, queryVisits={1}/256, live={2}/8, visibilityChecks={3}, emitted={4} (5s)", samples.Count, visited, cues.Count, checks, emitted);
                checks = emitted = 0;
            }
        }
        catch (Exception e)
        {
            failed = true; Clear();
            api.Logger.Error("[rfmechanics] Watchfulness renderer disabled: {0}", e);
        }
    }
    private void SampleMovement(RFMechanicsConfig cfg, float[] view)
    {
        bool test = testRequested;
        testRequested = false;
        if (test) cues.Clear();
        int added = 0;
        double minimum = Setting(cfg.WatchfulnessMinimumSpeed, 0.2, 0.05, 5);
        double cooldownMin = Setting(cfg.WatchfulnessCooldownMinimumSeconds, 5, 5, 60);
        double cooldownMax = Setting(cfg.WatchfulnessCooldownMaximumSeconds, 15, cooldownMin, 120);
        int index = 0, rayBudget = test ? CandidateCap : 8;
        // Rotate priority so the first moving target does not own the visibility budget.
        remove.Clear(); foreach (long id in samples.Keys) remove.Add(id);
        int count = remove.Count;
        for (int n = 0; n < count; n++)
        {
            index = (cursor + n) % count;
            Sample s = samples[remove[index]];
            if (!Valid(s.Entity)) continue;
            Position(s.Entity, point);
            double elapsed = now - s.Time, distance = point.DistanceTo(s.Position);
            bool discontinuity = elapsed > 0.5 || distance > 2 || (elapsed > 0 && distance / elapsed > 15);
            if (discontinuity)
            {
                s.NextCue = now + api.World.Rand.NextDouble() * 1.2; s.WasOnScreen = false;
                for (int c = cues.Count - 1; c >= 0; c--)
                    if (ReferenceEquals(cues[c].Source, s)) cues.RemoveAt(c);
            }
            bool motion = elapsed >= 0.05 && elapsed <= 0.5 && distance >= 0.025
                && distance <= 2 && distance / elapsed <= 15 && distance / elapsed >= minimum;
            // Always advance, including offscreen/blocked motion: no accumulated stale movement.
            s.Position.Set(point); s.Time = now;
            point.Y += Math.Clamp(s.Entity.SelectionBox.Y2 * 0.5, 0.2, 1.2);
            bool wasOnScreen = s.WasOnScreen;
            s.WasOnScreen = OnScreen(point, view);
            if (!motion || (!test && now < s.NextCue) || cues.Count >= (test ? CandidateCap : CueCap) || rayBudget <= 0
                || point.SquareDistanceTo(body) <= 25 || point.SquareDistanceTo(body) > radius * radius || !wasOnScreen || !s.WasOnScreen) continue;
            rayBudget--;
            if (!Visible(point, view)) continue;
            var cue = new Cue { Source = s, Born = now };
            cue.Position.Set(point); cues.Add(cue);
            added++;
            s.NextCue = now + cooldownMin + api.World.Rand.NextDouble() * (cooldownMax - cooldownMin); emitted++;
        }
        cursor = count == 0 ? 0 : (cursor + 8) % count;
        if (test) api.ShowChatMessage($"Watchfulness test: {added} moving visible targets cued simultaneously ({count} tracked; discovery capped at 256 visits). Nearby suppression and terrain blocking retained.");
    }
    private bool OnScreen(Vec3d p, float[] view)
    {
        double x = p.X-origin.X, y = p.Y-origin.Y, z = p.Z-origin.Z;
        double vx = view[0]*x+view[4]*y+view[8]*z+view[12];
        double vy = view[1]*x+view[5]*y+view[9]*z+view[13];
        double vz = view[2]*x+view[6]*y+view[10]*z+view[14];
        float[] projection = api.Render.CurrentProjectionMatrix;
        double w = projection[3]*vx+projection[7]*vy+projection[11]*vz+projection[15];
        return w > 0 && Math.Abs(projection[0]*vx+projection[4]*vy+projection[8]*vz+projection[12]) < w
            && Math.Abs(projection[1]*vx+projection[5]*vy+projection[9]*vz+projection[13]) < w;
    }
    private bool Visible(Vec3d p, float[] view)
    {
        checks++;
        if (!ClearRay(p.X, p.Y, p.Z)) return false;
        // Check the billboard corners as well as its center before disabling depth testing.
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
                if (!ClearRay(p.X+HalfSize*(a*view[0]+b*view[1]),
                    p.Y+HalfSize*(a*view[4]+b*view[5]), p.Z+HalfSize*(a*view[8]+b*view[9]))) return false;
        return true;
    }
    private bool ClearRay(double tx, double ty, double tz)
    {
        // Voxel DDA, not selection boxes or normal GPU occlusion. Conservative full occupied
        // voxel policy: air/leaves/plants/fluids/fire pass; all other materials block,
        // including glass, slabs, chisel blocks and unknown materials. Missing chunks block.
        double dx=tx-camera.X, dy=ty-camera.Y, dz=tz-camera.Z;
        int x=(int)Math.Floor(camera.X), y=(int)Math.Floor(camera.Y), z=(int)Math.Floor(camera.Z);
        int ex=(int)Math.Floor(tx), ey=(int)Math.Floor(ty), ez=(int)Math.Floor(tz);
        int sx=Math.Sign(dx), sy=Math.Sign(dy), sz=Math.Sign(dz);
        double ax=dx == 0 ? double.PositiveInfinity : Math.Abs(1/dx);
        double ay=dy == 0 ? double.PositiveInfinity : Math.Abs(1/dy);
        double az=dz == 0 ? double.PositiveInfinity : Math.Abs(1/dz);
        double nx=dx == 0 ? double.PositiveInfinity : (x+(sx>0?1:0)-camera.X)/dx;
        double ny=dy == 0 ? double.PositiveInfinity : (y+(sy>0?1:0)-camera.Y)/dy;
        double nz=dz == 0 ? double.PositiveInfinity : (z+(sz>0?1:0)-camera.Z)/dz;
        for (int step=0; step<192; step++)
        {
            blockPos.Set(x, y % BlockPos.DimensionBoundary, z); blockPos.dimension = dimension;
            var accessor = api.World.BlockAccessor;
            if (accessor.GetChunkAtBlockPos(blockPos) == null) return false;
            var block = accessor.GetBlock(blockPos, BlockLayersAccess.Solid);
            var material = block.BlockMaterial;
            if (block.Id != 0 && material != EnumBlockMaterial.Air && material != EnumBlockMaterial.Leaves
                && material != EnumBlockMaterial.Plant && material != EnumBlockMaterial.Water
                && material != EnumBlockMaterial.Lava && material != EnumBlockMaterial.Fire) return false;
            if (x==ex && y==ey && z==ez) return true;
            if (nx <= ny && nx <= nz) { x+=sx; nx+=ax; }
            else if (ny <= nz) { y+=sy; ny+=ay; }
            else { z+=sz; nz+=az; }
        }
        return false;
    }
    private void Draw(float[] view)
    {
        if (cues.Count == 0 || shader == null) return;
        using var state = new WatchfulnessRenderState(api);
        DrawCues(view);
    }
    private void DrawCues(float[] view)
    {
        if (cues.Count == 0 || shader == null) return;
        mesh.Clear();
        foreach (Cue cue in cues)
        {
            double age = (now-cue.Born)/Life;
            double distance = cue.Position.DistanceTo(body);
            double nearFade = Math.Clamp((distance - 5) / 7, 0, 1);
            double farFade = Math.Clamp((radius - distance) / (radius * 0.2), 0, 1);
            float alpha = (float)(0.34 * Math.Min(1, age / 0.1) * Math.Pow(1 - age, 1.4) * nearFade * farFade);
            double px=cue.Position.X-origin.X, py=cue.Position.Y-origin.Y, pz=cue.Position.Z-origin.Z;
            float x=(float)(view[0]*px+view[4]*py+view[8]*pz+view[12]);
            float y=(float)(view[1]*px+view[5]*py+view[9]*pz+view[13]);
            float z=(float)(view[2]*px+view[6]*py+view[10]*pz+view[14]);
            float h=(float)HalfSize;
            int color=OrcSmellVisuals.MeshColor(133, 148, 128, alpha), start=mesh.VerticesCount;
            mesh.AddVertex(x-h,y-h,z,0,0,color); mesh.AddVertex(x+h,y-h,z,1,0,color);
            mesh.AddVertex(x+h,y+h,z,1,1,color); mesh.AddVertex(x-h,y+h,z,0,1,color);
            mesh.AddIndex(start); mesh.AddIndex(start+1); mesh.AddIndex(start+2);
            mesh.AddIndex(start); mesh.AddIndex(start+2); mesh.AddIndex(start+3);
        }
        if (meshRef == null)
        {
            int v=mesh.VerticesCount, i=mesh.IndicesCount;
            mesh.VerticesCount=CandidateCap*4; mesh.IndicesCount=CandidateCap*6;
            meshRef=api.Render.UploadMesh(mesh); mesh.VerticesCount=v; mesh.IndicesCount=i;
        }
        api.Render.UpdateMesh(meshRef, mesh);
        WatchfulnessRenderState.BeginCue();
        // This shader has no engine includes, samplers or UBOs. Switch only the GL
        // program so the previous shader's Stop/Use cannot disturb its bindings.
        OpenTK.Graphics.OpenGL4.GL.UseProgram(shader.ProgramId);
        shader.UniformMatrix("projectionMatrix", api.Render.CurrentProjectionMatrix);
        api.Render.RenderMesh(meshRef);
    }
    public void Dispose()
    {
        api.Event.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
        api.Event.ReloadShader -= LoadShader; Clear(); meshRef?.Dispose(); shader?.Dispose();
    }
}
