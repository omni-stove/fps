using System;

namespace Fps.Core
{
    /// <summary>
    /// キャンペーンが HUD に一時的に出す案内。<see cref="Notice"/> が <see cref="HudNotice.None"/> なら何も出さない。
    /// <list type="bullet">
    /// <item><see cref="ShowSaved"/> は「セーブしました」を指定の秒数だけ出し、<see cref="Tick"/> で時間が過ぎたら消す。</item>
    /// <item><see cref="ShowRestartCountdown"/> は「チェックポイントから再開まで N 秒」を出し、<see cref="Clear"/> まで消さない
    /// (残り秒数は呼ぶ側が毎フレーム渡し直す)。カウントダウン中は「セーブしました」で上書きしない。</item>
    /// </list>
    /// 案内が変わったときだけ <see cref="Changed"/> を呼ぶ (カウントダウンは表示の秒の桁が変わったときだけ)。
    /// </summary>
    public sealed class CampaignNotice
    {
        private float _savedRemaining;
        private bool _countingDown;

        /// <summary>案内が変わったときに、新しい案内を渡して呼ばれる。</summary>
        public event Action<HudNotice> Changed;

        /// <summary>今の案内。無ければ <see cref="HudNotice.None"/>。</summary>
        public HudNotice Notice { get; private set; }

        /// <summary>再開までのカウントダウンを出していれば true。</summary>
        public bool IsCountingDown => _countingDown;

        /// <summary>
        /// 「セーブしました」を seconds 秒だけ出す。出している途中なら残り時間を seconds に戻す。
        /// 再開までのカウントダウン中は何もしない。
        /// </summary>
        public void ShowSaved(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a positive finite number");
            }

            if (_countingDown)
            {
                return;
            }

            _savedRemaining = seconds;
            SetNotice(HudNotice.CheckpointSaved);
        }

        /// <summary>
        /// 「チェックポイントから再開まで N 秒」を出す。N は remainingSeconds を切り上げた整数
        /// (<see cref="HudFormat.DisplayedSeconds"/>)。<see cref="Clear"/> を呼ぶまで出し続ける。
        /// </summary>
        public void ShowRestartCountdown(float remainingSeconds)
        {
            if (float.IsNaN(remainingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds), remainingSeconds, "remainingSeconds must not be NaN");
            }

            _countingDown = true;
            _savedRemaining = 0f;
            SetNotice(HudNotice.CheckpointRestart(HudFormat.DisplayedSeconds(remainingSeconds)));
        }

        /// <summary>「セーブしました」の残り時間を deltaSeconds 進め、尽きたら消す。カウントダウンには影響しない。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_countingDown || _savedRemaining <= 0f)
            {
                return;
            }

            _savedRemaining -= deltaSeconds;
            if (_savedRemaining > 0f)
            {
                return;
            }

            _savedRemaining = 0f;
            SetNotice(HudNotice.None);
        }

        /// <summary>案内を消し、カウントダウンもやめる。</summary>
        public void Clear()
        {
            _countingDown = false;
            _savedRemaining = 0f;
            SetNotice(HudNotice.None);
        }

        private void SetNotice(HudNotice notice)
        {
            if (notice == Notice)
            {
                return;
            }

            Notice = notice;
            Changed?.Invoke(notice);
        }
    }
}
