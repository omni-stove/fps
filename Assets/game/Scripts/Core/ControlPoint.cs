using System;

namespace Shooter.Core
{
    public sealed class ControlPoint
    {
        // 小刻みな Tick を足し合わせると端や 0 にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        public event Action<Team> Captured;

        public event Action<Team> Neutralized;

        private double _control;

        public float CaptureSeconds { get; }

        public int MaxCapturers { get; }

        public Team? Owner { get; private set; }

        public float Control => (float)_control;

        public Team? LeaningTeam => _control > 0d ? Team.Alpha : _control < 0d ? Team.Bravo : (Team?)null;

        public bool IsContested { get; private set; }

        public ControlPoint(float captureSeconds, int maxCapturers = 1)
        {
            if (!(captureSeconds > 0f) || float.IsInfinity(captureSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(captureSeconds), captureSeconds, "captureSeconds must be positive and finite");
            }

            if (maxCapturers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapturers), maxCapturers, "maxCapturers must be at least 1");
            }

            CaptureSeconds = captureSeconds;
            MaxCapturers = maxCapturers;
        }

        public void Update(int alphaCount, int bravoCount, float deltaSeconds)
        {
            if (alphaCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaCount), alphaCount, "alphaCount must not be negative");
            }

            if (bravoCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoCount), bravoCount, "bravoCount must not be negative");
            }

            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            IsContested = alphaCount > 0 && bravoCount > 0;
            if (IsContested || deltaSeconds == 0f)
            {
                return;
            }

            double target;
            int capturers;
            if (alphaCount > 0)
            {
                target = 1d;
                capturers = alphaCount;
            }
            else if (bravoCount > 0)
            {
                target = -1d;
                capturers = bravoCount;
            }
            else
            {
                target = EndOf(Owner);
                capturers = 1;
            }

            var step = Math.Min(capturers, MaxCapturers) * (double)deltaSeconds / CaptureSeconds;
            _control = MoveToward(_control, target, step);
            ApplyOwnership();
        }

        public void Reset()
        {
            _control = 0d;
            Owner = null;
            IsContested = false;
        }

        private void ApplyOwnership()
        {
            if ((Owner == Team.Alpha && _control <= 0d) || (Owner == Team.Bravo && _control >= 0d))
            {
                var previous = Owner.Value;
                Owner = null;
                Neutralized?.Invoke(previous);
            }

            // ハンドラの中で Reset されていたら、ここで改めて占領させない。
            if (_control >= 1d && Owner != Team.Alpha)
            {
                Owner = Team.Alpha;
                Captured?.Invoke(Team.Alpha);
            }
            else if (_control <= -1d && Owner != Team.Bravo)
            {
                Owner = Team.Bravo;
                Captured?.Invoke(Team.Bravo);
            }
        }

        private static double EndOf(Team? team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return 1d;
                case Team.Bravo:
                    return -1d;
                default:
                    return 0d;
            }
        }

        private static double MoveToward(double current, double target, double step)
        {
            var next = current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
            if (Math.Abs(target - next) < _snap)
            {
                return target;
            }

            return Math.Abs(next) < _snap ? 0d : next;
        }
    }
}
