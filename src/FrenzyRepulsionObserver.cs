using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace rfmechanics;

// Observe vanilla agent pushes without altering them. Aligned held input must not
// turn an external push into a Frenzy bill. The grace also covers residual motion.
[HarmonyPatch(typeof(EntityBehaviorRepulseAgents), nameof(EntityBehaviorRepulseAgents.OnGameTick))]
internal static class FrenzyRepulsionObserver
{
    [HarmonyPrefix]
    private static void Before(EntityBehaviorRepulseAgents __instance,
        out (double X, double Z, FrenzyBehavior? Frenzy) __state)
    {
        var e = __instance.entity;
        var frenzy = e.World.Side == EnumAppSide.Server && e is EntityPlayer
            ? e.GetBehavior<FrenzyBehavior>() : null;
        __state = (e.Pos.Motion.X, e.Pos.Motion.Z, frenzy);
    }
    [HarmonyPostfix]
    private static void After(EntityBehaviorRepulseAgents __instance,
        (double X, double Z, FrenzyBehavior? Frenzy) __state)
    {
        if (__state.Frenzy != null && (__instance.entity.Pos.Motion.X != __state.X
            || __instance.entity.Pos.Motion.Z != __state.Z)) __state.Frenzy.ExternalMotion();
    }
}
