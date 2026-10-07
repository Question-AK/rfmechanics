using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace rfmechanics;

// Passive server-only protection. No activation packets, movement stats, hunger
// debit, announcements or persisted ability state. The cache is diagnostic/identity only.
public sealed class OrcSkinModSystem : ModSystem
{
    private sealed class Identity
    {
        internal EntityPlayer Self = null!;
        internal Action Changed = null!;
        internal bool Orc;
        internal string Last = "none";
    }
    private readonly Dictionary<string, Identity> identities = new();
    private ICoreServerAPI? sapi;
    private RFMechanicsConfig cfg = new();
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        try { cfg = api.LoadModConfig<RFMechanicsConfig>("rfmechanics.json") ?? new(); }
        catch (Exception e) { api.Logger.Warning("[rfmechanics] Orc skin defaults: {0}", e.Message); }
        api.Event.PlayerDisconnect += Remove; api.Event.PlayerJoin += Remove; api.Event.PlayerDeath += Death;
        api.ChatCommands.Create("rfskin").WithDescription("Read Orc natural protection and the last physical hit.")
            .RequiresPlayer().RequiresPrivilege(Privilege.chat)
            .HandleWith(args => TextCommandResult.Success(Describe((EntityPlayer)args.Caller.Entity)));
    }

    private Identity? Get(EntityPlayer self)
    {
        if (sapi == null || self.Player is not IServerPlayer player) return null;
        if (identities.TryGetValue(player.PlayerUID, out var identity) && identity.Self == self) return identity;
        Remove(player);
        identity = new Identity { Self = self };
        identity.Changed = () => { identity.Orc = RaceTraits.HasTrait(player, cfg.OrcTraitCode); identity.Last = "none"; };
        self.WatchedAttributes.RegisterModifiedListener("characterClass", identity.Changed);
        self.WatchedAttributes.RegisterModifiedListener("extraTraits", identity.Changed);
        identity.Changed();
        identities[player.PlayerUID] = identity;
        return identity;
    }

    private void Remove(IServerPlayer player)
    {
        if (identities.Remove(player.PlayerUID, out var identity))
            identity.Self.WatchedAttributes.UnregisterListener(identity.Changed);
    }
    private void Death(IServerPlayer player, DamageSource source) => Remove(player);

    internal void Protect(EntityPlayer self, DamageSource source, ref float damage)
    {
        if (sapi == null || !cfg.EnableOrcNaturalProtection || !PhysicalAttack(source)) return;
        var identity = Get(self);
        if (identity?.Orc != true) return;
        float before = damage;
        damage = (float)OrcSkinRules.Protect(damage, source.DamageTier);
        identity.Last = $"weapon T{source.DamageTier}, skin T2, HP {before:F3} to {damage:F3}";
    }

    internal static bool PhysicalAttack(DamageSource source) =>
        source.Duration <= TimeSpan.Zero
        && source.Type is EnumDamageType.BluntAttack or EnumDamageType.PiercingAttack or EnumDamageType.SlashingAttack
        && source.Source is EnumDamageSource.Entity or EnumDamageSource.Player or EnumDamageSource.Unknown;

    internal string Describe(EntityPlayer self)
    {
        var identity = Get(self);
        bool enabled = identity?.Orc == true && self.Alive && cfg.EnableOrcNaturalProtection;
        return $"Orc skin={(enabled ? "T2" : "off")}\nLast: {identity?.Last ?? "none"}";
    }

    public override void Dispose()
    {
        if (sapi != null)
        {
            sapi.Event.PlayerDisconnect -= Remove; sapi.Event.PlayerJoin -= Remove; sapi.Event.PlayerDeath -= Death;
            foreach (var identity in identities.Values) identity.Self.WatchedAttributes.UnregisterListener(identity.Changed);
            identities.Clear();
        }
        base.Dispose();
    }
}
