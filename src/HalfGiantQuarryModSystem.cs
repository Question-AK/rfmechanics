using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

public sealed class HalfGiantQuarryModSystem : ModSystem
{
    private ICoreServerAPI? sapi;

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;
        api.Event.DidBreakBlock += OnDidBreakBlock;
    }

    private void OnDidBreakBlock(IServerPlayer player, int oldBlockId, BlockSelection selection)
    {
        var cfg = RFMechanicsModSystem.Config;
        if (sapi == null || cfg == null) return;

        Block oldBlock = sapi.World.GetBlock(oldBlockId);
        bool activeHandEmpty = player.InventoryManager.ActiveHotbarSlot.Itemstack == null;
        bool isHalfGiant = RaceTraits.HasTrait(player, cfg.HalfGiantTraitCode);
        if (!HalfGiantQuarryRules.MayQuarry(
                cfg.EnableHalfGiantQuarry,
                isHalfGiant,
                player.WorldData.CurrentGameMode == EnumGameMode.Creative,
                activeHandEmpty,
                oldBlock.Code?.Domain ?? "",
                oldBlock.Code?.Path ?? "")) return;

        EntityBehaviorHunger? hunger = player.Entity.GetBehavior<EntityBehaviorHunger>();
        if (hunger != null)
            hunger.Saturation = HalfGiantQuarryRules.ApplySatietyCost(hunger.Saturation, cfg.HalfGiantQuarrySatietyCost);
    }

    public override void Dispose()
    {
        if (sapi != null) sapi.Event.DidBreakBlock -= OnDidBreakBlock;
        sapi = null;
        base.Dispose();
    }
}
