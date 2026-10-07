using HarmonyLib;
using Vintagestory.API.Common;

namespace rfmechanics;

// Postfix on EntityPlayer.GetWalkSpeedMultiplier (ground physics on the owning client, animation speed on both sides):
// vanilla divides by 2.5 whenever FeetInLiquid and no stat reaches that term. A plain multiply, so its order against
// PlayerModelLib's ApplyMovementSpeedStats postfix on the same method does not matter.
[HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.GetWalkSpeedMultiplier))]
public static class HalfGiantWadeSpeedPatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityPlayer __instance, ref double __result)
    {
        if (!__instance.FeetInLiquid || __instance.Swimming) return;

        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null || !HalfGiantWater.Applies(__instance, cfg)) return;

        __result *= cfg.HalfGiantWadingSpeedFactor;
    }
}
