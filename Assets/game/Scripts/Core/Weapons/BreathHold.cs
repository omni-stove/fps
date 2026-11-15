using System;

namespace Fps.Core
{
    public sealed class BreathHold
    {
        public const float DefaultExhaustedSwayMultiplier = 2f;

        public const float DefaultSwayBlendSeconds = 0.3f;

        public const float DefaultSwayCycleSeconds = 4f;

        private bool _needsRelease;

        public BreathHold(
            BreathHoldSpec spec,
            float exhaustedSwayMultiplier = DefaultExhaustedSwayMultiplier,
            float swayBlendSeconds = DefaultSwayBlendSeconds)
        {
            if (!(exhaustedSwayMultiplier >= 1f) || float.IsInfinity(exhaustedSwayMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exhaustedSwayMultiplier), exhaustedSwayMultiplier, "exhaustedSwayMultiplier must be at least 1 and finite");
            }

            Spec = spec;
            ExhaustedSwayMultiplier = exhaustedSwayMultiplier;
            SwayBlendSeconds = SpecGuard.NonNegativeFinite(swayBlendSeconds, nameof(swayBlendSeconds));
            Reset();
        }

        public BreathHoldSpec Spec { get; }

        public float ExhaustedSwayMultiplier { get; }

        public float SwayBlendSeconds { get; }

        public float RemainingSeconds { get; private set; }

        public float RemainingFraction => Spec != null ? RemainingSeconds / Spec.MaxHoldSeconds : 0f;

        public bool IsHolding { get; private set; }

        public bool IsExhausted { get; private set; }

        public float TargetSwayMultiplier
        {
            get
            {
                if (IsHolding)
                {
                    return Spec.SwayMultiplier;
                }

                if (IsExhausted)
                {
                    return ExhaustedSwayMultiplier + ((1f - ExhaustedSwayMultiplier) * RemainingFraction);
                }

                return 1f;
            }
        }

        public float SwayMultiplier { get; private set; }

        public void Tick(bool wantsToHold, float deltaSeconds)
        {
            var left = SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!wantsToHold)
            {
                _needsRelease = false;
            }

            IsHolding = Spec != null && wantsToHold && !_needsRelease && !IsExhausted && RemainingSeconds > 0f;
            if (IsHolding)
            {
                var used = Math.Min(left, RemainingSeconds);
                RemainingSeconds -= used;
                left -= used;
                if (RemainingSeconds <= 0f)
                {
                    RemainingSeconds = 0f;
                    IsHolding = false;
                    IsExhausted = true;
                    _needsRelease = true;
                }
            }

            // 1 回の呼び出しの途中で使い切ったときも、残りの時間を捨てずに戻りへ回す (フレームの長さで回復が変わらないように)。
            if (!IsHolding)
            {
                Recover(left);
            }

            BlendSway(deltaSeconds);
        }

        // 押し続けたまま持ち替えたり生き返ったりしても、押し直すまで止めない。
        public void Reset()
        {
            RemainingSeconds = Spec != null ? Spec.MaxHoldSeconds : 0f;
            IsHolding = false;
            IsExhausted = false;
            _needsRelease = true;
            SwayMultiplier = 1f;
        }

        public static void SwayOffset(float seconds, float amplitudeDegrees, float cycleSeconds, out float pitchUpDegrees, out float yawDegrees)
        {
            SpecGuard.NonNegativeFinite(amplitudeDegrees, nameof(amplitudeDegrees));
            SpecGuard.PositiveFinite(cycleSeconds, nameof(cycleSeconds));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite");
            }

            var phase = 2.0 * Math.PI * (seconds / cycleSeconds);
            yawDegrees = (float)(amplitudeDegrees * Math.Sin(phase));
            pitchUpDegrees = (float)(0.5 * amplitudeDegrees * Math.Sin(2.0 * phase));
        }

        private void Recover(float seconds)
        {
            if (Spec == null)
            {
                return;
            }

            var max = Spec.MaxHoldSeconds;
            RemainingSeconds = Spec.RecoverSeconds <= 0f
                ? max
                : Math.Min(max, RemainingSeconds + (seconds * max / Spec.RecoverSeconds));
            if (RemainingSeconds >= max)
            {
                IsExhausted = false;
            }
        }

        private void BlendSway(float deltaSeconds)
        {
            var target = TargetSwayMultiplier;
            if (SwayBlendSeconds <= 0f)
            {
                SwayMultiplier = target;
                return;
            }

            // 倍率を即座に変えると揺れの途中で照準が跳ぶので寄せる。
            var keep = (float)Math.Exp(-deltaSeconds / SwayBlendSeconds);
            SwayMultiplier = target + ((SwayMultiplier - target) * keep);
        }
    }
}
