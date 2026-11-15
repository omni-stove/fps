using System;

namespace Fps.Core
{
    public sealed class MagazineAmmo : IAmmoModel
    {
        private float _reloadRemaining;
        private float _reloadElapsed;

        public MagazineAmmo(AmmoSpec spec, GameRules rules)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            AmmoInMagazine = spec.MagazineSize;
            ReserveAmmo = Weapon.ScaleReserveAmmo(spec.ReserveAmmo, rules.ReserveAmmoMultiplier);
            MaxReserveAmmo = ReserveAmmo;
            HasInfiniteReserve = rules.InfiniteReserve;
            HasActiveReload = rules.Actions.ActiveReload == ActiveReload.On;
        }

        // 予備弾はセーブの値をそのまま使い、倍率は掛けない (作ったときに掛けた値がセーブされているため)。
        public MagazineAmmo(AmmoSpec spec, int ammoInMagazine, int reserveAmmo, GameRules rules)
            : this(spec, rules)
        {
            if (ammoInMagazine < 0 || ammoInMagazine > spec.MagazineSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must be between 0 and the magazine size");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
            MaxReserveAmmo = Math.Max(MaxReserveAmmo, reserveAmmo);
        }

        public event Action Reloaded;

        public AmmoSpec Spec { get; }

        public int AmmoInMagazine { get; private set; }

        public int ReserveAmmo { get; private set; }

        public int MaxReserveAmmo { get; }

        public int ReserveAmmoRoom => HasInfiniteReserve ? 0 : Math.Max(0, MaxReserveAmmo - ReserveAmmo);

        public bool HasInfiniteReserve { get; }

        public bool HasActiveReload { get; }

        public bool HasJudgedActiveReload { get; private set; }

        public bool IsReloading { get; private set; }

        public float ReloadProgress
        {
            get
            {
                if (!IsReloading)
                {
                    return 0f;
                }

                // 経過と残りの和を分母にするのは、失敗で延びた時間も含めて 1 で終わる進みにするため。
                var total = _reloadElapsed + _reloadRemaining;
                if (!(total > 0f))
                {
                    return 0f;
                }

                return Math.Min(1f, Math.Max(0f, _reloadElapsed / total));
            }
        }

        public float? HeatFraction => null;

        public bool CanFire => !IsReloading && AmmoInMagazine > 0;

        public void Consume()
        {
            if (!CanFire)
            {
                throw new InvalidOperationException("cannot consume a round while reloading or with an empty magazine");
            }

            AmmoInMagazine--;
        }

        public int AddReserveAmmo(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var added = Math.Min(amount, ReserveAmmoRoom);
            ReserveAmmo += added;
            return added;
        }

        public bool TryStartReload()
        {
            if (IsReloading || AmmoInMagazine == Spec.MagazineSize || (!HasInfiniteReserve && ReserveAmmo == 0))
            {
                return false;
            }

            IsReloading = true;
            _reloadRemaining = Spec.ReloadSeconds;
            _reloadElapsed = 0f;
            HasJudgedActiveReload = false;
            return true;
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            result = ActiveReloadResult.Fail;
            if (!HasActiveReload || !IsReloading || HasJudgedActiveReload)
            {
                return false;
            }

            result = ActiveReloadJudge.Judge(ReloadProgress, Spec.ActiveReloadWindowStart, Spec.ActiveReloadWindowLength);
            HasJudgedActiveReload = true;
            if (result == ActiveReloadResult.Success)
            {
                CompleteReload();
            }
            else
            {
                _reloadRemaining += Spec.ActiveReloadFailSeconds;
            }

            return true;
        }

        public void CancelReload()
        {
            IsReloading = false;
            _reloadRemaining = 0f;
            _reloadElapsed = 0f;
            HasJudgedActiveReload = false;
        }

        // 予備弾の上限 (MaxReserveAmmo) で切らないのは、込めてあった弾を戻すだけで、拾って増やしたのではないため。
        public void Unload()
        {
            CancelReload();
            if (!HasInfiniteReserve)
            {
                ReserveAmmo = (int)Math.Min((long)ReserveAmmo + AmmoInMagazine, int.MaxValue);
            }

            AmmoInMagazine = 0;
        }

        public void Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!IsReloading)
            {
                return;
            }

            _reloadRemaining -= deltaSeconds;
            _reloadElapsed += deltaSeconds;
            if (_reloadRemaining > 0f)
            {
                return;
            }

            CompleteReload();
        }

        private void CompleteReload()
        {
            var missing = Spec.MagazineSize - AmmoInMagazine;
            if (HasInfiniteReserve)
            {
                AmmoInMagazine += missing;
            }
            else
            {
                var amount = Math.Min(missing, ReserveAmmo);
                AmmoInMagazine += amount;
                ReserveAmmo -= amount;
            }

            _reloadRemaining = 0f;
            _reloadElapsed = 0f;
            HasJudgedActiveReload = false;
            IsReloading = false;
            Reloaded?.Invoke();
        }
    }
}
