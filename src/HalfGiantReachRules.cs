namespace rfmechanics
{
    public static class HalfGiantReachRules
    {
        public static bool ShouldOverride(bool enabled, bool isHalfGiant, bool isSurvival)
        {
            return enabled && isHalfGiant && isSurvival;
        }

        public static float ResolvePickingRange(float baseline, float halfGiantRange, bool enabled, bool isHalfGiant, bool isSurvival)
        {
            return ShouldOverride(enabled, isHalfGiant, isSurvival) ? halfGiantRange : baseline;
        }
    }
}
