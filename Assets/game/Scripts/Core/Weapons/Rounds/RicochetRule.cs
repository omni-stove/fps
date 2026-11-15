using System.Numerics;

namespace Fps.Core
{
    public static class RicochetRule
    {
        public static bool TryBounce(
            RicochetSpec ricochet, int bounceCount, Vector3 direction, Vector3 normal, out Vector3 bounced, out float damageMultiplier)
        {
            bounced = direction;
            damageMultiplier = 1f;
            return false;
        }
    }
}
