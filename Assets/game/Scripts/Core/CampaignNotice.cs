using System;

namespace Shooter.Core
{
    public sealed class CampaignNotice
    {
        private float _savedRemaining;
        private bool _countingDown;

        public event Action<HudNotice> Changed;

        public HudNotice Notice { get; private set; }

        public bool IsCountingDown => _countingDown;

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
