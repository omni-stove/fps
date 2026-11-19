using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class ProjectileSteering
    {
        private const float _minLength = 1e-6f;

        // 向かう先を決める。囮に一度誘われたら、その囮が消えるまで追い続ける。近づくと囮と相手の見える角度が開き、相手へ戻ってしまうため。
        public static Vector3? Seek(ILockOnDecoy decoy, Vector3 position, Vector3? target, int? luredBy, out int? lure)
        {
            lure = null;
            if (decoy == null)
            {
                return target;
            }

            if (luredBy.HasValue && decoy.TryLocate(luredBy.Value, out var kept))
            {
                lure = luredBy;
                return kept;
            }

            if (target.HasValue && decoy.TryLure(position, target.Value, out var decoyId, out var decoyPoint))
            {
                lure = decoyId;
                return decoyPoint;
            }

            return target;
        }

        public static Vector3 Steer(HomingSpec homing, Vector3 position, Vector3 velocity, Vector3? targetPosition, float deltaSeconds)
        {
            if (homing == null)
            {
                throw new ArgumentNullException(nameof(homing));
            }

            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!targetPosition.HasValue)
            {
                return velocity;
            }

            var speed = velocity.Length();
            var offset = targetPosition.Value - position;
            var distance = offset.Length();
            if (speed < _minLength || distance < _minLength)
            {
                return velocity;
            }

            var heading = velocity / speed;
            var wanted = offset / distance;
            var angle = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, Vector3.Dot(heading, wanted))));
            var maxTurn = homing.TurnDegreesPerSecond * deltaSeconds * ((float)Math.PI / 180f);
            if (angle <= maxTurn)
            {
                return wanted * speed;
            }

            // 真後ろの相手には曲がる面が決まらないので、進む向きに垂直などれか 1 つの面で曲げる。
            var axis = Vector3.Cross(heading, wanted);
            if (axis.LengthSquared() < _minLength * _minLength)
            {
                axis = Vector3.Cross(heading, Math.Abs(heading.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
            }

            axis = Vector3.Normalize(axis);
            var turned = (heading * (float)Math.Cos(maxTurn)) + (Vector3.Cross(axis, heading) * (float)Math.Sin(maxTurn));
            return Vector3.Normalize(turned) * speed;
        }
    }
}
