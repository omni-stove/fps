namespace Shooter.Core
{
    public static class GunGameDisplayedTier
    {
        public static int Of(int tier, int tierCount)
        {
            if (tier < 0)
            {
                return 1;
            }

            return tier >= tierCount ? tierCount : tier + 1;
        }
    }
}
