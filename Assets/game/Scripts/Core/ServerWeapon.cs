using System;

namespace Shooter.Core
{
    // 連射間隔はここでは制限しない。射撃は RPC で届くので、間隔どおりに撃っても届く時刻が揺れて数発がまとめて届くことがあり、
    // フレームの経過時間で数える Weapon のクールダウンでは正しい射撃まで弾いてしまう。
    // 間隔は ShotBudget が WeaponSpec.FireInterval を使って受け持つ (二重に制限しない)。
    public sealed class ServerWeapon
    {
        public event Action Changed;

        public event Action<bool> ReloadStarted;

        public event Action Reloaded;

        // 連射間隔を 0 にした Spec。内側の Weapon はこれで作る
        private WeaponSpec _innerSpec;
        private readonly GameRules _rules;
        private Weapon _weapon;
        private IAmmoPool _pool;

        public ServerWeapon(WeaponSpec spec)
            : this(spec, GameRules.Default)
        {
        }

        public ServerWeapon(WeaponSpec spec, GameRules rules)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _innerSpec = spec.WithFireInterval(0f);
            _weapon = CreateWeapon();
        }

        public WeaponSpec Spec { get; private set; }

        public int AmmoInMagazine => _weapon.AmmoInMagazine;
        public int ReserveAmmo => _weapon.ReserveAmmo;

        public int StartingReserveAmmo => _weapon.StartingReserveAmmo;

        public bool HasAmmoPool => _weapon.AmmoPool != null;
        public bool IsReloading => _weapon.IsReloading;

        public AmmoTypeSelector AmmoTypes => _weapon.AmmoTypes;

        public float? HeatFraction => _weapon.Ammo.HeatFraction;

        public bool IsOverheated => _weapon.Ammo is HeatAmmo heat && heat.IsOverheated;

        public bool HasInfiniteReserve => _weapon.HasInfiniteReserve;

        public bool HasActiveReload => _weapon.HasActiveReload;

        public bool HasJudgedActiveReload => _weapon.HasJudgedActiveReload;

        public float ReloadProgress => _weapon.ReloadProgress;

        public float ReloadDurationSeconds
        {
            get
            {
                if (!_weapon.IsReloading)
                {
                    return 0f;
                }

                // 成功すればその場でリロードが終わるので、判定済みのままリロード中なのは失敗したときだけ
                return _weapon.HasJudgedActiveReload ? Spec.ReloadSeconds + Spec.ActiveReloadFailSeconds : Spec.ReloadSeconds;
            }
        }

        public ShotRejection TryFire()
        {
            // リロード中かどうかだけで弾かないのは、1 発ずつ込める弾薬 (PerRoundReload) が込めた弾で撃ってリロードを止めるため。
            if (_weapon.IsReloading && !_weapon.Ammo.CanFire)
            {
                return ShotRejection.Reloading;
            }

            if (_weapon.AmmoInMagazine == 0)
            {
                ReloadIfAuto();
                return ShotRejection.OutOfAmmo;
            }

            if (_weapon.Ammo is HeatAmmo heat && heat.IsOverheated)
            {
                return ShotRejection.Overheated;
            }

            if (!_weapon.TryFire())
            {
                // 連射間隔は 0 なので、リロード中でも空でもオーバーヒート中でもなければ必ず撃てる
                throw new InvalidOperationException("the inner weapon refused a shot with ammo and no reload");
            }

            Changed?.Invoke();
            if (_weapon.AmmoInMagazine == 0)
            {
                ReloadIfAuto();
            }

            return ShotRejection.None;
        }

        public bool TryStartReload()
        {
            return StartReload(false);
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            if (!_weapon.TryActiveReload(out result))
            {
                return false;
            }

            // 成功は Reloaded 経由で Changed を呼んでいる。失敗は弾数もリロード中かどうかも変わらないが、
            // かかる時間と判定済みかどうかが変わり、同期し直す必要があるので知らせる
            if (result == ActiveReloadResult.Fail)
            {
                Changed?.Invoke();
            }

            return true;
        }

        public bool TrySwitchAmmoType()
        {
            if (!_weapon.TrySwitchAmmoType())
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public void CancelReload()
        {
            if (!_weapon.IsReloading)
            {
                return;
            }

            _weapon.CancelReload();
            Changed?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            _weapon.Tick(deltaSeconds);
        }

        public void Refill()
        {
            _weapon.Reloaded -= HandleReloaded;
            _weapon.UseAmmoPool(null);
            _weapon = CreateWeapon();
            Changed?.Invoke();
        }

        // 弾倉が減って溢れる弾が予備弾の受け皿に入りきらなければ、弾を失わないよう何も変えずに false を返す。
        public bool ChangeSpec(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!string.Equals(spec.Id, Spec.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("spec is for weapon '" + spec.Id + "', not '" + Spec.Id + "'", nameof(spec));
            }

            var magazine = _weapon.AmmoInMagazine;
            var excess = magazine - spec.MagazineSize;
            if (excess > 0 && _weapon.AmmoPool != null && _weapon.AmmoPool.Room < excess)
            {
                return false;
            }

            _weapon.CancelReload();
            if (excess > 0)
            {
                magazine = spec.MagazineSize;
                if (_weapon.AmmoPool != null)
                {
                    _weapon.AmmoPool.Put(excess);
                }
            }

            var reserve = _weapon.ReserveAmmo + (excess > 0 && _weapon.AmmoPool == null ? excess : 0);
            var ammoType = _weapon.AmmoTypes.Index;
            _weapon.Reloaded -= HandleReloaded;
            _weapon.UseAmmoPool(null);
            Spec = spec;
            _innerSpec = spec.WithFireInterval(0f);
            _weapon = new Weapon(_innerSpec, magazine, reserve, _rules);
            _weapon.Reloaded += HandleReloaded;
            if (_pool != null)
            {
                _weapon.UseAmmoPool(_pool);
            }

            _weapon.AmmoTypes.TrySelect(ammoType);
            Changed?.Invoke();
            return true;
        }

        // 予備弾を pool から出し入れするようにする。満タン戻し (Refill) で武器を作り直しても同じ pool を使い続ける
        public bool UseAmmoPool(IAmmoPool pool)
        {
            _pool = pool;
            var used = _weapon.UseAmmoPool(pool);
            Changed?.Invoke();
            return used;
        }

        private void ReloadIfAuto()
        {
            if (_rules.ReloadMode == ReloadMode.Auto)
            {
                StartReload(true);
            }
        }

        private bool StartReload(bool automatic)
        {
            if (!_weapon.TryStartReload())
            {
                return false;
            }

            ReloadStarted?.Invoke(automatic);
            Changed?.Invoke();
            return true;
        }

        private Weapon CreateWeapon()
        {
            var weapon = new Weapon(_innerSpec, _rules);
            weapon.Reloaded += HandleReloaded;
            if (_pool != null)
            {
                weapon.UseAmmoPool(_pool);
            }

            return weapon;
        }

        private void HandleReloaded()
        {
            Reloaded?.Invoke();
            Changed?.Invoke();
        }
    }
}
