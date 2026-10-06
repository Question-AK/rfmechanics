using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics
{
    /// <summary>
    /// Lets Elves climb living tree trunks recognized by TreeBlockClassifier as if they were
    /// ladders, at plain vanilla ladder speed.
    /// Two postfixes on EntityBehaviorControlledPhysics, not EntityBehaviorPlayerPhysics: that
    /// subclass overrides OnPhysicsTick but not these two methods, so patching the base class
    /// covers players (see BranchyLeavesPassthroughPatch.SimPhysicsPrefix for the OnPhysicsTick case).
    /// MotionAndCollisionPostfix does the vertical-motion work, not ApplyTestsPostfix: an earlier
    /// version postfixed only ApplyTests (mirroring vanilla's IsClimbing flag), but that ran too
    /// late to affect that tick's ApplyTerrainCollision, and PModuleGravity.Applicable() skipping
    /// gravity off the stale flag from the previous tick's postfix suppressed gravity without ever
    /// running the climb-speed correction -- observed in-game as sticking to the tree with no
    /// gravity and no vertical movement. Postfixing MotionAndCollision runs before
    /// ApplyTerrainCollision consumes pos.Motion and sets an absolute value, so it's independent
    /// of whether gravity already ran that tick.
    /// ApplyTestsPostfix sets animation state and owns the corner-traversal bookkeeping (see
    /// ClimbCornerTraversal, shared with GoblinClimbingPatch), firing only when vanilla's own scan
    /// found nothing, so a real ladder (even one built onto a tree) still takes priority.
    /// </summary>
    [HarmonyPatch(typeof(EntityBehaviorControlledPhysics))]
    public static class TreeClimbingPatch
    {
        private static bool loggedException = false;

        private static readonly ClimbCornerTraversal corners = new();

        [HarmonyPatch(nameof(EntityBehaviorControlledPhysics.MotionAndCollision))]
        [HarmonyPostfix]
        public static void MotionAndCollisionPostfix(EntityBehaviorControlledPhysics __instance, EntityPos pos, EntityControls controls, float dt)
        {
            try
            {
                if (!TryGetElf(__instance, out Entity? entity)) return;
                if (!corners.HoldsGrip(entity!, pos, LogClimbFilter.Instance, CornersEnabled())) return;

                if (controls.Jump)
                {
                    pos.Motion.Y = __instance.climbDownSpeed * dt * 60f; // vanilla naming is inverted: Jump = ascend
                }
                else if (controls.Sneak)
                {
                    pos.Motion.Y = Math.Max(-__instance.climbUpSpeed, pos.Motion.Y - __instance.climbUpSpeed); // Sneak = descend
                }
                else
                {
                    // Cancels whatever gravity/other modules already applied to Motion.Y this tick -- we run after those modules, not instead of them.
                    pos.Motion.Y = 0;
                }
            }
            catch (Exception ex)
            {
                LogExceptionOnce(ex);
            }
        }

        [HarmonyPatch(nameof(EntityBehaviorControlledPhysics.ApplyTests))]
        [HarmonyPostfix]
        public static void ApplyTestsPostfix(EntityBehaviorControlledPhysics __instance, EntityPos pos, EntityControls controls)
        {
            try
            {
                if (!TryGetElf(__instance, out Entity? entity)) return;

                // A ladder grip is not a trunk grip, so it must not leave a corner window armed to
                // suspend gravity after the elf steps off it.
                if (controls.IsClimbing) { corners.Forget(entity!); return; }

                corners.Update(entity!, pos, controls, LogClimbFilter.Instance, CornersEnabled(),
                    RFMechanicsModSystem.Config?.ElfCornerGraceTicks ?? 0);
            }
            catch (Exception ex)
            {
                LogExceptionOnce(ex);
            }
        }

        // charClass null-check is load-bearing: HasTrait returns true for a null class by default.
        private static bool TryGetElf(EntityBehaviorControlledPhysics behavior, out Entity? entity)
        {
            entity = null;

            var cfg = RFMechanicsModSystem.Config;
            if (cfg == null || !cfg.EnableTreeClimbing) return false;

            Entity candidate = behavior.entity;
            if (candidate is not EntityPlayer player) return false;
            if (candidate.Properties.CanClimb != true) return false;

            IPlayer iplayer = player.World.PlayerByUid(player.PlayerUID);
            if (!RaceTraits.HasTrait(iplayer, cfg.ElfTraitCode)) return false;

            entity = candidate;
            return true;
        }

        private static bool CornersEnabled() => RFMechanicsModSystem.Config?.EnableElfCornerTraversal == true;

        /// <summary>Stateless, so one shared instance instead of the goblin's per-tick rebuild.</summary>
        private sealed class LogClimbFilter : IClimbBlockFilter
        {
            internal static readonly LogClimbFilter Instance = new();

            public ClimbGrip GetGrip(IWorldAccessor world, Block block, BlockPos pos)
                => IsClimbableLog(world, block, pos) ? ClimbGrip.Full : ClimbGrip.None;
        }

        private static bool IsClimbableLog(IWorldAccessor world, Block block, BlockPos pos)
            => TreeBlockClassifier.IsLivingTrunk(world, block, pos);

        private static void LogExceptionOnce(Exception ex)
        {
            if (!loggedException)
            {
                loggedException = true;
                RFMechanicsModSystem.Api?.Logger?.Warning("[rfmechanics] Exception in TreeClimbingPatch: {0}", ex);
            }
        }
    }
}
