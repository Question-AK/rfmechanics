using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace rfmechanics
{
    /// <summary>Automatic hunger-driven movement. Extra debt below the existing food
    /// threshold follows deliberate, measured ground travel; idle hunger still has its
    /// ordinary Thew cost. No new health trigger or combat bonus.</summary>
    public class FrenzyBehavior : EntityBehavior
    {
        private const string StatSource = "rf-orc-frenzy";

        private long fastListenerId = -1;
        private double lastElapsedHours = double.NaN;
        private Vec3d? previousPosition;
        private int previousDimension, previousPositionVersion;
        private long previousMs, hurtUntil;
        private bool previousEligible, previousSprint;
        private double previousWalkX, previousWalkZ;
        private float previousCurve;
        private readonly EntityControls sampledControls = new();
        internal double LastExertion { get; private set; }
        internal double LastDebtPerHour { get; private set; }

        internal void ExternalMotion() => hurtUntil = entity.World.ElapsedMilliseconds + 1500;
        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (damage > 0) ExternalMotion();
        }
        public override void OnEntityDeath(DamageSource source)
        {
            previousPosition = null; previousEligible = false; previousCurve = 0;
            lastElapsedHours = double.NaN; LastExertion = LastDebtPerHour = 0;
            if (entity.World.Side == EnumAppSide.Server) ClearStats();
        }
        private double SampleExertion(RFMechanicsConfig cfg)
        {
            var p = entity.Pos;
            var controls = (entity as EntityPlayer)?.ServerControls;
            long now = entity.World.ElapsedMilliseconds;
            double seconds = (now - previousMs) / 1000.0;
            bool eligible = entity.Alive && !entity.Teleporting && !entity.IsTeleport && controls != null && controls.TriesToMove && entity.OnGround
                && !entity.CollidedHorizontally && (entity as EntityAgent)?.MountedOn == null && !entity.Swimming
                && !controls.IsFlying && !controls.IsClimbing && !controls.FloorSitting
                && entity.Attributes.GetInt("dmgkb") == 0 && now >= hurtUntil;
            int positionVersion = entity.WatchedAttributes.GetInt("positionVersionNumber");
            double result = previousPosition != null && p.Dimension == previousDimension
                && positionVersion == previousPositionVersion
                ? OrcMetabolismFeedbackRules.Exertion(p.X - previousPosition.X, p.Z - previousPosition.Z,
                    seconds, previousWalkX, previousWalkZ, eligible && previousEligible,
                    controls?.Sprint == true && previousSprint, cfg.FrenzyWalkingDebtMultiplier) : 0;
            previousPosition = p.XYZ; previousDimension = p.Dimension; previousMs = now; previousPositionVersion = positionVersion;
            previousEligible = eligible; previousSprint = controls?.Sprint == true;
            // Compute the intent vector without assuming server physics updated WalkVector.
            sampledControls.FromInt(controls?.ToInt() ?? 0);
            sampledControls.CalcMovementVectors(p, 1);
            previousWalkX = sampledControls.WalkVector.X; previousWalkZ = sampledControls.WalkVector.Z;
            return result;
        }

        // Write-cache of the last values actually pushed via Stats.Set, so ties aren't rewritten.
        private float lastWalkSpeedDelta;
        private float lastJumpBonusDelta;

        public FrenzyBehavior(Entity entity) : base(entity) { }

        public override string PropertyName() => "rffrenzy";

        internal static float CurrentSpeedBonus(Entity entity) =>
            entity.GetBehavior<FrenzyBehavior>()?.lastWalkSpeedDelta ?? 0;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            var cfg = RFMechanicsModSystem.Config;
            int ms = Math.Clamp(cfg?.FrenzyFastTickMs ?? 500, 100, 1000);
            fastListenerId = entity.World.RegisterGameTickListener(FastTick, ms, 0);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (fastListenerId >= 0)
            {
                entity.World.UnregisterGameTickListener(fastListenerId);
                fastListenerId = -1;
            }
        }

        private void FastTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            // Sample before every eligibility return so inactive time never becomes a later bill.
            // A failed sample breaks the chain; the next valid sample is a fresh baseline.
            double nowElapsedHours = entity.World.Calendar.ElapsedHours;
            double elapsedGameHours = 0.0;
            if (double.IsFinite(nowElapsedHours))
            {
                if (double.IsFinite(lastElapsedHours))
                {
                    double elapsed = nowElapsedHours - lastElapsedHours;
                    if (double.IsFinite(elapsed) && elapsed > 0.0 && elapsed <= 0.05) elapsedGameHours = elapsed;
                }
                lastElapsedHours = nowElapsedHours;
            }
            else lastElapsedHours = double.NaN;

            var cfg = RFMechanicsModSystem.Config;
            LastExertion = LastDebtPerHour = 0;
            double exertion = cfg == null ? 0 : SampleExertion(cfg);
            if (cfg == null || !cfg.EnableFrenzy || !cfg.EnableThew || !entity.Alive || !IsOrc())
            {
                ClearStats();
                return;
            }

            var hunger = entity.GetBehavior<EntityBehaviorHunger>();
            var thewBhv = entity.GetBehavior<ThewBehavior>();
            if (hunger == null || hunger.MaxSaturation <= 0f || thewBhv == null)
            {
                ClearStats();
                return;
            }

            if (thewBhv.Thew <= 0f && (thewBhv.BurnDebt + thewBhv.FrenzyDebt) > 0f)
            {
                ClearStats();
                return;
            }

            float satFrac = hunger.Saturation / hunger.MaxSaturation;
            float gate = (float)OrcMetabolismFeedbackRules.Finite(cfg.FrenzySatietyGate, 0.5, 0.01, 1);
            if (satFrac >= gate)
            {
                ClearStats();
                return;
            }

            float curveMult = ComputeCurveMult(satFrac, cfg);
            // Bill only benefit that was already installed during this movement interval.
            // Food recovery cannot create a retrospective bill; the lower endpoint wins.
            double usedCurve = Math.Min(previousCurve, curveMult);
            if (satFrac < (float)cfg.FrenzyDebtSatietyThreshold && elapsedGameHours > 0)
            {
                LastExertion = exertion;
                LastDebtPerHour = OrcMetabolismFeedbackRules.Debt(cfg.FrenzyDebtPerGameHour, usedCurve, 1, exertion);
                float debtIncurred = (float)(LastDebtPerHour * elapsedGameHours);
                if (debtIncurred > 0) thewBhv.FrenzyDebt += debtIncurred;
            }

            ApplyStats(cfg, curveMult);
            previousCurve = cfg.FrenzyMaxSpeedBonus > 0
                ? Math.Clamp(lastWalkSpeedDelta / (float)cfg.FrenzyMaxSpeedBonus, 0, 1) : 0;
        }

        /// <summary>Write-threshold-gated: curveMult recomputes every fast tick, so an unconditional Stats.Set every tick would spam WatchedAttributes dirty/sync.</summary>
        private void ApplyStats(RFMechanicsConfig cfg, float curveMult)
        {
            float threshold = (float)OrcMetabolismFeedbackRules.Finite(cfg.FrenzyResponseWriteThreshold, 0.0025, 0.0001, 0.02);
            float walkSpeedDelta = (float)cfg.FrenzyMaxSpeedBonus * curveMult;
            float jumpBonusDelta = (float)cfg.FrenzyMaxJumpBonus * curveMult;

            if (Math.Abs(walkSpeedDelta - lastWalkSpeedDelta) > threshold)
            {
                entity.Stats.Set("walkspeed", StatSource, walkSpeedDelta);
                lastWalkSpeedDelta = walkSpeedDelta;
                OrcHuntModSystem.ClampPursuitSpeed(entity, cfg, walkSpeedDelta);
            }

            if (Math.Abs(jumpBonusDelta - lastJumpBonusDelta) > threshold)
            {
                entity.Stats.Set("jumpHeightMul", StatSource, jumpBonusDelta);
                lastJumpBonusDelta = jumpBonusDelta;
            }
        }

        private void ClearStats()
        {
            previousCurve = 0; previousEligible = false;
            if (lastWalkSpeedDelta != 0f)
            {
                entity.Stats.Remove("walkspeed", StatSource);
                lastWalkSpeedDelta = 0f;
            }
            if (lastJumpBonusDelta != 0f)
            {
                entity.Stats.Remove("jumpHeightMul", StatSource);
                lastJumpBonusDelta = 0f;
            }
        }

        /// <summary>Public static so /rfthew dump reports the exact ramp value FastTick is using.</summary>
        public static float ComputeCurveMult(float satFrac, RFMechanicsConfig cfg)
        {
            return (float)OrcMetabolismFeedbackRules.Curve(satFrac, cfg.FrenzySatietyGate, cfg.FrenzyResponseExponent);
        }

        private bool IsOrc()
        {
            var cfg = RFMechanicsModSystem.Config;
            if (cfg == null) return false;
            if (entity is not EntityPlayer player) return false;

            IPlayer? iplayer = player.World.PlayerByUid(player.PlayerUID);
            return RaceTraits.HasTrait(iplayer, cfg.OrcTraitCode);
        }
    }
}
