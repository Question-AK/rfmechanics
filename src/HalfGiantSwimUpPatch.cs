using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace rfmechanics;

// Postfix on PModulePlayerInLiquid.HandleSwimming (owning client only): vanilla sizes the vertical stroke from the
// feet block and two above minus SwimmingOffsetY, so Space alone never lifts the 3.9-block Half-Giant. PlayerModelLib's
// prefix re-enters this method and skips the outer original; __runOriginal keeps this to the inner, real call.
[HarmonyPatch(typeof(PModulePlayerInLiquid), nameof(PModulePlayerInLiquid.HandleSwimming))]
public static class HalfGiantSwimUpPatch
{
    [ThreadStatic] private static BlockPos? tmpPos;

    [HarmonyPostfix]
    public static void Postfix(PModulePlayerInLiquid __instance, bool __runOriginal, float dt, Entity entity, EntityPos pos, EntityControls controls)
    {
        if (!__runOriginal || entity is not EntityPlayer player) return;

        var cfg = RFMechanicsModSystem.Config;
        if (cfg == null || !HalfGiantWater.Applies(player, cfg)) return;

        tmpPos ??= new BlockPos(0);
        tmpPos.SetDimension(pos.Dimension);
        tmpPos.Set((int)pos.X, (int)pos.Y, (int)pos.Z);
        var blocks = entity.World.BlockAccessor;
        float baseTop = (int)pos.Y + blocks.GetBlock(tmpPos, BlockLayersAccess.Fluid).LiquidLevel / 8f - (float)pos.Y;

        float vanillaTop = baseTop;
        for (int k = 1; k <= 2; k++) if (blocks.GetBlockAbove(tmpPos, k, BlockLayersAccess.Fluid).IsLiquid()) vanillaTop += 1.125f;

        float swimLine = (float)HalfGiantWater.SwimLine(player, cfg);
        float giantTop = baseTop;
        for (int k = 1, reach = (int)swimLine + 2; k <= reach; k++) if (blocks.GetBlockAbove(tmpPos, k, BlockLayersAccess.Fluid).IsLiquid()) giantTop += 1.125f;

        double vanillaRise = Rise(vanillaTop - (float)entity.SwimmingOffsetY, dt, controls, __instance.Push);
        double giantRise = Rise(giantTop - swimLine, dt, controls, __instance.Push);
        if (giantRise > 0) giantRise *= cfg.HalfGiantSwimUpFactor;

        double inWaterGravity = GlobalConstants.GravityPerSecond * 0.33f * dt;
        pos.Motion.Y += giantRise - vanillaRise - inWaterGravity * cfg.HalfGiantSwimSink;

        // Space alone counts as a stroke, as movement keys already do, or no rise weaker than a human's could beat gravity.
        if (controls.Jump && !controls.TriesToMove && entity.ApplyGravity && !controls.IsClimbing && !controls.IsFlying)
            pos.Motion.Y += (GlobalConstants.GravityPerSecond + Math.Max(0, -0.015f * pos.Motion.Y)) * 0.33f * dt;
    }

    // Vanilla HandleSwimming's vertical term for a given liquid depth above the swim line.
    private static double Rise(float depthAboveLine, float dt, EntityControls controls, float push)
    {
        float n = Math.Min(1f, GameMath.Clamp(depthAboveLine, 0f, 1f) + 0.075f);
        if (controls.Jump) return n > 0.1f || !controls.TriesToMove ? 0.005f * n * dt * 60f : 0.0;
        return controls.FlyVector.Y * (1f + push) * 0.03f * n;
    }
}
