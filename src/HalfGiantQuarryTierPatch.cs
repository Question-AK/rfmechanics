using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace rfmechanics;

// Target: ServerSystemBlockSimulation.TryModifyBlockInWorld, which rejects breaks whose tool tier is below
// RequiredMiningTier; an empty hand is tier 0, so quarried rock reset client-side. GetRequiredMiningTier has
// no player argument, so the breaking player is captured for the duration of the server's own break handler.
[HarmonyPatch]
public static class HalfGiantQuarryTierPatch
{
    [ThreadStatic] private static IPlayer? breakingPlayer;

    internal static IPlayer? BreakingPlayer => breakingPlayer;

    private static MethodBase? TargetMethod() =>
        AccessTools.Method("Vintagestory.Server.ServerSystemBlockSimulation:TryModifyBlockInWorld");

    // Load-bearing: a missing internal target must skip this patch, not abort PatchAll for the whole mod.
    private static bool Prepare() => TargetMethod() != null;

    private static void Prefix(object player) => breakingPlayer = player as IPlayer;

    private static void Finalizer() => breakingPlayer = null;
}

[HarmonyPatch(typeof(Block), nameof(Block.GetRequiredMiningTier))]
public static class HalfGiantQuarryRequiredTierPatch
{
    [HarmonyPostfix]
    public static void Postfix(Block __instance, ref int __result)
    {
        IPlayer? player = HalfGiantQuarryTierPatch.BreakingPlayer;
        var cfg = RFMechanicsModSystem.Config;
        if (__result == 0 || player?.Entity == null || cfg == null) return;

        if (HalfGiantQuarryRules.MayQuarry(
                cfg.EnableHalfGiantQuarry,
                RaceTraits.HasTrait(player, cfg.HalfGiantTraitCode),
                player.WorldData.CurrentGameMode == EnumGameMode.Creative,
                player.InventoryManager.ActiveHotbarSlot.Itemstack == null,
                __instance.Code?.Domain ?? "",
                __instance.Code?.Path ?? ""))
        {
            __result = 0;
        }
    }
}
