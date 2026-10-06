using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

internal static class HalfGiantRockPull
{
    private const string CarriedRockPath = "carriedrock";
    // Picking range is measured to the face the player sees; the block centre can be up to ~0.87 further.
    private const double ReachMargin = 1.0;

    internal static TextCommandResult Pull(ICoreServerAPI sapi, IPlayer player, RFMechanicsConfig config)
    {
        EntityPlayer entity = player.Entity;
        BlockSelection? selection = player.CurrentBlockSelection;
        if (selection == null || selection.Position.dimension != entity.Pos.Dimension)
            return Result("rockpull-look");
        EnumGameMode mode = player.WorldData.CurrentGameMode;
        if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator)
            return Result("rockpull-survival");
        ItemSlot? mainHand = player.InventoryManager.ActiveHotbarSlot;
        if (mainHand == null || !mainHand.Empty)
            return Result("rockpull-handfull");
        if (entity.LeftHandItemSlot?.Itemstack?.Collectible is ItemCarriedAnimal or ItemCarriedRock)
            return Result("rockpull-carrying");

        BlockPos pos = selection.Position.Copy();
        var eye = new Vec3d(entity.Pos.X, entity.Pos.InternalY + entity.LocalEyePos.Y, entity.Pos.Z);
        var centre = new Vec3d(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
        if (!HalfGiantRockRules.IsWithinReach(eye.SquareDistanceTo(centre), player.WorldData.PickingRange, ReachMargin))
            return Result("rockpull-reach");

        IBlockAccessor blocks = sapi.World.BlockAccessor;
        Block rock = blocks.GetBlock(pos);
        if (!HalfGiantRockRules.IsEligible(rock.Code?.Domain ?? "", rock.Code?.Path ?? "", CountOpenFaces(blocks, pos), config.HalfGiantRockPullMinOpenFaces, IsAttached(blocks, pos)))
            return Result("rockpull-ineligible");
        if (!sapi.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak))
            return Result("rockpull-claim");
        Item? carriedRock = sapi.World.GetItem(new AssetLocation("rfmechanics", CarriedRockPath));
        if (carriedRock == null)
            return Result("rockpull-unavailable");

        // The vanilla break path keeps reinforcement and collapse; zero drops so the rock leaves only as the held item.
        int rockId = rock.Id;
        blocks.BreakBlock(pos, player, 0);
        bool stillPresent = blocks.GetBlock(pos).Id == rockId;
        bool handedOff = !stillPresent && HandOff(mainHand, carriedRock, rock, entity);
        HalfGiantRockPullOutcome outcome = HalfGiantRockRules.ResolvePull(stillPresent, handedOff);
        if (outcome == HalfGiantRockPullOutcome.Restored && blocks.GetBlock(pos).Id == 0)
            blocks.SetBlock(rockId, pos);

        if (HalfGiantRockRules.ShouldChargeSatiety(outcome))
        {
            EntityBehaviorHunger? hunger = entity.GetBehavior<EntityBehaviorHunger>();
            if (hunger != null)
                hunger.Saturation = HalfGiantQuarryRules.ApplySatietyCost(hunger.Saturation, config.HalfGiantQuarrySatietyCost);
        }

        return outcome switch
        {
            HalfGiantRockPullOutcome.Reinforced => Result("rockpull-reinforced"),
            HalfGiantRockPullOutcome.Restored => Result("rockpull-restored"),
            _ => Result("rockpull-done")
        };
    }

    private static bool HandOff(ItemSlot mainHand, Item carriedRock, Block rock, EntityPlayer holder)
    {
        if (!mainHand.Empty) return false;
        var stack = new ItemStack(carriedRock);
        stack.Attributes.SetString(ItemCarriedRock.RockCodeKey, rock.Code.ToShortString());
        stack.Attributes.SetFloat(ItemCarriedRock.HolderSizeKey, holder.Properties.Client?.Size ?? 0f);
        mainHand.Itemstack = stack;
        mainHand.MarkDirty();
        return mainHand.Itemstack == stack;
    }

    private static int CountOpenFaces(IBlockAccessor blocks, BlockPos pos)
    {
        return HalfGiantRockRules.CountOpenFaces(
            IsOpen(blocks, pos, BlockFacing.NORTH), IsOpen(blocks, pos, BlockFacing.EAST), IsOpen(blocks, pos, BlockFacing.SOUTH),
            IsOpen(blocks, pos, BlockFacing.WEST), IsOpen(blocks, pos, BlockFacing.UP));
    }

    private static bool IsOpen(IBlockAccessor blocks, BlockPos pos, BlockFacing face)
    {
        return !blocks.GetBlock(pos.AddCopy(face)).SideSolid.All;
    }

    // Mirrors BlockBehaviorBreakIfFloating.IsSurroundedByNonSolid, the test that makes a rock drop itself.
    private static bool IsAttached(IBlockAccessor blocks, BlockPos pos)
    {
        foreach (BlockFacing face in BlockFacing.ALLFACES)
        {
            if (blocks.IsSideSolid(pos.X + face.Normali.X, pos.InternalY + face.Normali.Y, pos.Z + face.Normali.Z, face.Opposite))
                return true;
        }
        return false;
    }

    private static TextCommandResult Result(string langCode)
    {
        return TextCommandResult.Success(Lang.Get("rfmechanics:" + langCode));
    }
}
