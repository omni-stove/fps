using System;

namespace Fps.Core
{
    public sealed class AwarenessMeter
    {
        public const float Full = 1f;

        private float _unseenSeconds;

        public AwarenessMeter(AwarenessSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public event Action<AwarenessStage, AwarenessStage> StageChanged;

        public AwarenessSpec Spec { get; }

        public float Level { get; private set; }

        public AwarenessStage Stage { get; private set; }

        public bool IsSeeing { get; private set; }

        public static bool InView(AwarenessSpec spec, float sightRange, AwarenessSighting sighting)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(sightRange > 0f) || float.IsInfinity(sightRange))
            {
                throw new ArgumentOutOfRangeException(nameof(sightRange), sightRange, "sightRange must be positive and finite");
            }

            return sighting.HasLineOfSight
                && sighting.Distance <= sightRange
                && sighting.AngleDegrees <= spec.FieldOfViewDegrees / 2f;
        }

        public static float GainPerSecond(AwarenessSpec spec, float sightRange, AwarenessSighting sighting)
        {
            if (!InView(spec, sightRange, sighting))
            {
                return 0f;
            }

            var far = sightRange - spec.FullGainDistance;
            var distanceT = far > 0f ? Clamp01((sighting.Distance - spec.FullGainDistance) / far) : 0f;
            var distance = Lerp(1f, spec.FarGainFactor, distanceT);

            var angle = Lerp(1f, spec.PeripheralGainFactor, Clamp01(sighting.AngleDegrees / (spec.FieldOfViewDegrees / 2f)));
            var motion = Lerp(spec.StillGainFactor, 1f, Clamp01(sighting.Speed / spec.FullMotionSpeed));

            return spec.GainPerSecond * distance * angle * spec.PostureFactor(sighting.Posture) * sighting.LightFactor * motion;
        }

        // 交戦は量が EngagedHoldThreshold を下回るまで残す。満ちた量から少し減っただけで外すと、見失った直後にまた見えても交戦に戻るまでたまり直しになる。
        public static AwarenessStage StageFor(AwarenessSpec spec, float level, AwarenessStage current)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (level >= Full || (current == AwarenessStage.Engaged && level >= spec.EngagedHoldThreshold))
            {
                return AwarenessStage.Engaged;
            }

            if (level >= spec.AlertThreshold)
            {
                return AwarenessStage.Alerted;
            }

            return level >= spec.NoticeThreshold ? AwarenessStage.Noticed : AwarenessStage.Unaware;
        }

        public AwarenessStage Tick(float deltaSeconds, float sightRange, AwarenessSighting sighting)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            var gain = GainPerSecond(Spec, sightRange, sighting);
            IsSeeing = InView(Spec, sightRange, sighting);
            if (IsSeeing)
            {
                _unseenSeconds = 0f;
                SetLevel(Level + (gain * deltaSeconds));
                return Stage;
            }

            // 待ちを越えた分だけ減らす。tick の刻み方で減る量が変わらないように。
            var before = _unseenSeconds;
            _unseenSeconds += deltaSeconds;
            var decaying = _unseenSeconds - Math.Max(before, Spec.DecayDelaySeconds);
            if (decaying > 0f)
            {
                SetLevel(Level - (Spec.DecayPerSecond * decaying));
            }

            return Stage;
        }

        // 今より低い値では下げない。撃たれたり仲間から伝わったりしたときに、自分で見てたまった分を消さないように。
        public void Raise(float level)
        {
            if (!(level >= 0f) || level > Full)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be between 0 and 1");
            }

            _unseenSeconds = 0f;
            if (level > Level)
            {
                SetLevel(level);
            }
        }

        public void Reset()
        {
            _unseenSeconds = 0f;
            IsSeeing = false;
            SetLevel(0f);
        }

        private void SetLevel(float level)
        {
            Level = Clamp01(level);
            var next = StageFor(Spec, Level, Stage);
            if (next == Stage)
            {
                return;
            }

            var previous = Stage;
            Stage = next;
            StageChanged?.Invoke(previous, next);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
        }
    }
}
