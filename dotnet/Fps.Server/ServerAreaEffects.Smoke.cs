using System.Numerics;
using Fps.Core;

namespace Fps.Server
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
