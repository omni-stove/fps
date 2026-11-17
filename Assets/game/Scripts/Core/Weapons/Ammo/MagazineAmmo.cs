using System;

namespace Shooter.Core
{
    public sealed class MagazineAmmo : IAmmoModel
    {
        private float _reloadRemaining;
        private float _reloadElapsed;
        private int _reserve;
        private int _maxReserve;
        private IAmmoPool _pool;

        public MagazineAmmo(AmmoSpec spec, GameRules rules)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            AmmoInMagazine = spec.MagazineSize;
            StartingReserveAmmo = Weapon.ScaleReserveAmmo(spec.ReserveAmmo, rules.ReserveAmmoMultiplier);
            _reserve = StartingReserveAmmo;
            _maxReserve = _reserve;
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
            _reserve = reserveAmmo;
            _maxReserve = Math.Max(_maxReserve, reserveAmmo);
        }

        public event Action Reloaded;

        public AmmoSpec Spec { get; }

        public int AmmoInMagazine { get; private set; }

        public int ReserveAmmo => _pool != null ? _pool.Count : _reserve;

        public int MaxReserveAmmo => _pool != null ? (int)Math.Min((long)_pool.Count + _pool.Room, int.MaxValue) : _maxReserve;

        public int ReserveAmmoRoom => HasInfiniteReserve ? 0 : _pool != null ? _pool.Room : Math.Max(0, _maxReserve - _reserve);

        public int StartingReserveAmmo { get; }

        public bool HasPool => _pool != null;

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

            if (_pool != null)
            {
                return HasInfiniteReserve ? 0 : _pool.Put(amount);
            }

            var added = Math.Min(amount, ReserveAmmoRoom);
            _reserve += added;
            return added;
        }

        // 予備弾を pool から出し入れするようにし、持っていた予備弾を pool に移す。
        // pool に既にある分は数えて足りない分だけ移すのは、作り直した武器 (持ち替えや拾い直しで同じ弾数のまま作る) が弾を増やさないようにするため。
        // 予備弾が無限なら数えないので pool を使わず false。
        public bool UsePool(IAmmoPool pool)
        {
            if (pool == null)
            {
                _pool = null;
                return false;
            }

            if (HasInfiniteReserve)
            {
                return false;
            }

            var own = _reserve;
            _pool = pool;
            _reserve = 0;
            var missing = own - pool.Count;
            if (missing > 0)
            {
                pool.Put(missing);
            }

            return true;
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
        // pool に入り切らない弾は戻せないので捨てる (持ち物の外に置く場所が無い)。
        public void Unload()
        {
            CancelReload();
            if (_pool != null)
            {
                _pool.Put(AmmoInMagazine);
            }
            else if (!HasInfiniteReserve)
            {
                _reserve = (int)Math.Min((long)_reserve + AmmoInMagazine, int.MaxValue);
            }

            AmmoInMagazine = 0;
        }

        internal bool CanLoadRound => !IsReloading && AmmoInMagazine < Spec.MagazineSize && (HasInfiniteReserve || ReserveAmmo > 0);

        // 1 発ずつ込めるリロード (PerRoundReload) が、弾倉と予備弾の数え方をここに置いたまま 1 発だけ移すため。
        internal void LoadRound()
        {
            if (!CanLoadRound)
            {
                throw new InvalidOperationException("cannot load a round while reloading, with a full magazine or without reserve ammo");
            }

            if (_pool != null)
            {
                if (_pool.Take(1) == 0)
                {
                    return;
                }
            }
            else if (!HasInfiniteReserve)
            {
                _reserve--;
            }

            AmmoInMagazine++;
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
            else if (_pool != null)
            {
                AmmoInMagazine += _pool.Take(Math.Min(missing, _pool.Count));
            }
            else
            {
                var amount = Math.Min(missing, _reserve);
                AmmoInMagazine += amount;
                _reserve -= amount;
            }

            _reloadRemaining = 0f;
            _reloadElapsed = 0f;
            HasJudgedActiveReload = false;
            IsReloading = false;
            Reloaded?.Invoke();
        }
    }
}
