using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace rfmechanics;

// Run after the health delegates (including vanilla armor/shields) but BEFORE health
// subtraction. No duplicate natural layer and no shared entity/type properties changed.
[HarmonyPatch(typeof(EntityBehaviorHealth), "ApplyOnDamageDelegates")]
public static class OrcNaturalProtectionPatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
    {
        if (__instance.entity is not EntityPlayer self || self.World.Side != EnumAppSide.Server
            || !self.Alive || !float.IsFinite(damage) || damage <= 0 || damageSource == null) return;
        self.Api.ModLoader.GetModSystem<OrcBracingModSystem>()?.Protect(self, damageSource, ref damage);
    }
}
