using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace rfmechanics;

public sealed class ItemCarriedAnimal : Item
{
    private const string DisplayItemKey = "displayitem";

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
            EnumItemRenderTarget.HandTp => TpHandTransform,
            EnumItemRenderTarget.HandTpOff => TpOffHandTransform,
            EnumItemRenderTarget.Ground => GroundTransform,
            _ => FpHandTransform
        };
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        dsc.AppendLine(Lang.Get("rfmechanics:carriedanimal-policy"));
    }

    private static Item? GetDisplayItem(ICoreClientAPI capi, ItemStack itemstack)
    {
        string code = itemstack.Attributes.GetString(DisplayItemKey);
        return string.IsNullOrWhiteSpace(code) ? null : capi.World.GetItem(AssetLocation.Create(code));
    }
}
