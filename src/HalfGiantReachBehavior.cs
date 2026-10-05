using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace rfmechanics;

public sealed class HalfGiantReachBehavior : EntityBehavior
{
    private const string BaselineKey = "rfmechanics:halfgiant-picking-range-baseline";

    public HalfGiantReachBehavior(Entity entity) : base(entity) { }

    public override string PropertyName() => "rfhalfgiantreach";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity is not EntityPlayer player) return;

        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null) return;

        IPlayer? owner = entity.World.PlayerByUid(player.PlayerUID);
        if (owner is not IServerPlayer serverPlayer || owner.WorldData.CurrentGameMode != EnumGameMode.Survival) return;

        bool isHalfGiant = entity.GetBehavior<PlayerRaceBehavior>()?.Race == PlayerRace.HalfGiant;
        float target = HalfGiantReachRules.ResolvePickingRange(
            GetServerBaseline(owner),
            (float)cfg.HalfGiantPickingRange,
            cfg.EnableHalfGiantReach,
            isHalfGiant,
            true);

        if (Math.Abs(owner.WorldData.PickingRange - target) < 0.0001f) return;

        owner.WorldData.PickingRange = target;
        serverPlayer.BroadcastPlayerData();
    }

    private static float GetServerBaseline(IPlayer owner)
    {
        byte[]? stored = owner.WorldData.GetModdata(BaselineKey);
        if (stored?.Length == sizeof(float)) return BitConverter.ToSingle(stored, 0);

        float baseline = owner.WorldData.PickingRange;
        owner.WorldData.SetModdata(BaselineKey, BitConverter.GetBytes(baseline));
        return baseline;
    }
}
