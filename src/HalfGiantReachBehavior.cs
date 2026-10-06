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

    private bool modeEventsSubscribed;
    private bool pendingEligibilityEvaluation = true;
    private bool wasEligible;

    public HalfGiantReachBehavior(Entity entity) : base(entity) { }

    public override string PropertyName() => "rfhalfgiantreach";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);

        SubscribeModeEvents();
    }

    public override void OnEntityRevive()
    {
        SubscribeModeEvents();
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity is not EntityPlayer player) return;
        if (entity.World.PlayerByUid(player.PlayerUID) is not IServerPlayer serverPlayer) return;

        UpdateReach(serverPlayer, true);
    }

    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        if (entity is EntityPlayer player && entity.World.PlayerByUid(player.PlayerUID) is IServerPlayer serverPlayer)
        {
            if (serverPlayer.WorldData.CurrentGameMode == EnumGameMode.Survival) RestoreOwnedOverride(serverPlayer, true);
            else ClearOverrideState(serverPlayer);
        }

        wasEligible = false;
        pendingEligibilityEvaluation = false;
        UnsubscribeModeEvents();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        UnsubscribeModeEvents();
    }

    private void OnPlayerSwitchGameMode(IServerPlayer serverPlayer)
    {
        if (entity is not EntityPlayer player || serverPlayer.PlayerUID != player.PlayerUID) return;

        ClearOverrideState(serverPlayer);
        wasEligible = false;
        pendingEligibilityEvaluation = true;
        UpdateReach(serverPlayer, false);
    }

    private void UpdateReach(IServerPlayer serverPlayer, bool broadcast)
    {
        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null || entity is not EntityPlayer player) return;

        bool isHalfGiant = entity.GetBehavior<PlayerRaceBehavior>()?.Race == PlayerRace.HalfGiant;
        bool isSurvival = serverPlayer.WorldData.CurrentGameMode == EnumGameMode.Survival;
        bool eligible = HalfGiantReachRules.ShouldOverride(cfg.EnableHalfGiantReach, isHalfGiant, isSurvival);

        if (!eligible)
        {
            if (isSurvival) RestoreOwnedOverride(serverPlayer, broadcast);
            else ClearOverrideState(serverPlayer);

            wasEligible = false;
            pendingEligibilityEvaluation = false;
            return;
        }

        float currentRange = serverPlayer.WorldData.PickingRange;
        float targetRange = (float)cfg.HalfGiantPickingRange;
        if (HasActiveOverride(serverPlayer))
        {
            float ownedRange = GetOverrideRange(serverPlayer, targetRange);
            if (!HalfGiantReachRules.IsSamePickingRange(currentRange, ownedRange))
            {
                ClearOverrideState(serverPlayer);
            }
            else if (!HalfGiantReachRules.IsSamePickingRange(ownedRange, targetRange))
            {
                SetOverrideState(serverPlayer, targetRange);
                SetPickingRange(serverPlayer, targetRange, broadcast);
            }

            wasEligible = true;
            pendingEligibilityEvaluation = false;
            return;
        }

        if ((pendingEligibilityEvaluation || !wasEligible) && HalfGiantReachRules.IsVanillaPickingRange(currentRange))
        {
            SetBaseline(serverPlayer, currentRange);
            SetOverrideState(serverPlayer, targetRange);
            SetPickingRange(serverPlayer, targetRange, broadcast);
        }

        wasEligible = true;
        pendingEligibilityEvaluation = false;
    }

    private void RestoreOwnedOverride(IServerPlayer serverPlayer, bool broadcast)
    {
        if (HasActiveOverride(serverPlayer)
            && TryGetBaseline(serverPlayer, out float baseline)
            && HalfGiantReachRules.IsSamePickingRange(serverPlayer.WorldData.PickingRange, GetOverrideRange(serverPlayer, 0)))
        {
            SetPickingRange(serverPlayer, baseline, broadcast);
        }

        ClearOverrideState(serverPlayer);
    }

    private void SubscribeModeEvents()
    {
        if (modeEventsSubscribed || entity.Api is not ICoreServerAPI serverApi || entity is not EntityPlayer) return;

        serverApi.Event.PlayerSwitchGameMode += OnPlayerSwitchGameMode;
        modeEventsSubscribed = true;
    }

    private void UnsubscribeModeEvents()
    {
        if (!modeEventsSubscribed || entity.Api is not ICoreServerAPI serverApi) return;

        serverApi.Event.PlayerSwitchGameMode -= OnPlayerSwitchGameMode;
        modeEventsSubscribed = false;
    }

    private static void SetPickingRange(IServerPlayer serverPlayer, float target, bool broadcast)
    {
        if (HalfGiantReachRules.IsSamePickingRange(serverPlayer.WorldData.PickingRange, target)) return;

        serverPlayer.WorldData.PickingRange = target;
        if (broadcast) serverPlayer.BroadcastPlayerData();
    }

    private static bool TryGetBaseline(IPlayer owner, out float baseline)
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

    private static void SetBaseline(IPlayer owner, float baseline)
    {
        owner.WorldData.SetModdata(BaselineKey, BitConverter.GetBytes(baseline));
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

    private static void SetOverrideState(IPlayer owner, float ownedRange)
    {
        owner.WorldData.SetModdata(OverrideActiveKey, new byte[] { 1 });
        owner.WorldData.SetModdata(OverrideRangeKey, BitConverter.GetBytes(ownedRange));
    }

    private static void ClearOverrideState(IPlayer owner)
    {
        owner.WorldData.RemoveModdata(BaselineKey);
        owner.WorldData.RemoveModdata(OverrideActiveKey);
        owner.WorldData.RemoveModdata(OverrideRangeKey);
    }
}
