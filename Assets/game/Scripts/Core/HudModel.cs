using System;

namespace Shooter.Core
{
    /// <summary>
    /// HUD に出す値と照準の状態を持つ。値は <see cref="SetHealth"/>・<see cref="SetAmmo(int, int, bool, bool)"/>・<see cref="SetTime"/> などで直接渡すか、
    /// <see cref="Health"/>・<see cref="Weapon"/>・<see cref="Match"/>・<see cref="CampaignNotice"/> を Bind してそのイベントで受ける
    /// (Bind 系も中では Set 系を呼ぶ)。毎フレーム値を読みに行かず、値が来たときだけ持ち直し、値が変わったときだけ
    /// <see cref="Changed"/> を呼ぶ (残り時間は秒の桁が変わったときだけ)。Bind で源を差し替えると前の源の購読は外し、今の値をすぐ反映する。
    /// null を渡すとその項目は仮の表示に戻る。
    /// どの項目も文字列にせず数や型の付いた値で持ち、どう書くか (体力・弾数・残り時間の書式を含む) は HUD に任せる。
    /// <para>
    /// 同じ項目を Bind しながら Set 系でも書くと、次に来た方が表示に残る。ネットワークの値のように Core の型が無い源は
    /// Bind せずに Set 系だけで書く。
    /// </para>
    /// </summary>
    public sealed class HudModel
    {
        private Health _health;
        private Weapon _weapon;
        private Match _match;
        private CampaignNotice _notice;

        /// <summary>値が変わった項目を渡して呼ばれる。呼ばれた時点で値は更新済み。</summary>
        public event Action<HudField> Changed;

        /// <summary>左下の今の体力。体力の源が無ければ null。</summary>
        public int? CurrentHealth { get; private set; }

        /// <summary>最大体力。<see cref="CurrentHealth"/> が null なら null。</summary>
        public int? MaxHealth { get; private set; }

        /// <summary>体力が少なく、警告の色で出すべきか (<see cref="HudFormat.IsLowHealth"/>)。</summary>
        public bool IsHealthLow => CurrentHealth.HasValue && HudFormat.IsLowHealth(CurrentHealth.Value, MaxHealth.Value);

        /// <summary>最大体力に対する今の体力の割合 ([0, 1])。体力が無い (仮の表示) なら null。</summary>
        public float? HealthFraction => CurrentHealth.HasValue ? Math.Min(1f, (float)CurrentHealth.Value / MaxHealth.Value) : (float?)null;

        /// <summary>右下の弾倉の弾数。武器が無いか、熱で撃つ武器なら null。</summary>
        public int? MagazineAmmo { get; private set; }

        /// <summary>予備の弾数。<see cref="MagazineAmmo"/> が null なら null。<see cref="IsReserveInfinite"/> なら減らないので数としては出さない。</summary>
        public int? ReserveAmmo { get; private set; }

        /// <summary>予備弾が無限 (<see cref="GameRules.InfiniteReserve"/>) か。<see cref="MagazineAmmo"/> が null なら false。</summary>
        public bool IsReserveInfinite { get; private set; }

        /// <summary>弾数に予備の数を並べるか (<see cref="HudVisibility.ReserveAmmo"/>)。<see cref="SetReserveAmmoShown"/> で変える。</summary>
        public bool ShowsReserveAmmo { get; private set; } = true;

        /// <summary>リロード中か。<see cref="MagazineAmmo"/> が null なら false。</summary>
        public bool IsReloading { get; private set; }

        /// <summary>弾数の上に名前を出す、持っている武器。武器が無ければ null。</summary>
        public WeaponSpec WeaponSpec { get; private set; }

        /// <summary>上中央の残り時間を、表示する整数の秒数 (<see cref="HudFormat.DisplayedSeconds"/>) で持つ。試合が無ければ null。</summary>
        public int? RemainingSeconds { get; private set; }

