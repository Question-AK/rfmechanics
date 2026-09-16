using System;

namespace rfmechanics
{

// Pure production arithmetic, also exercised by the existing PowerShell offline-check workflow.
public static class OrcMetabolismFeedbackRules
{
    public static double Finite(double value, double fallback, double min, double max)
        { return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value)); }

    public static double Curve(double satiety, double gate, double exponent)
    {
        gate = Finite(gate, 0.5, 0.01, 1);
        exponent = Finite(exponent, 1.5, 0.25, 5);
        return Math.Pow(Math.Max(0, Math.Min(1, 1 - Finite(satiety, 1, 0, 1) / gate)), exponent);
    }

    public static double LossPerHour(double before, double after, double gameHours)
    {
        if (gameHours <= 0 || double.IsNaN(gameHours) || double.IsInfinity(gameHours)) return 0;
        return Finite((before - after) / gameHours, 0, 0, 10);
    }

    // A conservative locomotion sample: deliberate ground travel aligned with the physics
    // walk vector. No charge on blocked input, riding, swimming, flying, falls, hurt/knockback,
    // teleports or stale samples. Unrecognised mod-applied pushes remain a compatibility limit.
    public static double Exertion(double dx, double dz, double seconds, double walkX, double walkZ,
        bool eligible, bool sprint, double walkingCost)
    {
        if (!eligible || seconds <= 0 || seconds > 1.5) return 0;
        double length = Math.Sqrt(dx * dx + dz * dz), intended = Math.Sqrt(walkX * walkX + walkZ * walkZ);
        if (double.IsNaN(length) || double.IsInfinity(length) || intended < 0.000001
            || length / seconds < 0.15 || length / seconds > 12) return 0;
        if ((dx * walkX + dz * walkZ) / (length * intended) < 0.75) return 0;
        return sprint ? 1 : Finite(walkingCost, 0.35, 0, 1);
    }

    public static double Debt(double hourlyRate, double curve, double hours, double exertion)
        { return Finite(hourlyRate, 0.6, 0, 10) * Finite(curve, 0, 0, 1)
            * Finite(hours, 0, 0, 1) * Finite(exertion, 0, 0, 1); }

    public static int DepthBand(double y, double seaLevel)
    {
        if (seaLevel <= 0 || double.IsNaN(y) || double.IsInfinity(y)) return 0;
        return Math.Min(3, (int)(Math.Max(0, (seaLevel - y) / seaLevel) * 4));
    }
}

}
