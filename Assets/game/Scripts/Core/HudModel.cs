using System;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す文字列を持つ。値は <see cref="SetHealth"/>・<see cref="SetAmmo(int, int, bool, bool)"/>・<see cref="SetTime"/> などで直接渡すか、
    /// <see cref="Health"/>・<see cref="Weapon"/>・<see cref="Match"/>・<see cref="CampaignNotice"/> を Bind してそのイベントで受ける
    /// (Bind 系も中では Set 系を呼ぶ)。毎フレーム値を読みに行かず、値が来たときだけ書式を通し、表示が変わったときだけ
    /// <see cref="Changed"/> を呼ぶ (残り時間は秒の桁が変わったときだけ)。Bind で源を差し替えると前の源の購読は外し、今の値をすぐ反映する。
    /// null を渡すとその項目は仮の表示に戻る。
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

        /// <summary>表示の文字列が変わった項目を渡して呼ばれる。呼ばれた時点で文字列は更新済み。</summary>
        public event Action<HudField> Changed;

        /// <summary>左下の体力。</summary>
        public string HealthText { get; private set; } = HudFormat.NoHealth;

        /// <summary>体力が少なく、警告の色で出すべきか (<see cref="HudFormat.IsLowHealth"/>)。</summary>
        public bool IsHealthLow { get; private set; }

        /// <summary>右下の「弾倉 / 予備」。</summary>
        public string AmmoText { get; private set; } = HudFormat.NoAmmo;

        /// <summary>リロード中か。<see cref="AmmoText"/> にも出るが、View が見た目を変えるのに使う。</summary>
        public bool IsReloading { get; private set; }

        /// <summary>弾数の上に出す、持っている武器の表示名 (<see cref="WeaponSpec.DisplayName"/>)。武器が無ければ <see cref="HudFormat.NoWeaponName"/>。</summary>
        public string WeaponNameText { get; private set; } = HudFormat.NoWeaponName;

        /// <summary>上中央の残り時間 (mm:ss)。</summary>
        public string TimeText { get; private set; } = HudFormat.NoTime;

        /// <summary>画面上部に一時的に出す案内。空文字なら出さない。</summary>
        public string NoticeText { get; private set; } = HudFormat.NoNotice;

        /// <summary>先に出すチーム (自分のチーム)。点数が無ければ null。</summary>
        public Team? FirstScoreTeam { get; private set; }

        /// <summary>先に出すチームの「チーム名 点数」。点数が無ければ <see cref="HudFormat.NoScore"/>。</summary>
        public string FirstScoreText { get; private set; } = HudFormat.NoScore;

        /// <summary>後に出すチーム (相手のチーム)。点数が無ければ null。</summary>
        public Team? SecondScoreTeam => FirstScoreTeam.HasValue ? HudFormat.Opponent(FirstScoreTeam.Value) : (Team?)null;

        /// <summary>後に出すチームの「チーム名 点数」。点数が無ければ <see cref="HudFormat.NoScore"/>。</summary>
        public string SecondScoreText { get; private set; } = HudFormat.NoScore;

        /// <summary>試合結果。試合が終わっていなければ <see cref="HudFormat.NoResult"/>。</summary>
        public string ResultText { get; private set; } = HudFormat.NoResult;

        /// <summary>リロードの進捗 (0〜1)。出さないときは null。</summary>
        public float? ReloadProgress { get; private set; }

        /// <summary>アクティブリロードで成功になる区間の始め (進捗と同じ 0〜1)。<see cref="ReloadProgress"/> が null なら 0。</summary>
        public float ReloadWindowStart { get; private set; }

        /// <summary>アクティブリロードで成功になる区間の終わり (進捗と同じ 0〜1)。<see cref="ReloadProgress"/> が null なら 0。</summary>
        public float ReloadWindowEnd { get; private set; }

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
        /// 弾数と武器名の源を差し替える。弾数は射撃 (Fired) とリロード完了 (Reloaded) で更新し、武器名はここで
        /// <see cref="WeaponSpec.DisplayName"/> にする。null なら両方とも仮の表示に戻す。
        /// </summary>
        public void BindWeapon(Weapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.Fired -= OnWeaponChanged;
                _weapon.Reloaded -= OnWeaponChanged;
            }

            _weapon = weapon;
            if (_weapon != null)
            {
                _weapon.Fired += OnWeaponChanged;
                _weapon.Reloaded += OnWeaponChanged;
            }

            OnWeaponChanged();
            SetWeaponName(_weapon != null ? _weapon.Spec.DisplayName : HudFormat.NoWeaponName);
        }

        /// <summary>残り時間の源を差し替える。null なら仮の表示に戻す。</summary>
        public void BindMatch(Match match)
        {
            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
            }

            _match = match;
            if (_match != null)
            {
                _match.RemainingSecondsChanged += SetTime;
                SetTime(_match.RemainingSeconds);
            }
            else
            {
                ClearTime();
            }
        }

        /// <summary>案内の源を差し替える。null なら案内を消す。</summary>
        public void BindNotice(CampaignNotice notice)
        {
            if (_notice != null)
            {
                _notice.TextChanged -= SetNotice;
            }

            _notice = notice;
            if (_notice != null)
            {
                _notice.TextChanged += SetNotice;
            }

            SetNotice(_notice != null ? _notice.Text : HudFormat.NoNotice);
        }

        /// <summary>すべての源の購読を外す。表示の文字列はそのまま残す。</summary>
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
                _weapon = null;
            }

            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match = null;
            }

            if (_notice != null)
            {
                _notice.TextChanged -= SetNotice;
                _notice = null;
            }
        }

        /// <summary>体力を出す。max は警告の色にするかどうかの判断にだけ使う。</summary>
        /// <exception cref="ArgumentOutOfRangeException">current が負か、max が 1 未満。</exception>
        public void SetHealth(int current, int max)
        {
            ApplyHealth(HudFormat.Health(current), HudFormat.IsLowHealth(current, max));
        }

        /// <summary>体力を仮の表示に戻す。</summary>
        public void ClearHealth()
        {
            ApplyHealth(HudFormat.NoHealth, false);
        }

        /// <summary>弾倉と予備の弾数と、リロード中かどうかを出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">magazine か reserve が負。</exception>
        public void SetAmmo(int magazine, int reserve, bool reloading)
        {
            SetAmmo(magazine, reserve, reloading, false);
        }

        /// <summary>弾数を出す。infiniteReserve なら予備の数の代わりに <see cref="HudFormat.InfiniteReserve"/> を出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">magazine か reserve が負。</exception>
        public void SetAmmo(int magazine, int reserve, bool reloading, bool infiniteReserve)
        {
            ApplyAmmo(HudFormat.Ammo(magazine, reserve, reloading, infiniteReserve), reloading);
        }

        /// <summary>弾数を仮の表示に戻す。</summary>
        public void ClearAmmo()
        {
            ApplyAmmo(HudFormat.NoAmmo, false);
        }

        /// <summary>武器名を出す。null は <see cref="HudFormat.NoWeaponName"/> (出さない) として扱う。</summary>
        public void SetWeaponName(string displayName)
        {
            displayName ??= HudFormat.NoWeaponName;
            if (displayName == WeaponNameText)
            {
                return;
            }

            WeaponNameText = displayName;
            Changed?.Invoke(HudField.WeaponName);
        }

        /// <summary>武器名を消す。</summary>
        public void ClearWeaponName()
        {
            SetWeaponName(HudFormat.NoWeaponName);
        }

        /// <summary>残り時間 (秒) を出す。表示は秒の桁が変わったときだけ変わる (<see cref="HudFormat.Time"/>)。</summary>
        public void SetTime(float remainingSeconds)
        {
            ApplyTime(HudFormat.Time(remainingSeconds));
        }

        /// <summary>残り時間を仮の表示に戻す。</summary>
        public void ClearTime()
        {
            ApplyTime(HudFormat.NoTime);
        }

        /// <summary>一時的な案内を出す。null か空文字なら消す。</summary>
        public void SetNotice(string text)
        {
            text ??= HudFormat.NoNotice;
            if (text == NoticeText)
            {
                return;
            }

            NoticeText = text;
            Changed?.Invoke(HudField.Notice);
        }

        /// <summary>倒れている間の案内を、リスポーン方式に合わせて出す (<see cref="HudFormat.RespawnNotice"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">mode が定義済みの値でないか、None 以外で seconds が負。</exception>
        public void SetRespawnNotice(RespawnMode mode, int seconds)
        {
            SetNotice(HudFormat.RespawnNotice(mode, seconds));
        }

        /// <summary>チームの点数を、ownTeam を先にして出す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">点数が負か、ownTeam が Team の値でない。</exception>
        public void SetScores(Team ownTeam, int alphaScore, int bravoScore)
        {
            var opponent = HudFormat.Opponent(ownTeam);
            var first = HudFormat.TeamScore(ownTeam, ownTeam == Team.Alpha ? alphaScore : bravoScore);
            var second = HudFormat.TeamScore(opponent, opponent == Team.Alpha ? alphaScore : bravoScore);
            ApplyScores(ownTeam, first, second);
        }

        /// <summary>チームの点数を消す。</summary>
        public void ClearScores()
        {
            ApplyScores(null, HudFormat.NoScore, HudFormat.NoScore);
        }

        /// <summary>試合結果 (winner が null なら引き分け) を出す。</summary>
        public void SetResult(Team? winner)
        {
            ApplyResult(HudFormat.Result(winner));
        }

        /// <summary>試合結果を消す。</summary>
        public void ClearResult()
        {
            ApplyResult(HudFormat.NoResult);
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
            if (_weapon != null)
            {
                SetAmmo(_weapon.AmmoInMagazine, _weapon.ReserveAmmo, _weapon.IsReloading, _weapon.HasInfiniteReserve);
            }
            else
            {
                ClearAmmo();
            }

            // 次のフレームを待たずに、リロードが終わった・武器が替わった時点で進みを消すため。
            RefreshReloadProgress();
        }

        private void ApplyHealth(string text, bool low)
        {
            if (text == HealthText && low == IsHealthLow)
            {
                return;
            }

            HealthText = text;
            IsHealthLow = low;
            Changed?.Invoke(HudField.Health);
        }

        private void ApplyAmmo(string text, bool reloading)
        {
            if (text == AmmoText && reloading == IsReloading)
            {
                return;
            }

            AmmoText = text;
            IsReloading = reloading;
            Changed?.Invoke(HudField.Ammo);
        }

        private void ApplyTime(string text)
        {
            if (text == TimeText)
            {
                return;
            }

            TimeText = text;
            Changed?.Invoke(HudField.Time);
        }

        private void ApplyScores(Team? firstTeam, string first, string second)
        {
            if (firstTeam == FirstScoreTeam && first == FirstScoreText && second == SecondScoreText)
            {
                return;
            }

            FirstScoreTeam = firstTeam;
            FirstScoreText = first;
            SecondScoreText = second;
            Changed?.Invoke(HudField.Score);
        }

        private void ApplyResult(string text)
        {
            text ??= HudFormat.NoResult;
            if (text == ResultText)
            {
                return;
            }

            ResultText = text;
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

        private static bool IsFraction(float value)
        {
            return value >= 0f && value <= 1f;
        }
    }
}
