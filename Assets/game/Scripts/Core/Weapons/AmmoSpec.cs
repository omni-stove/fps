using System;

namespace Shooter.Core
{
    public sealed class AmmoSpec
    {
        public const int DefaultMagazineSize = 30;

        public const int DefaultReserveAmmo = 90;

        public const float DefaultReloadSeconds = 2f;

        public const float DefaultActiveReloadWindowStart = 0.5f;

        public const float DefaultActiveReloadWindowLength = 0.15f;

        public const float DefaultActiveReloadFailSeconds = 0.5f;

        public static readonly AmmoSpec Default = new AmmoSpec();

        public AmmoSpec(
            int magazineSize = DefaultMagazineSize,
            int reserveAmmo = DefaultReserveAmmo,
            float reloadSeconds = DefaultReloadSeconds,
            float activeReloadWindowStart = DefaultActiveReloadWindowStart,
            float activeReloadWindowLength = DefaultActiveReloadWindowLength,
            float activeReloadFailSeconds = DefaultActiveReloadFailSeconds,
            ReloadStyle reloadStyle = ReloadStyle.Whole,
            float reloadSecondsPerRound = 0f,
            AmmoKind kind = AmmoKind.Magazine,
            HeatSpec heat = null)
        {
            if (magazineSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineSize), magazineSize, "magazineSize must be positive");
            }

            SpecGuard.NonNegative(reserveAmmo, nameof(reserveAmmo));
            SpecGuard.NonNegativeFinite(reloadSeconds, nameof(reloadSeconds));

            if (!(activeReloadWindowStart >= 0f && activeReloadWindowStart < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReloadWindowStart), activeReloadWindowStart, "activeReloadWindowStart must be at least 0 and less than 1");
            }

            if (!(activeReloadWindowLength > 0f && activeReloadWindowStart + activeReloadWindowLength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReloadWindowLength), activeReloadWindowLength, "activeReloadWindowLength must be positive and end the window at or before the end of the reload");
            }

            SpecGuard.NonNegativeFinite(activeReloadFailSeconds, nameof(activeReloadFailSeconds));
            SpecGuard.Defined(reloadStyle, nameof(reloadStyle));

            // 1 発ずつ込めるのに 1 発に 0 秒かかると、リロードが一瞬で終わり Whole と見分けがつかない。
            if (reloadStyle == ReloadStyle.PerRound)
            {
                SpecGuard.PositiveFinite(reloadSecondsPerRound, nameof(reloadSecondsPerRound));
            }
            else
            {
                SpecGuard.NonNegativeFinite(reloadSecondsPerRound, nameof(reloadSecondsPerRound));
            }

            SpecGuard.Defined(kind, nameof(kind));

            if (kind == AmmoKind.Heat && heat == null)
            {
                throw new ArgumentNullException(nameof(heat), "heat is required when kind is Heat");
            }

            // 弾倉制の武器に熱の設定があると、どちらで撃てなくなるのか読み手が迷う。
            if (kind != AmmoKind.Heat && heat != null)
            {
                throw new ArgumentException("heat must be null unless kind is Heat", nameof(heat));
            }

            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            ReloadSeconds = reloadSeconds;
            ActiveReloadWindowStart = activeReloadWindowStart;
            ActiveReloadWindowLength = activeReloadWindowLength;
            ActiveReloadFailSeconds = activeReloadFailSeconds;
            ReloadStyle = reloadStyle;
            ReloadSecondsPerRound = reloadSecondsPerRound;
            Kind = kind;
            Heat = heat;
        }

        public AmmoKind Kind { get; }

        public int MagazineSize { get; }

        public int ReserveAmmo { get; }

        public float ReloadSeconds { get; }

        public float ActiveReloadWindowStart { get; }

        public float ActiveReloadWindowLength { get; }

        public float ActiveReloadFailSeconds { get; }

        public ReloadStyle ReloadStyle { get; }

        public float ReloadSecondsPerRound { get; }

        public HeatSpec Heat { get; }
    }
}
