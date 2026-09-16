using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

// Run before vanilla sends character selection state to the joining client.
// This changes only retired class codes; racial model/extra traits and gear stay intact.
[HarmonyPatch(typeof(CharacterSystem), "Event_PlayerJoinServer")]
public static class RetiredDwarfClassMigration
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(CharacterSystem __instance, IServerPlayer byPlayer)
    {
        var entity = byPlayer.Entity;
        string previous = entity.WatchedAttributes.GetString("characterClass");
        if (previous != "rf-mountain-dwarf" && previous != "rf-hill-dwarf") return;

        __instance.setCharacterClass(entity, "commoner", initializeGear: false);
        entity.World.Logger.Notification("[rfmechanics] Migrated retired dwarf class {0} to commoner; inventory and racial model preserved.", previous);
    }
}

// Player Model Lib reads the saved class while initializing player behaviors,
// before PlayerJoin is raised. Change the saved code before that initialization;
// the normal behavior initialization then composes race and class traits.
[HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.Initialize))]
public static class RetiredDwarfClassInitializationMigration
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(EntityPlayer __instance, ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;
        string previous = __instance.WatchedAttributes.GetString("characterClass");
        if (previous != "rf-mountain-dwarf" && previous != "rf-hill-dwarf") return;

        __instance.WatchedAttributes.SetString("characterClass", "commoner");
        api.Logger.Notification("[rfmechanics] Migrated retired dwarf class {0} to commoner before player initialization; inventory and racial model preserved.", previous);
    }
}