        /// <summary>残り時間を出しているか。<see cref="SetTime"/> で true、<see cref="ClearTime"/> と制限時間の無い試合で false。</summary>
        public bool HasTime => RemainingSeconds.HasValue;

        /// <summary>画面上部に一時的に出す案内。<see cref="HudNotice.None"/> なら出さない。</summary>
        public HudNotice Notice { get; private set; }

        /// <summary>先に出すチーム (自分のチーム)。点数が無ければ null。</summary>
        public Team? FirstScoreTeam { get; private set; }

        /// <summary>先に出すチームの点数。点数が無ければ null。</summary>
        public int? FirstScore { get; private set; }

        /// <summary>後に出すチーム (相手のチーム)。点数が無ければ null。</summary>
        public Team? SecondScoreTeam => FirstScoreTeam.HasValue ? HudFormat.Opponent(FirstScoreTeam.Value) : (Team?)null;

        /// <summary>後に出すチームの点数。点数が無ければ null。</summary>
        public int? SecondScore { get; private set; }

        /// <summary>試合が終わって結果を出しているか。</summary>
        public bool HasResult { get; private set; }

        /// <summary>勝ったチーム。結果が無いか引き分けなら null。</summary>
        public Team? Winner { get; private set; }

        /// <summary>Bind したソロの試合 (<see cref="Match"/>) の勝敗。終わっていないか Bind していなければ null。</summary>
        public MatchResult? SoloResult { get; private set; }

        /// <summary>ソロの試合の結果の下に出す、やり直すキーの表記。空なら案内を出さない。</summary>
        public string RestartKey { get; private set; } = string.Empty;

        /// <summary>リロードの進捗 (0〜1)。出さないときは null。</summary>
        public float? ReloadProgress { get; private set; }

        /// <summary>アクティブリロードで成功になる区間の始め (進捗と同じ 0〜1)。<see cref="ReloadProgress"/> が null なら 0。</summary>
        public float ReloadWindowStart { get; private set; }

        /// <summary>アクティブリロードで成功になる区間の終わり (進捗と同じ 0〜1)。<see cref="ReloadProgress"/> が null なら 0。</summary>
        public float ReloadWindowEnd { get; private set; }

        /// <summary>照準の広がり (0〜1)。0 で線が中央に最も寄り、1 で最も離れる。<see cref="CrosshairConeGap"/> の外へ足す。</summary>
        public float CrosshairSpread { get; private set; }

        /// <summary>弾が実際に散る円錐の縁が画面の中央から離れる距離 (HUD の基準解像度の px、0 以上)。照準の線はここより内へ寄らない。</summary>
        public float CrosshairConeGap { get; private set; }

        /// <summary>照準を出すか。一人称の ADS 中とダッシュ中は false。</summary>
        public bool IsCrosshairVisible { get; private set; } = true;

        /// <summary>目から狙う点までの線が、狙う点より手前で物に遮られているか。</summary>
        public bool IsAimBlocked { get; private set; }

        /// <summary>
        /// 遮られた点の画面上の位置 (ビューポート座標。x は左 0〜右 1、y は下 0〜上 1)。
        /// 遮られていないか、遮られた点がカメラの後ろにあるときは null。
        /// </summary>
        public System.Numerics.Vector2? AimBlockPoint { get; private set; }

        public float? ChargeFraction { get; private set; }

        public float? HeatFraction { get; private set; }

        public AmmoTypeSpec AmmoType { get; private set; }

        public float? Magnification { get; private set; }

        public float? LockOnProgress { get; private set; }

        public bool IsLockedOn => LockOnProgress == 1f;

        /// <summary>体力の源を差し替える。null なら仮の表示に戻す。</summary>
        public void BindHealth(Health health)
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
            }

            _health = health;
            if (_health != null)
            {
                _health.Changed += OnHealthChanged;
            }

