using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace rfmechanics
{
    public sealed class RFGoblinScoutingBehavior : EntityBehavior
    {
        private float accum;
        private float lastWalkspeed = 1f;

        public RFGoblinScoutingBehavior(Entity entity) : base(entity) { }

        public override string PropertyName() => "rfgoblinscouting";

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            RFMechanicsConfig? cfg = RFMechanicsModSystem.Config;
            if (cfg == null) return;
            accum += deltaTime;
            if (accum < cfg.GoblinScoutingMovementTickInterval) return;
            accum = 0;

            float walkspeed = 1f;
            if (cfg.EnableGoblinScouting && entity is EntityPlayer player
                && GoblinScoutingPatch.IsGoblin(player, cfg)
                && player.Controls.Sneak
                && GoblinScoutingPatch.IsDark(player, cfg)
                && !GoblinScoutingPatch.HasEmittingHeldLight(player))
            {
                walkspeed += (float)(cfg.GoblinScoutingSneakSpeedBonus + cfg.GoblinScoutingDarknessWalkSpeedBonus);
            }
            TrySet(walkspeed, cfg);
        }

        private void TrySet(float walkspeed, RFMechanicsConfig cfg)
        {
            if (Math.Abs(walkspeed - lastWalkspeed) <= cfg.GoblinScoutingStatWriteThreshold) return;
            entity.Stats.Set("walkspeed", "goblinscouting", walkspeed - 1f);
            lastWalkspeed = walkspeed;
        }
    }
}
