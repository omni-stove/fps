namespace Shooter.Core
{
    public static class AmmoModels
    {
        public static IAmmoModel For(AmmoSpec spec, GameRules rules)
        {
            return Wrap(spec, new MagazineAmmo(spec, rules));
        }

        public static IAmmoModel For(AmmoSpec spec, int ammoInMagazine, int reserveAmmo, GameRules rules)
        {
            return Wrap(spec, new MagazineAmmo(spec, ammoInMagazine, reserveAmmo, rules));
        }

        private static IAmmoModel Wrap(AmmoSpec spec, MagazineAmmo magazine)
        {
            if (spec.Kind == AmmoKind.Heat)
            {
                return new HeatAmmo(spec.Heat, magazine);
            }

            return spec.ReloadStyle == ReloadStyle.PerRound ? new PerRoundReload(spec, magazine) : (IAmmoModel)magazine;
        }
    }
}
