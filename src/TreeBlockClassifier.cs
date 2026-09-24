using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics
{
    /// <summary>
    /// Identifies tree trunk and branch-foliage block families without deciding what any race may
    /// do with them. Prefixes are deliberately bounded at the family separator: a placed block or
    /// an unrelated mod block must not match just because it contains a familiar word.
    /// </summary>
    internal static class TreeBlockClassifier
    {
        private static readonly string[] livingTrunkPrefixes =
        {
            "log-grown-",
            "logsection-grown-",
            "lognarrow-grown-"
        };

        private static readonly string[] branchFoliagePrefixes =
        {
            "leavesbranchy-"
        };

        // Vanilla has one static branchy foliage identifier without the family separator.
        private const string StaticBranchFoliagePath = "leavesbranchystatic";

        internal static bool IsLivingTrunk(IWorldAccessor world, Block block, BlockPos pos)
        {
            if (IsLivingTrunkPath(block?.Code?.Path)) return true;

            // Chiselling replaces the visible path with "chiseledblock". Preserve the existing
            // constituent-material behavior, but keep the classification in this shared helper.
            BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
            if (blockEntity is BlockEntityMicroBlock micro && micro.BlockIds != null)
            {
                foreach (int id in micro.BlockIds)
                {
                    if (IsLivingTrunkPath(world.GetBlock(id)?.Code?.Path)) return true;
                }
            }

            return false;
        }

        internal static bool IsLivingTrunkPath(string? path)
            => HasAnyPrefix(path, livingTrunkPrefixes);

        internal static bool IsBranchFoliagePath(string? path)
            => path == StaticBranchFoliagePath || HasAnyPrefix(path, branchFoliagePrefixes);

        private static bool HasAnyPrefix(string? path, string[] prefixes)
        {
            if (string.IsNullOrEmpty(path)) return false;

            for (int i = 0; i < prefixes.Length; i++)
            {
                if (path.StartsWith(prefixes[i], StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
