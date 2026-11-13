using System;

namespace Fps.Core
{
    /// <summary>
    /// HUD の各項目を出すか。<see cref="For"/> で <see cref="HudDensity"/> から決める。
    /// <list type="bullet">
    /// <item><see cref="HudDensity.Full"/>: すべて出す。</item>
    /// <item><see cref="HudDensity.Minimal"/>: 弾数は弾倉の数だけにし (予備を出さない)、照準を <see cref="SmallCrosshairScale"/> に縮める。ほかは Full と同じ。</item>
    /// <item><see cref="HudDensity.Hardcore"/>: 体力・弾数・武器名・照準・ヒットマーカーを消す。残り時間・点数・被弾方向・キルログは残す。</item>
    /// </list>
    /// 一時的な案内 (リスポーン・セーブ)・試合結果・リロードの進み・スコアボードは、遊ぶのに要るのでどの量でも出す (ここでは扱わない)。
    /// </summary>
    public sealed class HudVisibility
    {
        /// <summary>照準をそのままの大きさで出すときの倍率。</summary>
        public const float FullCrosshairScale = 1f;

        /// <summary><see cref="HudDensity.Minimal"/> で照準を縮める倍率。</summary>
        public const float SmallCrosshairScale = 0.6f;

        /// <summary>すべて出す。</summary>
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
            killFeed: true);

        /// <summary>弾数を弾倉の数だけにし、照準を小さくする。</summary>
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
            killFeed: true);

        /// <summary>体力・弾数・武器名・照準・ヒットマーカーを消し、残り時間・点数・被弾方向・キルログだけ残す。</summary>
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
            killFeed: true);

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
            bool killFeed)
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
        }

        /// <summary>この組を決めた HUD の量。</summary>
        public HudDensity Density { get; }

        /// <summary>体力を出すか。</summary>
        public bool Health { get; }

        /// <summary>弾数の欄を出すか。</summary>
        public bool Ammo { get; }

        /// <summary>弾数に予備の数を並べるか。false なら弾倉の数 (リロード中はその案内) だけを出す。</summary>
        public bool ReserveAmmo { get; }

        /// <summary>持っている武器の名前を出すか。</summary>
        public bool WeaponName { get; }

        /// <summary>残り時間を出すか。</summary>
        public bool Time { get; }

        /// <summary>チームの点数を出すか。</summary>
        public bool Score { get; }

        /// <summary>照準を出すか。true でも ADS 中とダッシュ中は HUD が隠す。</summary>
        public bool Crosshair { get; }

        /// <summary>照準の大きさの倍率 (線と隙間をまとめて縮める)。</summary>
        public float CrosshairScale { get; }

        /// <summary>弾が当たった印 (と音) を出すか。</summary>
        public bool HitMarker { get; }

        /// <summary>撃たれた方向を出すか。</summary>
        public bool DamageDirection { get; }

        /// <summary>キルログを出すか。</summary>
        public bool KillFeed { get; }

        /// <summary>density の HUD で各項目を出すか。</summary>
        /// <exception cref="ArgumentOutOfRangeException">density が定義済みの値でない。</exception>
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
