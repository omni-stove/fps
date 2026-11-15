using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    internal sealed partial class ServerAreaEffects
    {
        partial void StepFire(Zone zone, float deltaSeconds)
        {
            var burn = FireArea.Affliction(zone.Spec);
            if (burn == null || !FireArea.Pulses(zone.Spec, zone.Spec.DurationSeconds - zone.Remaining, deltaSeconds))
            {
                return;
            }

            var statusEffects = _combat.StatusEffects;
            foreach (var body in _combat.Context.Bodies.All)
            {
                var shape = body.Shape;
                var bottom = body.Position + new Vector3(0f, shape.BottomY, 0f);
                var top = body.Position + new Vector3(0f, shape.TopY, 0f);
                if (ServerCombat.DistanceToCapsule(zone.Center, bottom, top, body.Radius) <= zone.Spec.Radius)
                {
                    statusEffects.Afflict(zone.OwnerId, zone.WeaponId, body.PlayerId, burn);
                }
            }
        }
    }
}
