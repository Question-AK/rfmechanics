namespace rfmechanics
{
    public enum GoblinScoutingState
    {
        None,
        CrouchedDarkGround,
        EmptyHandWallClimb,
        OneHandWallClimb,
        Sprinting,
        Standing
    }

    public static class GoblinScoutingRules
    {
        public static float ResolveFactor(
            bool drifterFamily,
            bool animalFamily,
            bool dark,
            bool emittingHeldLight,
            bool activeTarget,
            bool closeContact,
            bool sneaking,
            bool onGround,
            bool freeHandWallClimb,
            int freeHands,
            bool sprinting,
            RFMechanicsConfig cfg,
            out GoblinScoutingState state)
        {
            state = GoblinScoutingState.None;
            if (!drifterFamily && !animalFamily) return 1f;
            if (!dark || emittingHeldLight || activeTarget || closeContact) return 1f;

            float factor;
            if (sneaking && onGround)
            {
                state = GoblinScoutingState.CrouchedDarkGround;
                factor = (float)cfg.GoblinScoutingCrouchedDarkGroundFactor;
            }
            else if (freeHandWallClimb)
            {
                if (freeHands >= 2)
                {
                    state = GoblinScoutingState.EmptyHandWallClimb;
                    factor = (float)cfg.GoblinScoutingEmptyHandWallClimbFactor;
                }
                else
                {
                    state = GoblinScoutingState.OneHandWallClimb;
                    factor = (float)cfg.GoblinScoutingOneHandWallClimbFactor;
                }
            }
            else if (sprinting)
            {
                state = GoblinScoutingState.Sprinting;
                factor = (float)cfg.GoblinScoutingSprintingFactor;
            }
            else
            {
                state = GoblinScoutingState.Standing;
                factor = (float)cfg.GoblinScoutingStandingFactor;
            }

            return animalFamily ? System.MathF.Max(factor, (float)cfg.GoblinScoutingAnimalFactor) : factor;
        }

        public static bool MatchesFamily(string? codePath, string[]? roots)
        {
            if (string.IsNullOrEmpty(codePath) || roots == null) return false;
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (string.IsNullOrEmpty(root)) continue;
                if (codePath == root || (codePath.Length > root.Length && codePath[root.Length] == '-' && codePath.StartsWith(root))) return true;
            }
            return false;
        }
    }
}
