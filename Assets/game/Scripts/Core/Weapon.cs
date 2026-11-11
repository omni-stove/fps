using System;

namespace Fps.Core
{
    /// <summary>
    /// 武器。弾倉・予備弾・連射間隔・リロードを管理する。時間は <see cref="Tick"/> で外から進める。
    /// 弾倉が空になっても自動ではリロードしない (呼び出し側が <see cref="TryStartReload"/> を呼ぶ)。
    /// </summary>
    public sealed class Weapon
    {
        /// <summary>射撃に成功するたびに呼ばれる。</summary>
        public event Action Fired;

        /// <summary>リロードが完了し弾倉が補充されたときに呼ばれる。</summary>
        public event Action Reloaded;

        private float _cooldownRemaining;
        private float _reloadRemaining;

        public WeaponSpec Spec { get; }
        public int AmmoInMagazine { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }

        /// <param name="spec">武器の設定。開始時は弾倉満タン、予備弾は spec の初期値。</param>
        public Weapon(WeaponSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            AmmoInMagazine = spec.MagazineSize;
            ReserveAmmo = spec.ReserveAmmo;
        }

        /// <summary>
        /// 弾があり、前回射撃から連射間隔が経過済みで、リロード中でなければ 1 発撃って true を返す。
        /// それ以外は何もせず false を返す。
        /// </summary>
        public bool TryFire()
        {
            if (IsReloading || AmmoInMagazine == 0 || _cooldownRemaining > 0f)
            {
                return false;
            }

            AmmoInMagazine--;
            _cooldownRemaining = Spec.FireInterval;
            Fired?.Invoke();
            return true;
        }

        /// <summary>
        /// 弾倉が満タンでなく、予備弾があり、リロード中でなければリロードを始めて true を返す。
        /// 補充は <see cref="Tick"/> でリロード時間が経過したときに行う。
        /// </summary>
        public bool TryStartReload()
        {
            if (IsReloading || AmmoInMagazine == Spec.MagazineSize || ReserveAmmo == 0)
            {
                return false;
            }

            IsReloading = true;
            _reloadRemaining = Spec.ReloadSeconds;
            return true;
        }

        /// <summary>連射のクールダウンとリロードを deltaSeconds 進める。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);

            if (!IsReloading)
            {
                return;
            }

            _reloadRemaining -= deltaSeconds;
            if (_reloadRemaining > 0f)
            {
                return;
            }

            var amount = Math.Min(Spec.MagazineSize - AmmoInMagazine, ReserveAmmo);
            AmmoInMagazine += amount;
            ReserveAmmo -= amount;
            _reloadRemaining = 0f;
            IsReloading = false;
            Reloaded?.Invoke();
        }
    }
}
