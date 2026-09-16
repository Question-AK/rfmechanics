using System;

namespace rfmechanics;

// Pure interaction state, independent of rendering and the calendar clock.
internal sealed class OrcSmellFocusState
{
    internal float HeldMs { get; private set; }
    internal float StillMs { get; private set; }
    internal bool Active { get; private set; }
    internal bool NeedsRelease { get; private set; }
    private float settledMs;

    internal void Update(float dt, bool held, bool eligible, bool stationary, bool hurt, bool interrupted = false, float fullMs = 4000)
    {
        dt = Math.Clamp(dt, 0, 0.1f);
        NeedsRelease = false;
        if (!eligible || !held)
        {
            HeldMs = StillMs = settledMs = 0;
            Active = false;
            return;
        }
        settledMs += dt * 1000;
        if (settledMs < 50) return;
        Active = true;
        HeldMs += dt * 1000;
        // Sprinting, jumping and damage shed deep focus, without latching the button.
        // Walking builds more slowly and cannot retain full stationary concentration.
        if (hurt || interrupted) StillMs = 0;
        else if (stationary) StillMs += dt * 1000;
        else StillMs = Math.Min(StillMs + dt * 350, Math.Clamp(fullMs, 1000, 15000) * 0.5f);
    }

    internal float Quality(float fullMs) => Math.Clamp(StillMs / Math.Max(1, fullMs), 0, 1);

    internal float Darkness(float engageMs, float movingWeight)
    {
        if (!Active) return 0;
        float warmup = Math.Clamp(HeldMs / Math.Max(1, engageMs), 0, 1);
        return Math.Clamp(movingWeight, 0, 0.5f) * warmup
            + (1 - Math.Clamp(movingWeight, 0, 0.5f)) * Math.Clamp(StillMs / Math.Max(1, engageMs), 0, 1);
    }
}
