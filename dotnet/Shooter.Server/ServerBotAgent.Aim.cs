using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 撃つ前の見え方・向き・散らばりと、持っている武器の引き金
    internal sealed partial class ServerBotAgent
    {
        private const float _minHorizontalLengthSquared = 1e-6f;

        private FireTrigger _trigger;
        private WeaponSpec _triggerSpec;

        public bool IsSightBlocked(Vector3 eye, Vector3 target)
        {
            return _sightBlocker.Blocks(eye, target);
        }

        // オフラインの EnemyAgent.IsFacing と同じ見方にする。
        public bool IsFacing(Vector3 eye, Vector3 direction, Vector3? lookAt)
        {
            var tolerance = Brain.Spec.FireToleranceDegrees;
            if (!lookAt.HasValue || tolerance >= EnemySpec.MaxFireToleranceDegrees)
            {
                return true;
            }

            var toTarget = new Vector2(lookAt.Value.X - eye.X, lookAt.Value.Z - eye.Z);
            var forward = new Vector2(direction.X, direction.Z);
            if (toTarget.LengthSquared() < _minHorizontalLengthSquared || forward.LengthSquared() < _minHorizontalLengthSquared)
            {
                return true;
            }

            var cos = Vector2.Dot(Vector2.Normalize(toTarget), Vector2.Normalize(forward));
            var degrees = Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * ServerBotMath.DegreesPerRadian;
            return degrees <= tolerance;
        }

        public Vector3 Scatter(Vector3 eye, Vector3 direction, Vector3? lookAt)
        {
            var range = lookAt.HasValue ? Brain.Spec.RangeSpreadDegreesAt(Vector3.Distance(eye, lookAt.Value)) : 0f;
            var halfAngle = Math.Min(EnemySpec.MaxSpreadDegrees, Brain.NextShotSpreadDegrees + range);
            return BlindFireShot.Spread(direction, halfAngle, _random);
        }

        public FireTrigger TriggerFor(WeaponSpec spec)
        {
            if (_trigger == null || !ReferenceEquals(spec, _triggerSpec))
            {
                _trigger = new FireTrigger(spec);
                _triggerSpec = spec;
            }

            return _trigger;
        }
    }
}
