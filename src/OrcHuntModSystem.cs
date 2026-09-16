using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

[ProtoContract]
public sealed class OrcHuntStatus
{
    [ProtoMember(1)] public long[] BloodIds = Array.Empty<long>();
    [ProtoMember(2)] public bool Engaged;
    [ProtoMember(3)] public float Bonus;
    [ProtoMember(4)] public bool ProviderAvailable;
}

// No persisted pursuit/stance state. Speed is server-owned; client packets only
// carry eligible blood IDs and presentation state, never accept target/stat claims.
public sealed class OrcHuntModSystem : ModSystem
{
    private const string Channel = "rfmechanics:orc-hunt-v1", Stat = "rf-orc-pursuit";
    private sealed class Hunt
    {
        internal EntityPlayer Self = null!;
        internal Entity? Target;
        internal Vec3d Previous = new();
        internal int Dimension;
        internal double Charge, Loss, ScanAt, LastHint = -100000;
        internal float Bonus;
        internal bool Episode;
        internal readonly List<Entity> Blood = new();
    }
    private readonly Dictionary<string, Hunt> hunts = new();
    private ICoreServerAPI? sapi;
    private ICoreClientAPI? capi;
    private IServerNetworkChannel? server;
    private EntityPartitioning? partitions;
    private OrcBloodSource? blood;
    private RFMechanicsConfig cfg = new();
    private long tick;
    private double lastStatus;
    private long toggleAt;
    internal bool Stance { get; private set; }
    internal readonly HashSet<long> BloodIds = new();
    private OrcHuntStatus status = new();
    internal bool HasBlood => FreshStatus && BloodIds.Count > 0;
    private bool FreshStatus => capi != null && capi.World.ElapsedMilliseconds - lastStatus < 2000;
    internal bool IsBlood(Entity e) => FreshStatus && BloodIds.Contains(e.EntityId) && e.Alive;

