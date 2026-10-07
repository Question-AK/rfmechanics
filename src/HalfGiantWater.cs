using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace rfmechanics;

internal static class HalfGiantWater
{
    internal static bool Applies(EntityPlayer player, RFMechanicsConfig? cfg) =>
        cfg?.EnableHalfGiantWater == true && player.GetBehavior<PlayerRaceBehavior>()?.Race == PlayerRace.HalfGiant;

    // Measured from the live eye, so sneaking lowers it the way it lowers vanilla's SelectionBox-based line.
    internal static double SwimLine(EntityPlayer player, RFMechanicsConfig cfg) =>
        player.LocalEyePos.Y - cfg.HalfGiantSwimLineBelowEye;
}

// Postfix on EntityBehaviorControlledPhysics.ApplyTests: it sets Swimming from SelectionBox.Y2 x 0.66 (2.56 for the
// Half-Giant) on the owning client and, for players, on the server, with no public seam. It finishes before the next
// tick's PlayerModelLib HandleSwimming prefix and PModuleGravity.DoApply postfix, so both read the corrected flag.
[HarmonyPatch(typeof(EntityBehaviorControlledPhysics), nameof(EntityBehaviorControlledPhysics.ApplyTests))]
public static class HalfGiantWadingPatch
{
    private static readonly ConditionalWeakTable<EntityPlayer, StrongBox<double>> swimEyeRestore = new();

    [HarmonyPostfix]
    public static void Postfix(EntityBehaviorControlledPhysics __instance, EntityPos pos)
    {
        if (__instance.Entity is not EntityPlayer player) return;

        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null || !HalfGiantWater.Applies(player, cfg))
        {
            if (swimEyeRestore.TryGetValue(player, out var saved))
            {
                player.Properties.SwimmingEyeHeight = saved.Value;
                swimEyeRestore.Remove(player);
            }
            return;
        }

        // Breath (server) and IsEyesSubmerged (client) sample SwimmingEyeHeight, which PlayerModelLib leaves at 1.7.
        swimEyeRestore.GetValue(player, p => new StrongBox<double>(p.Properties.SwimmingEyeHeight));
        player.Properties.SwimmingEyeHeight = player.LocalEyePos.Y;

        if (!player.Swimming) return;

        int x = (int)(pos.X + (player.CollisionBox.X2 - player.OriginCollisionBox.X2));
        int y = (int)(pos.InternalY + HalfGiantWater.SwimLine(player, cfg));
        int z = (int)(pos.Z + (player.CollisionBox.Z2 - player.OriginCollisionBox.Z2));
        if (!player.World.BlockAccessor.GetBlockRaw(x, y, z, BlockLayersAccess.Fluid).IsLiquid()) player.Swimming = false;
    }
}
