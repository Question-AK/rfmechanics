using System;

namespace rfmechanics
{
    public static class DwarfStonebraceRules
    {
        public static double DepthFraction(double y, double seaLevel, double floorY)
        {
            double normalization = seaLevel - floorY;
            if (!IsFinite(y) || !IsFinite(seaLevel) || !IsFinite(floorY) || normalization <= 0) return 0;
            return Clamp((seaLevel - y) / normalization, 0, 1);
        }

        public static double EnclosureFraction(int[] stoneDistances, int closeDistance, int scanDistance, int requiredFaces)
        {
            if (stoneDistances == null || stoneDistances.Length < requiredFaces || closeDistance < 1 || scanDistance <= closeDistance || requiredFaces < 1) return 0;

            double[] scores = new double[stoneDistances.Length];
            for (int i = 0; i < stoneDistances.Length; i++)
            {
                int distance = stoneDistances[i];
                scores[i] = distance >= 1 && distance <= scanDistance
                    ? Clamp((scanDistance - distance) / (double)(scanDistance - closeDistance), 0, 1)
                    : 0;
            }

            Array.Sort(scores);
            double total = 0;
            for (int i = 0; i < requiredFaces; i++) total += scores[scores.Length - 1 - i];
            return Clamp(total / requiredFaces, 0, 1);
        }

        public static double Reduction(double depth, double enclosure, double openReduction, double enclosedReduction, double cap)
        {
            cap = Clamp(cap, 0, 0.60);
            openReduction = Clamp(openReduction, 0, cap);
            enclosedReduction = Clamp(enclosedReduction, openReduction, cap);
            return Clamp(Clamp(depth, 0, 1) * (openReduction + (enclosedReduction - openReduction) * Clamp(enclosure, 0, 1)), 0, cap);
        }

        public static double FadeToward(double current, double target, double maximumReduction, double elapsedSeconds, double fadeSeconds)
        {
            maximumReduction = Clamp(maximumReduction, 0, 0.60);
            current = Clamp(current, 0, maximumReduction);
            target = Clamp(target, 0, maximumReduction);
            if (target >= current) return target;
            double rate = maximumReduction / Math.Max(0.05, fadeSeconds);
            return Math.Max(target, current - Math.Max(0, elapsedSeconds) * rate);
        }

        public static float ApplyDamage(float postArmorDamage, double reduction)
        {
            if (float.IsNaN(postArmorDamage) || float.IsInfinity(postArmorDamage) || postArmorDamage <= 0) return postArmorDamage;
            return (float)(postArmorDamage * (1 - Clamp(reduction, 0, 0.60)));
        }

        public static bool IsPhysicalAttack(bool physicalAttackType, bool attackerProvenance)
        {
            return physicalAttackType && attackerProvenance;
        }

        public static bool MatchesStone(string path, string[] prefixes)
        {
            if (string.IsNullOrEmpty(path) || prefixes == null) return false;
            for (int i = 0; i < prefixes.Length; i++)
                if (!string.IsNullOrEmpty(prefixes[i]) && path.StartsWith(prefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
