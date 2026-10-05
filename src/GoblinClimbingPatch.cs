using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics
{
    /// <summary>
    /// Lets Goblins climb living tree trunks (shared classifier, same as Elf) and raw
    /// natural rock (whitelist below) as if they were ladders, at plain vanilla ladder speed --
    /// no saturation cost, since vanilla's own baseline hunger drain has no IsClimbing-specific
    /// term at all (only Dwarf's ClimbSaturationPatch is a deliberately ADDED cost).
    /// Parallel to TreeClimbingPatch (Elf), not an extension of it: raw rock is never vanilla
    /// Climbable-flagged, so the dwarf-style extension-of-vanilla-ladder-detection shape never
    /// fires for it -- only TreeClimbingPatch's self-contained-scan shape generalizes. The two
    /// patches share ClimbCornerTraversal for face selection and corner wrapping; only the block
    /// filter below is goblin-specific.
    /// Rock and dry-earth climbing additionally require the Clamber stance (GoblinClamberStance),
    /// so a goblin doesn't stick to ordinary terrain walls during ordinary work; trunks are ungated
    /// like the Elf's. Tree, rock and earth climbing are independently toggleable; the rock
    /// whitelist (GoblinRockClimbCodePrefixes) covers raw rock plus rough worked-stone/masonry
    /// families and the earth list (GoblinEarthClimbCodes) covers bare/packed earth and ground
    /// cover -- see the config's doc comments for the different match rules the two use. Chiseled logs/rock (BlockEntityMicroBlock) are climbable
    /// via the same fallback TreeClimbingPatch.IsClimbableLog uses for Elves -- see
    /// GoblinClimbFilter.
    /// </summary>
    [HarmonyPatch(typeof(EntityBehaviorControlledPhysics))]
    public static class GoblinClimbingPatch
    {
        private static bool loggedException = false;

        private static readonly ClimbCornerTraversal corners = new();

        [HarmonyPatch(nameof(EntityBehaviorControlledPhysics.MotionAndCollision))]
        [HarmonyPostfix]
        public static void MotionAndCollisionPostfix(EntityBehaviorControlledPhysics __instance, EntityPos pos, EntityControls controls, float dt)
        {
            try
            {
                if (!TryGetGoblin(__instance, out Entity? entity)) return;
                int freeHands = FreeHandCount((EntityPlayer)entity!);
                if (freeHands == 0) corners.ForgetFreeHandGrip(entity!);
                if (!corners.TryGetGrip(entity!, pos, BuildFilter(entity!, freeHands), CornersEnabled(), out ClimbGrip grip)) return;

                float motionFactor = grip.MotionFactor;
                if (controls.Jump)
                {
                    pos.Motion.Y = __instance.climbDownSpeed * dt * 60f * motionFactor; // vanilla naming is inverted: Jump = ascend
                }
                else if (controls.Sneak)
                {
                    pos.Motion.Y = Math.Max(-__instance.climbUpSpeed * motionFactor, pos.Motion.Y - __instance.climbUpSpeed * motionFactor); // Sneak = descend
                }
                else
                {
                    // Cancels whatever gravity/other modules already applied to Motion.Y this tick -- runs after those modules, not instead of them.
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
                if (!TryGetGoblin(__instance, out Entity? entity)) return;
                int freeHands = FreeHandCount((EntityPlayer)entity!);
                if (freeHands == 0) corners.ForgetFreeHandGrip(entity!);

                // A ladder or trunk grip is not a wall grip, so it must not leave a corner window
                // armed to suspend gravity after the goblin steps off it.
                if (controls.IsClimbing) { corners.Forget(entity!); return; }

                corners.Update(entity!, pos, controls, BuildFilter(entity!, freeHands), CornersEnabled(),
                    RFMechanicsModSystem.Config?.GoblinCornerGraceTicks ?? 0);
            }
            catch (Exception ex)
            {
                LogExceptionOnce(ex);
            }
        }

        /// <summary>charClass null check is load-bearing (HasTrait returns true for a null class
        /// by default). Does not gate on the individual tree/rock/earth toggles here -- those are
        /// per-match-group toggles inside the filter, so one toggle off and another on
        /// still gets partial climbing.</summary>
        private static bool TryGetGoblin(EntityBehaviorControlledPhysics behavior, out Entity? entity)
        {
            entity = null;

            var cfg = RFMechanicsModSystem.Config;
            if (cfg == null) return false;
            if (!cfg.EnableGoblinTreeClimbing && !cfg.EnableGoblinRockClimbing && !cfg.EnableGoblinEarthClimbing) return false;

            Entity candidate = behavior.entity;
            if (candidate is not EntityPlayer player) return false;
            if (candidate.Properties.CanClimb != true) return false;

            IPlayer iplayer = player.World.PlayerByUid(player.PlayerUID);
            if (!RaceTraits.HasTrait(iplayer, cfg.GoblinTraitCode)) return false;

            entity = candidate;
            return true;
        }

        private static bool CornersEnabled() => RFMechanicsModSystem.Config?.EnableGoblinCornerTraversal == true;

        /// <summary>Null when nothing is currently grippable, which the scan takes as an immediate
        /// miss. Rebuilt per tick because the Clamber stance can drop rock and earth mid-climb.</summary>
        private static GoblinClimbFilter? BuildFilter(Entity entity, int freeHands)
        {
            var cfg = RFMechanicsModSystem.Config;
            if (cfg == null) return null;

            bool wallAllowed = GoblinClamberStance.AllowsWallClimb(entity, cfg);
            bool checkTrees = cfg.EnableGoblinTreeClimbing;
            bool freeHandWalls = !cfg.EnableGoblinFreeHandClimbing || freeHands > 0;
            bool checkRock = cfg.EnableGoblinRockClimbing && wallAllowed && freeHandWalls;
            bool checkEarth = cfg.EnableGoblinEarthClimbing && wallAllowed && freeHandWalls;
            if (!checkTrees && !checkRock && !checkEarth) return null;

            float wallMotionFactor = !cfg.EnableGoblinFreeHandClimbing || freeHands >= 2
                ? 1f
                : (float)cfg.GoblinFreeHandOneHandWallSpeedFactor;
            return new GoblinClimbFilter(checkTrees, checkRock, checkEarth, wallMotionFactor,
                cfg.GoblinRockClimbCodePrefixes ?? Array.Empty<string>(),
                cfg.GoblinEarthClimbCodes ?? Array.Empty<string>());
        }

        internal static bool IsFreeHandWallClimbing(EntityPlayer player)
            => corners.HasFreeHandGrip(player);

        internal static int FreeHandCount(EntityPlayer player)
        {
            int freeHands = player.RightHandItemSlot?.Empty != false ? 1 : 0;
            return freeHands + (player.LeftHandItemSlot?.Empty != false ? 1 : 0);
        }

        private sealed class GoblinClimbFilter : IClimbBlockFilter
        {
            private readonly bool checkTrees, checkRock, checkEarth;
            private readonly float wallMotionFactor;
            private readonly string[] rockPrefixes;
            private readonly string[] earthCodes;

            internal GoblinClimbFilter(bool checkTrees, bool checkRock, bool checkEarth, float wallMotionFactor, string[] rockPrefixes, string[] earthCodes)
            {
                this.checkTrees = checkTrees;
                this.checkRock = checkRock;
                this.checkEarth = checkEarth;
                this.wallMotionFactor = wallMotionFactor;
                this.rockPrefixes = rockPrefixes;
                this.earthCodes = earthCodes;
            }

            public ClimbGrip GetGrip(IWorldAccessor world, Block block, BlockPos pos)
            {
                if (checkTrees && TreeBlockClassifier.IsLivingTrunk(world, block, pos)) return ClimbGrip.Full;
                if (MatchesNonTreePath(block?.Code?.Path)) return new ClimbGrip(wallMotionFactor, true);

                BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
                if (blockEntity is BlockEntityMicroBlock micro && micro.BlockIds != null)
                {
                    foreach (int id in micro.BlockIds)
                    {
                        string? path = world.GetBlock(id)?.Code?.Path;
                        if (checkTrees && TreeBlockClassifier.IsLivingTrunkPath(path)) return ClimbGrip.Full;
                        if (MatchesNonTreePath(path)) return new ClimbGrip(wallMotionFactor, true);
                    }
                }

                return ClimbGrip.None;
            }

            private bool MatchesNonTreePath(string? path)
            {
                if (path == null) return false;
                return (checkRock && MatchesAnyPrefix(path, rockPrefixes))
                    || (checkEarth && MatchesAnyCode(path, earthCodes));
            }
        }

        /// <summary>Bare code or code-plus-variant, not a raw prefix: "cob" must hit cob without
        /// also hitting the cobblestone family.</summary>
        private static bool MatchesAnyCode(string path, string[] codes)
        {
            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i];
                if (string.IsNullOrEmpty(code)) continue;
                if (path.Length == code.Length ? path == code
                    : path.Length > code.Length && path[code.Length] == '-' && path.StartsWith(code)) return true;
            }
            return false;
        }

        private static bool MatchesAnyPrefix(string path, string[] prefixes)
        {
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (!string.IsNullOrEmpty(prefixes[i]) && path.StartsWith(prefixes[i])) return true;
            }
            return false;
        }

        private static void LogExceptionOnce(Exception ex)
        {
            if (!loggedException)
            {
                loggedException = true;
                RFMechanicsModSystem.Api?.Logger?.Warning("[rfmechanics] Exception in GoblinClimbingPatch: {0}", ex);
            }
        }
    }
}
