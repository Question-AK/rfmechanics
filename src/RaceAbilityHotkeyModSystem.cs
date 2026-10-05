using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rfmechanics
{
    /// <summary>
    /// Client hotkey registrations for race abilities. "rfraceability" (default R) replaced the four old per-race V-bound keys
    /// (orcsmellfocus, rfelfzoom, rfdwarforesong, rfgoblinspit) -- those four all defaulted to the
    /// same key on the assumption that races are mutually exclusive per player, which never
    /// actually isolated them from third-party mods also bound to V (see
    /// notes/slowwalkmod-orcsmell-hotkey-crash.md). Those four code strings must never be
    /// reused: a removed hotkey code's clientsettings.json rebind entry is orphaned forever, not
    /// cleaned up, so re-registering one would silently resurrect a player's old rebind under new
    /// semantics.
    ///
    /// The default must be a key vanilla leaves unbound: hotkeys dispatch in registration order,
    /// vanilla registers before any mod, and the first handler returning true ends the press -- both
    /// C (characterdialog) and X (fliphandslots) swallow it before Dispatch ever runs. See
    /// notes/race-mechanics/race-ability-hotkey-default-2026-09-16.md.
    ///
    /// "rfclamber" (Ctrl+H) is the shared stance key, retaining existing rebinds. It dispatches
    /// Dwarf Stonebrace, persistent Goblin Clamber, or session-only Elf Watchfulness independently
    /// of held abilities.
    ///
    /// The press handler covers dwarf/goblin. Elf zoom polls the saved Race Ability
    /// binding independently. Orc smell is stance-owned; Orc skin is passive.
    /// </summary>
    public class RaceAbilityHotkeyModSystem : ModSystem
    {
        // One ability per race is assumed, for both the press table below and the held abilities
        // handled elsewhere -- a race needing a second ability needs a redesign here, not a second
        // dictionary entry or a second hotkey.
        private static readonly Dictionary<PlayerRace, System.Func<ICoreClientAPI, bool>> PressAbilities = new()
        {
            [PlayerRace.Dwarf] = api => api.ModLoader.GetModSystem<DwarfOreSongModSystem>().TryTrigger(api),
            [PlayerRace.Goblin] = api => api.ModLoader.GetModSystem<RFMechanicsModSystem>().TryTriggerGoblinSpit(api),
        };

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.Input.RegisterHotKey("rfraceability", "Race Ability", GlKeys.R, HotkeyType.CharacterControls);
            api.Input.SetHotKeyHandler("rfraceability", _ => Dispatch(api));

            // New code, not the retired "rfelfstepheighttoggle" that held Ctrl+H before M1 --
            // see the orphaned-rebind warning above.
            api.Input.RegisterHotKey("rfclamber", "Race Stance (Clamber / Watchfulness / Hunt)", GlKeys.H, HotkeyType.CharacterControls, ctrlPressed: true);
            api.Input.SetHotKeyHandler("rfclamber", _ => DispatchClamber(api));
        }

        /// <summary>Race lookup uses the cached path (PlayerRaceBehavior.Race), never a fresh
        /// RaceTraits.HasTrait call -- a race with no ability, including Human, falls through to
        /// the dictionary miss below and returns false so other mods bound to the same key still see the
        /// press.</summary>
        private static bool Dispatch(ICoreClientAPI api)
        {
            IPlayer? player = api.World.Player;
            PlayerRace race = player?.Entity?.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None;

            return PressAbilities.TryGetValue(race, out var ability) && ability(api);
        }

        /// <summary>Other races fall through; the server validates each stance request independently.</summary>
        private static bool DispatchClamber(ICoreClientAPI api)
        {
            IPlayer? player = api.World.Player;
            PlayerRace race = player?.Entity?.GetBehavior<PlayerRaceBehavior>()?.Race ?? PlayerRace.None;
            if (race == PlayerRace.Elf)
                return api.ModLoader.GetModSystem<ElfWatchfulnessModSystem>().TryToggle();
            if (race == PlayerRace.Orc)
                return api.ModLoader.GetModSystem<OrcHuntModSystem>().TryToggle();
            if (race == PlayerRace.Dwarf)
                return api.ModLoader.GetModSystem<DwarfStonebraceModSystem>().TryToggle(api);
            if (race != PlayerRace.Goblin) return false;

            return api.ModLoader.GetModSystem<GoblinClamberStanceModSystem>().TryToggle(api);
        }
    }
}
