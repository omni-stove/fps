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

        /// <summary>リロードが完了し弾倉が補充されたときに呼ばれる。アクティブリロードに成功してその場で終えたときも呼ばれる。</summary>
        public event Action Reloaded;

        /// <summary><see cref="AddReserveAmmo"/> で予備弾が 1 発以上増えたときに呼ばれる。</summary>
        public event Action ReserveAmmoAdded;

        private float _cooldownRemaining;
        private float _reloadRemaining;
        private float _reloadElapsed;

        public WeaponSpec Spec { get; }
        public int AmmoInMagazine { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }

        /// <summary>
        /// true ならリロードで予備弾を減らさず、予備弾が 0 でもリロードできる。
        /// <see cref="ReserveAmmo"/> は表示用にそのままの値を残す。
        /// </summary>
        public bool HasInfiniteReserve { get; }

        /// <summary>
        /// true ならリロード中の <see cref="TryActiveReload"/> で判定する (<see cref="ActionRules.ActiveReload"/> が
        /// <see cref="ActiveReload.On"/>)。false なら <see cref="TryActiveReload"/> は常に false。
        /// </summary>
        public bool HasActiveReload { get; }

        /// <summary>今のリロードで <see cref="TryActiveReload"/> の判定を済ませたか。リロード中でなければ false。</summary>
        public bool HasJudgedActiveReload { get; private set; }

        /// <summary>
        /// リロードの進み (経過時間 / リロードにかかる時間)。0〜1。リロード中でなければ 0。
        /// アクティブリロードに失敗すると、かかる時間が <see cref="WeaponSpec.ActiveReloadFailSeconds"/> だけ延びるので進みは下がる。
        /// </summary>
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

        /// <param name="spec">武器の設定。開始時は弾倉満タン、予備弾は spec の初期値。予備弾は有限。</param>
        public Weapon(WeaponSpec spec)
            : this(spec, GameRules.Default)
        {
        }

        /// <param name="spec">武器の設定。開始時は弾倉満タン。</param>
        /// <param name="rules">
        /// 予備弾のルール。予備弾は <see cref="WeaponSpec.ReserveAmmo"/> × <see cref="GameRules.ReserveAmmoMultiplier"/>
        /// (<see cref="ScaleReserveAmmo"/>)、<see cref="GameRules.InfiniteReserve"/> は <see cref="HasInfiniteReserve"/> になる。
        /// </param>
        public Weapon(WeaponSpec spec, GameRules rules)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            AmmoInMagazine = spec.MagazineSize;
            ReserveAmmo = ScaleReserveAmmo(spec.ReserveAmmo, rules.ReserveAmmoMultiplier);
            MaxReserveAmmo = ReserveAmmo;
            HasInfiniteReserve = rules.InfiniteReserve;
            HasActiveReload = rules.Actions.ActiveReload == ActiveReload.On;
        }

        /// <summary>弾数を指定して作る。セーブデータから戻すときに使う。リロード中でも連射の待ち中でもない状態で始まる。</summary>
        /// <param name="spec">武器の設定。</param>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上 <see cref="WeaponSpec.MagazineSize"/> 以下。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
        public Weapon(WeaponSpec spec, int ammoInMagazine, int reserveAmmo)
            : this(spec, ammoInMagazine, reserveAmmo, GameRules.Default)
        {
        }

        /// <summary>
        /// 弾数とルールを指定して作る。セーブデータから戻すときに使う。<paramref name="reserveAmmo"/> はセーブの値をそのまま使い、
        /// <see cref="GameRules.ReserveAmmoMultiplier"/> は掛けない (作ったときに掛けた値がセーブされているため)。
        /// </summary>
        /// <param name="spec">武器の設定。</param>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上 <see cref="WeaponSpec.MagazineSize"/> 以下。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
        /// <param name="rules">
        /// 予備弾とアクティブリロードのルール。<see cref="GameRules.InfiniteReserve"/> と <see cref="GameRules.Actions"/> の
        /// <see cref="ActionRules.ActiveReload"/> だけを使う。
        /// </param>
        public Weapon(WeaponSpec spec, int ammoInMagazine, int reserveAmmo, GameRules rules)
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

        /// <summary>
        /// 予備弾を拾って持てる上限。作ったときの予備弾 (<see cref="WeaponSpec.ReserveAmmo"/> × <see cref="GameRules.ReserveAmmoMultiplier"/>) で、
        /// 弾数を指定して作ったときはそれと渡した予備弾の大きい方。撃ったりリロードしたりしても変わらない。
        /// </summary>
        public int MaxReserveAmmo { get; }

        /// <summary>予備弾をあと何発拾えるか。<see cref="HasInfiniteReserve"/> なら拾う意味が無いので 0。</summary>
        public int ReserveAmmoRoom => HasInfiniteReserve ? 0 : Math.Max(0, MaxReserveAmmo - ReserveAmmo);

        /// <summary>
        /// 予備弾を amount 足す。<see cref="MaxReserveAmmo"/> を超える分は捨て、実際に足した数を返す。
        /// <see cref="HasInfiniteReserve"/> なら何もせず 0。リロード中でも足せ、足した分はそのリロードの補充に使う。
        /// </summary>
        /// <param name="amount">足す数。0 以上。</param>
        public int AddReserveAmmo(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var added = Math.Min(amount, ReserveAmmoRoom);
            if (added > 0)
            {
                ReserveAmmo += added;
                ReserveAmmoAdded?.Invoke();
            }

            return added;
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
        /// 初期の予備弾 <paramref name="reserveAmmo"/> × <paramref name="multiplier"/> を整数にする。
        /// 端数は四捨五入 (0.5 は 0 から遠い側)、0 未満にはせず、int に収まらなければ <see cref="int.MaxValue"/>。
        /// <paramref name="reserveAmmo"/> か <paramref name="multiplier"/> が 0 なら倍率の大きさに関わらず 0。
        /// </summary>
        /// <param name="reserveAmmo">倍率を掛ける前の予備弾。0 以上。</param>
        /// <param name="multiplier">倍率。0 以上の有限値。</param>
        public static int ScaleReserveAmmo(int reserveAmmo, float multiplier)
        {
            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (!(multiplier >= 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be a non-negative finite number");
            }

            // 0 のときは積が 0 のままなので、下の decimal 変換 (倍率が巨大だと OverflowException) に進めない。
            if (reserveAmmo == 0 || multiplier == 0f)
            {
                return 0;
            }

            if ((double)reserveAmmo * multiplier >= int.MaxValue)
            {
                return int.MaxValue;
            }

            // decimal で掛けるのは、0.7f のような倍率が 2 進では 0.69999... になり、ちょうど .5 になる積が切り捨てられるのを避けるため。
            // float の最短往復表記 ("R") を経由するのは、(decimal)float の丸め桁数が実行環境で変わっても設定に書いた 0.7 のまま掛けるため。
            var decimalMultiplier = decimal.Parse(
                multiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
            var scaled = Math.Round(reserveAmmo * decimalMultiplier, MidpointRounding.AwayFromZero);
            return (int)Math.Min(Math.Max(scaled, 0m), int.MaxValue);
        }

        /// <summary>
        /// 弾倉が満タンでなく、予備弾があり (<see cref="HasInfiniteReserve"/> なら 0 でもよい)、リロード中でなければリロードを始めて true を返す。
        /// 補充は <see cref="Tick"/> でリロード時間が経過したときに行う。
        /// </summary>
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

        /// <summary>
        /// リロード中にもう一度 Reload を押したときのアクティブリロード。今の <see cref="ReloadProgress"/> を
        /// <see cref="ActiveReloadJudge.Judge"/> で判定し、成功ならその場で補充してリロードを終え (<see cref="Reloaded"/> を呼ぶ)、
        /// 失敗なら残り時間に <see cref="WeaponSpec.ActiveReloadFailSeconds"/> を足す。判定したら true。
        /// <see cref="HasActiveReload"/> が false、リロード中でない、今のリロードで判定済みのときは何もせず false。
        /// </summary>
        /// <param name="result">判定の結果。false を返したときは <see cref="ActiveReloadResult.Fail"/> で、意味を持たない。</param>
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

        /// <summary>リロード中ならやめる。弾数は変えない。リロード中でなければ何もしない。</summary>
        public void CancelReload()
        {
            IsReloading = false;
            _reloadRemaining = 0f;
            _reloadElapsed = 0f;
            HasJudgedActiveReload = false;
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
