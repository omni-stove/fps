using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public sealed class LockOnTracker
    {
        private const float _minLength = 1e-6f;

        private HomingSpec _homing;

        public int? TargetId { get; private set; }

        public float HeldSeconds { get; private set; }

        public float Progress
        {
            get
            {
                if (!TargetId.HasValue)
                {
                    return 0f;
                }

                var needed = _homing.LockOnSeconds;
                return needed <= 0f ? 1f : Math.Min(1f, HeldSeconds / needed);
            }
        }

        public bool IsLocked => TargetId.HasValue && Progress >= 1f;

        public int? LockedTargetId => IsLocked ? TargetId : null;

        public void Update(
            HomingSpec homing, float deltaSeconds, bool aiming, Vector3 eye, Vector3 forward, IReadOnlyList<LockOnCandidate> candidates)
        {
            if (homing == null)
            {
                throw new ArgumentNullException(nameof(homing));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            // 持ち替えた武器の秒数で前の武器の捉えた時間を数えないよう、武器が変わったら捉え直す。
            if (!ReferenceEquals(homing, _homing))
            {
                Clear();
                _homing = homing;
            }

            if (!aiming || forward.LengthSquared() < _minLength * _minLength)
            {
                Clear();
                return;
            }

            var unit = Vector3.Normalize(forward);
            int? nearest = null;
            var nearestAngle = float.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var angle = AngleTo(eye, unit, candidate.Point);
                if (angle > homing.LockOnConeDegrees)
                {
                    continue;
                }

                if (candidate.Id == TargetId)
                {
                    HeldSeconds += deltaSeconds;
                    return;
                }

                if (angle < nearestAngle)
                {
                    nearestAngle = angle;
                    nearest = candidate.Id;
                }
            }

            // 捉えたこの刻みも円錐に入っていた時間なので数える。0 から始めると、ロックが毎回 1 刻み遅れる。
            TargetId = nearest;
            HeldSeconds = nearest.HasValue ? deltaSeconds : 0f;
        }

        public void Clear()
        {
            TargetId = null;
            HeldSeconds = 0f;
        }

        public static float AngleTo(Vector3 eye, Vector3 forward, Vector3 point)
        {
            var offset = point - eye;
            var distance = offset.Length();
            if (distance < _minLength)
            {
                return 0f;
            }

            var cos = Math.Max(-1f, Math.Min(1f, Vector3.Dot(forward, offset / distance)));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        public static bool IsInCone(HomingSpec homing, Vector3 eye, Vector3 forward, Vector3 point)
        {
            if (homing == null)
            {
                throw new ArgumentNullException(nameof(homing));
            }

            return forward.LengthSquared() >= _minLength * _minLength
                && AngleTo(eye, Vector3.Normalize(forward), point) <= homing.LockOnConeDegrees;
        }
    }
}
