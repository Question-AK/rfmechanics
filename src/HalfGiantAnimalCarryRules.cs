using System;
using System.Collections.Generic;

namespace rfmechanics;

public static class HalfGiantAnimalCarryRules
{
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