            OnHealthChanged();
        }

        /// <summary>
        /// 弾数と武器名の源を差し替える。弾数は射撃 (Fired)・リロード完了 (Reloaded)・予備弾の補充 (ReserveAmmoAdded)・持ち物の弾薬の増減 (ReserveAmmoChanged)・弾薬の種類の切り替え (AmmoTypeSwitched) で更新し、武器名は
        /// ここで <see cref="WeaponSpec"/> を差し替える。null なら両方とも仮の表示に戻す。
        /// </summary>
        public void BindWeapon(Weapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.Fired -= OnWeaponChanged;
                _weapon.Reloaded -= OnWeaponChanged;
                _weapon.ReserveAmmoAdded -= OnWeaponChanged;
                _weapon.ReserveAmmoChanged -= OnWeaponChanged;
                _weapon.AmmoTypeSwitched -= OnWeaponChanged;
            }

            _weapon = weapon;
            if (_weapon != null)
            {
                _weapon.Fired += OnWeaponChanged;
                _weapon.Reloaded += OnWeaponChanged;
                _weapon.ReserveAmmoAdded += OnWeaponChanged;
                _weapon.ReserveAmmoChanged += OnWeaponChanged;
                _weapon.AmmoTypeSwitched += OnWeaponChanged;
            }

            OnWeaponChanged();
            SetWeaponSpec(_weapon?.Spec);
        }

        /// <summary>
        /// 残り時間と勝敗の源を差し替える。null なら残り時間を仮の表示に戻して勝敗を消す。
        /// 制限時間の無い試合 (<see cref="Match.HasTimeLimit"/> が false) では残り時間を出さない。
        /// </summary>
        public void BindMatch(Match match)
        {
            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
            }

            _match = match;
            if (_match != null)
            {
                _match.RemainingSecondsChanged += SetTime;
                _match.StateChanged += OnMatchStateChanged;
                if (_match.HasTimeLimit)
                {
                    SetTime(_match.RemainingSeconds);
                }
                else
                {
                    ClearTime();
                }
            }
            else
            {
                ClearTime();
            }

            ApplySoloResult(_match?.Result, RestartKey);
        }

        /// <summary>ソロの試合の結果の下に出す、やり直すキーの表記を変える。null か空なら案内を出さない。</summary>
        public void SetRestartKey(string key)
        {
            ApplySoloResult(SoloResult, key ?? string.Empty);
        }

        /// <summary>案内の源を差し替える。null なら案内を消す。</summary>
        public void BindNotice(CampaignNotice notice)
        {
            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
            }

            _notice = notice;
            if (_notice != null)
            {
                _notice.Changed += SetNotice;
            }

            SetNotice(_notice != null ? _notice.Notice : HudNotice.None);
        }

        /// <summary>すべての源の購読を外す。表示の値はそのまま残す。</summary>
        public void UnbindAll()
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
                _health = null;
            }

            if (_weapon != null)
            {
                _weapon.Fired -= OnWeaponChanged;
                _weapon.Reloaded -= OnWeaponChanged;
                _weapon.ReserveAmmoAdded -= OnWeaponChanged;
                _weapon.ReserveAmmoChanged -= OnWeaponChanged;
                _weapon.AmmoTypeSwitched -= OnWeaponChanged;
                _weapon = null;
            }

            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
                _match = null;
            }

            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
                _notice = null;
            }
        }

        /// <summary>今の体力と最大体力を出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">current が負か、max が 1 未満。</exception>
        public void SetHealth(int current, int max)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            ApplyHealth(current, max);
        }

        /// <summary>体力を仮の表示に戻す。</summary>
        public void ClearHealth()
        {
            ApplyHealth(null, null);
        }

        /// <summary>弾倉と予備の弾数と、リロード中かどうかを出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">magazine か reserve が負。</exception>
        public void SetAmmo(int magazine, int reserve, bool reloading)
        {
            SetAmmo(magazine, reserve, reloading, false);
        }

        /// <summary>弾倉と予備の弾数、リロード中か、予備弾が無限かを出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">magazine か reserve が負。</exception>
        public void SetAmmo(int magazine, int reserve, bool reloading, bool infiniteReserve)
        {
            if (magazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            if (reserve < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            ApplyAmmo(magazine, reserve, reloading, infiniteReserve);
        }

        /// <summary>弾数を仮の表示に戻す。</summary>
        public void ClearAmmo()
        {
            ApplyAmmo(null, null, false, false);
        }

        /// <summary>
        /// 弾数に予備の数を並べるかを決める。変わったら <see cref="HudField.Ammo"/> を知らせる。
        /// HUD の量 (<see cref="HudVisibility.ReserveAmmo"/>) に合わせて HUD が呼ぶ。
        /// </summary>
        public void SetReserveAmmoShown(bool shown)
        {
            if (shown == ShowsReserveAmmo)
            {
                return;
            }

            ShowsReserveAmmo = shown;
            Changed?.Invoke(HudField.Ammo);
        }

        /// <summary>名前を出す武器を決める。null なら出さない。</summary>
        public void SetWeaponSpec(WeaponSpec spec)
        {
            // 同じ武器を Weapon ごと作り直して Bind し直しても、名前が変わらなければ欄を描き直させない。
            var same = spec == null ? WeaponSpec == null : WeaponSpec != null && spec.Id == WeaponSpec.Id && string.Equals(spec.NameKey, WeaponSpec.NameKey, StringComparison.Ordinal);
            WeaponSpec = spec;
            if (!same)
            {
                Changed?.Invoke(HudField.WeaponName);
            }
        }

        /// <summary>武器名を消す。</summary>
        public void ClearWeaponSpec()
        {
            SetWeaponSpec(null);
        }

        /// <summary>残り時間 (秒) を出す。値は秒の桁が変わったときだけ変わる (<see cref="HudFormat.DisplayedSeconds"/>)。</summary>
        public void SetTime(float remainingSeconds)
        {
            ApplyTime(HudFormat.DisplayedSeconds(remainingSeconds));
        }

        /// <summary>残り時間を仮の表示に戻し、出さないようにする。</summary>
        public void ClearTime()
        {
            ApplyTime(null);
        }

        /// <summary>一時的な案内を出す。<see cref="HudNotice.None"/> なら消す。</summary>
        public void SetNotice(HudNotice notice)
        {
            if (notice == Notice)
            {
                return;
            }

            Notice = notice;
            Changed?.Invoke(HudField.Notice);
        }

        /// <summary>案内を消す。</summary>
        public void ClearNotice()
        {
            SetNotice(HudNotice.None);
        }

        /// <summary>倒れている間の案内を、リスポーン方式に合わせて出す (<see cref="HudNotice.ForRespawn"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">mode が定義済みの値でないか、None 以外で seconds が負。</exception>
        public void SetRespawnNotice(RespawnMode mode, int seconds)
        {
            SetNotice(HudNotice.ForRespawn(mode, seconds));
        }

        /// <summary>チームの点数を、ownTeam を先にして出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">点数が負か、ownTeam が Team の値でない。</exception>
        public void SetScores(Team ownTeam, int alphaScore, int bravoScore)
        {
            var opponent = HudFormat.Opponent(ownTeam);
            if (alphaScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaScore), alphaScore, "score must not be negative");
            }

            if (bravoScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoScore), bravoScore, "score must not be negative");
            }

            ApplyScores(ownTeam, ownTeam == Team.Alpha ? alphaScore : bravoScore, opponent == Team.Alpha ? alphaScore : bravoScore);
        }

        /// <summary>チームの点数を消す。</summary>
        public void ClearScores()
        {
            ApplyScores(null, null, null);
        }

        /// <summary>試合結果 (winner が null なら引き分け) を出す。</summary>
        public void SetResult(Team? winner)
        {
            ApplyResult(true, winner);
        }

        /// <summary>試合結果を消す。</summary>
        public void ClearResult()
        {
            ApplyResult(false, null);
        }

        /// <summary>リロードの進捗と、成功になる区間を出す。どれも 0〜1 で、windowStart は windowEnd 以下。</summary>
        /// <exception cref="ArgumentOutOfRangeException">値が 0〜1 の外か NaN、または windowStart が windowEnd より大きい。</exception>
        public void SetReloadProgress(float progress, float windowStart, float windowEnd)
        {
            if (!IsFraction(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be between 0 and 1");
            }

            if (!IsFraction(windowStart))
            {
                throw new ArgumentOutOfRangeException(nameof(windowStart), windowStart, "windowStart must be between 0 and 1");
            }

            if (!IsFraction(windowEnd))
            {
                throw new ArgumentOutOfRangeException(nameof(windowEnd), windowEnd, "windowEnd must be between 0 and 1");
            }

            if (windowStart > windowEnd)
            {
                throw new ArgumentOutOfRangeException(nameof(windowStart), windowStart, "windowStart must not exceed windowEnd");
            }

            ApplyReloadProgress(progress, windowStart, windowEnd);
        }

        /// <summary>リロードの進捗を消す。</summary>
        public void ClearReloadProgress()
        {
            ApplyReloadProgress(null, 0f, 0f);
        }

        /// <summary>照準の広がりと、出すかどうかを決める。散らばりの円錐の縁 (<see cref="CrosshairConeGap"/>) は 0 にする。</summary>
        /// <exception cref="ArgumentOutOfRangeException">spread が 0〜1 の外か NaN。</exception>
        public void SetCrosshair(float spread, bool visible)
        {
            SetCrosshair(spread, 0f, visible);
        }

        /// <summary>
        /// 照準の広がり、散らばりの円錐の縁までの距離 (基準解像度の px)、出すかどうかを決める。広がりも縁も時間で変わるので HUD が毎フレーム呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">spread が 0〜1 の外か NaN、coneGap が負か有限でない。</exception>
        public void SetCrosshair(float spread, float coneGap, bool visible)
        {
            if (!IsFraction(spread))
            {
                throw new ArgumentOutOfRangeException(nameof(spread), spread, "spread must be between 0 and 1");
            }

            if (float.IsNaN(coneGap) || float.IsInfinity(coneGap) || coneGap < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(coneGap), coneGap, "coneGap must be finite and not negative");
            }

            if (spread == CrosshairSpread && coneGap == CrosshairConeGap && visible == IsCrosshairVisible)
            {
                return;
            }

            CrosshairSpread = spread;
            CrosshairConeGap = coneGap;
            IsCrosshairVisible = visible;
            Changed?.Invoke(HudField.Crosshair);
        }

        /// <summary>照準を広がり 0 で出す状態に戻す。</summary>
        public void ResetCrosshair()
        {
            SetCrosshair(0f, true);
        }

        /// <summary>
        /// 目からの線が遮られている状態にし、遮られた点の画面上の位置 (ビューポート座標) を決める。点が画面に写らなければ null。
        /// 狙う向きは毎フレーム変わるので HUD が毎フレーム呼び、状態か位置が変わったときだけ <see cref="HudField.AimBlock"/> を知らせる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">viewport の成分が有限でない。</exception>
        public void SetAimBlock(System.Numerics.Vector2? viewport)
        {
            if (viewport.HasValue && !IsFinite(viewport.Value.X, viewport.Value.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(viewport), viewport, "viewport must be finite");
            }

            ApplyAimBlock(true, viewport);
        }

        /// <summary>遮られていない状態に戻す。</summary>
        public void ClearAimBlock()
        {
            ApplyAimBlock(false, null);
        }

        private void ApplyAimBlock(bool blocked, System.Numerics.Vector2? viewport)
        {
            if (blocked == IsAimBlocked && viewport == AimBlockPoint)
            {
                return;
            }

            IsAimBlocked = blocked;
            AimBlockPoint = viewport;
            Changed?.Invoke(HudField.AimBlock);
        }

        private static bool IsFinite(float x, float y)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);
        }

        /// <summary>
        /// Bind した武器の今の <see cref="Weapon.ReloadProgress"/> を読み、アクティブリロードを使う武器 (<see cref="Weapon.HasActiveReload"/>) が
        /// リロード中なら <see cref="SetReloadProgress"/>、そうでなければ <see cref="ClearReloadProgress"/> を呼ぶ。
        /// 進みは時間で変わりイベントが無いので、HUD が毎フレーム呼ぶ。判定を済ませた後は判定の幅を 0 (始め = 終わり = 0) にして出さない。
        /// </summary>
        public void RefreshReloadProgress()
        {
            if (_weapon == null || !_weapon.HasActiveReload || !_weapon.IsReloading)
            {
                ClearReloadProgress();
                return;
            }

            if (_weapon.HasJudgedActiveReload)
            {
                // 失敗で時間が延びた後は、幅の位置が進みの尺と合わなくなるため出さない。
                SetReloadProgress(_weapon.ReloadProgress, 0f, 0f);
                return;
            }

            var spec = _weapon.Spec;
            SetReloadProgress(
                _weapon.ReloadProgress,
                spec.ActiveReloadWindowStart,
                spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength);
        }

        public void SetCharge(float fraction)
        {
            ApplyGauge(HudField.Charge, Fraction(fraction, nameof(fraction)));
        }

        public void ClearCharge()
        {
            ApplyGauge(HudField.Charge, null);
        }

        public void SetHeat(float fraction)
        {
            ApplyGauge(HudField.Heat, Fraction(fraction, nameof(fraction)));
        }

        public void ClearHeat()
        {
            ApplyGauge(HudField.Heat, null);
        }

        public void SetAmmoType(AmmoTypeSpec ammoType)
        {
            var same = ammoType == null ? AmmoType == null : AmmoType != null && ammoType.Id == AmmoType.Id && string.Equals(ammoType.NameKey, AmmoType.NameKey, StringComparison.Ordinal);
            AmmoType = ammoType;
            if (!same)
            {
                Changed?.Invoke(HudField.AmmoType);
            }
        }

        public void ClearAmmoType()
        {
            SetAmmoType(null);
        }

        public void SetMagnification(float magnification)
        {
            if (!(magnification > 0f) || float.IsInfinity(magnification))
            {
                throw new ArgumentOutOfRangeException(nameof(magnification), magnification, "magnification must be positive and finite");
            }

            ApplyGauge(HudField.Magnification, magnification);
        }

        public void ClearMagnification()
        {
            ApplyGauge(HudField.Magnification, null);
        }

        public void SetLockOn(float progress)
        {
            ApplyGauge(HudField.LockOn, Fraction(progress, nameof(progress)));
        }

        public void ClearLockOn()
        {
            ApplyGauge(HudField.LockOn, null);
        }

        // 溜め・熱・弾薬の種類は時間や入力で変わり Weapon にイベントが無いので、リロードの進みと同じく HUD が毎フレーム呼ぶ。
        public void RefreshWeaponGauges()
        {
            if (_weapon == null)
            {
                ClearCharge();
                ClearHeat();
                ClearAmmoType();
                return;
            }

            // 溜めの無い撃ち方も ChargeFraction は 0 を返すので、Spec に溜めがあるかで出すかを決める。
            if (_weapon.Spec.Trigger.Charge != null)
            {
                SetCharge(_weapon.Trigger.ChargeFraction);
            }
            else
            {
                ClearCharge();
            }

            var heat = _weapon.Ammo.HeatFraction;
            if (heat.HasValue)
            {
                SetHeat(heat.Value);
            }
            else
            {
                ClearHeat();
            }

            SetAmmoType(_weapon.AmmoTypes.Current);
        }

        private void OnHealthChanged()
        {
            if (_health != null)
            {
                SetHealth(_health.Current, _health.Max);
            }
            else
            {
                ClearHealth();
            }
        }

        private void OnWeaponChanged()
        {
            // 熱で撃つ武器は弾倉を減らさないので、弾数を出すと撃っても変わらない数が並ぶ。撃てるかは熱のゲージで見せる。
            if (_weapon != null && !_weapon.Ammo.HeatFraction.HasValue)
            {
                SetAmmo(_weapon.AmmoInMagazine, _weapon.ReserveAmmo, _weapon.IsReloading, _weapon.HasInfiniteReserve);
            }
            else
            {
                ClearAmmo();
            }

            // 次のフレームを待たずに、リロードが終わった・武器が替わった時点で進みを消すため。
            RefreshReloadProgress();
            RefreshWeaponGauges();
        }

        private void ApplyHealth(int? current, int? max)
        {
            if (current == CurrentHealth && max == MaxHealth)
            {
                return;
            }

            CurrentHealth = current;
            MaxHealth = max;
            Changed?.Invoke(HudField.Health);
        }

        private void ApplyAmmo(int? magazine, int? reserve, bool reloading, bool infiniteReserve)
        {
            if (magazine == MagazineAmmo && reserve == ReserveAmmo && reloading == IsReloading && infiniteReserve == IsReserveInfinite)
            {
                return;
            }

            MagazineAmmo = magazine;
            ReserveAmmo = reserve;
            IsReloading = reloading;
            IsReserveInfinite = infiniteReserve;
            Changed?.Invoke(HudField.Ammo);
        }

        private void ApplyTime(int? seconds)
        {
            if (seconds == RemainingSeconds)
            {
                return;
            }

            RemainingSeconds = seconds;
            Changed?.Invoke(HudField.Time);
        }

        private void OnMatchStateChanged(GameModeState state)
        {
            ApplySoloResult(_match?.Result, RestartKey);
        }

        private void ApplySoloResult(MatchResult? result, string restartKey)
        {
            if (result == SoloResult && restartKey == RestartKey)
            {
                return;
            }

            SoloResult = result;
            RestartKey = restartKey;
            Changed?.Invoke(HudField.Result);
        }

        private void ApplyScores(Team? firstTeam, int? first, int? second)
        {
            if (firstTeam == FirstScoreTeam && first == FirstScore && second == SecondScore)
            {
                return;
            }

            FirstScoreTeam = firstTeam;
            FirstScore = first;
            SecondScore = second;
            Changed?.Invoke(HudField.Score);
        }

        private void ApplyResult(bool hasResult, Team? winner)
        {
            if (hasResult == HasResult && winner == Winner)
            {
                return;
            }

            HasResult = hasResult;
            Winner = winner;
            Changed?.Invoke(HudField.Result);
        }

        private void ApplyReloadProgress(float? progress, float windowStart, float windowEnd)
        {
            if (progress == ReloadProgress && windowStart == ReloadWindowStart && windowEnd == ReloadWindowEnd)
            {
                return;
            }

            ReloadProgress = progress;
            ReloadWindowStart = windowStart;
            ReloadWindowEnd = windowEnd;
            Changed?.Invoke(HudField.ReloadProgress);
        }

        private void ApplyGauge(HudField field, float? value)
        {
            switch (field)
            {
                case HudField.Charge when value != ChargeFraction:
                    ChargeFraction = value;
                    break;
                case HudField.Heat when value != HeatFraction:
                    HeatFraction = value;
                    break;
                case HudField.Magnification when value != Magnification:
                    Magnification = value;
                    break;
                case HudField.LockOn when value != LockOnProgress:
                    LockOnProgress = value;
                    break;
                default:
                    return;
            }

            Changed?.Invoke(field);
        }

        private static float Fraction(float value, string paramName)
        {
            if (!IsFraction(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be between 0 and 1");
            }

            return value;
        }

        private static bool IsFraction(float value)
        {
            return value >= 0f && value <= 1f;
        }
    }
}
