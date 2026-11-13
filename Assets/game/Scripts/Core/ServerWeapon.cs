using System;

namespace Fps.Core
{
    /// <summary>
    /// サーバーが持つプレイヤー 1 人ぶんの武器。<see cref="Weapon"/> (弾倉・予備弾・リロード) に、撃てなかった理由
    /// (<see cref="ShotRejection"/>)、弾倉が空のまま撃とうとしたときの自動リロード、リスポーンでの満タン戻しを足す。
    /// 時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// 連射間隔はここでは制限しない。射撃は RPC で届くので、間隔どおりに撃っても届く時刻が揺れて数発がまとめて届くことがあり、
    /// フレームの経過時間で数える <see cref="Weapon"/> のクールダウンでは正しい射撃まで弾いてしまう。
    /// 間隔は <see cref="ShotBudget"/> が <see cref="WeaponSpec.FireInterval"/> を使って受け持つ (二重に制限しない)。
    /// </remarks>
    public sealed class ServerWeapon
    {
        /// <summary>
        /// 弾倉・予備弾・リロード中かどうか・今のリロードにかかる時間 (<see cref="ReloadDurationSeconds"/>) のどれかが変わったときに呼ばれる。
        /// </summary>
        public event Action Changed;

        /// <summary>リロードを始めたときに呼ばれる。引数は空撃ちによる自動リロードなら true。</summary>
        public event Action<bool> ReloadStarted;

        /// <summary>リロードが終わって弾倉が補充されたときに呼ばれる。アクティブリロードに成功してその場で終えたときも呼ばれる。</summary>
        public event Action Reloaded;

        // 連射間隔を 0 にした Spec。内側の Weapon はこれで作る
        private readonly WeaponSpec _innerSpec;
        private readonly GameRules _rules;
        private Weapon _weapon;

        /// <param name="spec">武器の設定。<see cref="WeaponSpec.FireInterval"/> は使わない (<see cref="ShotBudget"/> に渡す)。予備弾は有限で倍率 1。</param>
        public ServerWeapon(WeaponSpec spec)
            : this(spec, GameRules.Default)
        {
        }

        /// <param name="spec">武器の設定。<see cref="WeaponSpec.FireInterval"/> は使わない (<see cref="ShotBudget"/> に渡す)。</param>
        /// <param name="rules">
        /// 予備弾とアクティブリロードのルール。初期の予備弾と <see cref="Refill"/> で戻す値に <see cref="GameRules.ReserveAmmoMultiplier"/> を掛け、
        /// <see cref="GameRules.InfiniteReserve"/> ならリロードで予備弾を減らさない。<see cref="ActionRules.ActiveReload"/> が
        /// <see cref="ActiveReload.On"/> ならリロード中の <see cref="TryActiveReload"/> で判定する。
        /// </param>
        public ServerWeapon(WeaponSpec spec, GameRules rules)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _innerSpec = spec.FireInterval == 0f
                ? spec
                : new WeaponSpec(
                    spec.Id,
                    spec.DisplayName,
                    spec.Category,
                    spec.FireMode,
                    spec.MagazineSize,
                    spec.ReserveAmmo,
                    0f,
                    spec.ReloadSeconds,
                    spec.Damage,
                    spec.Range,
                    spec.Falloff,
                    spec.ExplosionRadius,
                    spec.AdsFieldOfView,
                    spec.BlindFireSpreadDegrees,
                    spec.ActiveReloadWindowStart,
                    spec.ActiveReloadWindowLength,
                    spec.ActiveReloadFailSeconds,
                    spec.RecoilPitchDegrees,
                    spec.RecoilYawDegrees,
                    spec.RecoilRecoveryPerSecond,
                    spec.HipSpreadDegrees,
                    spec.AdsSpreadDegrees,
                    spec.MoveSpreadDegrees);
            _weapon = CreateWeapon();
        }

        /// <summary>作るときに渡された設定。</summary>
        public WeaponSpec Spec { get; }

        public int AmmoInMagazine => _weapon.AmmoInMagazine;
        public int ReserveAmmo => _weapon.ReserveAmmo;
        public bool IsReloading => _weapon.IsReloading;

        /// <summary>true ならリロードで予備弾を減らさず、予備弾が 0 でもリロードできる。</summary>
        public bool HasInfiniteReserve => _weapon.HasInfiniteReserve;

