using System;

namespace Shooter.Core
{
    public sealed class AdsZoom
    {
        public const float DefaultFieldOfView = 50f;

        public const float DefaultTransitionSeconds = 0.15f;

        // 0° と 180° では透視投影ができないので、MinFieldOfView と MaxFieldOfView の値そのものは受け付けない。
        public const float MinFieldOfView = 0f;

        public const float MaxFieldOfView = 180f;

        private float _transitionSeconds;

        public AdsZoom(float transitionSeconds = DefaultTransitionSeconds)
        {
            _transitionSeconds = ValidateTransitionSeconds(transitionSeconds, nameof(transitionSeconds));
        }

        public float TransitionSeconds
        {
            get => _transitionSeconds;
            set => _transitionSeconds = ValidateTransitionSeconds(value, nameof(value));
        }

        public static float ValidateTransitionSeconds(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "transitionSeconds must be a non-negative finite number");
            }

            return value;
        }

        public bool IsAiming { get; private set; }

        public float Progress { get; private set; }

        public float Step(bool aiming, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            IsAiming = aiming;
            var target = aiming ? 1f : 0f;
            if (TransitionSeconds <= 0f)
            {
                Progress = target;
                return Progress;
            }

            var step = deltaSeconds / TransitionSeconds;
            Progress = aiming ? Math.Min(1f, Progress + step) : Math.Max(0f, Progress - step);
            return Progress;
        }

        public void Reset()
        {
            IsAiming = false;
            Progress = 0f;
        }

        public float FieldOfView(float hipFieldOfView, float adsFieldOfView)
        {
            return Interpolate(hipFieldOfView, adsFieldOfView, Progress);
        }

        // 線形に補間しないのは、動き始めと止まり際が急にならないよう、進み具合を smoothstep で緩めるため。
        public static float Interpolate(float hipFieldOfView, float adsFieldOfView, float progress)
        {
            ValidateFieldOfView(hipFieldOfView, nameof(hipFieldOfView));
            ValidateFieldOfView(adsFieldOfView, nameof(adsFieldOfView));
            if (float.IsNaN(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be a number");
            }

            var t = Math.Max(0f, Math.Min(1f, progress));
            var eased = t * t * (3f - (2f * t));
            return hipFieldOfView + ((adsFieldOfView - hipFieldOfView) * eased);
        }

        // 視野角の比ではなく半視野角の正接の比にするのは、画面上の同じ距離を動かすのに同じ手の動きで済むようにするため。
        public static float SensitivityMultiplier(float hipFieldOfView, float fieldOfView)
        {
            ValidateFieldOfView(hipFieldOfView, nameof(hipFieldOfView));
            ValidateFieldOfView(fieldOfView, nameof(fieldOfView));
            if (hipFieldOfView == fieldOfView)
            {
                return 1f;
            }

            var hip = Math.Tan(hipFieldOfView * Math.PI / 360d);
            var current = Math.Tan(fieldOfView * Math.PI / 360d);
            return (float)(current / hip);
        }

        public static bool IsValidFieldOfView(float value)
        {
            return value > MinFieldOfView && value < MaxFieldOfView;
        }

        public static float ValidateFieldOfView(float value, string paramName)
        {
            if (!IsValidFieldOfView(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName, value, "field of view must be greater than 0 and less than 180 degrees");
            }

            return value;
        }
    }
}
