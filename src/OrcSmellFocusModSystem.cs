using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace rfmechanics;

public sealed class OrcSmellFocusModSystem : ModSystem, IRenderer
{
    private ICoreClientAPI? capi;
    private OrcHuntModSystem? hunt;
    private AmbientModifier? ambient;
    private readonly OrcSmellFocusState focus = new();
    private Vec3d? previous;
    private long playerId;
    private int dimension, revision;
    private double distance, elapsed, speed;
    private bool logged;
    public double RenderOrder => 0.05;
    public int RenderRange => 1;
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api; hunt = api.ModLoader.GetModSystem<OrcHuntModSystem>();
        ambient = new AmbientModifier {
            FogColor = new WeightedFloatArray(new float[] { 0, 0, 0 }, 0),
            AmbientColor = new WeightedFloatArray(new float[] { 0, 0, 0 }, 0),
            FogDensity = new WeightedFloat(0, 0)
        }.EnsurePopulated();
        api.Ambient.CurrentModifiers["orcsmellfocus"] = ambient;
        api.Event.RegisterRenderer(this, EnumRenderStage.Before, "rforcsmellfocus");
        api.Event.LeaveWorld += Reset;
    }
    private void Reset()
    {
        focus.Reset(); previous = null; playerId = 0; distance = elapsed = speed = 0;
        OrcSmellShared.Quality = OrcSmellShared.FocusWeight = 0;
        OrcSmellShared.Resting = OrcSmellShared.Sitting = false; OrcSmellShared.Speed = 0;
        OrcSmellShared.FocusActive = OrcSmellShared.SensoryActive = false;
        Apply(0, 0);
    }
    private void Apply(float weight, float fogDensity)
    {
        if (ambient == null) return;
        ambient.FogColor.Weight = ambient.AmbientColor.Weight = ambient.FogDensity.Weight = weight;
        ambient.FogDensity.Value = fogDensity * weight * weight;
    }
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (capi == null || hunt == null) return;
        try
        {
            var cfg = RFMechanicsModSystem.Config;
            var self = capi.World.Player?.Entity;
            if (cfg?.SmellEnabled != true || !cfg.EnableOrcHunting || self?.Alive != true
                || self.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Orc)
            { Reset(); return; }
            if (capi.IsGamePaused) return;
            bool discontinuity = playerId != self.EntityId || dimension != self.Pos.Dimension || deltaTime > 0.5
                || previous != null && self.Pos.SquareDistanceTo(previous) > 4;
            if (discontinuity) Reset();
            if (revision != OrcSmellShared.StanceRevision)
            { focus.Reset(false); revision = OrcSmellShared.StanceRevision; }
            playerId = self.EntityId; dimension = self.Pos.Dimension;
            double dx = previous == null ? 0 : self.Pos.X - previous.X;
            double dz = previous == null ? 0 : self.Pos.Z - previous.Z;
            distance += Math.Sqrt(dx*dx+dz*dz); elapsed += Math.Max(0, deltaTime);
            if (elapsed >= 0.2) { speed = distance/elapsed; distance = elapsed = 0; }
            previous = self.Pos.XYZ;
            var controls = self.Controls;
            // No OnGround or per-frame Motion*60 cancellation. Floor-sitting needs
            // no separate sniff input; stationary seats also qualify by displacement.
            bool travelling = controls.TriesToMove || speed > 0.15;
            bool unavailable = self.Swimming || controls.IsFlying || controls.IsClimbing || controls.Jump;
            bool resting = !unavailable && !travelling;
            bool sitting = resting && controls.FloorSitting;
            float target = resting ? 1 : unavailable || controls.Sprint && travelling ? 0
                : (float)(Math.Clamp(cfg.OrcSlowFocusLevel, 0, 0.75) * Math.Clamp(
                    1 - speed / Math.Clamp(cfg.OrcSlowFocusMaxSpeed, 0.2, 5), 0, 1));
            focus.Update(deltaTime, hunt.Stance, target, resting, cfg);
            OrcSmellShared.FocusActive = hunt.Stance;
            OrcSmellShared.Quality = focus.Quality;
            OrcSmellShared.Resting = resting;
            OrcSmellShared.Sitting = sitting;
            OrcSmellShared.Speed = speed;
            float weight = focus.Fade * (float)Math.Clamp(cfg.OrcFocusMaximumDarkness, 0, 1);
            OrcSmellShared.FocusWeight = weight;
            Apply(weight, (float)Math.Clamp(cfg.SmellFocusFogDensity, 0, 1));
        }
        catch (Exception e)
        {
            Reset();
            if (!logged) { logged = true; capi.Logger.Warning("[rfmechanics] Orc focus: {0}", e); }
        }
    }
    public override void Dispose()
    {
        Reset();
        if (capi != null) {
            capi.Event.LeaveWorld -= Reset;
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            capi.Ambient.CurrentModifiers.Remove("orcsmellfocus");
        }
        base.Dispose();
    }
}
