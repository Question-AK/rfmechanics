using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics
{
    [HarmonyPatch(typeof(AiTaskBaseTargetable), "CanSensePlayer")]
    public static class GoblinScoutingBaseTargetablePatch
    {
        [HarmonyPrefix]
        public static void Prefix(AiTaskBaseTargetable __instance, EntityPlayer eplr, ref double range)
        {
            float factor = GoblinScoutingPatch.GetFactor(GoblinScoutingPatch.GetTaskEntity(__instance), eplr,
                __instance.TargetEntity == eplr, out bool applies, out GoblinScoutingState state);
            if (!applies) return;
            float seekingRange = eplr.Stats.GetBlended("animalSeekingRange");
            if (seekingRange <= 0) return;
            factor /= seekingRange;
            if (state == GoblinScoutingState.CrouchedDarkGround) factor /= 0.6f;
            range *= factor;
        }
    }

    [HarmonyPatch(typeof(AiTaskBaseTargetableR), "GetDetectionRangeMultiplier")]
    public static class GoblinScoutingRangedTargetablePatch
    {
        [HarmonyPostfix]
        public static void Postfix(AiTaskBaseTargetableR __instance, Entity target, ref float __result)
        {
            if (target is not EntityPlayer player) return;

            float factor = GoblinScoutingPatch.GetFactor(GoblinScoutingPatch.GetTaskEntity(__instance), player,
                __instance.TargetEntity == player, out bool applies, out GoblinScoutingState state);
            if (!applies) return;
            float seekingRange = player.Stats.GetBlended("animalSeekingRange");
            if (seekingRange <= 0) return;
            factor /= seekingRange;
            if (state == GoblinScoutingState.CrouchedDarkGround)
            {
                AiTaskBaseTargetableConfig? config = Traverse.Create(__instance).Property("Config").GetValue<AiTaskBaseTargetableConfig>();
                if (config != null && config.SneakRangeReduction > 0) factor /= config.SneakRangeReduction;
            }
            __result *= factor;
        }
    }

    internal static class GoblinScoutingPatch
    {
        internal static Entity? GetTaskEntity(object task)
            => Traverse.Create(task).Field("entity").GetValue<Entity>();

        internal static float GetFactor(Entity? source, EntityPlayer player, bool activeTarget, out bool applies, out GoblinScoutingState state)
        {
            applies = false;
            state = GoblinScoutingState.None;
            RFMechanicsConfig? cfg = RFMechanicsModSystem.Config;
            if (cfg == null || !cfg.EnableGoblinScouting || source == null) return 1f;
            if (!IsGoblin(player, cfg)) return 1f;

            bool drifterFamily = GoblinScoutingRules.MatchesFamily(source.Code?.Path, cfg.GoblinScoutingDrifterFamilyCodes);
            bool animalFamily = GoblinScoutingRules.MatchesFamily(source.Code?.Path, cfg.GoblinScoutingAnimalFamilyCodes);
            applies = drifterFamily || animalFamily;
            if (!applies) return 1f;
            bool dark = IsDark(player, cfg);
            bool heldLight = HasEmittingHeldLight(player);
            bool closeContact = source.Pos.DistanceTo(player.Pos) <= cfg.GoblinScoutingContactRevealRange;
            bool freeHandWallClimb = GoblinClimbingPatch.IsFreeHandWallClimbing(player);
            int freeHands = GoblinClimbingPatch.FreeHandCount(player);
            bool sprinting = player.Controls.Sprint && player.Controls.TriesToMove;

            return GoblinScoutingRules.ResolveFactor(drifterFamily, animalFamily, dark, heldLight, activeTarget,
                closeContact, player.Controls.Sneak, player.OnGround, freeHandWallClimb, freeHands, sprinting, cfg, out state);
        }

        internal static bool IsGoblin(EntityPlayer player, RFMechanicsConfig cfg)
        {
            IPlayer iplayer = player.World.PlayerByUid(player.PlayerUID);
            return RaceTraits.HasTrait(iplayer, cfg.GoblinTraitCode);
        }

        internal static bool IsDark(EntityPlayer player, RFMechanicsConfig cfg)
        {
            BlockPos sample = new((int)player.Pos.X, (int)(player.Pos.Y + player.LocalEyePos.Y), (int)player.Pos.Z, player.Pos.Dimension);
            return player.World.BlockAccessor.GetLightLevel(sample, EnumLightLevelType.MaxTimeOfDayLight) <= cfg.GoblinScoutingMaxAmbientLight;
        }

        internal static bool HasEmittingHeldLight(EntityPlayer player)
            => SlotEmits(player, player.RightHandItemSlot) || SlotEmits(player, player.LeftHandItemSlot);

        private static bool SlotEmits(EntityPlayer player, ItemSlot? slot)
        {
            ItemStack? stack = slot?.Itemstack;
            if (stack?.Collectible == null) return false;
            byte[] light = stack.Collectible.GetLightHsv(player.World.BlockAccessor, null, stack);
            return light != null && light.Length > 2 && light[2] > 0;
        }
    }
}
