namespace rfmechanics
{
    public static class HalfGiantReachRules
    {
        private const float PickingRangeTolerance = 0.0001f;

        public static bool ShouldOverride(bool enabled, bool isHalfGiant, bool isSurvival)
        {
            return enabled && isHalfGiant && isSurvival;
        }

        public static float ResolvePickingRange(float baseline, float halfGiantRange, bool enabled, bool isHalfGiant, bool isSurvival)
        {
            return ShouldOverride(enabled, isHalfGiant, isSurvival) ? halfGiantRange : baseline;
        }

        public static float ResolveManagedPickingRange(
            float currentRange,
            float baseline,
            float ownedRange,
            bool hasActiveOverride,
            float halfGiantRange,
            bool enabled,
            bool isHalfGiant,
            bool isSurvival,
            out bool hasActiveOverrideAfter)
        {
            if (ShouldOverride(enabled, isHalfGiant, isSurvival))
            {
                hasActiveOverrideAfter = true;
                return halfGiantRange;
            }

            hasActiveOverrideAfter = false;
            return hasActiveOverride && IsSamePickingRange(currentRange, ownedRange) ? baseline : currentRange;
        }

        public static bool IsSamePickingRange(float left, float right)
        {
            return System.Math.Abs(left - right) < PickingRangeTolerance;
        }
    }
}
