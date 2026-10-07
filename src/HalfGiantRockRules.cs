using System;
using System.Collections.Generic;

// C# 5 only: Verify-HalfGiantWork.ps1 compiles this file with Windows PowerShell 5.1 Add-Type.
namespace rfmechanics
{
    public enum HalfGiantRockPullOutcome
    {
        Reinforced,
        Restored,
        Pulled
    }

    public static class HalfGiantRockRules
    {
        // PlayerModelLib's halfgiant ModelSizeFactor, used when a stack lacks the stored holder size.
        public const float DefaultHolderSize = 2.1f;

        public static int CountOpenFaces(bool northOpen, bool eastOpen, bool southOpen, bool westOpen, bool upOpen)
        {
            int count = 0;
            if (northOpen) count++;
            if (eastOpen) count++;
            if (southOpen) count++;
            if (westOpen) count++;
            if (upOpen) count++;
            return count;
        }

        // A detached rock is refused: BreakIfFloating would drop the raw block even at zero drop quantity.
        public static bool IsEligible(string domain, string path, int openFaces, int minimumOpenFaces, bool attached)
        {
            return attached && HalfGiantQuarryRules.IsNaturalRock(domain, path) && openFaces >= Math.Max(1, minimumOpenFaces);
        }

        public static bool IsWithinReach(double squaredDistance, double pickingRange, double margin)
        {
            double reach = pickingRange + margin;
            return pickingRange > 0 && squaredDistance <= reach * reach;
        }

        public static HalfGiantRockPullOutcome ResolvePull(bool blockStillPresent, bool handedOff)
        {
            if (blockStillPresent) return HalfGiantRockPullOutcome.Reinforced;
            return handedOff ? HalfGiantRockPullOutcome.Pulled : HalfGiantRockPullOutcome.Restored;
        }

        public static bool ShouldChargeSatiety(HalfGiantRockPullOutcome outcome)
        {
            return outcome == HalfGiantRockPullOutcome.Pulled;
        }

        // Held items render at the holder's size times this scale; the rock block's own size is 1.
        public static float HeldScale(float holderSize, float defaultHolderSize)
        {
            float holder = holderSize > 0 && !float.IsInfinity(holderSize) ? holderSize : defaultHolderSize;
            return 1f / holder;
        }

        public static bool RecordHit(ICollection<long> hitEntityIds, long targetId)
        {
            if (hitEntityIds.Contains(targetId)) return false;
            hitEntityIds.Add(targetId);
            return true;
        }

        public static bool IsLandingDrop(bool isBlock, string domain, string path)
        {
            return !(isBlock && HalfGiantQuarryRules.IsNaturalRock(domain, path));
        }
    }
}
