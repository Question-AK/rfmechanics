using System;

namespace rfmechanics;

// Stance-owned concentration; no ability key or release latch.
internal sealed class OrcSmellFocusState
{
    internal float Quality { get; private set; }
    internal float Fade { get; private set; }
    internal void Reset(bool vision = true) { Quality = 0; if (vision) Fade = 0; }
    internal void Update(float dt, bool active, float targetQuality, bool resting, RFMechanicsConfig cfg)
    {
        dt = Math.Clamp(dt, 0, 0.1f);
        if (!active) Quality = 0;
        else Quality = Move(Quality, targetQuality, dt / (float)Math.Clamp(
            Quality < targetQuality ? cfg.OrcDeepFocusSeconds : cfg.OrcFocusRecoverySeconds, 0.1, 30));
        float desiredFade = active && resting ? Quality : 0;
        Fade = Move(Fade, desiredFade, dt / (float)Math.Clamp(
            Fade < desiredFade ? cfg.OrcFocusFadeSeconds : cfg.OrcFocusVisionRecoverySeconds, 0.1, 30));
    }
    private static float Move(float value, float target, float step) => value + Math.Clamp(target - value, -step, step);
}
