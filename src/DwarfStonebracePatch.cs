using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace rfmechanics;

// Runs after vanilla armor and shield delegates but before health subtraction, so Stonebrace composes once with their result.
[HarmonyPatch(typeof(EntityBehaviorHealth), "ApplyOnDamageDelegates")]
public static class DwarfStonebracePatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
    {
        if (__instance.entity is not EntityPlayer self || self.World.Side != EnumAppSide.Server || !self.Alive || damageSource == null) return;
        self.Api.ModLoader.GetModSystem<DwarfStonebraceModSystem>()?.Protect(self, damageSource, ref damage);
    }
}
