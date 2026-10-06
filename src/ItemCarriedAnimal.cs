using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace rfmechanics;

public sealed class ItemCarriedAnimal : Item
{
    internal const string CreatureSizeKey = "creatureSize";
    internal const string HolderSizeKey = "holderSize";
    private const string DisplayItemKey = "displayitem";
    // PlayerModelLib's halfgiant ModelSizeFactor; stacks captured before sizes were stored lack HolderSizeKey.
    private const float DefaultHolderSize = 2.1f;
    // Vanilla CollectibleBehaviorThrowable windup, so a creature throw feels like a stone throw.
    private const float ThrowWindupSeconds = 0.35f;

    // Keyed by resolved scale, so every stack of a species shares one transform instead of allocating per frame.
    private readonly Dictionary<(float Scale, bool Offhand), ModelTransform> heldTransforms = new();

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
        return byEntity.Attributes.GetInt("aimingCancel") != 1;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation("aim");
        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            byEntity.Attributes.SetInt("aimingCancel", 1);
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation("aim");
        if (slot != byEntity.RightHandItemSlot || !HalfGiantAnimalCarryRules.IsThrowReady(secondsUsed, ThrowWindupSeconds)) return;

        if (api.Side == EnumAppSide.Client)
        {
            byEntity.StartAnimation("throw");
            return;
        }
        if (byEntity is EntityPlayer { Player: IServerPlayer player })
            api.ModLoader.GetModSystem<HalfGiantAnimalCarryModSystem>()?.Throw(player, slot);
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
    private ModelTransform HeldTransform(ItemStack itemstack, ModelTransform pose, bool offhand)
    {
        float scale = HalfGiantAnimalCarryRules.HeldScale(
            itemstack.Attributes.GetFloat(CreatureSizeKey), itemstack.Attributes.GetFloat(HolderSizeKey), DefaultHolderSize);
        if (heldTransforms.TryGetValue((scale, offhand), out ModelTransform? transform)) return transform;

        transform = pose.Clone();
        transform.Scale = scale;
        heldTransforms[(scale, offhand)] = transform;
        return transform;
    }

    private static Item? GetDisplayItem(ICoreClientAPI capi, ItemStack itemstack)
    {
        string code = itemstack.Attributes.GetString(DisplayItemKey);
        return string.IsNullOrWhiteSpace(code) ? null : capi.World.GetItem(AssetLocation.Create(code));
    }
}
