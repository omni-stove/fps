using System;

namespace Shooter.Core
{
    public enum LowHealthChange
    {
        None,

        Started,

        Recovered,

        Cleared,
    }

    public sealed class DamageFeedbackSettings
    {
        public static readonly DamageFeedbackSettings Default = new DamageFeedbackSettings(
            hitPeakIntensity: 0.8f,
            fullHitDamage: 50f,
            hitFadeSeconds: 0.8f,
            lowHealthThreshold: HudFormat.LowHealthRatio,
            lowHealthMinIntensity: 0.35f,
            lowHealthMaxIntensity: 0.7f);

        public DamageFeedbackSettings(
            float hitPeakIntensity,
            float fullHitDamage,
            float hitFadeSeconds,
            float lowHealthThreshold,
            float lowHealthMinIntensity,
            float lowHealthMaxIntensity)
        {
            if (!IsFinite(hitPeakIntensity) || hitPeakIntensity <= 0f || hitPeakIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(hitPeakIntensity), hitPeakIntensity, "must be in (0, 1]");
            }

            if (!IsFinite(fullHitDamage) || fullHitDamage <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullHitDamage), fullHitDamage, "must be a finite number > 0");
            }

            if (!IsFinite(hitFadeSeconds) || hitFadeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hitFadeSeconds), hitFadeSeconds, "must be a finite number > 0");
            }

            if (!IsFinite(lowHealthThreshold) || lowHealthThreshold <= 0f || lowHealthThreshold >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lowHealthThreshold), lowHealthThreshold, "must be in (0, 1)");
            }

            if (!IsFinite(lowHealthMinIntensity) || lowHealthMinIntensity < 0f || lowHealthMinIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lowHealthMinIntensity), lowHealthMinIntensity, "must be in [0, 1]");
            }

            if (!IsFinite(lowHealthMaxIntensity) || lowHealthMaxIntensity < lowHealthMinIntensity || lowHealthMaxIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lowHealthMaxIntensity), lowHealthMaxIntensity, "must be in [lowHealthMinIntensity, 1]");
            }

            HitPeakIntensity = hitPeakIntensity;
            FullHitDamage = fullHitDamage;
            HitFadeSeconds = hitFadeSeconds;
            LowHealthThreshold = lowHealthThreshold;
            LowHealthMinIntensity = lowHealthMinIntensity;
            LowHealthMaxIntensity = lowHealthMaxIntensity;
        }

        public float HitPeakIntensity { get; }

        public float FullHitDamage { get; }

        public float HitFadeSeconds { get; }

        public float LowHealthThreshold { get; }

        public float LowHealthMinIntensity { get; }

        public float LowHealthMaxIntensity { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class DamageFeedback
    {
        private float _hitStrength;
        private float _hitElapsed;
        private float? _healthFraction;

        public DamageFeedback(DamageFeedbackSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DamageFeedbackSettings Settings { get; }

        public bool IsLowHealth { get; private set; }

        public float? HealthFraction => _healthFraction;

        public float HitIntensity
        {
            get
            {
                if (_hitStrength <= 0f)
                {
                    return 0f;
                }

                var remaining = 1f - (_hitElapsed / Settings.HitFadeSeconds);
                return remaining > 0f ? _hitStrength * remaining : 0f;
            }
        }

        public float LowHealthIntensity
        {
            get
            {
                if (!IsLowHealth || !_healthFraction.HasValue)
                {
                    return 0f;
                }

                var depth = 1f - (_healthFraction.Value / Settings.LowHealthThreshold);
                depth = Math.Max(0f, Math.Min(1f, depth));
                return Settings.LowHealthMinIntensity + ((Settings.LowHealthMaxIntensity - Settings.LowHealthMinIntensity) * depth);
            }
        }

        public float Intensity => Math.Max(HitIntensity, LowHealthIntensity);

        // 薄くなりかけの前の被弾の方が強ければその強さで最初から薄くし、続けて撃たれたときに弱まらないようにする。
        public bool Hit(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var strength = Settings.HitPeakIntensity * Math.Min(1f, amount / Settings.FullHitDamage);
            _hitStrength = Math.Max(strength, HitIntensity);
            _hitElapsed = 0f;
            return true;
        }

        public LowHealthChange SetHealth(float? fraction)
        {
            if (fraction.HasValue && float.IsNaN(fraction.Value))
            {
                fraction = null;
            }

            _healthFraction = fraction.HasValue ? Math.Max(0f, Math.Min(1f, fraction.Value)) : (float?)null;
            var low = _healthFraction.HasValue && _healthFraction.Value > 0f && _healthFraction.Value <= Settings.LowHealthThreshold;
            if (low == IsLowHealth)
            {
                return LowHealthChange.None;
            }

            IsLowHealth = low;
            if (low)
            {
                return LowHealthChange.Started;
            }

            return _healthFraction.HasValue && _healthFraction.Value > 0f ? LowHealthChange.Recovered : LowHealthChange.Cleared;
        }

        public void Tick(float deltaSeconds)
        {
            if (_hitStrength <= 0f || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _hitElapsed += deltaSeconds;
            if (_hitElapsed >= Settings.HitFadeSeconds)
            {
                _hitStrength = 0f;
                _hitElapsed = 0f;
            }
        }

        public void Reset()
        {
            _hitStrength = 0f;
            _hitElapsed = 0f;
            _healthFraction = null;
            IsLowHealth = false;
        }
    }
}
