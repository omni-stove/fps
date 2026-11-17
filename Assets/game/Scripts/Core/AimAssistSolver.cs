using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static class AimAssistSolver
    {
        private const float _minLength = 1e-6f;
        private const double _degreesPerRadian = 180.0 / Math.PI;

        /// <param name="spec">調整値。</param>
        /// <param name="deltaSeconds">この刻みの秒数。</param>
        /// <param name="aiming">ADS 中か。</param>
        /// <param name="aimingSeconds">ADS を始めてからの秒数。</param>
        /// <param name="eye">視点の位置。</param>
        /// <param name="forward">照準の向き。</param>
        /// <param name="candidates">敵の候補の点。</param>
        /// <exception cref="ArgumentNullException">spec か candidates が null のとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds か aimingSeconds が負か有限でないとき。</exception>
        public static AimAssistResult Solve(
            AimAssistSpec spec,
            float deltaSeconds,
            bool aiming,
            float aimingSeconds,
            Vector3 eye,
            Vector3 forward,
            IReadOnlyList<Vector3> candidates)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            SpecGuard.NonNegativeFinite(aimingSeconds, nameof(aimingSeconds));
            if (!(forward.LengthSquared() >= _minLength * _minLength) || float.IsInfinity(forward.LengthSquared()))
            {
                return AimAssistResult.None;
            }

            var unit = Vector3.Normalize(forward);
            var nearestAngle = float.MaxValue;
            var nearest = Vector3.Zero;
            for (var i = 0; i < candidates.Count; i++)
            {
                var point = candidates[i];
                if (!(Vector3.Distance(eye, point) <= spec.MaxDistance))
                {
                    continue;
                }

                var angle = LockOnTracker.AngleTo(eye, unit, point);
                if (angle < nearestAngle)
                {
                    nearestAngle = angle;
                    nearest = point;
                }
            }

            // 倍率 0 を掛けると視点が止まって照準を外せなくなるので、0 は減速を切る値として扱う。
            var lookScale = spec.SlowdownScale > 0f && spec.SlowdownDegrees > 0f && nearestAngle <= spec.SlowdownDegrees ? spec.SlowdownScale : 1f;
            if (!aiming || !(aimingSeconds < spec.MagnetismSeconds) || !(spec.MagnetismDegrees > 0f) || !(nearestAngle <= spec.MagnetismDegrees))
            {
                return new AimAssistResult(lookScale, 0f, 0f);
            }

            var offset = nearest - eye;
            if (offset.LengthSquared() < _minLength * _minLength)
            {
                return new AimAssistResult(lookScale, 0f, 0f);
            }

            var yaw = LookAngles.ToSigned(YawOf(Vector3.Normalize(offset)) - YawOf(unit));
            var pitchUp = PitchUpOf(Vector3.Normalize(offset)) - PitchUpOf(unit);
            var length = (float)Math.Sqrt((yaw * yaw) + (pitchUp * pitchUp));
            var step = spec.MagnetismDegreesPerSecond * deltaSeconds;
            if (length > step)
            {
                var scale = step / length;
                yaw *= scale;
                pitchUp *= scale;
            }

            return new AimAssistResult(lookScale, yaw, pitchUp);
        }

        private static float YawOf(Vector3 unit)
        {
            return (float)(Math.Atan2(unit.X, unit.Z) * _degreesPerRadian);
        }

        private static float PitchUpOf(Vector3 unit)
        {
            return (float)(Math.Asin(Math.Max(-1f, Math.Min(1f, unit.Y))) * _degreesPerRadian);
        }
    }
}
