using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace rfmechanics;

// Smoke means actual net reserve loss. No branch reads hunger, debt, Burn or Frenzy.
// All observers, including the local player, read the same server-owned loss rate.
public class OrcPuffModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private long tick;
    private bool disabled;
    private readonly Dictionary<long, float> accum = new();
    private readonly HashSet<long> seen = new();
    private readonly List<long> remove = new();
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        tick = api.Event.RegisterGameTickListener(Tick, Math.Clamp(RFMechanicsModSystem.Config?.PuffTickIntervalMs ?? 100, 50, 1000));
        api.Event.LeaveWorld += Clear;
    }
    private void Clear() { accum.Clear(); seen.Clear(); remove.Clear(); }
    private void Tick(float dt)
    {
        if (capi == null || disabled || capi.IsGamePaused) return;
        var cfg = RFMechanicsModSystem.Config;
        var self = capi.World.Player?.Entity;
        if (cfg?.EnablePuff != true || self == null) { Clear(); return; }
        try
        {
            float range = (float)OrcMetabolismFeedbackRules.Finite(cfg.PuffRenderRange, 32, 4, 64);
            seen.Clear();
            // Explicit local call: some entity partitions do not return the observing player.
            Consider(self, self, cfg, dt);
            foreach (var e in capi.World.GetEntitiesAround(self.Pos.XYZ, range, range, e => e is EntityPlayer && e.Alive))
            {
                if (e.EntityId == self.EntityId || seen.Count >= 32 || e.Pos.Dimension != self.Pos.Dimension) continue;
                Consider(e, self, cfg, dt);
            }
            remove.Clear();
            foreach (long id in accum.Keys) if (!seen.Contains(id)) remove.Add(id);
            foreach (long id in remove) accum.Remove(id);
        }
        catch (Exception e)
        {
            disabled = true; Clear();
            capi.Logger.Warning("[rfmechanics] Thew loss wisps disabled: {0}", e);
        }
    }
    private void Consider(Entity e, Entity self, RFMechanicsConfig cfg, float dt)
    {
        if (!e.Alive || e.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Orc) return;
        float rate = e.WatchedAttributes.GetFloat(ThewBehavior.LossRateKey);
        if (!float.IsFinite(rate) || rate <= 0) return;
        seen.Add(e.EntityId);
        double fullRate = OrcMetabolismFeedbackRules.Finite(cfg.ThewLossSmokeFullRate, 0.12, 0.001, 10);
        double strength = Math.Clamp(Math.Sqrt(rate / fullRate), 0, 1);
        double thin = OrcMetabolismFeedbackRules.Finite(cfg.ThewLossSmokeThinInterval, 6, 1, 30);
        double full = OrcMetabolismFeedbackRules.Finite(cfg.ThewLossSmokeFullInterval, 1.2, 0.4, thin);
        double interval = thin + (full - thin) * strength;
        float elapsed = accum.TryGetValue(e.EntityId, out float value) ? value : 0;
        elapsed += Math.Clamp(dt, 0, 0.5f);
        if (elapsed >= interval) { Emit(e, self, cfg, (float)strength); elapsed = 0; }
        accum[e.EntityId] = elapsed;
    }
    private void Emit(Entity e, Entity self, RFMechanicsConfig cfg, float strength)
    {
        bool local = e.EntityId == self.EntityId;
        double side = capi!.World.Rand.Next(2) == 0 ? -1 : 1;
        double yaw = e.Pos.Yaw;
        // Side/back of torso, below eyes: first-person wisps escape past the shoulders.
        double x = e.Pos.X + Math.Cos(yaw) * side * 0.32 + Math.Sin(yaw) * 0.16;
        double z = e.Pos.Z - Math.Sin(yaw) * side * 0.32 + Math.Cos(yaw) * 0.16;
        int[] rgb = cfg.PuffSmokeColorRgb?.Length >= 3 ? cfg.PuffSmokeColorRgb : new[] { 100, 95, 90 };
        int alpha = (int)(Math.Clamp(cfg.PuffOpacity, 20, 120) * (0.55 + strength * 0.45));
        var particle = new SimpleParticleProperties {
            ParticleModel = EnumParticleModel.Quad,
            Color = ColorUtil.ToRgba(alpha, Math.Clamp(rgb[0],0,255), Math.Clamp(rgb[1],0,255), Math.Clamp(rgb[2],0,255)),
            MinQuantity = 1, AddQuantity = strength * (local ? 1 : 2),
            MinPos = new Vec3d(x, e.Pos.Y + e.LocalEyePos.Y * (local ? 0.62 : 0.7), z),
            AddPos = new Vec3d(0.08, 0.16, 0.08),
            MinVelocity = new Vec3f(-0.025f, 0.12f, -0.025f), AddVelocity = new Vec3f(0.05f, 0.1f, 0.05f),
            MinSize = 0.08f, MaxSize = 0.12f + strength * 0.06f,
            LifeLength = local ? 0.9f : 1.3f, addLifeLength = 0.3f,
            GravityEffect = 0, WindAffected = cfg.PuffWindAffected, WindAffectednes = 0.1f,
            WithTerrainCollision = true,
            OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -alpha),
            SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.18f)
        };
        capi.World.SpawnParticles(particle);
    }
    public override void Dispose()
    {
        if (capi != null) { capi.Event.UnregisterGameTickListener(tick); capi.Event.LeaveWorld -= Clear; }
        Clear(); base.Dispose();
    }
}
