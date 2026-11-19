using System;
using System.Numerics;

namespace Shooter.Core
{
    // 振りの秒数と当たりの瞬間は MeleeSpec から受け取り、当たる瞬間に銃が最も前へ突き出るよう、振りかぶり・打ち込み・戻りの 3 段に分ける。
    public sealed class ViewmodelMeleeSwing
    {
        private ViewmodelSwaySettings _settings;
        private float _elapsed;
        private float _swingSeconds;
        private float _impactSeconds;
        private bool _active;
        private float _windup;
        private float _strike;

        public ViewmodelMeleeSwing(ViewmodelSwaySettings settings = null)
        {
            _settings = settings ?? ViewmodelSwaySettings.Default;
        }

        public ViewmodelSwaySettings Settings
        {
            get => _settings;
            set => _settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsActive => _active;

        public float Elapsed => _elapsed;

        public float WindupWeight => _windup;

        public float StrikeWeight => _strike;

        public Vector3 Position => new Vector3(0f, 0f, (_strike * _settings.MeleeThrustMeters) - (_windup * _settings.MeleeWindupBackMeters));

        public float YawRightDegrees => (_windup - _strike) * _settings.MeleeSweepDegrees * 0.5f;

        public float RollLeftDegrees => _strike * _settings.MeleeRollDegrees;

        public void Start(float swingSeconds, float impactSeconds)
        {
            if (!(swingSeconds > 0f) || float.IsInfinity(swingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(swingSeconds), swingSeconds, "swingSeconds must be a positive finite number");
            }

            if (!(impactSeconds >= 0f) || impactSeconds > swingSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(impactSeconds), impactSeconds, "impactSeconds must be within [0, swingSeconds]");
            }

            _swingSeconds = swingSeconds;
            _impactSeconds = impactSeconds;
            _elapsed = 0f;
            _active = true;
            Evaluate();
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_active)
            {
                return;
            }

            _elapsed += deltaSeconds;
            Evaluate();
        }

        public void Reset()
        {
            _active = false;
            _elapsed = 0f;
            _windup = 0f;
            _strike = 0f;
        }

        private void Evaluate()
        {
            if (_elapsed >= _swingSeconds)
            {
                Reset();
                return;
            }

            var windupEnd = _impactSeconds * _settings.MeleeWindupFraction;
            if (_elapsed < windupEnd)
            {
                _windup = Smooth(_elapsed / windupEnd);
                _strike = 0f;
            }
            else if (_elapsed < _impactSeconds)
            {
                var strike = Smooth((_elapsed - windupEnd) / (_impactSeconds - windupEnd));
                _windup = 1f - strike;
                _strike = strike;
            }
            else
            {
                _windup = 0f;
                _strike = 1f - Smooth((_elapsed - _impactSeconds) / (_swingSeconds - _impactSeconds));
            }
        }

        private static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - (2f * t));
        }
    }
}
