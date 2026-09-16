using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace rfmechanics
{
    /// <summary>
    /// Sets StepHeight per race -- ElfStepHeightOverride for elves, StepHeightValue for everyone
    /// else (humans cache as PlayerRace.None and are therefore included) -- and restores whatever
    /// value the entity had before this behavior touched it when EnableStepHeight is off. A plain
    /// field write, not a Harmony patch -- StepHeight is public on EntityBehaviorControlledPhysics
    /// (BehaviorControlledPhysics.cs:67) and MotionAndCollision (the FindSteppableCollisionBox call
    /// site) runs for players on both sides, same reason TreeClimbingPatch patches the base class
    /// instead of EntityBehaviorPlayerPhysics -- so this behavior is attached dual-side too
    /// (seraph-stepheight.json), same lesson as PlayerRaceBehavior.
    ///
    /// restoreValue is captured once in Initialize(), not hardcoded to vanilla's 0.6f default --
    /// a pinned literal would go stale against a future vanilla change or another mod's own
    /// SetProperties override, and the per-tick correction would then overwrite that value with a
    /// wrong number instead of restoring it. EntityProperties.loadBehaviors constructs and
    /// Initializes behaviors in array order, and this behavior's JSON patch appends to the end of
    /// the array, so the physics behavior (registered earlier, core) has already run its own
    /// Initialize/SetProperties by the time this reads it.
    /// </summary>
    public class StepHeightBehavior : EntityBehavior
    {
        private EntityBehaviorControlledPhysics? physics;
        private PlayerRaceBehavior? identity;
        private float restoreValue = 0.6f;
        private bool captured;

        public StepHeightBehavior(Entity entity) : base(entity) { }

        public override string PropertyName() => "rfstepheight";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            physics = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            if (physics != null)
            {
                restoreValue = physics.StepHeight;
                captured = true;
            }
        }

        /// <summary>Only writes when the current value disagrees with the target. The reassert
        /// only needs to win against SetProperties, which runs once per Initialize() (entity
        /// (re)creation), not every tick -- a plain per-tick compare is enough to catch that
        /// without forcing an unconditional write every tick.</summary>
        public override void OnGameTick(float deltaTime)
        {
            if (!captured || physics == null) return;

            var cfg = RFMechanicsModSystem.Config;
            if (cfg == null) return;

            float target = ResolveTarget(cfg);

            if (physics.StepHeight != target) physics.StepHeight = target;
        }

        /// <summary>Recomputed from the live race cache every tick rather than latched at
        /// Initialize(): a character-class change hands the entity back to the baseline on the next
        /// tick after PlayerRaceBehavior notices, so an elf's 2.0 can never stick to another race.
        /// A missing identity behavior reads as baseline for the same reason, never as elf.</summary>
        private float ResolveTarget(RFMechanicsConfig cfg)
        {
            if (!cfg.EnableStepHeight) return restoreValue;

            // Resolved here, not in Initialize(): loadBehaviors constructs and Initializes behaviors
            // one at a time in array order, so rfelfidentity need not exist yet when this one starts.
            identity ??= entity.GetBehavior<PlayerRaceBehavior>();

            return (float)(identity?.IsElf == true ? cfg.ElfStepHeightOverride : cfg.StepHeightValue);
        }
    }
}
