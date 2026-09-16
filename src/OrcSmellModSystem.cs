using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics;

public sealed class OrcSmellModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private OrcSmellRenderer? renderer;
    private OrcHuntModSystem? hunt;
    private EntityPartitioning? partitions;
    private long tick;
    private double scanAt, whiffAt, whiffUntil;
    private bool wasEmitting, failed;
    private long playerId;
    private int dimension;
    private Vec3d? previous;
    private readonly List<Entity> sources = new();
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api; renderer = new(api);
        hunt = api.ModLoader.GetModSystem<OrcHuntModSystem>();
        partitions = api.ModLoader.GetModSystem<EntityPartitioning>();
        tick = api.Event.RegisterGameTickListener(Tick, 50);
        api.Event.LeaveWorld += Clear;
    }
    private void Clear()
    {
        sources.Clear(); renderer?.Clear(); previous = null; playerId = 0;
        scanAt = whiffAt = whiffUntil = 0; wasEmitting = false;
        OrcSmellShared.SensoryActive = false;
    }
    private void Tick(float dt)
    {
        if (failed || capi == null || renderer == null || hunt == null) return;
        var cfg = RFMechanicsModSystem.Config;
        var self = capi.World.Player?.Entity;
        if (cfg?.SmellEnabled != true || !cfg.EnableOrcHunting || self?.Alive != true
            || self.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Orc)
        { Clear(); hunt.ClearClient(); return; }
        if (capi.IsGamePaused) return;
        try
        {
            double now = capi.World.ElapsedMilliseconds / 1000.0;
            if (playerId != self.EntityId || dimension != self.Pos.Dimension
                || previous != null && self.Pos.SquareDistanceTo(previous) > 4 || dt > 0.5)
            { Clear(); }
            playerId = self.EntityId; dimension = self.Pos.Dimension; previous = self.Pos.XYZ;
            bool held = OrcSmellShared.FocusActive && OrcSmellShared.HeldMs >= Math.Clamp(cfg.OrcQuickSniffMs, 0, 1000);
            if (hunt.Stance && now >= whiffAt)
            {
                whiffAt = now + Math.Clamp(cfg.OrcWhiffIntervalSeconds, 1, 15) * (0.85 + capi.World.Rand.NextDouble()*0.3);
                whiffUntil = now + Math.Clamp(cfg.OrcWhiffDurationSeconds, 0.2, 2);
            }
            bool passive = hunt.Stance && now < whiffUntil;
            bool emit = held || passive || hunt.HasBlood;
            OrcSmellShared.SensoryActive = held || hunt.Stance || hunt.HasBlood;
            if (!emit) { wasEmitting = false; return; }
            if (!wasEmitting) scanAt = 0;
            wasEmitting = true;
            double quality = held ? OrcSmellShared.Quality : 0;
            double range = held ? Math.Clamp(cfg.OrcQuickRange, 4, 64) +
                (Math.Clamp(cfg.OrcDeepRange, 4, 64) - Math.Clamp(cfg.OrcQuickRange, 4, 64))*quality : Math.Clamp(cfg.OrcPassiveRange, 4, 64);
            if (now >= scanAt)
            {
                scanAt = now + 0.5; sources.Clear(); int visits = 0;
                double scanRange = Math.Min(64, Math.Max(range * 1.2, cfg.OrcBloodRange));
                partitions!.WalkEntities(self.Pos.X, self.Pos.Y, self.Pos.Z, scanRange, e =>
                {
                    if (++visits > 256) return false;
                    if (sources.Count < 64 && Eligible(e, self) && (hunt.IsBlood(e) || (held || passive) && InRange(e, self, range))) sources.Add(e);
                    return true;
                }, null, EnumEntitySearchType.Creatures);
                sources.Sort((a,b) => {
                    int bloodOrder = hunt.IsBlood(b).CompareTo(hunt.IsBlood(a));
                    return bloodOrder != 0 ? bloodOrder : a.Pos.SquareDistanceTo(self.Pos).CompareTo(b.Pos.SquareDistanceTo(self.Pos));
                });
                int count = Math.Clamp(cfg.SmellMaxSources, 1, 6);
                if (sources.Count > count) sources.RemoveRange(count, sources.Count-count);
            }
            Vec3d eye = self.Pos.XYZ.Add(self.LocalEyePos.X, self.LocalEyePos.Y, self.LocalEyePos.Z);
            foreach (var e in sources)
            {
                bool blood = hunt.IsBlood(e) && (!cfg.OrcTargetSwimmingBreaksBlood || !e.Swimming);
                if (!Eligible(e, self) || (!blood && (!(held || passive) || !InRange(e, self, range)))) continue;
                ScentCategory category = blood ? ScentCategory.Blood : e is EntityPlayer ? ScentCategory.Player : OrcSmellClassifier.Classify(e, cfg);
                Emit(cfg, eye, e, category, quality, passive && !held);
            }
        }
        catch (Exception e)
        {
            failed = true; Clear(); capi.Logger.Error("[rfmechanics] Orc hunting scent disabled: {0}", e);
        }
    }
    private static bool Eligible(Entity e, EntityPlayer self) => e != self && e.Alive
        && ReferenceEquals(self.World.GetEntityById(e.EntityId), e) && e.Pos.Dimension == self.Pos.Dimension
        && (e is EntityPlayer || OrcSmellClassifier.IsSmellableFauna(e));
    private static bool InRange(Entity e, EntityPlayer self, double range)
    {
        double size = Math.Clamp(OrcSmellVisuals.BodyScale(e.CollisionBox.XSize, e.CollisionBox.YSize, 1), 0.7, 1.2);
        double radius = Math.Min(64, range * size);
        return e.Pos.SquareDistanceTo(self.Pos) <= radius * radius;
    }
    private void Emit(RFMechanicsConfig cfg, Vec3d eye, Entity e, ScentCategory category, double quality, bool passive)
    {
        bool blood = category == ScentCategory.Blood;
        double dx = e.Pos.X-eye.X, dz = e.Pos.Z-eye.Z;
        double distance = Math.Sqrt(dx*dx+dz*dz), bearing = Math.Atan2(dz, dx);
        double size = OrcSmellVisuals.BodyScale(e.CollisionBox.XSize, e.CollisionBox.YSize, cfg.SmellVisualSizeExponent);
        float particleSize = OrcSmellVisuals.ParticleSize(size, cfg);
        if (blood) particleSize = Math.Max(0.13f, particleSize);
        double blow = Math.Clamp((20-distance)/5, 0, 1);
        double spread = blood ? 8 : (15 + 285*blow) * (1 + 0.3*(1-quality));
        int[] rgb = category switch {
            ScentCategory.Blood => new[] { 175, 32, 48 },
            ScentCategory.Player => new[] { 170, 174, 205 },
            ScentCategory.Predator => cfg.SmellColorPredator,
            ScentCategory.Herbivore => cfg.SmellColorHerbivore,
            ScentCategory.Omnivore => cfg.SmellColorOmnivore,
            _ => cfg.SmellColorUnknown
        };
        int count = Math.Clamp((int)Math.Round(2 + size * (blood ? 4 : passive ? 1 : 2)), 2, 8);
        Random rand = capi!.World.Rand;
        for (int i=0; i<count; i++)
        {
            double angle = bearing + (rand.NextDouble()-0.5)*spread*GameMath.DEG2RAD_DOUBLE;
            // Blood remains a directional stream at close range, not a body marker.
            double t = blood ? 2.2 + rand.NextDouble()*3.5 : 2.5 + rand.NextDouble()*(passive ? 4 : 8);
            double vOff = (rand.NextDouble()-0.5)*(blood ? 0.25 : 0.8);
            var position = new Vec3d(eye.X+Math.Cos(angle)*t, eye.Y+vOff, eye.Z+Math.Sin(angle)*t);
            double speed = blood ? 3 : Math.Clamp(cfg.SmellDriftSpeed, 0.5, 4);
            // Match the runner's approach speed so whiffs survive sprinting instead of
            // being swallowed by the moving body-exclusion volume on their first frame.
            var self = capi.World.Player.Entity;
            speed += self.Controls.Sprint ? 2 : 0;
            renderer!.Add(e.EntityId, sources.Count, position, angle, speed, blood ? 0.7 : passive ? 1.1 : 1.8,
                particleSize, blood ? 0.7f : passive ? 0.48f : 0.55f, rgb, category);
        }
    }
    public override void Dispose()
    {
        if (capi != null) { capi.Event.UnregisterGameTickListener(tick); capi.Event.LeaveWorld -= Clear; }
        Clear(); renderer?.Dispose(); base.Dispose();
    }
}
