using System;

namespace Shooter.Core
{
    // 一時的な案内 (リスポーン・セーブ)・試合結果・リロードの進み・スコアボードをここで扱わないのは、遊ぶのに要るのでどの量でも出すため。
    public sealed class HudVisibility
    {
        public const float FullCrosshairScale = 1f;

        public const float SmallCrosshairScale = 0.6f;

        public static readonly HudVisibility Full = new HudVisibility(
            HudDensity.Full,
            health: true,
            ammo: true,
            reserveAmmo: true,
            weaponName: true,
            time: true,
            score: true,
            crosshair: true,
            crosshairScale: FullCrosshairScale,
            hitMarker: true,
            damageDirection: true,
            killFeed: true,
            minimap: true,
            compass: true);

        public static readonly HudVisibility Minimal = new HudVisibility(
            HudDensity.Minimal,
            health: true,
            ammo: true,
            reserveAmmo: false,
            weaponName: true,
            time: true,
            score: true,
            crosshair: true,
            crosshairScale: SmallCrosshairScale,
            hitMarker: true,
            damageDirection: true,
            killFeed: true,
            minimap: false,
            compass: false);

        public static readonly HudVisibility Hardcore = new HudVisibility(
            HudDensity.Hardcore,
            health: false,
            ammo: false,
            reserveAmmo: false,
            weaponName: false,
            time: true,
            score: true,
            crosshair: false,
            crosshairScale: FullCrosshairScale,
            hitMarker: false,
            damageDirection: true,
            killFeed: true,
            minimap: false,
            compass: false);

        private HudVisibility(
            HudDensity density,
            bool health,
            bool ammo,
            bool reserveAmmo,
            bool weaponName,
            bool time,
            bool score,
            bool crosshair,
            float crosshairScale,
            bool hitMarker,
            bool damageDirection,
            bool killFeed,
            bool minimap,
            bool compass)
        {
            Density = density;
            Health = health;
            Ammo = ammo;
            ReserveAmmo = reserveAmmo;
            WeaponName = weaponName;
            Time = time;
            Score = score;
            Crosshair = crosshair;
            CrosshairScale = crosshairScale;
            HitMarker = hitMarker;
            DamageDirection = damageDirection;
            KillFeed = killFeed;
            Minimap = minimap;
            Compass = compass;
        }

        public HudDensity Density { get; }

        public bool Health { get; }

        public bool Ammo { get; }

        public bool ReserveAmmo { get; }

        public bool WeaponName { get; }

        public bool Time { get; }

        public bool Score { get; }

        public bool Crosshair { get; }

        public float CrosshairScale { get; }

        public bool HitMarker { get; }

        public bool DamageDirection { get; }

        public bool KillFeed { get; }

        public bool Minimap { get; }

        public bool Compass { get; }

        public static HudVisibility For(HudDensity density)
        {
            switch (density)
            {
                case HudDensity.Full:
                    return Full;
                case HudDensity.Minimal:
                    return Minimal;
                case HudDensity.Hardcore:
                    return Hardcore;
                default:
                    throw new ArgumentOutOfRangeException(nameof(density), density, "density is not a defined HudDensity");
            }
        }

        public override string ToString()
        {
            return "density=" + Density;
        }
    }
}
