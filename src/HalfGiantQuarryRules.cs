using System;

namespace rfmechanics
{
    public static class HalfGiantQuarryRules
    {
        public static bool IsNaturalRock(string domain, string path)
        {
            return domain == "game" && path != null && (path.StartsWith("rock-", StringComparison.Ordinal) || path.StartsWith("crackedrock-", StringComparison.Ordinal));
        }

        public static bool MayQuarry(bool enabled, bool isHalfGiant, bool isCreative, bool activeHandEmpty, string domain, string path)
        {
            return enabled && isHalfGiant && !isCreative && activeHandEmpty && IsNaturalRock(domain, path);
        }

        public static float ApplySatietyCost(float saturation, float cost)
        {
            return saturation > cost ? saturation - cost : 0f;
        }
    }
}
