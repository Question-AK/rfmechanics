using System;
using System.Collections.Generic;

namespace rfmechanics;

public enum HalfGiantCarryAdmission
{
    Refused,
    Animal,
    TagExempt
}

public enum HalfGiantThrowLanding
{
    Keep,
    RemoveWithoutDrops
}

public readonly struct HalfGiantCarryCandidate
{
    public HalfGiantCarryCandidate(long entityId, double angleRadians, double distance)
    {
        EntityId = entityId;
        AngleRadians = angleRadians;
        Distance = distance;
    }

    public long EntityId { get; }
    public double AngleRadians { get; }
    public double Distance { get; }
}

public static class HalfGiantAnimalCarryRules
{
    // A dedicated player.json code reusing vanilla's throwaim keyframes, so only this item's windup
    // speed changes (not the spear/snowball/beenade throws that also trigger the shared "aim" code).
    public const string ThrowWindupAnimationCode = "halfgiantthrowaim";

    public static HalfGiantCarryAdmission Admit(bool hasAnimalTag, string? codePath, IEnumerable<string>? tagExemptPrefixes)
    {
        if (hasAnimalTag) return HalfGiantCarryAdmission.Animal;
        return MatchesPrefix(tagExemptPrefixes, codePath) ? HalfGiantCarryAdmission.TagExempt : HalfGiantCarryAdmission.Refused;
    }

    public static double CaptureReach(HalfGiantCarryAdmission admission, double animalReach, double tagExemptReach)
    {
        return admission == HalfGiantCarryAdmission.TagExempt ? tagExemptReach : animalReach;
    }

    public static bool IsEligible(
        bool enabled,
        bool isHalfGiant,
        bool isLivingAnimal,
        bool hasEmptyOffhand,
        bool isVisibleAndInReach,
        bool hasClaimAccess,
        bool hasOwnerAccess,
        bool isOrdinary,
        double volume,
        double longestDimension,
        double maximumVolume,
        double maximumDimension,
        string code,
        IEnumerable<string>? allowedCodes,
        IEnumerable<string>? deniedCodes)
    {
        if (!enabled || !isHalfGiant || !isLivingAnimal || !hasEmptyOffhand || !isVisibleAndInReach || !hasClaimAccess || !hasOwnerAccess || !isOrdinary)
            return false;

        if (Matches(deniedCodes, code)) return false;
        if (Matches(allowedCodes, code)) return true;

        return double.IsFinite(volume) && double.IsFinite(longestDimension)
            && volume <= maximumVolume && longestDimension <= maximumDimension;
    }

    public static bool IsSnapshotValid(string? className, string? entityCode, byte[]? payload, string? captureId)
    {
        return !string.IsNullOrWhiteSpace(className)
            && !string.IsNullOrWhiteSpace(entityCode)
            && payload is { Length: > 0 }
            && Guid.TryParseExact(captureId, "N", out _);
    }

    public static bool CanRelease(bool snapshotIsValid, bool collisionFree, bool hasClaimAccess, bool isHalfGiant, bool wasReleased)
    {
        return snapshotIsValid && collisionFree && hasClaimAccess && isHalfGiant && !wasReleased;
    }

    public static bool ShouldConsumeCapture(bool spawned)
    {
        return spawned;
    }

    public static bool IsThrowReady(float secondsUsed, float windupSeconds)
    {
        return secondsUsed >= windupSeconds;
    }

    // Full charge is a separate, longer hold past IsThrowReady; releasing before it still fires the quick throw.
    public static bool IsFullyCharged(float secondsUsed, float fullChargeSeconds)
    {
        return secondsUsed >= fullChargeSeconds;
    }

    // throwaim's final keyframe sits at quantityFrames - 1 with onAnimationEnd "Hold"; RunningAnimation.Progress
    // advances CurrentFrame by 30 * dt * animationSpeed each tick, so this speed lands on that frame exactly
    // at fullChargeSeconds and holds there, while the quick-throw point shows a proportionally shorter pull-back.
    public static float WindupAnimationSpeed(int quantityFrames, double fullChargeSeconds)
    {
        if (quantityFrames <= 1 || !(fullChargeSeconds > 0)) return 1f;
        return (float)((quantityFrames - 1) / (30.0 * fullChargeSeconds));
    }

    // The quick throw keeps base speed exactly; only a full charge is multiplied.
    public static double ChargedThrowSpeed(double speed, bool isFullyCharged, double fullChargeSpeedMultiplier)
    {
        return isFullyCharged ? speed * Math.Max(1.0, fullChargeSpeedMultiplier) : speed;
    }

