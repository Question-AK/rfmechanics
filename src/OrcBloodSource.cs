using System;
using System.Collections;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace rfmechanics;

// Server-only reads of actual provider state. Neither health fraction nor hit counters
// mean bleeding. The Hunter 0.2.58 creates a Bleeding DoT marker alongside its silent
// damage effect; BloodTrail 1.2.5 owns a separate synchronized flag.
internal sealed class OrcBloodSource
{
    internal readonly bool BloodTrail, Hunter;
    private readonly object? hunter;
    private readonly FieldInfo? silentEffects, ticksLeft;
    internal OrcBloodSource(ICoreAPI api)
    {
        BloodTrail = api.ModLoader.IsModEnabled("bloodtrail");
        Hunter = api.ModLoader.IsModEnabled("thehunter");
        if (Hunter)
        {
            hunter = api.ModLoader.GetModSystem("TheHunter.TheHunterModSystem");
            silentEffects = hunter?.GetType().GetField("silentBleedsByEntityId", BindingFlags.NonPublic | BindingFlags.Instance);
            ticksLeft = hunter?.GetType().GetNestedType("SilentBleedEffect", BindingFlags.NonPublic)?.GetField("TicksLeft");
            if (silentEffects == null || ticksLeft == null)
            {
                Hunter = false;
                api.Logger.Warning("[rfmechanics] Hunter bleeding schema unavailable; adapter disabled.");
            }
        }
    }
    internal bool Available => BloodTrail || Hunter;
    internal bool IsBleeding(Entity entity)
    {
        if (!entity.Alive) return false;
        if (BloodTrail && entity.GetBehavior("isBleeding")?.GetType().FullName ==
            "BloodTrail.src.Server.EntityBleedingBehavior" && entity.WatchedAttributes.GetBool("isBleeding")) return true;
        if (Hunter)
        {
            // The health marker can outlive a removed silent effect (e.g. provider
            // disable). Require the provider's actual live effect as well as its marker.
            if (silentEffects?.GetValue(hunter) is IDictionary map && map[entity.EntityId] is IList active)
            {
                bool live = false;
                for (int i = 0; i < Math.Min(active.Count, 32); i++)
                    if (ticksLeft?.GetValue(active[i]) is int remaining && remaining > 0) { live = true; break; }
                var effects = entity.GetBehavior<EntityBehaviorHealth>()?.ActiveDoTEffects;
                if (live && effects != null)
                    foreach (var effect in effects)
                        if (effect.EffectType == (int)EnumDamageOverTimeEffectType.Bleeding && effect.TicksLeft > 0) return true;
            }
        }
        return false;
    }
}
