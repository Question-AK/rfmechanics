using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace rfmechanics;

// Not EntityThrownItem: its OnGameTick re-hits the nearest entity every tick with no memory and dies on impact.
public sealed class EntityThrownRock : EntityProjectileBase
{
    private bool shattered;

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
    {
        base.OnTesselation(ref entityShape, shapePathForLogging);
        if (Api is not ICoreClientAPI capi || ProjectileStack == null || !ProjectileStack.ResolveBlockOrItem(Api.World)) return;
        Block? rock = ProjectileStack.Block;
        if (rock?.Shape == null) return;

        // Same texture hand-over as EntityThrownItem, so the rock draws with its own block shape and textures.
        entityShape = capi.TesselatorManager.GetCachedShape(rock.Shape.Base);
        IDictionary<string, CompositeTexture> textures = Properties.Client.Textures;
        foreach (KeyValuePair<string, CompositeTexture> entry in rock.Textures)
        {
            CompositeTexture texture = entry.Value.Clone();
            textures[entry.Key] = texture;
            texture.Bake(Api.Assets);
            capi.EntityTextureAtlas.GetOrInsertTexture(texture.Baked.TextureFilenames[0], out int textureSubId, out _);
            texture.Baked.TextureSubId = textureSubId;
        }
    }

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);
        RFMechanicsConfig? config = RFMechanicsModSystem.Config;
        if (World.Side == EnumAppSide.Server && config != null
            && World.ElapsedMilliseconds - msLaunch > config.HalfGiantRockThrowFlightTimeoutSeconds * 1000)
        {
            Shatter();
        }
    }

    public override void OnCollided()
    {
        base.OnCollided();
        Shatter();
    }

    public override void OnCollideWithLiquid()
    {
        base.OnCollideWithLiquid();
        Shatter();
    }

    public override bool CanCollect(Entity byEntity)
    {
        return false;
    }

    // The raw rock block never returns to an inventory; it only shatters into its drops.
    public override ItemStack? OnCollected(Entity byEntity)
    {
        return null;
    }

    // A rock whose thrower left or whose chunk reloaded (FiredBy null) never damages, so PvP rules cannot be bypassed.
    protected override bool CanDealDamage(Entity target)
    {
        return FiredBy is EntityPlayer { Player: IServerPlayer { ConnectionState: EnumClientState.Playing } } && base.CanDealDamage(target);
    }

    protected override void ImpactOnEntity(Entity target)
    {
        if (HalfGiantRockRules.RecordHit(entitiesHit, target.EntityId))
            base.ImpactOnEntity(target);
    }

    // Base impact stops the rock dead; it then falls and shatters on the ground instead of vanishing on the target.
    protected override void DamageProjectile(Entity target)
    {
    }

    private void Shatter()
    {
        if (shattered || !Alive || World.Side != EnumAppSide.Server) return;
        shattered = true;

        if (ProjectileStack != null && ProjectileStack.ResolveBlockOrItem(World) && ProjectileStack.Block is Block rock)
        {
            Vec3d at = Pos.XYZ.AddCopy(0, CollisionBox.Y2 * 0.5, 0);
            foreach (ItemStack drop in LandingDrops(rock))
                World.SpawnItemEntity(drop, at);
            if (rock.Sounds != null)
                World.PlaySoundAt(rock.Sounds.GetBreakSound((IPlayer?)null), this);
            World.SpawnCubeParticles(at, ProjectileStack, 0.5f, 20);
        }
        Die();
    }

    // The block's own drop list, not Block.GetDrops: BreakIfFloating returns the raw block at a floating spot.
    private List<ItemStack> LandingDrops(Block rock)
    {
        var drops = new List<ItemStack>();
        if (rock.Drops == null) return drops;
        foreach (BlockDropItemStack drop in rock.Drops)
        {
            ItemStack? stack = drop.ToRandomItemstackForPlayer(null, World, 1f);
            if (stack == null) continue;
            if (HalfGiantRockRules.IsLandingDrop(stack.Class == EnumItemClass.Block, stack.Collectible.Code.Domain, stack.Collectible.Code.Path))
                drops.Add(stack);
            if (drop.LastDrop) break;
        }
        return drops;
    }
}
