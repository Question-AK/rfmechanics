using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace rfmechanics;

[ProtoContract]
public sealed class RaceFeedbackNotice
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public string Key = "";
    [ProtoMember(3)] public int Race;
}

public sealed class RaceFeedbackModSystem : ModSystem
{
    private const string Channel = "rfmechanics:feedback-v1";
    private ICoreClientAPI? capi;
    private ICoreServerAPI? sapi;
    private IServerNetworkChannel? server;
    private RaceFeedbackHud? hud;
    private long clientTick, serverTick, localEntity, lastShow = -100000;
    private PlayerRace localRace;
    private int lastPriority, smellRevision = -1;
    private string lastGroup = "";
    private bool? clamber, watch, stonebrace;
    private bool discovered, concentrated;
    private readonly Dictionary<string, long> shown = new();
    private readonly Dictionary<string, State> states = new();

    private sealed class State
    {
        internal long Entity;
        internal PlayerRace Race;
        internal int Depth;
        internal bool Aura, Frenzy;
        internal double TreeSince = -1, TunnelSince = -1;
        internal bool TreeNotified, TunnelNotified;
    }

    public override void Start(ICoreAPI api) => api.Network.RegisterChannel(Channel).RegisterMessageType<RaceFeedbackNotice>();
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Network.GetChannel(Channel).SetMessageHandler<RaceFeedbackNotice>(notice => {
            var self = api.World.Player?.Entity;
            if (HasBehaviorState(self) && self?.EntityId == notice.EntityId && (int)(self.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None) == notice.Race)
                Show(notice.Key);
        });
        clientTick = api.Event.RegisterGameTickListener(ClientTick, 100);
        api.Event.LeaveWorld += ClearClient;
    }
    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api; server = api.Network.GetChannel(Channel);
        serverTick = api.Event.RegisterGameTickListener(ServerTick, 500);
        api.Event.PlayerDisconnect += Remove;
        api.Event.PlayerDeath += Death;
        api.Event.DidBreakBlock += Mined;
    }
    internal static void Send(Entity entity, string key)
    {
        if (!HasBehaviorState(entity) || entity.World.Side != EnumAppSide.Server || entity is not EntityPlayer player) return;
        var system = entity.Api.ModLoader.GetModSystem<RaceFeedbackModSystem>();
        if (entity.World.PlayerByUid(player.PlayerUID) is IServerPlayer recipient)
            system.server?.SendPacket(new RaceFeedbackNotice { EntityId = entity.EntityId, Key = key,
                Race = (int)(entity.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None) }, recipient);
    }
    internal static void Local(ICoreClientAPI api, string key) => api.ModLoader.GetModSystem<RaceFeedbackModSystem>().Show(key);

    // Stance replies can also be lifecycle resets. Only actual transitions are acknowledged.
    internal void Clamber(bool active)
    {
        EnsureClientIdentity();
        if (clamber == active) return;
        clamber = active; Show(active ? "clamber-on" : "clamber-off");
    }
    internal void Watch(bool active)
    {
        EnsureClientIdentity();
        if (watch == active || watch == null && !active) { watch = active; return; }
        watch = active; discovered = false;
        Show(active ? "watch-on" : "watch-off");
    }
    internal void Stonebrace(bool active)
    {
        EnsureClientIdentity();
        if (stonebrace == active || stonebrace == null && !active) { stonebrace = active; return; }
        stonebrace = active;
        Show(active ? "stonebrace-on" : "stonebrace-off");
    }
    internal void Discovered()
    {
        EnsureClientIdentity();
        if (watch != true || discovered) return;
        // Retry on a later legitimate glimpse if another line is still being read.
        discovered = Show("watch-discovered");
    }
    internal void Smell(bool active)
    {
        EnsureClientIdentity(); concentrated = false; smellRevision = OrcSmellShared.StanceRevision;
        Show(active ? "smell-on" : "smell-off");
    }
    private void EnsureClientIdentity()
    {
        var self = capi?.World.Player?.Entity;
        if (!HasBehaviorState(self)) { ClearClient(); return; }
        var race = self!.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None;
        if (self?.Alive != true || self.EntityId != localEntity || race != localRace)
        {
            ClearClient(); localEntity = self?.EntityId ?? 0; localRace = race;
        }
    }
    private void ClearClient()
    {
        hud?.Dispose(); hud = null; shown.Clear(); lastShow = -100000; lastPriority = 0; lastGroup = "";
        clamber = watch = stonebrace = null; discovered = concentrated = false; smellRevision = -1;
        localEntity = 0; localRace = PlayerRace.None;
    }
    private void ClientTick(float dt)
    {
        EnsureClientIdentity();
        if (capi == null || localRace != PlayerRace.Orc) return;
        if (smellRevision != OrcSmellShared.StanceRevision)
        { smellRevision = OrcSmellShared.StanceRevision; concentrated = false; }
        if (!concentrated && OrcSmellShared.FocusActive && OrcSmellShared.Quality >= 0.995f)
            concentrated = Show("smell-focused");
    }
    private bool Show(string key)
    {
        if (capi == null || RFMechanicsModSystem.Config?.EnableRacialFeedback != true) return false;
        EnsureClientIdentity();
        var self = capi.World.Player?.Entity;
        if (self?.Alive != true || localRace == PlayerRace.None) return false;
        var spec = Spec(key);
        if (spec.Race != localRace) return false;
        long now = capi.World.ElapsedMilliseconds;
        // Direct stance changes replace their own previous line immediately, preventing stale
        // "on" text after "off". Passive cues never interrupt a line. No historical queue.
        bool transition = spec.Priority == 3;
        if (!transition && shown.TryGetValue(key, out long at) && now - at < spec.Cooldown * 1000) return false;
        if (now - lastShow < 3600 && !(transition || spec.Priority > lastPriority)) return false;
        // A reply with the same transition is redundant.
        if (transition && lastGroup == key && now - lastShow < 1000) return false;
        string langKey = key.StartsWith("clamber-") ? "rfmechanics:" + key
            : key.StartsWith("stonebrace-") ? "rfmechanics:dwarf-" + key
            : "rfmechanics:feedback-" + key;
        hud ??= new RaceFeedbackHud(capi);
        hud.Show(Lang.Get(langKey), self.EntityId, localRace);
        lastShow = now; lastPriority = spec.Priority; lastGroup = key; shown[key] = now;
        return true;
    }
    private static (PlayerRace Race, int Priority, int Cooldown) Spec(string key) => key switch {
        "clamber-on" or "clamber-off" => (PlayerRace.Goblin, 3, 0),
        "rot-spit" or "spit-repaired" or "spit-last" or "spit-empty" or "spit-invalid" or "spit-full" => (PlayerRace.Goblin, 2, 5),
        "miasma-on" or "miasma-off" or "tunnel" => (PlayerRace.Goblin, 1, 120),
        "watch-on" or "watch-off" => (PlayerRace.Elf, 3, 0),
        "watch-discovered" => (PlayerRace.Elf, 1, 0),
        "woodland" => (PlayerRace.Elf, 1, 120),
        "depth-1" or "depth-2" or "depth-3" => (PlayerRace.Dwarf, 1, 120),
        "stonebrace-on" or "stonebrace-off" => (PlayerRace.Dwarf, 3, 0),
        "smell-on" or "smell-off" => (PlayerRace.Orc, 3, 0),
        "smell-focused" => (PlayerRace.Orc, 1, 0),
        "blood" => (PlayerRace.Orc, 2, 15),
        "burn" or "frenzy-on" or "frenzy-off" => (PlayerRace.Orc, 1, 60),
        _ => (PlayerRace.None, 0, 0)
    };

    private void Remove(IServerPlayer player) => states.Remove(player.PlayerUID);
    private void Death(IServerPlayer player, DamageSource _) => Remove(player);

    // Online players can be visible during delayed spawn before Entity.Initialize.
    // GetBehavior dereferences SidedProperties; Alive alone does not make it safe.
    internal static bool HasBehaviorState(Entity? entity) =>
        entity?.World != null && entity.Api != null && entity.SidedProperties?.Behaviors != null;

    private State? GetState(IServerPlayer player)
    {
        var e = player.Entity;
        if (!HasBehaviorState(e) || !e.Alive) { Remove(player); return null; }
        var race = e.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None;
        if (!states.TryGetValue(player.PlayerUID, out var state) || state.Entity != e.EntityId || state.Race != race)
        {
            state = new State { Entity = e.EntityId, Race = race,
                Aura = GoblinRotAuraState.ReadVisual(e).Active,
                Frenzy = FrenzyBehavior.CurrentSpeedBonus(e) >= 0.03 };
            states[player.PlayerUID] = state;
        }
        return state;
    }
    private void ServerTick(float dt)
    {
        if (sapi == null) return;
        double now = sapi.World.ElapsedMilliseconds / 1000.0;
        foreach (var online in sapi.World.AllOnlinePlayers)
        {
            if (online is not IServerPlayer player) continue;
            var e = player.Entity;
            var s = GetState(player);
            if (s == null) continue;
            if (s.Race == PlayerRace.Goblin)
            {
                bool aura = GoblinRotAuraState.ReadVisual(e).Active;
                if (aura != s.Aura) { Send(e, aura ? "miasma-on" : "miasma-off"); s.Aura = aura; }
                Sustained(e, Stat(e, "tunneling") > 0.001, now, ref s.TunnelSince, ref s.TunnelNotified, "tunnel");
            }
            if (s.Race == PlayerRace.Elf)
                Sustained(e, Stat(e, "treeproximity") >= 0.04, now, ref s.TreeSince, ref s.TreeNotified, "woodland");
            if (s.Race == PlayerRace.Orc)
            {
                float bonus = FrenzyBehavior.CurrentSpeedBonus(e);
                bool frenzy = s.Frenzy ? bonus > 0.01 : bonus >= 0.03;
                if (frenzy != s.Frenzy) { Send(e, frenzy ? "frenzy-on" : "frenzy-off"); s.Frenzy = frenzy; }
            }
        }
    }
    private static float Stat(Entity e, string source) => e.Stats["walkspeed"].ValuesByKey.TryGetValue(source, out var v) ? v.Value : 0;
    private static void Sustained(Entity e, bool active, double now, ref double since, ref bool notified, string key)
    {
        if (!active) { since = -1; notified = false; return; }
        if (since < 0) since = now;
        if (!notified && now - since >= 4) { Send(e, key); notified = true; }
    }
    private void Mined(IServerPlayer player, int oldId, BlockSelection selection)
    {
        if (sapi == null || RFMechanicsModSystem.Config?.EnableMiningCurve != true) return;
        var s = GetState(player);
        if (s == null || s.Race != PlayerRace.Dwarf) return;
        var material = sapi.World.GetBlock(oldId).BlockMaterial;
        if (material != EnumBlockMaterial.Ore && material != EnumBlockMaterial.Stone) return;
        int band = OrcMetabolismFeedbackRules.DepthBand(selection.Position.Y, sapi.World.SeaLevel);
        if (band <= s.Depth) return;
        s.Depth = band; // Highest reached this life/session; no boundary oscillation messages.
        Send(player.Entity, "depth-" + band);
    }
    public override void Dispose()
    {
        if (capi != null) { capi.Event.UnregisterGameTickListener(clientTick); capi.Event.LeaveWorld -= ClearClient; }
        if (sapi != null) {
            sapi.Event.UnregisterGameTickListener(serverTick); sapi.Event.PlayerDisconnect -= Remove;
            sapi.Event.PlayerDeath -= Death; sapi.Event.DidBreakBlock -= Mined;
        }
        ClearClient(); states.Clear(); base.Dispose();
    }
}
