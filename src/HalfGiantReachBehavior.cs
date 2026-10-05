using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace rfmechanics;

public sealed class HalfGiantReachBehavior : EntityBehavior
{
    private const string BaselineKey = "rfmechanics:halfgiant-picking-range-baseline";
    private const string OverrideActiveKey = "rfmechanics:halfgiant-picking-range-override-active";
    private const string OverrideRangeKey = "rfmechanics:halfgiant-picking-range-override-range";

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
        if (owner is not IServerPlayer serverPlayer) return;

        bool isHalfGiant = entity.GetBehavior<PlayerRaceBehavior>()?.Race == PlayerRace.HalfGiant;
        bool isSurvival = owner.WorldData.CurrentGameMode == EnumGameMode.Survival;
        bool shouldOverride = HalfGiantReachRules.ShouldOverride(cfg.EnableHalfGiantReach, isHalfGiant, isSurvival);
        float currentRange = owner.WorldData.PickingRange;
        float halfGiantRange = (float)cfg.HalfGiantPickingRange;

        if (shouldOverride)
        {
            float baseline = GetServerBaseline(owner);
            float ownedRange = GetOverrideRange(owner, halfGiantRange);
            float target = HalfGiantReachRules.ResolveManagedPickingRange(
                currentRange,
                baseline,
                ownedRange,
                HasActiveOverride(owner),
                halfGiantRange,
                cfg.EnableHalfGiantReach,
                isHalfGiant,
                isSurvival,
                out bool hasActiveOverrideAfter);

            SetOverrideState(owner, target, hasActiveOverrideAfter);
            SetPickingRange(serverPlayer, target);
            return;
        }

        if (!TryGetServerBaseline(owner, out float storedBaseline)) return;

        bool hasActiveOverride = HasActiveOverride(owner);
        float storedOverrideRange = GetOverrideRange(owner, halfGiantRange);
        bool ownsLegacyOverride = !hasActiveOverride && HalfGiantReachRules.IsSamePickingRange(currentRange, storedOverrideRange);
        float restoredRange = HalfGiantReachRules.ResolveManagedPickingRange(
            currentRange,
            storedBaseline,
            storedOverrideRange,
            hasActiveOverride || ownsLegacyOverride,
            halfGiantRange,
            cfg.EnableHalfGiantReach,
            isHalfGiant,
            isSurvival,
            out _);

        ClearOverrideState(owner);
        SetPickingRange(serverPlayer, restoredRange);
    }

    private static void SetPickingRange(IServerPlayer serverPlayer, float target)
    {
        if (HalfGiantReachRules.IsSamePickingRange(serverPlayer.WorldData.PickingRange, target)) return;

        serverPlayer.WorldData.PickingRange = target;
        serverPlayer.BroadcastPlayerData();
    }

    private static bool TryGetServerBaseline(IPlayer owner, out float baseline)
    {
        byte[]? stored = owner.WorldData.GetModdata(BaselineKey);
        if (stored?.Length == sizeof(float))
        {
            baseline = BitConverter.ToSingle(stored, 0);
            return true;
        }

        baseline = 0;
        return false;
    }

    private static float GetServerBaseline(IPlayer owner)
    {
        if (TryGetServerBaseline(owner, out float baseline)) return baseline;

        baseline = owner.WorldData.PickingRange;
        owner.WorldData.SetModdata(BaselineKey, BitConverter.GetBytes(baseline));
        return baseline;
    }

    private static bool HasActiveOverride(IPlayer owner)
    {
        byte[]? stored = owner.WorldData.GetModdata(OverrideActiveKey);
        return stored?.Length == 1 && stored[0] == 1;
    }

    private static float GetOverrideRange(IPlayer owner, float fallback)
    {
        byte[]? stored = owner.WorldData.GetModdata(OverrideRangeKey);
        return stored?.Length == sizeof(float) ? BitConverter.ToSingle(stored, 0) : fallback;
    }

    private static void SetOverrideState(IPlayer owner, float ownedRange, bool isActive)
    {
        owner.WorldData.SetModdata(OverrideActiveKey, new[] { isActive ? (byte)1 : (byte)0 });
        owner.WorldData.SetModdata(OverrideRangeKey, BitConverter.GetBytes(ownedRange));
    }

    private static void ClearOverrideState(IPlayer owner)
    {
        owner.WorldData.RemoveModdata(BaselineKey);
        owner.WorldData.RemoveModdata(OverrideActiveKey);
        owner.WorldData.RemoveModdata(OverrideRangeKey);
    }
}
