using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

public sealed class ItemCarriedRock : Item
{
    internal const string RockCodeKey = "rockcode";
    internal const string HolderSizeKey = "holderSize";
    // On the stack, as vanilla ItemBow syncs renderVariant, so other players also see the wind-up hold.
    private const string WindupKey = "windup";
    // Vanilla CollectibleBehaviorThrowable windup and stone-throw launch geometry, as for carried creatures.
    private const float ThrowWindupSeconds = 0.35f;
    private const double ThrowDispersion = 0.75;
    private const double ThrowVerticalOffset = 0.1;
    private const double ThrowHorizontalOffset = 0.4;
    private const double ThrowForwardOffset = -0.21;
    private const double ThrowParallaxDistance = 20;
    private static readonly AssetLocation ThrownRockCode = new("rfmechanics", "thrownrock");
    private static readonly AssetLocation ThrowSound = new("game", "sounds/player/throw");

    private enum HeldPose { Main, Offhand, Windup }

    // Keyed by resolved scale, so every stack shares one transform instead of allocating per frame.
    private readonly Dictionary<(float Scale, HeldPose Pose), ModelTransform> heldTransforms = new();
    private ModelTransform? tpHandWindupTransform;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        tpHandWindupTransform = Attributes?["tpHandWindupTransform"].AsObject<ModelTransform>()?.EnsureDefaultValues();
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        Block? rock = GetRock(capi.World, itemstack);
        if (rock == null)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
            return;
        }

        // The tesselator owns and disposes default block meshes, so no local cache is needed.
        renderinfo.ModelRef = capi.TesselatorManager.GetDefaultBlockMeshRef(rock);
        renderinfo.Transform = target switch
        {
            EnumItemRenderTarget.Gui => GuiTransform,
            EnumItemRenderTarget.HandTp when tpHandWindupTransform != null && itemstack.Attributes.GetBool(WindupKey)
                => HeldTransform(itemstack, tpHandWindupTransform, HeldPose.Windup),
            EnumItemRenderTarget.HandTp => HeldTransform(itemstack, TpHandTransform, HeldPose.Main),
            EnumItemRenderTarget.HandTpOff => HeldTransform(itemstack, TpOffHandTransform, HeldPose.Offhand),
            EnumItemRenderTarget.Ground => GroundTransform,
            _ => FpHandTransform
        };
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        Block? rock = GetRock(api.World, itemStack);
        return rock == null
            ? base.GetHeldItemName(itemStack)
            : Lang.Get("rfmechanics:carriedrock-name", rock.GetHeldItemName(new ItemStack(rock)));
    }

    // PreventDefault also cancels block breaking: a hand holding a rock neither strikes nor mines.
    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (slot != byEntity.RightHandItemSlot || byEntity.Controls.ShiftKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        // The aiming attributes drive the player's aiming-accuracy behavior that GetProjectileDirection reads.
        byEntity.Attributes.SetInt("aiming", 1);
        byEntity.Attributes.SetInt("aimingCancel", 0);
        byEntity.StartAnimation(HalfGiantAnimalCarryRules.ThrowWindupAnimationCode);
        SetWindup(slot, byEntity, true);
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        return byEntity.Attributes.GetInt("aimingCancel") != 1;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(HalfGiantAnimalCarryRules.ThrowWindupAnimationCode);
        SetWindup(slot, byEntity, false);
        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            byEntity.Attributes.SetInt("aimingCancel", 1);
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(HalfGiantAnimalCarryRules.ThrowWindupAnimationCode);
        SetWindup(slot, byEntity, false);
        if (slot != byEntity.RightHandItemSlot || !HalfGiantAnimalCarryRules.IsThrowReady(secondsUsed, ThrowWindupSeconds)) return;

        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        bool fullyCharged = config != null && HalfGiantAnimalCarryRules.IsFullyCharged(secondsUsed, (float)config.HalfGiantThrowFullChargeSeconds);

        if (api.Side == EnumAppSide.Client)
        {
            byEntity.StartAnimation("throw");
            return;
        }
        if (byEntity is EntityPlayer { Player: IServerPlayer player })
            Throw(player, slot, fullyCharged);
    }

    // Idle means no hand use on either side, so this only clears a flag a disconnect left mid-wind-up.
    public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
    {
        base.OnHeldIdle(slot, byEntity);
        SetWindup(slot, byEntity, false);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new[]
        {
            new WorldInteraction { ActionLangCode = "heldhelp-throw", MouseButton = EnumMouseButton.Right }
        };
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        dsc.AppendLine(Lang.Get("rfmechanics:carriedrock-policy"));
    }

    // Not gated by EnableHalfGiantRockPull, so disabling pulls never strands a rock already held.
    private void Throw(IServerPlayer player, ItemSlot slot, bool fullyCharged)
    {
        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        EntityPlayer? thrower = player.Entity;
        if (config == null || thrower == null || slot != player.InventoryManager.ActiveHotbarSlot || slot.Itemstack?.Collectible != this)
            return;
        if (!RaceTraits.HasTrait(player, config.HalfGiantTraitCode))
        {
            Tell(player, "rockthrow-halfgiantonly");
            return;
        }

        Block? rock = GetRock(api.World, slot.Itemstack);
        EntityProperties? thrownType = api.World.GetEntityType(ThrownRockCode);
        if (rock == null || thrownType == null)
        {
            Tell(player, "rockthrow-unavailable");
            return;
        }

        (FastVec3d launch, FastVec3d aim) = EntityProjectileBase.GetProjectileDirection(
            thrower, ThrowDispersion, ThrowVerticalOffset, ThrowHorizontalOffset, ThrowForwardOffset, ThrowParallaxDistance);
        var position = new Vec3d(launch.X, launch.Y - thrownType.CollisionBoxSize.Y * 0.5, launch.Z);
        if (api.World.CollisionTester.IsColliding(api.World.BlockAccessor, thrownType.SpawnCollisionBox, position, false))
        {
            Tell(player, "rockthrow-noroom");
            return;
        }
        if (api.World.ClassRegistry.CreateEntity(thrownType) is not EntityThrownRock thrown)
        {
            Tell(player, "rockthrow-unavailable");
            return;
        }

        double speed = HalfGiantAnimalCarryRules.ChargedThrowSpeed(config.HalfGiantRockThrowSpeed, fullyCharged, config.HalfGiantThrowFullChargeSpeedMultiplier);
        thrown.ProjectileStack = new ItemStack(rock);
        thrown.FiredBy = thrower;
        thrown.Damage = (float)config.HalfGiantRockThrowDamage;
        thrown.DamageType = EnumDamageType.BluntAttack;
        thrown.Weight = (float)config.HalfGiantRockThrowWeight;
        thrown.Collectible = false;
        thrown.Pos.SetPosWithDimension(position);
        thrown.Pos.Yaw = thrower.Pos.Yaw;
        thrown.Pos.Motion.Set(aim.X * speed, aim.Y * speed, aim.Z * speed);
        thrown.World = api.World;
        thrown.PreInitialize();

        try
        {
            api.World.SpawnPriorityEntity(thrown);
        }
        catch (Exception error)
        {
            api.Logger.Error("[rfmechanics] Rock throw preserved {0}: {1}", rock.Code, error);
            Tell(player, "rockthrow-failed");
            return;
        }

        slot.TakeOut(1);
        slot.MarkDirty();
        api.World.PlaySoundAt(ThrowSound, thrower, null, false, 8f);
    }

    private ModelTransform HeldTransform(ItemStack itemstack, ModelTransform source, HeldPose pose)
    {
        float scale = HalfGiantRockRules.HeldScale(itemstack.Attributes.GetFloat(HolderSizeKey), HalfGiantRockRules.DefaultHolderSize);
        if (heldTransforms.TryGetValue((scale, pose), out ModelTransform? transform)) return transform;

        transform = source.Clone();
        transform.Scale = scale;
        heldTransforms[(scale, pose)] = transform;
        return transform;
    }

    private static void SetWindup(ItemSlot slot, EntityAgent byEntity, bool windup)
    {
        ItemStack? stack = slot.Itemstack;
        if (stack == null || stack.Attributes.GetBool(WindupKey) == windup) return;

        if (windup) stack.Attributes.SetBool(WindupKey, true);
        else stack.Attributes.RemoveAttribute(WindupKey);
        // A no-op on the client; the server sends the hand slot to everyone but its holder.
        (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
    }

    internal static Block? GetRock(IWorldAccessor world, ItemStack itemstack)
    {
        string code = itemstack.Attributes.GetString(RockCodeKey);
        return string.IsNullOrWhiteSpace(code) ? null : world.GetBlock(AssetLocation.Create(code));
    }

    private static void Tell(IServerPlayer player, string langCode)
    {
        if (player.ConnectionState == EnumClientState.Playing)
            player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("rfmechanics:" + langCode), EnumChatType.Notification);
    }
}
