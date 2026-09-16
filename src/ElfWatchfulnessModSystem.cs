using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rfmechanics;

[ProtoContract]
public sealed class WatchfulnessRequest { [ProtoMember(1)] public bool Active; }
[ProtoContract]
public sealed class WatchfulnessReply { [ProtoMember(1)] public bool Active; }

// Intentionally no saved attributes. Clamber's independent persistent protocol is unchanged.
public sealed class ElfWatchfulnessModSystem : ModSystem
{
    private const string Channel = "rfmechanics:watchfulness-v1";
    private readonly HashSet<IServerPlayer> active = new();
    private ICoreServerAPI? sapi;
    private ICoreClientAPI? capi;
    private IServerNetworkChannel? server;
    private IClientNetworkChannel? client;
    private RFMechanicsConfig config = new();
    private WatchfulnessRenderer? renderer;
    private long tick, lastSent = -1000;
    internal bool Active { get; private set; }

    public override void Start(ICoreAPI api) => api.Network.RegisterChannel(Channel)
        .RegisterMessageType<WatchfulnessRequest>().RegisterMessageType<WatchfulnessReply>();

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        client = api.Network.GetChannel(Channel).SetMessageHandler<WatchfulnessReply>(reply =>
        {
            Active = reply.Active;
            if (!Active) renderer?.Clear();
            api.ShowChatMessage(reply.Active ? "Watchfulness on" : "Watchfulness off");
        });
        renderer = new WatchfulnessRenderer(api, this);
        api.Event.LeaveWorld += LeaveWorld;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        try { config = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception e) { api.Logger.Warning("[rfmechanics] Watchfulness defaults: {0}", e.Message); }
        server = api.Network.GetChannel(Channel).SetMessageHandler<WatchfulnessRequest>((player, request) =>
        {
            bool allowed = request.Active && Eligible(player);
            if (allowed) active.Add(player); else active.Remove(player);
            server?.SendPacket(new WatchfulnessReply { Active = allowed }, player);
        });
        api.Event.PlayerJoin += Reset;
        api.Event.PlayerDisconnect += Disconnect;
        api.Event.PlayerDeath += Death;
        tick = api.Event.RegisterGameTickListener(_ =>
        {
            // Only active observers; no world entity scan. Race changes clear within 100 ms.
            active.RemoveWhere(player =>
            {
                if (Eligible(player)) return false;
                server?.SendPacket(new WatchfulnessReply(), player);
                return true;
            });
        }, 100);
    }

    private bool Eligible(IServerPlayer player) => config.EnableElfWatchfulness
        && player.Entity?.Alive == true && RaceTraits.HasTrait(player, config.ElfTraitCode);
    private void Reset(IServerPlayer player) { active.Remove(player); server?.SendPacket(new WatchfulnessReply(), player); }
    private void Disconnect(IServerPlayer player) => active.Remove(player);
    private void Death(IServerPlayer player, DamageSource source) => Reset(player);
    private void LeaveWorld() { Active = false; lastSent = -1000; renderer?.Clear(); }
    internal bool TryToggle()
    {
        if (capi?.World.Player?.Entity?.Alive != true || client?.Connected != true
            || RFMechanicsModSystem.Config?.EnableElfWatchfulness != true) return false;
        long now = capi.World.ElapsedMilliseconds;
        if (now - lastSent < 200) return true;
        lastSent = now;
        client.SendPacket(new WatchfulnessRequest { Active = !Active });
        return true;
    }
    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.PlayerJoin -= Reset; sapi.Event.PlayerDisconnect -= Disconnect;
            sapi.Event.PlayerDeath -= Death; sapi.Event.UnregisterGameTickListener(tick);
        }
        if (capi != null) capi.Event.LeaveWorld -= LeaveWorld;
        renderer?.Dispose(); active.Clear(); Active = false;
        base.Dispose();
    }
}
