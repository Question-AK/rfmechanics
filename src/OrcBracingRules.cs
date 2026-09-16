using System;

namespace rfmechanics
{
    // Pure production rules also exercised by the offline PowerShell check. No game state.
    public sealed class OrcBraceTuning
    {
        public double Arc = 120, InitialRate = 1, MaximumRate = 5, RampSeconds = 30;
        public double RecoverySeconds = 60, FoodFloor = 0.30, RestartMargin = 0.02;

        public void Normalize()
        {
            Arc = Bound(Arc, 120, 20, 180);
            InitialRate = Bound(InitialRate, 1, 0.1, 20);
            MaximumRate = Bound(MaximumRate, 5, InitialRate, 40);
            RampSeconds = Bound(RampSeconds, 30, 1, 300);
            RecoverySeconds = Bound(RecoverySeconds, 60, 1, 600);
            FoodFloor = Bound(FoodFloor, 0.30, 0.01, 0.90);
            RestartMargin = Bound(RestartMargin, 0.02, 0.005, 0.09);
        }

        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public static double Bound(double value, double fallback, double min, double max)
        { return Math.Max(min, Math.Min(max, Finite(value) ? value : fallback)); }

        public double Rate(double exertion) { return InitialRate + (MaximumRate - InitialRate) * exertion; }

        // Exact integral of the linear ramp, splitting at the cap. Units are satiety points.
        public double Cost(double exertion, double seconds)
        {
            double ramp = Math.Min(seconds, (1 - exertion) * RampSeconds);
            return Rate(exertion) * ramp + (MaximumRate - InitialRate) * ramp * ramp / (2 * RampSeconds)
                + MaximumRate * (seconds - ramp);
        }
    }

    public sealed class OrcBraceState
    {
        public bool Active;
        public double Exertion;

        public bool CanStart(double food, double maxFood, OrcBraceTuning tuning)
        {
            return OrcBraceTuning.Finite(food) && OrcBraceTuning.Finite(maxFood) && maxFood > 0
                && food >= maxFood * (tuning.FoodFloor + tuning.RestartMargin);
        }

        // Returns true only on forced release. Does not refill already-low food.
        // Settles on every tick AND before every toggle: repeated toggles cannot erase elapsed cost.
        public bool Advance(double seconds, ref double food, double maxFood, OrcBraceTuning tuning)
        {
            seconds = OrcBraceTuning.Bound(seconds, 0, 0, double.MaxValue);
            if (!Active)
            {
                Exertion = Math.Max(0, Exertion - seconds / tuning.RecoverySeconds);
                return false;
            }
            if (!OrcBraceTuning.Finite(food) || !OrcBraceTuning.Finite(maxFood) || maxFood <= 0)
            { Active = false; return true; }

            double available = Math.Max(0, food - maxFood * tuning.FoodFloor);
            double cost = tuning.Cost(Exertion, seconds);
            bool forced = available <= cost;
            double activeSeconds = seconds;
            if (forced)
            {
                // Solve the monotonic integral at the food floor, including long server ticks.
                double lo = 0, hi = Math.Min(seconds, available / tuning.InitialRate);
                for (int i = 0; i < 40; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (tuning.Cost(Exertion, mid) < available) lo = mid; else hi = mid;
                }
                activeSeconds = (lo + hi) / 2;
                cost = available;
            }
            food -= cost;
            Exertion = Math.Min(1, Exertion + activeSeconds / tuning.RampSeconds);
            if (forced)
            {
                Active = false;
                Exertion = Math.Max(0, Exertion - (seconds - activeSeconds) / tuning.RecoverySeconds);
            }
            return forced;
        }
    }

    public static class OrcProtectionRules
    {
        // VS 1.22.6 armor.json: sewn leather (T1), iron lamellar (T3). Tier alone
        // does not specify strength. These are complete, explicit interim profiles.
        public static double Protect(double damage, int weaponTier, int protectionTier)
        {
            if (!OrcBraceTuning.Finite(damage) || damage <= 0) return damage;
            bool braced = protectionTier == 3;
            double flat = braced ? 0.7 : 0.6;
            double relative = braced ? 0.79 : 0.6;
            int tier = braced ? 3 : 1;
            int attack = Math.Max(0, weaponTier);
            int within = Math.Min(attack, tier), above = attack - within;
            // Same per-weapon-tier losses and flat-then-relative order as vanilla
            // ModSystemWearableStats.handleDamaged, in closed form (bounded work).
            flat -= within * (braced ? 0.1 : 0.05) + above * (braced ? 0.2 : 0.1);
            relative *= Math.Pow(braced ? 0.97 : 0.985, within) * Math.Pow(braced ? 0.85 : 0.925, above);
            // Vanilla can make flat negative at high weapon tiers. Natural skin must
            // never INCREASE incoming damage; clip that term at zero explicitly.
            return Math.Max(0, damage - Math.Max(0, flat)) * (1 - Math.Max(0, relative));
        }

        public static bool InFront(double yaw, double towardX, double towardZ, double arc)
        {
            double length = Math.Sqrt(towardX * towardX + towardZ * towardZ);
            if (!OrcBraceTuning.Finite(length) || length < 0.000001 || !OrcBraceTuning.Finite(yaw)) return false;
            // EntityPos.GetViewVector(0, yaw) = (-sin(yaw), 0, -cos(yaw)).
            double dot = (-Math.Sin(yaw) * towardX - Math.Cos(yaw) * towardZ) / length;
            return dot >= Math.Cos(arc * Math.PI / 360) - 0.000000001;
        }
    }
}
