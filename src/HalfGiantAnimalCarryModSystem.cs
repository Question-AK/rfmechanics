using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

public sealed class HalfGiantAnimalCarryModSystem : ModSystem
{
    private const string CarriedAnimalPath = "carriedanimal";
    private const string ClassNameKey = "classname";
    private const string CreatureCodeKey = "creaturecode";
    private const string SerializedKey = "animalSerialized";
    private const string CaptureIdentityKey = "carryIdentity";
    private const string DisplayItemKey = "displayitem";
    private const string SourceEntityIdKey = "sourceEntityId";

    private readonly HashSet<string> releasedCaptureIdentities = new(StringComparer.Ordinal);
    private ICoreServerAPI? sapi;
    private TagSetFast animalTag;
    private bool animalTagAvailable;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        animalTagAvailable = api.EntityTagRegistry.TryCreateTagSet(out animalTag, "animal") == TagRegistryError.None;
        api.ChatCommands.Create("rfhalfgiantcarry")
            .WithDescription("Capture an eligible animal or drifter into a Half-Giant's offhand, or release one carried in either hand.")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("use")
            .HandleWith(args => Use(args.Caller.Player))
            .EndSubCommand();
    }

    private TextCommandResult Use(IPlayer? player)
    {
        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        if (sapi == null || player?.Entity == null || config == null)
            return TextCommandResult.Success("Animal carrying is unavailable.");
        if (!config.EnableHalfGiantAnimalCarry)
            return TextCommandResult.Success("Animal carrying is disabled.");
        if (!RaceTraits.HasTrait(player, config.HalfGiantTraitCode))
            return TextCommandResult.Success("Only Half-Giants can carry animals.");

        ItemSlot? offhand = player.Entity.LeftHandItemSlot;
        if (offhand != null && IsCarriedAnimal(offhand.Itemstack))
            return Release(player, offhand, config);

        ItemSlot? mainHand = player.InventoryManager.ActiveHotbarSlot;
        if (mainHand != null && IsCarriedAnimal(mainHand.Itemstack))
            return Release(player, mainHand, config);

        return offhand == null
            ? TextCommandResult.Success("Your offhand is unavailable.")
            : Capture(player, offhand, config);
    }

    private TextCommandResult Capture(IPlayer player, ItemSlot offhand, RFMechanicsConfig config)
    {
        if (!offhand.Empty)
            return TextCommandResult.Success("Your offhand must be empty.");

        Entity? target = player.CurrentEntitySelection?.Entity;
        if (target is not EntityAgent agent || target is EntityPlayer)
            return TextCommandResult.Success("Look at a living animal or drifter to carry it.");
        if (!target.Alive || target.Pos.Dimension != player.Entity.Pos.Dimension)
            return TextCommandResult.Success("That creature cannot be carried.");
        bool hasAnimalTag = animalTagAvailable && animalTag.IsFullyContainedIn(target.Tags);
        HalfGiantCarryAdmission admission = HalfGiantAnimalCarryRules.Admit(hasAnimalTag, target.Code.Path, config.HalfGiantAnimalCarryTagExemptCodePathPrefixes);
        if (admission == HalfGiantCarryAdmission.Refused)
            return TextCommandResult.Success("That creature cannot be carried.");
        double reach = HalfGiantAnimalCarryRules.CaptureReach(admission, config.HalfGiantAnimalCarryReach, config.HalfGiantAnimalCarryTagExemptReach);
        if (!CanSeeAndReach(player.Entity, target, reach))
            return TextCommandResult.Success("That creature is out of reach.");
        if (!sapi!.World.Claims.TryAccess(player, target.Pos.AsBlockPos, EnumBlockAccessFlags.BuildOrBreak))
            return TextCommandResult.Success("You do not have permission to carry that creature here.");
        if (!HasOwnerAccess(player, agent))
            return TextCommandResult.Success("That creature belongs to someone else.");
        if (!IsOrdinary(agent))
            return TextCommandResult.Success("Mounted, tethered, or laden creatures cannot be carried.");

        string creatureCode = target.Code.ToShortString();
        if (!TryGetSizeLimit(config, target.Properties, creatureCode, out double maximumVolume, out double maximumDimension))
            return TextCommandResult.Success("The carry size reference is unavailable.");

        Vec2f collisionSize = target.Properties.CollisionBoxSize;
        double volume = collisionSize.X * collisionSize.X * collisionSize.Y;
        double longestDimension = Math.Max(collisionSize.X, collisionSize.Y);
        if (HalfGiantAnimalCarryRules.MatchesPrefix(config.HalfGiantAnimalCarryDenyCodePathPrefixes, target.Code.Path)
            || !HalfGiantAnimalCarryRules.IsEligible(
                true, true, true, true, true, true, true, true, volume, longestDimension,
                maximumVolume, maximumDimension, creatureCode,
                config.HalfGiantAnimalCarryAllowCodes, config.HalfGiantAnimalCarryDenyCodes))
        {
            return TextCommandResult.Success("That creature is too large or not allowed for carrying.");
        }

        if (!TrySerialize(agent, out string className, out byte[] bytes) || !TryDeserialize(className, creatureCode, bytes, out _))
            return TextCommandResult.Success("That creature's state could not be safely preserved.");
        if (!TryResolveDisplayItem(config, creatureCode, out string displayItem))
            return TextCommandResult.Success("No compatible carried-animal appearance is available.");

        Item? carryItem = sapi.World.GetItem(new AssetLocation("rfmechanics", CarriedAnimalPath));
        if (carryItem == null)
            return TextCommandResult.Success("The carried-animal item is unavailable.");

        var carried = new ItemStack(carryItem);
        string captureIdentity = Guid.NewGuid().ToString("N");
        carried.Attributes.SetString(ClassNameKey, className);
        carried.Attributes.SetString(CreatureCodeKey, creatureCode);
        carried.Attributes.SetBytes(SerializedKey, bytes);
        carried.Attributes.SetString(CaptureIdentityKey, captureIdentity);
        carried.Attributes.SetString(DisplayItemKey, displayItem);
        carried.Attributes.SetLong(SourceEntityIdKey, agent.EntityId);
        carried.Attributes.SetString("capturedBy", player.PlayerUID);

        offhand.Itemstack = carried;
        try
        {
            agent.Die(EnumDespawnReason.PickedUp);
        }
        catch (Exception error)
        {
            offhand.Itemstack = null;
            offhand.MarkDirty();
            sapi.Logger.Error("[rfmechanics] Animal carry capture rollback for {0}: {1}", creatureCode, error);
            return TextCommandResult.Success("The creature could not be carried; it was left in place.");
        }

        offhand.MarkDirty();
        return TextCommandResult.Success("Creature carried in offhand.");
    }

    private TextCommandResult Release(IPlayer player, ItemSlot carrySlot, RFMechanicsConfig config)
    {
        ItemStack stack = carrySlot.Itemstack!;
        if (!TryReadSnapshot(stack, out string className, out string creatureCode, out byte[] bytes, out string captureIdentity))
            return TextCommandResult.Success("This carried animal is damaged and was not consumed.");
        if (releasedCaptureIdentities.Contains(captureIdentity))
            return TextCommandResult.Success("This carried animal has already been released.");

        BlockSelection? selection = player.CurrentBlockSelection;
        if (selection == null || selection.Position.dimension != player.Entity.Pos.Dimension || !CanReachBlock(player.Entity, selection, config.HalfGiantAnimalCarryReach))
            return TextCommandResult.Success("Look at a nearby clear place to release the animal.");
        if (!sapi!.World.Claims.TryAccess(player, selection.Position, EnumBlockAccessFlags.BuildOrBreak))
            return TextCommandResult.Success("You do not have permission to release an animal there.");
        if (!TryDeserialize(className, creatureCode, bytes, out Entity? entity) || entity is not EntityAgent)
            return TextCommandResult.Success("This carried animal cannot be restored and was not consumed.");
        // entity.Properties stays null until SpawnEntity initializes it, as on a chunk load.
        EntityProperties? entityType = sapi!.World.GetEntityType(entity.Code);
        if (entityType == null)
            return TextCommandResult.Success("This carried animal cannot be restored and was not consumed.");

        Vec3d position = ReleasePosition(selection);
        entity.Pos.SetPosWithDimension(position);
        entity.Pos.Yaw = player.Entity.Pos.Yaw + GameMath.PI;
        entity.Pos.Motion.Set(0, 0, 0);
        entity.PositionBeforeFalling.Set(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
        entity.Attributes.SetString("origin", "playerplaced");
        entity.WatchedAttributes.SetBool("noSpawnAnim", true);

        Cuboidf collisionBox = entityType.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
        bool collisionFree = !sapi.World.CollisionTester.IsColliding(sapi.World.BlockAccessor, collisionBox, position, false);
        if (!HalfGiantAnimalCarryRules.CanRelease(true, collisionFree, true, true, false))
            return TextCommandResult.Success("There is not enough clear space; the carried animal remains safe in your hand.");

        try
        {
            sapi.World.SpawnEntity(entity);
        }
        catch (Exception error)
        {
            sapi.Logger.Error("[rfmechanics] Animal carry release preserved {0}: {1}", creatureCode, error);
            return TextCommandResult.Success("The animal could not be released and remains in your hand.");
        }

        releasedCaptureIdentities.Add(captureIdentity);
        if (HalfGiantAnimalCarryRules.ShouldConsumeCapture(true))
        {
            carrySlot.TakeOut(1);
            carrySlot.MarkDirty();
        }
        return TextCommandResult.Success("Animal released.");
    }

    private bool TryGetSizeLimit(RFMechanicsConfig config, EntityProperties properties, string creatureCode, out double maximumVolume, out double maximumDimension)
    {
        maximumVolume = config.HalfGiantAnimalCarryMaximumVolume;
        maximumDimension = config.HalfGiantAnimalCarryMaximumDimension;
        if (config.HalfGiantAnimalCarrySizeOverrides.TryGetValue(creatureCode, out HalfGiantAnimalCarrySizeOverride? overrideSize))
        {
            if (overrideSize.MaximumVolume > 0) maximumVolume = overrideSize.MaximumVolume;
            if (overrideSize.MaximumDimension > 0) maximumDimension = overrideSize.MaximumDimension;
        }

        // The reference limits volume only; a bear's height would otherwise refuse tall, lighter sheep.
        if (maximumDimension <= 0) maximumDimension = double.PositiveInfinity;
        if (maximumVolume > 0) return true;

        EntityProperties? reference = sapi!.World.GetEntityType(AssetLocation.Create(config.HalfGiantAnimalCarryReferenceEntityCode));
        if (reference == null) return false;
        Vec2f referenceSize = reference.CollisionBoxSize;
        maximumVolume = referenceSize.X * referenceSize.X * referenceSize.Y;
        return maximumVolume > 0;
    }

    private bool TryResolveDisplayItem(RFMechanicsConfig config, string creatureCode, out string displayItem)
    {
        displayItem = "";
        if (config.HalfGiantAnimalCarryDisplayItems.TryGetValue(creatureCode, out string? configured))
        {
            if (sapi!.World.GetItem(AssetLocation.Create(configured)) == null) return false;
            displayItem = configured;
            return true;
        }

        AssetLocation entityCode = AssetLocation.Create(creatureCode);
        AssetLocation candidate = new(entityCode.Domain, "creature-" + entityCode.Path);
        if (sapi!.World.GetItem(candidate) == null) return false;
        displayItem = candidate.ToShortString();
        return true;
    }

    private bool TrySerialize(Entity entity, out string className, out byte[] bytes)
    {
        className = sapi!.World.Api.ClassRegistry.GetEntityClassName(entity.GetType());
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(className)) return false;
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            entity.ToBytes(writer, false);
            bytes = stream.ToArray();
            return bytes.Length > 0;
        }
        catch (Exception error)
        {
            sapi.Logger.Error("[rfmechanics] Animal carry serialization failed for {0}: {1}", entity.Code, error);
            return false;
        }
    }

    private bool TryDeserialize(string className, string creatureCode, byte[] bytes, out Entity? entity)
    {
        entity = null;
        try
        {
            string remappedCode = creatureCode;
            IServerWorldAccessor world = sapi!.World;
            if (world.RemappedEntities.TryGetValue(remappedCode, out string? remapped)) remappedCode = remapped;
            EntityProperties? properties = sapi.World.GetEntityType(AssetLocation.Create(remappedCode));
            if (properties == null) return false;

            entity = sapi.World.Api.ClassRegistry.CreateEntity(className);
            if (entity == null) return false;
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            entity.FromBytes(reader, false, world.RemappedEntities);
            return true;
        }
        catch (Exception error)
        {
            sapi!.Logger.Error("[rfmechanics] Animal carry restoration validation failed for {0}: {1}", creatureCode, error);
            entity = null;
            return false;
        }
    }

    private static bool TryReadSnapshot(ItemStack stack, out string className, out string creatureCode, out byte[] bytes, out string captureIdentity)
    {
        className = stack.Attributes.GetString(ClassNameKey);
        creatureCode = stack.Attributes.GetString(CreatureCodeKey);
        bytes = stack.Attributes.GetBytes(SerializedKey);
        captureIdentity = stack.Attributes.GetString(CaptureIdentityKey);
        return HalfGiantAnimalCarryRules.IsSnapshotValid(className, creatureCode, bytes, captureIdentity);
    }

    private static bool IsCarriedAnimal(ItemStack? stack)
    {
        return stack?.Collectible?.Code.Domain == "rfmechanics" && stack.Collectible.Code.Path == CarriedAnimalPath;
    }

    private static bool HasOwnerAccess(IPlayer player, EntityAgent target)
    {
        EntityBehaviorOwnable? ownable = target.GetBehavior<EntityBehaviorOwnable>();
        if (ownable != null && !ownable.IsOwner(player.Entity)) return false;
        ITreeAttribute? ownedBy = target.WatchedAttributes.GetTreeAttribute("ownedby");
        string? ownerUid = ownedBy?.GetString("uid");
        return string.IsNullOrEmpty(ownerUid) || ownerUid == player.PlayerUID;
    }

    private static bool IsOrdinary(EntityAgent target)
    {
        if (target.MountedOn != null || target.WatchedAttributes["clothIds"] != null) return false;
        if (target.GetBehavior<EntityBehaviorSeatable>()?.AnyMounted() == true) return false;

        bool hasInventory = false;
        target.WalkInventory(slot =>
        {
            hasInventory |= !slot.Empty;
            return !hasInventory;
        });
        return !hasInventory;
    }

    private bool CanSeeAndReach(EntityPlayer player, Entity target, double maximumReach)
    {
        if (!double.IsFinite(maximumReach) || maximumReach <= 0 || player.Pos.Dimension != target.Pos.Dimension) return false;
        var from = new Vec3d(player.Pos.X, player.Pos.InternalY + player.LocalEyePos.Y, player.Pos.Z);
        var to = new Vec3d(target.Pos.X, target.Pos.InternalY + target.Properties.CollisionBoxSize.Y * 0.5, target.Pos.Z);
        if (from.SquareDistanceTo(to) > maximumReach * maximumReach) return false;

        BlockSelection? blockSelection = null;
        EntitySelection? entitySelection = null;
        sapi!.World.RayTraceForSelection(from, to, ref blockSelection, ref entitySelection);
        return entitySelection?.Entity?.EntityId == target.EntityId;
    }

    private bool CanReachBlock(EntityPlayer player, BlockSelection selection, double maximumReach)
    {
        if (!double.IsFinite(maximumReach) || maximumReach <= 0) return false;
        var from = new Vec3d(player.Pos.X, player.Pos.InternalY + player.LocalEyePos.Y, player.Pos.Z);
        var to = selection.FullPosition;
        if (from.SquareDistanceTo(to) > maximumReach * maximumReach) return false;

        BlockSelection? tracedBlock = null;
        EntitySelection? tracedEntity = null;
        sapi!.World.RayTraceForSelection(from, to, ref tracedBlock, ref tracedEntity);
        return tracedBlock?.Position.Equals(selection.Position) == true;
    }

    private static Vec3d ReleasePosition(BlockSelection selection)
    {
        return new Vec3d(
            selection.Position.X + (selection.DidOffset ? 0 : selection.Face.Normali.X) + 0.5,
            selection.Position.InternalY + (selection.DidOffset ? 0 : selection.Face.Normali.Y),
            selection.Position.Z + (selection.DidOffset ? 0 : selection.Face.Normali.Z) + 0.5)
        {
            Y = selection.Position.InternalY + (selection.DidOffset ? 0 : selection.Face.Normali.Y)
        };
    }
}
