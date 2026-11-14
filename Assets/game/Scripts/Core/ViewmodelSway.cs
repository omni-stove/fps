using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class ViewmodelSway
    {
        // 指数で近づけるだけだと 0 に届かず「静止時は揺れ 0」にならないので、これより小さい差は詰め切る。
        public const float RestEpsilon = 1e-5f;

        private const float TwoPi = 2f * MathF.PI;

        private ViewmodelSwaySettings _settings;
        private float _bobPhase;
        private float _bobWeight;
        private float _lagYaw;
        private float _lagPitch;
        private float _kick;
        private float _ads;

        public ViewmodelSway(ViewmodelSwaySettings settings = null)
        {
            _settings = settings ?? ViewmodelSwaySettings.Default;
        }

        public ViewmodelSwaySettings Settings
        {
            get => _settings;
            set => _settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        public float BobWeight => _bobWeight;

        public float Kicks => _kick;

        public bool IsAtRest => _bobWeight == 0f && _lagYaw == 0f && _lagPitch == 0f && _kick == 0f;

        public Vector3 Position
        {
            get
            {
                var sway = SwayScale;
                var bob = _bobWeight * sway;
                var x = MathF.Sin(_bobPhase) * _settings.BobHorizontalMeters * bob;

                // 上下は左右の倍の速さで沈む (一歩ごとに 1 回)。位相 0 で 0 から始め、沈む向きだけに振る。
                var y = (MathF.Cos(2f * _bobPhase) - 1f) * 0.5f * _settings.BobVerticalMeters * bob;
                var z = -_settings.KickBackMeters * _kick * KickScale;
                return new Vector3(x, y, z);
            }
        }

        public float PitchUpDegrees => (_lagPitch * SwayScale) + (_settings.KickPitchDegrees * _kick * KickScale);

        public float YawRightDegrees => _lagYaw * SwayScale;

        private float SwayScale => Lerp(1f, _settings.AdsSwayScale, _ads);

        private float KickScale => Lerp(1f, _settings.AdsKickScale, _ads);

        public void Tick(
            float deltaSeconds,
            float horizontalSpeed,
            float yawRightDegreesPerSecond,
            float pitchUpDegreesPerSecond,
            float adsProgress)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be a non-negative finite number");
            }

            RequireFinite(yawRightDegreesPerSecond, nameof(yawRightDegreesPerSecond));
            RequireFinite(pitchUpDegreesPerSecond, nameof(pitchUpDegreesPerSecond));
            RequireFinite(adsProgress, nameof(adsProgress));

            _ads = Math.Clamp(adsProgress, 0f, 1f);
            if (deltaSeconds == 0f)
            {
                return;
            }

            var s = _settings;
            _bobWeight = Approach(_bobWeight, Math.Min(1f, horizontalSpeed / s.BobReferenceSpeed), s.BobSharpness, deltaSeconds);
            _bobPhase = (_bobPhase + (TwoPi * s.BobCyclesPerMeter * horizontalSpeed * deltaSeconds)) % TwoPi;

            var maxLag = s.MaxLookLagDegrees;
            var yawTarget = Math.Clamp(-yawRightDegreesPerSecond * s.LookLagSeconds, -maxLag, maxLag);
            var pitchTarget = Math.Clamp(-pitchUpDegreesPerSecond * s.LookLagSeconds, -maxLag, maxLag);
            _lagYaw = Approach(_lagYaw, yawTarget, s.LookLagSharpness, deltaSeconds);
            _lagPitch = Approach(_lagPitch, pitchTarget, s.LookLagSharpness, deltaSeconds);

            _kick = Approach(_kick, 0f, s.KickReturnPerSecond, deltaSeconds);
        }

        public void Kick()
        {
            _kick = Math.Min(_settings.MaxKicks, _kick + 1f);
        }

        public void Reset()
        {
            _bobPhase = 0f;
            _bobWeight = 0f;
            _lagYaw = 0f;
            _lagPitch = 0f;
            _kick = 0f;
            _ads = 0f;
        }

        private static float Approach(float current, float target, float sharpness, float deltaSeconds)
        {
            var next = target + ((current - target) * MathF.Exp(-sharpness * deltaSeconds));
            return MathF.Abs(next - target) < RestEpsilon ? target : next;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite");
            }
        }
    }
}