    public override void Start(ICoreAPI api) => api.Network.RegisterChannel(Channel).RegisterMessageType<OrcHuntStatus>();
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Network.GetChannel(Channel).SetMessageHandler<OrcHuntStatus>(packet =>
        {
            status = packet; lastStatus = api.World.ElapsedMilliseconds;
            BloodIds.Clear(); foreach (long id in packet.BloodIds) BloodIds.Add(id);
            if (packet.Engaged) api.TriggerIngameDiscovery(this, "orchunt", "Blood is in the air.");
        });
        api.Event.LeaveWorld += ClearClient;
        api.ChatCommands.Create("rfhunttest").WithDescription("Report Orc sniff and real bleeding integration state; does not create bleeding.")
            .HandleWith(_ => {
                string report = $"Orc hunt: stance={Stance}, focus={OrcSmellShared.FocusActive}, heldMs={OrcSmellShared.HeldMs:F0}, quality={OrcSmellShared.Quality:F2}, provider={status.ProviderAvailable && FreshStatus}, blood={ (FreshStatus ? BloodIds.Count : 0)}, pursuit={status.Bonus:P0}";
                api.Logger.Notification("[rfmechanics] {0}", report);
                return TextCommandResult.Success(report);
            });
    }
    internal bool TryToggle()
    {
        if (capi?.World.Player?.Entity?.Alive != true || RFMechanicsModSystem.Config?.SmellEnabled != true
            || !RFMechanicsModSystem.Config.EnableOrcHunting) return false;
        if (capi.World.ElapsedMilliseconds < toggleAt) return true;
        toggleAt = capi.World.ElapsedMilliseconds + 250;
        Stance = !Stance;
        capi.ShowChatMessage(Stance ? "Hunting stance on" : "Hunting stance off");
        return true;
    }
    internal void ClearClient() { Stance = false; BloodIds.Clear(); status = new(); lastStatus = -100000; toggleAt = 0; }
    internal static void ClampPursuitSpeed(Entity entity, RFMechanicsConfig config, float frenzy)
    {
        var values = entity.Stats["walkspeed"].ValuesByKey;
        if (values.TryGetValue(Stat, out var value))
        {
            float limit = (float)Math.Max(0, Math.Clamp(config.OrcHuntCombinedSpeedBonusCap, 0, 1) - frenzy);
            if (value.Value > limit) entity.Stats.Set("walkspeed", Stat, limit);
        }
    }
    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        try { cfg = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception e) { api.Logger.Warning("[rfmechanics] Orc hunt defaults: {0}", e.Message); }
        blood = new(api); partitions = api.ModLoader.GetModSystem<EntityPartitioning>();
        server = api.Network.GetChannel(Channel);
        api.Logger.Notification("[rfmechanics] Orc hunting bleeding providers: BloodTrail={0}, Hunter={1}. Presence is not an active bleeding target.", blood.BloodTrail, blood.Hunter);
        api.Event.PlayerDisconnect += Remove;
        api.Event.PlayerDeath += Death;
        api.Event.PlayerJoin += Remove;
        tick = api.Event.RegisterGameTickListener(Tick, 200);
    }
    private void Remove(IServerPlayer player)
    {
        player.Entity?.Stats.Remove("walkspeed", Stat);
        if (hunts.Remove(player.PlayerUID, out var hunt)) hunt.Self.Stats.Remove("walkspeed", Stat);
    }
    private void Death(IServerPlayer player, DamageSource _) { Remove(player); server?.SendPacket(new OrcHuntStatus(), player); }
    private bool Valid(Entity target, EntityPlayer self)
    {
        double range = Math.Clamp(cfg.OrcBloodRange, 4, 64);
        return target != self && target.Alive && target.Pos.Dimension == self.Pos.Dimension
            && (!cfg.OrcTargetSwimmingBreaksBlood || !target.Swimming)
            && target.Pos.SquareDistanceTo(self.Pos) <= range * range
            && (target is EntityPlayer || OrcSmellClassifier.IsSmellableFauna(target))
            && blood!.IsBleeding(target);
    }
    private void Tick(float dt)
    {
        if (sapi == null || blood == null) return;
        double now = sapi.World.ElapsedMilliseconds / 1000.0;
        foreach (var online in sapi.World.AllOnlinePlayers)
        {
            if (online is not IServerPlayer player) continue;
            EntityPlayer self = player.Entity;
            if (!cfg.EnableOrcHunting || !self.Alive || !RaceTraits.HasTrait(player, cfg.OrcTraitCode))
            {
                if (hunts.ContainsKey(player.PlayerUID)) { Remove(player); server?.SendPacket(new OrcHuntStatus(), player); }
                continue;
            }
            if (!hunts.TryGetValue(player.PlayerUID, out Hunt? hunt) || hunt.Self != self)
            {
                Remove(player);
                hunt = new Hunt { Self = self, Previous = self.Pos.XYZ, Dimension = self.Pos.Dimension, ScanAt = now + (self.EntityId % 5) * 0.04 };
                hunts[player.PlayerUID] = hunt;
            }
            double dx = self.Pos.X - hunt.Previous.X, dz = self.Pos.Z - hunt.Previous.Z;
            double moved = Math.Sqrt(dx*dx + dz*dz);
            bool discontinuity = dt > 0.5 || moved > 4 || hunt.Dimension != self.Pos.Dimension;
            hunt.Previous.Set(self.Pos.XYZ); hunt.Dimension = self.Pos.Dimension;
            if (discontinuity) { hunt.Charge = hunt.Loss = 0; hunt.Target = null; hunt.Blood.Clear(); hunt.Episode = false; hunt.ScanAt = now; }
            if (now >= hunt.ScanAt && blood.Available)
            {
                hunt.ScanAt = now + 1; hunt.Blood.Clear(); int visits = 0;
                partitions!.WalkEntities(self.Pos.X, self.Pos.Y, self.Pos.Z, Math.Clamp(cfg.OrcBloodRange, 4, 64), e =>
                {
                    if (++visits > 256) return false;
                    if (hunt.Blood.Count < 32 && Valid(e, self)) hunt.Blood.Add(e);
                    return true;
                }, null, EnumEntitySearchType.Creatures);
            }
            hunt.Blood.RemoveAll(e => !Valid(e, self));
            if (hunt.Target == null || !Valid(hunt.Target, self))
            {
                Entity? next = null; double nearest = double.MaxValue;
                foreach (var e in hunt.Blood)
                {
                    double distance = e.Pos.SquareDistanceTo(self.Pos);
                    if (distance < nearest) { nearest = distance; next = e; }
                }
                // Losing and reacquiring the same target retains its charge through grace.
                // Switching to a different target starts the ramp again, without another hint.
                if (next != null && next != hunt.Target) { hunt.Charge = 0; hunt.Target = next; }
            }
            bool valid = hunt.Target != null && Valid(hunt.Target, self);
            double tx = valid ? hunt.Target!.Pos.X - self.Pos.X : 0, tz = valid ? hunt.Target!.Pos.Z - self.Pos.Z : 0;
            double distanceToTarget = Math.Sqrt(tx*tx + tz*tz);
            double toward = moved > 0.0001 && distanceToTarget > 0.05 ? (dx*tx + dz*tz)/(moved*distanceToTarget) : 0;
            bool runningToward = !discontinuity && valid && self.Controls.Sprint && self.Controls.TriesToMove
                && !self.Controls.IsFlying && moved / Math.Max(0.01, dt) >= 1 && toward >= Math.Clamp(cfg.OrcPursuitDirectionCosine, 0, 1);
            bool hint = false;
            if (runningToward)
            {
                hunt.Loss = 0;
                hunt.Charge = Math.Min(1, hunt.Charge + Math.Clamp(dt, 0, 0.25) / Math.Clamp(cfg.OrcPursuitRampSeconds, 1, 30));
                if (!hunt.Episode)
                {
                    hunt.Episode = true;
                    if (now - hunt.LastHint >= 15) { hint = true; hunt.LastHint = now; }
                }
            }
            else
            {
                hunt.Loss += Math.Clamp(dt, 0, 0.25);
                if (hunt.Loss > Math.Clamp(cfg.OrcPursuitGraceSeconds, 0, 5))
                    hunt.Charge = Math.Max(0, hunt.Charge - Math.Clamp(dt, 0, 0.25) / Math.Clamp(cfg.OrcPursuitDecaySeconds, 0.5, 20));
                if (hunt.Charge <= 0 && hunt.Loss >= 5) { hunt.Episode = false; hunt.Target = null; }
            }
            float frenzy = FrenzyBehavior.CurrentSpeedBonus(self);
            float bonus = (float)Math.Min(hunt.Charge * Math.Clamp(cfg.OrcPursuitMaxSpeedBonus, 0, 0.5),
                Math.Max(0, Math.Clamp(cfg.OrcHuntCombinedSpeedBonusCap, 0, 1) - frenzy));
            float installedBonus = self.Stats["walkspeed"].ValuesByKey.TryGetValue(Stat, out var current) ? current.Value : 0;
            if (Math.Abs(bonus - installedBonus) > 0.001 || bonus == 0 && installedBonus != 0)
            {
                if (bonus == 0) self.Stats.Remove("walkspeed", Stat); else self.Stats.Set("walkspeed", Stat, bonus);
                hunt.Bonus = bonus;
            }
            var ids = new long[hunt.Blood.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = hunt.Blood[i].EntityId;
            server!.SendPacket(new OrcHuntStatus { BloodIds = ids, Bonus = bonus, Engaged = hint, ProviderAvailable = blood.Available }, player);
        }
    }
    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(tick); sapi.Event.PlayerDisconnect -= Remove;
            sapi.Event.PlayerJoin -= Remove; sapi.Event.PlayerDeath -= Death;
            foreach (var hunt in hunts.Values) hunt.Self.Stats.Remove("walkspeed", Stat);
        }
        if (capi != null) capi.Event.LeaveWorld -= ClearClient;
        hunts.Clear(); ClearClient(); base.Dispose();
    }
}