        /// <summary>true ならリロード中の <see cref="TryActiveReload"/> で判定する。false なら <see cref="TryActiveReload"/> は常に false。</summary>
        public bool HasActiveReload => _weapon.HasActiveReload;

        /// <summary>今のリロードで <see cref="TryActiveReload"/> の判定を済ませたか。リロード中でなければ false。</summary>
        public bool HasJudgedActiveReload => _weapon.HasJudgedActiveReload;

        /// <summary>
        /// リロードの進み (経過時間 / リロードにかかる時間)。0〜1。リロード中でなければ 0。
        /// アクティブリロードに失敗すると、かかる時間が <see cref="WeaponSpec.ActiveReloadFailSeconds"/> だけ延びるので進みは下がる。
        /// </summary>
        public float ReloadProgress => _weapon.ReloadProgress;

        /// <summary>
        /// 今のリロードにかかる秒数。<see cref="WeaponSpec.ReloadSeconds"/> に、アクティブリロードに失敗していれば
        /// <see cref="WeaponSpec.ActiveReloadFailSeconds"/> を足した値。リロード中でなければ 0。
        /// </summary>
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

        /// <summary>
        /// 1 発撃つ。撃てたら弾倉を 1 減らして <see cref="ShotRejection.None"/>。
        /// リロード中なら <see cref="ShotRejection.Reloading"/>、弾倉が空なら <see cref="ShotRejection.OutOfAmmo"/> を返し、
        /// 空のときは予備弾があれば (<see cref="HasInfiniteReserve"/> なら常に) リロードを始める。
        /// </summary>
        public ShotRejection TryFire()
        {
            if (_weapon.IsReloading)
            {
                return ShotRejection.Reloading;
            }

            if (_weapon.AmmoInMagazine == 0)
            {
                StartReload(true);
                return ShotRejection.OutOfAmmo;
            }

            if (!_weapon.TryFire())
            {
                // 連射間隔は 0 なので、リロード中でも空でもなければ必ず撃てる
                throw new InvalidOperationException("the inner weapon refused a shot with ammo and no reload");
            }

            Changed?.Invoke();
            return ShotRejection.None;
        }

        /// <summary>
        /// リロードを始める。弾倉が満タンか、予備弾が無い (<see cref="HasInfiniteReserve"/> なら問わない) か、もうリロード中なら何もせず false。
        /// 補充は <see cref="Tick"/> で <see cref="WeaponSpec.ReloadSeconds"/> 経ったときに行う。
        /// </summary>
        public bool TryStartReload()
        {
            return StartReload(false);
        }

        /// <summary>
        /// リロード中にもう一度 Reload を押したときのアクティブリロード。今の <see cref="ReloadProgress"/> を
        /// <see cref="ActiveReloadJudge.Judge"/> で判定し、成功ならその場で補充してリロードを終え (<see cref="Reloaded"/> を呼ぶ)、
        /// 失敗なら残り時間に <see cref="WeaponSpec.ActiveReloadFailSeconds"/> を足す。どちらでも <see cref="Changed"/> を 1 回呼び、true を返す。
        /// <see cref="HasActiveReload"/> が false、リロード中でない、今のリロードで判定済みのときは何もせず false。
        /// </summary>
        /// <param name="result">判定の結果。false を返したときは <see cref="ActiveReloadResult.Fail"/> で、意味を持たない。</param>
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

        /// <summary>リロード中なら弾数を変えずにやめて <see cref="Changed"/> を呼ぶ。リロード中でなければ何もしない。</summary>
        public void CancelReload()
        {
            if (!_weapon.IsReloading)
            {
                return;
            }

            _weapon.CancelReload();
            Changed?.Invoke();
        }

        /// <summary>リロードを deltaSeconds 進める。0 以上の有限値。</summary>
        public void Tick(float deltaSeconds)
        {
            _weapon.Tick(deltaSeconds);
        }

        /// <summary>弾倉と予備弾を初期値 (満タン、予備弾は倍率を掛けた値) に戻し、リロード中ならやめる。リスポーンと試合の開始時に呼ぶ。</summary>
        public void Refill()
        {
            _weapon.Reloaded -= HandleReloaded;
            _weapon = CreateWeapon();
            Changed?.Invoke();
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
            return weapon;
        }

        private void HandleReloaded()
        {
            Reloaded?.Invoke();
            Changed?.Invoke();
        }
    }
}