    // Square-root falloff: a creature ten times a chicken's volume leaves the hand at about a third of its speed.
    public static double ThrowSpeed(double volume, double fullSpeedVolume, double baseSpeed, double minimumSpeed)
    {
        if (!(baseSpeed > 0) || double.IsPositiveInfinity(baseSpeed)) return 0;
        double floor = minimumSpeed > 0 ? Math.Min(minimumSpeed, baseSpeed) : 0;
        if (!(volume > 0) || double.IsPositiveInfinity(volume) || !(fullSpeedVolume > 0)) return floor;
        return Math.Min(baseSpeed, Math.Max(floor, baseSpeed * Math.Sqrt(fullSpeedVolume / volume)));
    }

    public static float HeldScale(float creatureSize, float holderSize, float defaultHolderSize)
    {
        float creature = creatureSize > 0 && !float.IsPositiveInfinity(creatureSize) ? creatureSize : 1f;
        float holder = holderSize > 0 && !float.IsPositiveInfinity(holderSize) ? holderSize : defaultHolderSize;
        return creature / holder;
    }

    public static float HitDamage(double volume, double damagePerVolume, double minimumDamage, double maximumDamage)
    {
        double maximum = maximumDamage > 0 ? maximumDamage : 0;
        double minimum = minimumDamage > 0 ? Math.Min(minimumDamage, maximum) : 0;
        double damage = volume * damagePerVolume;
        return (float)(double.IsNaN(damage) ? minimum : Math.Min(maximum, Math.Max(minimum, damage)));
    }

    // Mirrors EntityProjectileBase.CanDealDamage: a player target is also an EntityAgent, so it needs both privileges.
    public static bool CanDamage(bool targetIsPlayer, bool targetIsCreature, bool pvpAllowed, bool canAttackPlayers, bool canAttackCreatures)
    {
        if (targetIsPlayer && (!pvpAllowed || !canAttackPlayers)) return false;
        return !targetIsCreature || canAttackCreatures;
    }

    public static bool ShouldHit(ISet<long> hitEntityIds, long targetId, long throwerId, long thrownId, bool canDamage)
    {
        if (targetId == throwerId || targetId == thrownId || !canDamage) return false;
        return hitEntityIds.Add(targetId);
    }

    public static bool HasLanded(bool onGroundOrInLiquid, long flightMilliseconds, long minimumFlightMilliseconds)
    {
        return onGroundOrInLiquid && flightMilliseconds >= minimumFlightMilliseconds;
    }

    public static HalfGiantThrowLanding Landing(bool isHostile, bool removeHostilesInForeignClaims, bool throwerCanBuild)
    {
        return isHostile && removeHostilesInForeignClaims && !throwerCanBuild
            ? HalfGiantThrowLanding.RemoveWithoutDrops
            : HalfGiantThrowLanding.Keep;
    }

    // Nearest-angle wins so the candidate closest to the crosshair is preferred over a merely-closer one; ties break on distance.
    public static long? SelectNearestInCone(IEnumerable<HalfGiantCarryCandidate> candidates, double maxConeRadians)
    {
        long? bestId = null;
        double bestAngle = double.PositiveInfinity;
        double bestDistance = double.PositiveInfinity;
        foreach (HalfGiantCarryCandidate candidate in candidates)
        {
            if (!(candidate.AngleRadians <= maxConeRadians)) continue;
            if (candidate.AngleRadians > bestAngle) continue;
            if (candidate.AngleRadians == bestAngle && candidate.Distance >= bestDistance) continue;
            bestId = candidate.EntityId;
            bestAngle = candidate.AngleRadians;
            bestDistance = candidate.Distance;
        }
        return bestId;
    }

    // A fresh pickup's own guard window; repeated "R" presses within it must not immediately release what was just captured.
    public static bool IsWithinReleaseGuard(long millisecondsSinceCapture, long guardMilliseconds)
    {
        return guardMilliseconds > 0 && millisecondsSinceCapture >= 0 && millisecondsSinceCapture < guardMilliseconds;
    }

    public static bool MatchesPrefix(IEnumerable<string>? prefixes, string? codePath)
    {
        if (prefixes == null || string.IsNullOrWhiteSpace(codePath)) return false;
        foreach (string prefix in prefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && codePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool Matches(IEnumerable<string>? codes, string code)
    {
        if (codes == null || string.IsNullOrWhiteSpace(code)) return false;
        foreach (string configuredCode in codes)
        {
            if (string.Equals(configuredCode, code, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
