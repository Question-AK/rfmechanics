using HarmonyLib;
using Vintagestory.API.Common;

namespace rfmechanics;

[HarmonyPatch(typeof(Block), nameof(Block.OnGettingBroken))]
public static class HalfGiantQuarryPatch
{
    [HarmonyPostfix]
    public static void Postfix(Block __instance, IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, ref float __result)
    {
        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null) return;

        bool activeHandEmpty = itemslot.Itemstack == null && player.InventoryManager.ActiveHotbarSlot.Itemstack == null;
        bool isHalfGiant = player.Entity.GetBehavior<PlayerRaceBehavior>()?.Race == PlayerRace.HalfGiant;
        if (!HalfGiantQuarryRules.MayQuarry(
                cfg.EnableHalfGiantQuarry,
                isHalfGiant,
                player.WorldData.CurrentGameMode == EnumGameMode.Creative,
                activeHandEmpty,
                __instance.Code?.Domain ?? "",
                __instance.Code?.Path ?? "")) return;

        __result = remainingResistance - (float)cfg.HalfGiantQuarryMiningSpeed * dt;
    }
}
