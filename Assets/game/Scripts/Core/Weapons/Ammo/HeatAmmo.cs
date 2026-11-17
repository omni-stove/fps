using System;

namespace Shooter.Core
{
    public sealed class HeatAmmo : IAmmoModel
    {
        private readonly MagazineAmmo _magazine;

        public HeatAmmo(HeatSpec heat, MagazineAmmo magazine)
        {
            Spec = heat ?? throw new ArgumentNullException(nameof(heat));
            _magazine = magazine ?? throw new ArgumentNullException(nameof(magazine));
            Heat = new WeaponHeat(heat);
        }

        public event Action Reloaded
        {
            add { }
            remove { }
        }

        public HeatSpec Spec { get; }

        public WeaponHeat Heat { get; }

        public bool IsOverheated => Heat.IsOverheated;

        // 弾倉は減らさないので、HUD やセーブには作ったときの値をそのまま返す。
        public int AmmoInMagazine => _magazine.AmmoInMagazine;

        public int ReserveAmmo => _magazine.ReserveAmmo;

        public int MaxReserveAmmo => _magazine.MaxReserveAmmo;

        // 弾薬箱が熱の武器に弾を配っても撃てる量は変わらないので、拾える余地を 0 にして弾を取らせない。
        public int ReserveAmmoRoom => 0;

        public int StartingReserveAmmo => 0;

        public bool HasInfiniteReserve => _magazine.HasInfiniteReserve;

        public bool HasActiveReload => false;

        public bool HasJudgedActiveReload => false;

        public bool IsReloading => false;

        public float ReloadProgress => 0f;

        public float? HeatFraction => Heat.Fraction;

        public bool CanFire => Heat.CanFire;

        public void Consume()
        {
            Heat.AddShot();
        }

        public int AddReserveAmmo(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            return 0;
        }

        // 熱で撃つ武器は弾を使わないので、持ち物の弾薬も減らさない
        public bool UsePool(IAmmoPool pool)
        {
            return false;
        }

        public bool TryStartReload()
        {
            return false;
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            result = ActiveReloadResult.Fail;
            return false;
        }

        public void CancelReload()
        {
        }

        public void Unload()
        {
            _magazine.Unload();
        }

        public void Tick(float deltaSeconds)
        {
            Heat.Tick(deltaSeconds);
        }
    }
}
