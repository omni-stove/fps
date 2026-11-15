using System;
using System.Numerics;

namespace Fps.Core
{
    public static class ProjectileSteering
    {
        private const float _minLength = 1e-6f;

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
