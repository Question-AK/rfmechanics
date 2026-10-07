using System;

namespace rfmechanics
{
    public static class OrcSkinRules
    {
        // Complete vanilla 1.22.6 tin-bronze lamellar profile, ProtectionTier=2.
        // Tier controls weapon-tier loss rates; it is not a percentage or divisor.
        public static double Protect(double damage, int weaponTier)
        {
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0) return damage;
            int attack = Math.Max(0, weaponTier);
            int within = Math.Min(attack, 2), above = attack - within;
            double flat = Math.Max(0, 0.6 - within * 0.1 - above * 0.2);
            double relative = 0.77 * Math.Pow(0.97, within) * Math.Pow(0.85, above);
            return Math.Max(0, damage - flat) * (1 - relative);
        }
    }
}
