namespace Fps.Core
{
    public static class PenetrationRule
    {
        public static bool TryPassThrough(
            PenetrationSpec penetration, int passedCount, string surfaceMaterialId, float thickness, out float damageMultiplier)
        {
            damageMultiplier = 1f;
            return false;
        }
    }
}
