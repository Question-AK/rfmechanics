using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace rfmechanics;

[ProtoContract]
public sealed class StonebraceStanceRequest
{
    [ProtoMember(1)] public bool Active;
}

[ProtoContract]
public sealed class StonebraceStanceStatus
{
    [ProtoMember(1)] public bool Active;
}

public sealed class DwarfStonebraceModSystem : ModSystem
{
    private const string Channel = "rfmechanics:dwarf-stonebrace-v1";
    private const string MovementStat = "rf-dwarf-stonebrace";
    private static readonly (int X, int Y, int Z)[] Directions =
    {
        (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)
    };

    private sealed class State
    {
        internal EntityPlayer Self = null!;
        internal bool Active;
        internal double Reduction;
        internal double TargetReduction;
        internal double LastUpdate;
        internal double NextEnvironmentSample;
    }

    private readonly Dictionary<string, State> states = new();
    private ICoreServerAPI? sapi;
    private ICoreClientAPI? capi;
    private IServerNetworkChannel? serverChannel;
    private IClientNetworkChannel? clientChannel;
    private RFMechanicsConfig config = new();
    private long serverTick;
    private long lastSentMs;
    private bool clientActive;

    public override void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(Channel)
            .RegisterMessageType<StonebraceStanceRequest>()
            .RegisterMessageType<StonebraceStanceStatus>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.GetChannel(Channel).SetMessageHandler<StonebraceStanceStatus>(OnStatus);
        api.Event.LeaveWorld += ClearClient;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        try { config = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception error) { api.Logger.Warning("[rfmechanics] Stonebrace using defaults: {0}", error.Message); }
        serverChannel = api.Network.GetChannel(Channel).SetMessageHandler<StonebraceStanceRequest>(OnRequest);
        api.Event.PlayerDisconnect += Remove;
        api.Event.PlayerJoin += Remove;
        api.Event.PlayerDeath += Death;
        serverTick = api.Event.RegisterGameTickListener(Tick, 100);
    }

    internal bool TryToggle(ICoreClientAPI api)
    {
        EntityPlayer? self = api.World.Player?.Entity;
        if (self?.Alive != true || clientChannel?.Connected != true) return false;
        long now = api.World.ElapsedMilliseconds;
        if (now - lastSentMs < 200) return true;
        lastSentMs = now;
        clientChannel.SendPacket(new StonebraceStanceRequest { Active = !clientActive });
        return true;
    }

    private void OnStatus(StonebraceStanceStatus status)
    {
        clientActive = status.Active;
        capi?.ModLoader.GetModSystem<RaceFeedbackModSystem>().Stonebrace(status.Active);
    }

    private void ClearClient()
    {
        clientActive = false;
        lastSentMs = 0;
    }

    private void OnRequest(IServerPlayer player, StonebraceStanceRequest request)
    {
        if (!config.EnableDwarfStonebrace || !RaceTraits.HasTrait(player, config.DwarfTraitCode) || player.Entity is not EntityPlayer self || !self.Alive)
        {
            Remove(player);
            Send(player, false);
            return;
        }

        if (!states.TryGetValue(player.PlayerUID, out State? state) || state.Self != self)
        {
            Remove(player);
            state = new State { Self = self, LastUpdate = Now() };
            states[player.PlayerUID] = state;
        }

        state.Active = request.Active;
        if (state.Active)
        {
            state.TargetReduction = SampleReduction(self);
            state.Reduction = state.TargetReduction;
            state.NextEnvironmentSample = Now() + EnvironmentIntervalSeconds();
            ApplyMovement(state, true);
        }
        else
        {
            state.TargetReduction = 0;
            ApplyMovement(state, false);
        }
        state.LastUpdate = Now();
        Send(player, state.Active);
    }

    private void Tick(float _)
    {
        if (sapi == null) return;
        double now = Now();
        foreach (var online in sapi.World.AllOnlinePlayers)
        {
            if (online is not IServerPlayer player || !states.TryGetValue(player.PlayerUID, out State? state)) continue;
            EntityPlayer self = player.Entity;
            if (state.Self != self || !self.Alive || !config.EnableDwarfStonebrace || !RaceTraits.HasTrait(player, config.DwarfTraitCode))
            {
                Remove(player);
                Send(player, false);
                continue;
            }

            if (state.Active && now >= state.NextEnvironmentSample)
            {
                state.TargetReduction = SampleReduction(self);
                state.NextEnvironmentSample = now + EnvironmentIntervalSeconds();
            }
            state.Reduction = DwarfStonebraceRules.FadeToward(state.Reduction, state.TargetReduction, MaximumReduction(), now - state.LastUpdate, config.DwarfStonebraceReleaseFadeSeconds);
            state.LastUpdate = now;
            if (!state.Active && state.Reduction <= 0) Remove(player);
        }
    }

    internal void Protect(EntityPlayer self, DamageSource source, ref float damage)
    {
        if (sapi == null || !config.EnableDwarfStonebrace || !IsPhysicalAttack(source)) return;
        if (self.Player is not IServerPlayer player || !states.TryGetValue(player.PlayerUID, out State? state) || state.Self != self
            || !RaceTraits.HasTrait(player, config.DwarfTraitCode)) return;

        if (state.Active)
            source.KnockbackStrength *= 1f - (float)Math.Clamp(config.DwarfStonebraceKnockbackReduction, 0, 1);
        damage = DwarfStonebraceRules.ApplyDamage(damage, state.Reduction);
    }

    private double SampleReduction(EntityPlayer self)
    {
        if (sapi == null) return 0;
        var accessor = sapi.World.BlockAccessor;
        int x = (int)Math.Floor(self.Pos.X);
        int y = (int)Math.Floor(self.Pos.Y);
        int z = (int)Math.Floor(self.Pos.Z);
        var center = new BlockPos(x, y, z, self.Pos.Dimension);
        if (!IsLoadedInBounds(accessor, center) || accessor.GetLightLevel(center, EnumLightLevelType.TimeOfDaySunLight) >= config.DwarfStonebraceSunlightThreshold) return 0;

        int scanDistance = Math.Clamp(config.DwarfStonebraceEnclosureScanDistance, 3, 8);
        int[] distances = new int[Directions.Length];
        for (int face = 0; face < Directions.Length; face++)
        {
            var direction = Directions[face];
            for (int distance = 1; distance <= scanDistance; distance++)
            {
                var pos = new BlockPos(x + direction.X * distance, y + direction.Y * distance, z + direction.Z * distance, self.Pos.Dimension);
                if (!IsLoadedInBounds(accessor, pos)) return 0;
                string path = accessor.GetBlockRaw(pos.X, pos.InternalY, pos.Z)?.Code?.Path ?? string.Empty;
                if (DwarfStonebraceRules.MatchesStone(path, config.DwarfStonebraceStoneCodePrefixes))
                {
                    distances[face] = distance;
                    break;
                }
            }
        }

        double depth = DwarfStonebraceRules.DepthFraction(y, sapi.World.SeaLevel, config.DwarfStonebraceDepthFloorY);
        double enclosure = DwarfStonebraceRules.EnclosureFraction(distances, 2, scanDistance, 4);
        return DwarfStonebraceRules.Reduction(depth, enclosure, config.DwarfStonebraceOpenDamageReduction,
            config.DwarfStonebraceEnclosedDamageReduction, MaximumReduction());
    }

    private static bool IsLoadedInBounds(IBlockAccessor accessor, BlockPos pos) =>
        pos.X >= 0 && pos.X < accessor.MapSizeX && pos.Y >= 0 && pos.Y < accessor.MapSizeY && pos.Z >= 0 && pos.Z < accessor.MapSizeZ
        && accessor.GetChunkAtBlockPos(pos) != null;

    private bool IsPhysicalAttack(DamageSource source) => DwarfStonebraceRules.IsPhysicalAttack(
        source.Type is EnumDamageType.BluntAttack or EnumDamageType.PiercingAttack or EnumDamageType.SlashingAttack,
        (source.Source is EnumDamageSource.Entity or EnumDamageSource.Player) && source.GetCauseEntity() != null);

    private double MaximumReduction() => Math.Clamp(config.DwarfStonebraceMaximumDamageReduction, 0, 0.60);
    private double EnvironmentIntervalSeconds() => Math.Clamp(config.DwarfStonebraceEnvironmentIntervalMilliseconds, 100, 10000) / 1000.0;
    private double Now() => sapi!.World.ElapsedMilliseconds / 1000.0;

    private void ApplyMovement(State state, bool active)
    {
        if (!active) { state.Self.Stats.Remove("walkspeed", MovementStat); return; }
        state.Self.Stats.Set("walkspeed", MovementStat, (float)Math.Clamp(config.DwarfStonebraceMovementFactor, 0, 1) - 1f);
    }

    private void Send(IServerPlayer player, bool active) => serverChannel?.SendPacket(new StonebraceStanceStatus { Active = active }, player);

    private void Death(IServerPlayer player, DamageSource _)
    {
        Remove(player);
        Send(player, false);
    }

    private void Remove(IServerPlayer player)
    {
        player.Entity?.Stats.Remove("walkspeed", MovementStat);
        if (states.Remove(player.PlayerUID, out State? state)) state.Self.Stats.Remove("walkspeed", MovementStat);
    }

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.UnregisterGameTickListener(serverTick);
            sapi.Event.PlayerDisconnect -= Remove;
            sapi.Event.PlayerJoin -= Remove;
            sapi.Event.PlayerDeath -= Death;
            foreach (State state in states.Values) state.Self.Stats.Remove("walkspeed", MovementStat);
        }
        if (capi != null) capi.Event.LeaveWorld -= ClearClient;
        states.Clear();
        ClearClient();
        base.Dispose();
    }
}
