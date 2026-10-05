namespace rfmechanics
{
    public static class HalfGiantReachRules
    {
        public const float VanillaPickingRange = 4.5f;
        private const float PickingRangeTolerance = 0.0001f;

        public static bool ShouldOverride(bool enabled, bool isHalfGiant, bool isSurvival)
        {
            return enabled && isHalfGiant && isSurvival;
        }

        public static bool IsVanillaPickingRange(float range)
        {
            return IsSamePickingRange(range, VanillaPickingRange);
        }

        public static bool IsSamePickingRange(float left, float right)
        {
            return System.Math.Abs(left - right) < PickingRangeTolerance;
        }
    }
}
