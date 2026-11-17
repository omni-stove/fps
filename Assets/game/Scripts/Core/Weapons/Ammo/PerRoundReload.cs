using System;

namespace Shooter.Core
{
    public sealed class PerRoundReload : IAmmoModel
    {
        private readonly MagazineAmmo _magazine;
        private float _roundElapsed;
        private int _loadedThisReload;

        public PerRoundReload(AmmoSpec ammo, MagazineAmmo magazine)
        {
            Spec = ammo ?? throw new ArgumentNullException(nameof(ammo));
            _magazine = magazine ?? throw new ArgumentNullException(nameof(magazine));
        }

        // 1 発込めるたびに呼ぶ。最後の 1 発でだけ呼ぶと、HUD とサーバーの同期 (ServerWeapon.Changed) が途中の弾数を知らないまま進む。
        public event Action Reloaded;

        public AmmoSpec Spec { get; }

        public int AmmoInMagazine => _magazine.AmmoInMagazine;

        public int ReserveAmmo => _magazine.ReserveAmmo;

        public int MaxReserveAmmo => _magazine.MaxReserveAmmo;

        public int ReserveAmmoRoom => _magazine.ReserveAmmoRoom;

        public int StartingReserveAmmo => _magazine.StartingReserveAmmo;

        public bool HasInfiniteReserve => _magazine.HasInfiniteReserve;

        // 1 発ずつ込める間に押し直す時機を決められないので、アクティブリロードはルールに関わらず使わない。
        public bool HasActiveReload => false;

        public bool HasJudgedActiveReload => false;

        public bool IsReloading { get; private set; }

        // 予備弾を拾うと込める数が途中で増えるので、分母は始めたときではなく今から込められる数で数え直す。
        public float ReloadProgress
        {
            get
            {
                if (!IsReloading)
                {
                    return 0f;
                }

                var remaining = Math.Min(Spec.MagazineSize - AmmoInMagazine, HasInfiniteReserve ? int.MaxValue : ReserveAmmo);
                var total = _loadedThisReload + remaining;
                if (total <= 0)
                {
                    return 0f;
                }

                return Math.Min(1f, (_loadedThisReload + (_roundElapsed / Spec.ReloadSecondsPerRound)) / total);
            }
        }

        public float? HeatFraction => null;

        // リロード中でも込めた弾があれば撃てる。撃つとリロードは止まる (Consume)。
        public bool CanFire => _magazine.CanFire;

        public void Consume()
        {
            if (!CanFire)
            {
                throw new InvalidOperationException("cannot consume a round with an empty magazine");
            }

            CancelReload();
            _magazine.Consume();
        }

        public int AddReserveAmmo(int amount)
        {
            return _magazine.AddReserveAmmo(amount);
        }

        public bool UsePool(IAmmoPool pool)
        {
            return _magazine.UsePool(pool);
        }

        public bool TryStartReload()
        {
            if (IsReloading || !_magazine.CanLoadRound)
            {
                return false;
            }

            IsReloading = true;
            _roundElapsed = 0f;
            _loadedThisReload = 0;
            return true;
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            result = ActiveReloadResult.Fail;
            return false;
        }

        public void CancelReload()
        {
            IsReloading = false;
            _roundElapsed = 0f;
            _loadedThisReload = 0;
        }

        public void Unload()
        {
            CancelReload();
            _magazine.Unload();
        }

        public void Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!IsReloading)
            {
                return;
            }

            _roundElapsed += deltaSeconds;
            while (IsReloading && _roundElapsed >= Spec.ReloadSecondsPerRound)
            {
                _roundElapsed -= Spec.ReloadSecondsPerRound;
                _magazine.LoadRound();
                _loadedThisReload++;
                if (!_magazine.CanLoadRound)
                {
                    CancelReload();
                }

                Reloaded?.Invoke();
            }
        }
    }
}
