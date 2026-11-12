using System;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す文字列を持ち、<see cref="Health"/>・<see cref="Weapon"/>・<see cref="Match"/>・<see cref="CampaignNotice"/> のイベントで更新する。
    /// 毎フレーム値を読みに行かず、イベントが来たときだけ書式を通し、表示が変わったときだけ <see cref="Changed"/> を呼ぶ
    /// (残り時間は秒の桁が変わったときだけ)。Bind で源を差し替えると前の源の購読は外し、今の値をすぐ反映する。
    /// null を渡すとその項目は仮の表示に戻る。
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

        /// <summary>右下の「弾倉 / 予備」。</summary>
        public string AmmoText { get; private set; } = HudFormat.NoAmmo;

        /// <summary>上中央の残り時間 (mm:ss)。</summary>
        public string TimeText { get; private set; } = HudFormat.NoTime;

        /// <summary>画面上部に一時的に出す案内。空文字なら出さない。</summary>
        public string NoticeText { get; private set; } = HudFormat.NoNotice;

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

        /// <summary>弾数の源を差し替える。射撃 (Fired) とリロード完了 (Reloaded) で更新する。null なら仮の表示に戻す。</summary>
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
        }

        /// <summary>残り時間の源を差し替える。null なら仮の表示に戻す。</summary>
        public void BindMatch(Match match)
        {
            if (_match != null)
            {
                _match.RemainingSecondsChanged -= OnRemainingSecondsChanged;
            }

            _match = match;
            if (_match != null)
            {
                _match.RemainingSecondsChanged += OnRemainingSecondsChanged;
            }

            SetTime(_match != null ? HudFormat.Time(_match.RemainingSeconds) : HudFormat.NoTime);
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
                _match.RemainingSecondsChanged -= OnRemainingSecondsChanged;
                _match = null;
            }

            if (_notice != null)
            {
                _notice.TextChanged -= SetNotice;
                _notice = null;
            }
        }

        private void OnHealthChanged()
        {
            var text = _health != null ? HudFormat.Health(_health.Current) : HudFormat.NoHealth;
            if (text == HealthText)
            {
                return;
            }

            HealthText = text;
            Changed?.Invoke(HudField.Health);
        }

        private void OnWeaponChanged()
        {
            var text = _weapon != null ? HudFormat.Ammo(_weapon.AmmoInMagazine, _weapon.ReserveAmmo) : HudFormat.NoAmmo;
            if (text == AmmoText)
            {
                return;
            }

            AmmoText = text;
            Changed?.Invoke(HudField.Ammo);
        }

        private void OnRemainingSecondsChanged(float remainingSeconds)
        {
            SetTime(HudFormat.Time(remainingSeconds));
        }

        private void SetTime(string text)
        {
            if (text == TimeText)
            {
                return;
            }

            TimeText = text;
            Changed?.Invoke(HudField.Time);
        }

        private void SetNotice(string text)
        {
            text ??= HudFormat.NoNotice;
            if (text == NoticeText)
            {
                return;
            }

            NoticeText = text;
            Changed?.Invoke(HudField.Notice);
        }
    }
}
