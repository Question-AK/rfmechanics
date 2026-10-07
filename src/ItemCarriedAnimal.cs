using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace rfmechanics;

public sealed class ItemCarriedAnimal : Item
{
    internal const string CreatureSizeKey = "creatureSize";
    internal const string HolderSizeKey = "holderSize";
    private const string DisplayItemKey = "displayitem";
    private const string CreatureHeldPosesAttribute = "tpHandTransformByCreature";
    // Set once the hold crosses HalfGiantThrowFullChargeSeconds, swapping to the deeper pull-back pose.
    private const string FullWindupKey = "fullwindup";
    // PlayerModelLib's halfgiant ModelSizeFactor; stacks captured before sizes were stored lack HolderSizeKey.
    private const float DefaultHolderSize = 2.1f;
    // Vanilla CollectibleBehaviorThrowable windup, so a creature throw feels like a stone throw.
    private const float ThrowWindupSeconds = 0.35f;

    private (AssetLocation Pattern, ModelTransform Pose)[] creatureHeldPoses = Array.Empty<(AssetLocation, ModelTransform)>();
    // Keyed by creature, scale and hand, so every stack of a species shares one transform instead of allocating per frame.
    private readonly Dictionary<(string Creature, float Scale, bool Offhand), ModelTransform> heldTransforms = new();
    // Generic (not per-creature): only used briefly at full charge, unlike the per-creature idle/quick-aim pose.
    private ModelTransform? tpHandFullWindupTransform;
    private readonly Dictionary<float, ModelTransform> fullWindupTransforms = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        Dictionary<string, ModelTransform>? poses = Attributes?[CreatureHeldPosesAttribute].AsObject<Dictionary<string, ModelTransform>>();
        if (poses != null)
            creatureHeldPoses = poses.Select(entry => (AssetLocation.Create(entry.Key), entry.Value.EnsureDefaultValues())).ToArray();
        tpHandFullWindupTransform = Attributes?["tpHandFullWindupTransform"].AsObject<ModelTransform>()?.EnsureDefaultValues();
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        Item? displayItem = GetDisplayItem(capi, itemstack);
        if (displayItem == null)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
            return;
        }

        var displayStack = new ItemStack(displayItem);
        renderinfo.ModelRef = capi.TesselatorManager.GetDefaultItemMeshRef(displayItem);
        displayItem.OnBeforeRender(capi, displayStack, target, ref renderinfo);
        renderinfo.Transform = target switch
        {
            EnumItemRenderTarget.Gui => GuiTransform,
            EnumItemRenderTarget.HandTp when tpHandFullWindupTransform != null && itemstack.Attributes.GetBool(FullWindupKey)
                => FullWindupTransform(itemstack),
            EnumItemRenderTarget.HandTp => HeldTransform(itemstack, TpHandTransform, false),
            EnumItemRenderTarget.HandTpOff => HeldTransform(itemstack, TpOffHandTransform, true),
            EnumItemRenderTarget.Ground => GroundTransform,
            _ => FpHandTransform
        };
    }

    // PreventDefault also cancels block breaking: a hand holding an animal neither strikes nor mines.
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
        byEntity.StartAnimation("aim");
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1) return false;
        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        if (config != null)
            SetFullWindup(slot, byEntity, HalfGiantAnimalCarryRules.IsFullyCharged(secondsUsed, (float)config.HalfGiantThrowFullChargeSeconds));
        return true;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation("aim");
        SetFullWindup(slot, byEntity, false);
        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            byEntity.Attributes.SetInt("aimingCancel", 1);
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation("aim");
        SetFullWindup(slot, byEntity, false);
        if (slot != byEntity.RightHandItemSlot || !HalfGiantAnimalCarryRules.IsThrowReady(secondsUsed, ThrowWindupSeconds)) return;

        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        bool fullyCharged = config != null && HalfGiantAnimalCarryRules.IsFullyCharged(secondsUsed, (float)config.HalfGiantThrowFullChargeSeconds);

        if (api.Side == EnumAppSide.Client)
        {
            byEntity.StartAnimation("throw");
            return;
        }
        if (byEntity is EntityPlayer { Player: IServerPlayer player })
            api.ModLoader.GetModSystem<HalfGiantAnimalCarryModSystem>()?.Throw(player, slot, fullyCharged);
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
        dsc.AppendLine(Lang.Get("rfmechanics:carriedanimal-policy"));
    }

    // Held items render at the holder's Client.Size times this scale; dividing it out leaves the creature's own size.
    private ModelTransform HeldTransform(ItemStack itemstack, ModelTransform handPose, bool offhand)
    {
        string creatureCode = itemstack.Attributes.GetString(HalfGiantAnimalCarryModSystem.CreatureCodeKey) ?? "";
        float scale = HalfGiantAnimalCarryRules.HeldScale(
            itemstack.Attributes.GetFloat(CreatureSizeKey), itemstack.Attributes.GetFloat(HolderSizeKey), DefaultHolderSize);
        if (heldTransforms.TryGetValue((creatureCode, scale, offhand), out ModelTransform? transform)) return transform;

        transform = (CreatureHeldPose(creatureCode) ?? handPose).Clone();
        transform.Scale = scale;
        heldTransforms[(creatureCode, scale, offhand)] = transform;
        return transform;
    }

    private ModelTransform? CreatureHeldPose(string creatureCode)
    {
        if (creatureCode.Length == 0) return null;
        AssetLocation code = AssetLocation.Create(creatureCode);
        foreach ((AssetLocation pattern, ModelTransform pose) in creatureHeldPoses)
            if (WildcardUtil.Match(pattern, code)) return pose;
        return null;
    }

    // Generic across creatures: a full charge is a brief hold, not worth per-creature authoring like the idle pose.
    private ModelTransform FullWindupTransform(ItemStack itemstack)
    {
        float scale = HalfGiantAnimalCarryRules.HeldScale(
            itemstack.Attributes.GetFloat(CreatureSizeKey), itemstack.Attributes.GetFloat(HolderSizeKey), DefaultHolderSize);
        if (fullWindupTransforms.TryGetValue(scale, out ModelTransform? transform)) return transform;

        transform = tpHandFullWindupTransform!.Clone();
        transform.Scale = scale;
        fullWindupTransforms[scale] = transform;
        return transform;
    }

    private static void SetFullWindup(ItemSlot slot, EntityAgent byEntity, bool fullWindup)
    {
        ItemStack? stack = slot.Itemstack;
        if (stack == null || stack.Attributes.GetBool(FullWindupKey) == fullWindup) return;

        if (fullWindup) stack.Attributes.SetBool(FullWindupKey, true);
        else stack.Attributes.RemoveAttribute(FullWindupKey);
        (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
    }

    private static Item? GetDisplayItem(ICoreClientAPI capi, ItemStack itemstack)
    {
        string code = itemstack.Attributes.GetString(DisplayItemKey);
        return string.IsNullOrWhiteSpace(code) ? null : capi.World.GetItem(AssetLocation.Create(code));
    }
}
