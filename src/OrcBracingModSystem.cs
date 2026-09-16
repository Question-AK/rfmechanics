using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Client.NoObf;

namespace rfmechanics;

[ProtoContract]
public sealed class OrcBraceRequest
{
    [ProtoMember(1)] public long EntityId;
}

[ProtoContract]
public sealed class OrcBraceNotice
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public string Hint = "";
}

public sealed class OrcBracingModSystem : ModSystem
{
    private const string Channel = "rfmechanics:orc-brace-v1";
    private sealed class Session
    {
        internal IServerPlayer Player = null!;
        internal EntityPlayer Self = null!;
        internal Action IdentityChanged = null!;
        internal OrcBraceState Body = new();
        internal bool IsOrc, RecoveryPending;
        internal long Updated, NextRequest, NextHint;
        internal string LastHit = "none";
    }
    private readonly Dictionary<string, Session> sessions = new();
    private ICoreServerAPI? sapi;
    private ICoreClientAPI? capi;
    private IServerNetworkChannel? server;
    private RFMechanicsConfig cfg = new();
    private OrcBraceTuning tuning = new();
    private long tick;
    private long inputTick;
    private int heldKey = -1;
    private long nextClientHint;
    private OrcBraceHud? hud;

    public override void Start(ICoreAPI api) => api.Network.RegisterChannel(Channel)
        .RegisterMessageType<OrcBraceRequest>().RegisterMessageType<OrcBraceNotice>();

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Network.GetChannel(Channel).SetMessageHandler<OrcBraceNotice>(Notice);
        api.Event.KeyUp += KeyUp;
        api.Event.MouseUp += MouseUp;
        api.Event.LeaveWorld += ClearClient;
        inputTick = api.Event.RegisterGameTickListener(PollRelease, 50);
    }

    internal bool TryToggle()
    {
        var self = capi?.World.Player?.Entity;
        if (self?.Alive != true) return false;
        if (!capi!.Input.HotKeys.TryGetValue("rfraceability", out var hotkey)) return false;
        if (heldKey >= 0 && heldKey != hotkey.CurrentMapping.KeyCode) heldKey = -1;
        if (heldKey >= 0) return true; // OS repeat cannot turn a held key into many taps.
        heldKey = hotkey.CurrentMapping.KeyCode;
        capi.Network.GetChannel(Channel).SendPacket(new OrcBraceRequest { EntityId = self.EntityId });
        return true;
    }

    private void KeyUp(KeyEvent e) { if (e.KeyCode == heldKey) heldKey = -1; }
    private void MouseUp(MouseEvent e) { if (heldKey == KeyCombination.MouseStart + (int)e.Button) heldKey = -1; }
    internal void ReleaseKey(int key) { if (key == heldKey) heldKey = -1; }
    private void PollRelease(float dt)
    {
        if (capi == null || heldKey < 0) return;
        if (capi.World.Player?.Entity?.Alive != true
            || (capi.World is ClientMain client && !client.Platform.IsFocused)
            || !capi.Input.HotKeys.TryGetValue("rfraceability", out var binding)
            || binding.CurrentMapping.KeyCode != heldKey)
        { heldKey = -1; return; }
        // Mouse4/5 do not necessarily emit logical MouseUp. Raw mouse dispatch writes
        // KeyboardKeyState[240+button], whereas keyboard releases use the Raw array.
        var keys = heldKey >= KeyCombination.MouseStart ? capi.Input.KeyboardKeyState : capi.Input.KeyboardKeyStateRaw;
        if (heldKey >= keys.Length || !keys[heldKey]) heldKey = -1;
    }
    private void ClearClient() { heldKey = -1; nextClientHint = 0; hud?.Dispose(); hud = null; }
    private void Notice(OrcBraceNotice notice)
    {
        var self = capi?.World.Player?.Entity;
        if (self?.Alive != true || notice.EntityId != self.EntityId
            || self.GetBehavior<PlayerRaceBehavior>()?.Race != PlayerRace.Orc) return;
        // Never retain our own queue; stale/repeated transition notices are discarded.
        long now = capi!.World.ElapsedMilliseconds;
        bool urgent = notice.Hint == "hungry";
        if (!urgent && now < nextClientHint) return;
        string? text = notice.Hint switch
        {
            "brace" => "You brace yourself.",
            "release" => "You ease your guard.",
            "recovered" => "The tension leaves your body.",
            "hungry" => "Your empty belly leaves you too weak to brace.",
            "disabled" => "Your body cannot brace just now.",
            _ => null
        };
        if (text == null) return;
        nextClientHint = now + 3000;
        hud ??= new OrcBraceHud(capi);
        hud.Show(text, self.EntityId);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        // Per-server snapshot: do not use the shared single-player client/server static config.
        try { cfg = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception e) { api.Logger.Warning("[rfmechanics] Orc bracing defaults: {0}", e.Message); }
        tuning = new OrcBraceTuning
        {
            Arc = cfg.OrcBraceFrontalArcDegrees,
            InitialRate = cfg.OrcBraceInitialSatietyPerSecond, MaximumRate = cfg.OrcBraceMaxSatietyPerSecond,
            RampSeconds = cfg.OrcBraceRampSeconds, RecoverySeconds = cfg.OrcBraceRecoverySeconds,
            FoodFloor = cfg.OrcBraceLowFoodFraction, RestartMargin = cfg.OrcBraceRestartFoodMargin
        };
        tuning.Normalize();
        server = api.Network.GetChannel(Channel).SetMessageHandler<OrcBraceRequest>(Request);
        api.Event.PlayerDisconnect += Remove;
        api.Event.PlayerJoin += Remove;
        api.Event.PlayerDeath += Death;
        tick = api.Event.RegisterGameTickListener(Tick, 100);
        api.ChatCommands.Create("rfbrace").WithDescription("Read your authoritative bracing, food and last natural-protection hit.")
            .RequiresPlayer().RequiresPrivilege(Privilege.chat)
            .HandleWith(args => TextCommandResult.Success(Describe((EntityPlayer)args.Caller.Entity)));
    }

    private Session? Get(EntityPlayer self)
    {
        if (sapi == null || self.Player is not IServerPlayer player) return null;
        if (sessions.TryGetValue(player.PlayerUID, out var prior) && ReferenceEquals(prior.Self, self)) return prior;
        Remove(player);
        var session = new Session { Player = player, Self = self, Updated = sapi.World.ElapsedMilliseconds };
        session.IdentityChanged = () =>
        {
            // Any class/extra-trait change releases and clears the session immediately,
            // even Orc->other->Orc between ticks. No stale cached protection window.
            session.Body = new(); session.RecoveryPending = false; session.LastHit = "none";
            session.Updated = sapi.World.ElapsedMilliseconds;
            session.IsOrc = RaceTraits.HasTrait(player, cfg.OrcTraitCode);
        };
        self.WatchedAttributes.RegisterModifiedListener("characterClass", session.IdentityChanged);
        self.WatchedAttributes.RegisterModifiedListener("extraTraits", session.IdentityChanged);
        session.IdentityChanged();
        sessions[player.PlayerUID] = session;
        return session;
    }

    private void Remove(IServerPlayer player)
    {
        if (!sessions.Remove(player.PlayerUID, out var session)) return;
        session.Self.WatchedAttributes.UnregisterListener(session.IdentityChanged);
    }
    private void Death(IServerPlayer player, DamageSource source) => Remove(player);

    private void Hint(Session session, string hint, bool force = false)
    {
        long now = sapi!.World.ElapsedMilliseconds;
        if (!force && now < session.NextHint) return;
        session.NextHint = now + 3000;
        server?.SendPacket(new OrcBraceNotice { EntityId = session.Self.EntityId, Hint = hint }, session.Player);
    }

    private void Advance(Session session)
    {
        long now = sapi!.World.ElapsedMilliseconds;
        double elapsed = Math.Max(0, (now - session.Updated) / 1000.0);
        session.Updated = now;
        if (!session.Self.Alive || !session.IsOrc || !cfg.EnableOrcBracing || !cfg.EnableOrcNaturalProtection)
        { session.Body = new(); session.RecoveryPending = false; return; }
        var hunger = session.Self.GetBehavior<EntityBehaviorHunger>();
        double food = hunger?.Saturation ?? double.NaN, before = food;
        bool forced = session.Body.Advance(elapsed, ref food, hunger?.MaxSaturation ?? 0, tuning);
        // Saturation's setter syncs the hunger tree. ConsumeSaturation is a vanilla
        // metabolic multiplier with food-delay/nutrition side effects, NOT a point debit.
        if (hunger != null && double.IsFinite(food) && food != before) hunger.Saturation = (float)food;
        if (forced) Hint(session, "hungry", true);
        if (!session.Body.Active && session.Body.Exertion <= 0 && session.RecoveryPending)
        {
            // Keep a single pending recovery transition until the hint cooldown expires.
            // Reactivation keeps the episode open; this is not a queue of old toggles.
            if (now >= session.NextHint)
            { Hint(session, "recovered"); session.RecoveryPending = false; }
        }
    }

    private void Request(IServerPlayer player, OrcBraceRequest request)
    {
        if (sapi == null || request.EntityId != player.Entity.EntityId || !player.Entity.Alive) return;
        var session = Get(player.Entity);
        if (session == null || !session.IsOrc) return;
        long now = sapi.World.ElapsedMilliseconds;
        if (now < session.NextRequest) return;
        session.NextRequest = now + 200;
        bool wasActive = session.Body.Active;
        Advance(session);
        // A request that discovers forced release must not immediately undo it.
        if (wasActive && !session.Body.Active) return;
        if (!cfg.EnableOrcBracing || !cfg.EnableOrcNaturalProtection) { Hint(session, "disabled"); return; }
        if (session.Body.Active)
        { session.Body.Active = false; Hint(session, "release"); return; }
        var hunger = player.Entity.GetBehavior<EntityBehaviorHunger>();
        if (!session.Body.CanStart(hunger?.Saturation ?? 0, hunger?.MaxSaturation ?? 0, tuning))
        { Hint(session, "hungry"); return; }
        session.Body.Active = true;
        session.RecoveryPending = true;
        Hint(session, "brace");
    }

    private void Tick(float dt)
    {
        if (sapi == null) return;
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            var session = Get(player.Entity);
            if (session != null) Advance(session);
        }
    }

    internal void Protect(EntityPlayer self, DamageSource source, ref float damage)
    {
        if (sapi == null || !cfg.EnableOrcNaturalProtection || !PhysicalAttack(source)) return;
        var session = Get(self);
        if (session?.IsOrc != true) return;
        Advance(session); // Food exhaustion on this hit releases BEFORE tier selection.
        bool front = FrontalSource(self, source);
        int tier = session.Body.Active && front ? 3 : 1;
        float before = damage;
        damage = (float)OrcProtectionRules.Protect(damage, source.DamageTier, tier);
        session.LastHit = $"weapon T{source.DamageTier}, skin T{tier}, front={front}, after equipment {before:F3} -> {damage:F3} HP";
    }

    internal static bool PhysicalAttack(DamageSource source) =>
        source.Duration <= TimeSpan.Zero
        && source.Type is EnumDamageType.BluntAttack or EnumDamageType.PiercingAttack or EnumDamageType.SlashingAttack
        && source.Source is EnumDamageSource.Entity or EnumDamageSource.Player or EnumDamageSource.Unknown;

    private bool FrontalSource(EntityPlayer self, DamageSource source)
    {
        // Unknown/source-less physical hits get baseline only. No environmental frontal benefit.
        if (source.Source is not (EnumDamageSource.Entity or EnumDamageSource.Player)) return false;
        var attacker = source.SourceEntity;
        if (attacker == null || attacker == self || attacker.Pos.Dimension != self.Pos.Dimension) return false;
        bool projectile = attacker.Properties.Attributes?["isProjectile"].AsBool(false) == true
            || (source.CauseEntity != null && source.CauseEntity != attacker);
        double x, z;
        if (projectile)
        {
            // Use incoming travel, never the shooter's CURRENT position. Missing/stopped
            // projectile motion deliberately gets only T1, even if the shooter is in front.
            x = -attacker.Pos.Motion.X; z = -attacker.Pos.Motion.Z;
        }
        else
        {
            x = attacker.Pos.X - self.Pos.X;
            z = attacker.Pos.Z - self.Pos.Z;
        }
        return OrcProtectionRules.InFront(self.Pos.Yaw, x, z, tuning.Arc);
    }

    internal string Describe(EntityPlayer self)
    {
        var session = Get(self);
        if (session == null) return "Bracing server unavailable.";
        var food = self.GetBehavior<EntityBehaviorHunger>();
        return $"Orc={session.IsOrc}, natural={cfg.EnableOrcNaturalProtection}, braceEnabled={cfg.EnableOrcBracing}, "
            + $"active={session.Body.Active}, exertion={session.Body.Exertion:P1}, "
            + $"drain={(session.Body.Active ? tuning.Rate(session.Body.Exertion) : 0):F2} sat/s, "
            + $"food={food?.Saturation:F1}/{food?.MaxSaturation:F1}, cutoff={tuning.FoodFloor:P0}, "
            + $"start={tuning.FoodFloor + tuning.RestartMargin:P0}, arc={tuning.Arc:F0}; last: {session.LastHit}";
    }

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(tick);
            sapi.Event.PlayerDisconnect -= Remove; sapi.Event.PlayerJoin -= Remove; sapi.Event.PlayerDeath -= Death;
            foreach (var session in sessions.Values) session.Self.WatchedAttributes.UnregisterListener(session.IdentityChanged);
            sessions.Clear();
        }
        if (capi != null)
        {
            capi.Event.KeyUp -= KeyUp; capi.Event.MouseUp -= MouseUp; capi.Event.LeaveWorld -= ClearClient;
            capi.Event.UnregisterGameTickListener(inputTick);
            ClearClient();
        }
        base.Dispose();
    }
}
