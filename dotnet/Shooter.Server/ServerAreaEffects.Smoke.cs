using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerAreaEffects : ISightBlocker
    {
        public bool Blocks(Vector3 from, Vector3 to)
        {
            foreach (var zone in _zones)
            {
                if (SmokeScreen.IsSmoke(zone.Spec) && SmokeScreen.Blocks(zone.Center, zone.Spec.Radius, from, to))
                {
                    return true;
                }
            }

            return false;
        }

        partial void AttachSmoke()
        {
            _combat.SightBlockers.Add(this);
        }
    }
}
