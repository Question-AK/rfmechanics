using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace rfmechanics;

[ProtoContract]
public sealed class ClamberStanceRequest
{
    // Absolute state makes duplicate requests idempotent. Ordering still depends on transport;
    // rapid presses before attribute synchronization can request the same state twice.
    [ProtoMember(1)] public bool Active;
}

[ProtoContract]
public sealed class ClamberStanceReply
{
    [ProtoMember(1)] public bool Active;
}

public static class GoblinClamberStance
{
    internal const string Channel = "rfmechanics:stance-v1";

    /// <summary>On WatchedAttributes, unlike ClimbSpeedPatch's deliberately transient
    /// ConditionalWeakTable state: this one must survive rejoin and respawn, and the server
    /// must own it.</summary>
    internal const string AttributeKey = "rfmechanics:clamber";

    internal static bool IsActive(Entity entity) => entity.WatchedAttributes.GetBool(AttributeKey, false);

    /// <summary>With the stance system switched off, wall climbing reverts to always-on rather
    /// than permanently off -- the config toggle removes the gate, not the ability.</summary>
    internal static bool AllowsWallClimb(Entity entity, RFMechanicsConfig cfg)
        => !cfg.EnableGoblinClamberStance || IsActive(entity);
}

/// <summary>
/// Ctrl+H Clamber stance: a sticky, server-owned per-player flag gating goblin wall climbing so
/// a goblin doesn't stick to ordinary terrain during ordinary work. Tree trunks and vanilla
/// ladders are never gated.
///
/// Same channel/[ProtoContract] shape as Ore-Song, on its own channel: the client only ever asks
/// and the server alone writes the attribute, since race state is never the client's to set.
/// </summary>
public class GoblinClamberStanceModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private IClientNetworkChannel? clientChannel;
    private IServerNetworkChannel? serverChannel;
    private RFMechanicsConfig serverConfig = new();
    private long lastSentMs;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.Network.RegisterChannel(GoblinClamberStance.Channel)
            .RegisterMessageType<ClamberStanceRequest>().RegisterMessageType<ClamberStanceReply>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;
        clientChannel = api.Network.GetChannel(GoblinClamberStance.Channel)
            .SetMessageHandler<ClamberStanceReply>(OnReply);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        // Own config snapshot: the static Config is last-writer-wins between the two sides in
        // singleplayer, and eligibility is a server decision.
        try { serverConfig = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception error) { api.Logger.Warning("[rfmechanics] Clamber stance using defaults: {0}", error.Message); }
        serverChannel = api.Network.GetChannel(GoblinClamberStance.Channel)
            .SetMessageHandler<ClamberStanceRequest>(OnRequest);
    }

    /// <summary>Called from RaceAbilityHotkeyModSystem once it has confirmed the presser is cached
    /// as Goblin. Debounced like the goblin spit press -- a hotkey handler fires on key-repeat.</summary>
    internal bool TryToggle(ICoreClientAPI api)
    {
        EntityPlayer? entity = api.World.Player?.Entity;
        if (entity == null || clientChannel?.Connected != true) return false;

        long now = api.World.ElapsedMilliseconds;
        if (now - lastSentMs < 200) return true;
        lastSentMs = now;

        clientChannel.SendPacket(new ClamberStanceRequest { Active = !GoblinClamberStance.IsActive(entity) });
        return true;
    }

    private void OnReply(ClamberStanceReply reply)
        => capi?.ShowChatMessage(Lang.Get(reply.Active ? "rfmechanics:clamber-on" : "rfmechanics:clamber-off"));

    /// <summary>The client's own race gate only keeps a non-goblin's Ctrl+H free for other mods;
    /// this trait check is the authoritative one.</summary>
    private void OnRequest(IServerPlayer player, ClamberStanceRequest request)
    {
        if (!serverConfig.EnableGoblinClamberStance) return;
        if (!RaceTraits.HasTrait(player, serverConfig.GoblinTraitCode)) return;

        EntityPlayer? entity = player.Entity;
        if (entity == null) return;

        entity.WatchedAttributes.SetBool(GoblinClamberStance.AttributeKey, request.Active);
        serverChannel?.SendPacket(new ClamberStanceReply { Active = request.Active }, player);
    }
}
