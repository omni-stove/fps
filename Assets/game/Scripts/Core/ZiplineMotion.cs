using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class ZiplineMotion
    {
        public const float LevelSlope = 1e-3f;

        // 端の際で掴まってすぐ着く、を Interact の押し直しのたびに繰り返さないための下限。
        public const float MinRideDistance = 0.05f;

        private float _distance;
        private float _speed;
        private int _sign = 1;
        private float _grabHeight;
        private float _entrySpeed;
        private bool _isRiding;
        private bool _hasArrived;

        public ZiplineMotion(Vector3 start, Vector3 end, ZiplineSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            RequireFinite(start, nameof(start));
            RequireFinite(end, nameof(end));

            var length = Vector3.Distance(start, end);
            if (!(length > (spec.EndMargin * 2f) + MinRideDistance))
            {
                throw new ArgumentException(
                    $"the line must be longer than twice the end margin ({spec.EndMargin}m) plus {MinRideDistance}m (got {length}m)", nameof(end));
            }

            Start = start;
            End = end;
            Length = length;
            Direction = (end - start) / length;
        }

        public Vector3 Start { get; }

        public Vector3 End { get; }

        public float Length { get; }

        public Vector3 Direction { get; }

        public ZiplineSpec Spec { get; }

        public bool IsRiding => _isRiding;

        public bool HasArrived => _hasArrived;

        public float Distance => _distance;

        public float Speed => _speed;

        public int TravelSign => _sign;

        public Vector3 Position => PointAt(_distance);

        public Vector3 TravelDirection => Direction * _sign;

        // 離れた後も残すのは、ジャンプで離れたときの勢いとして体に渡すため。
        public Vector3 Velocity => TravelDirection * _speed;

        public float StopDistance => _sign > 0 ? Length - Spec.EndMargin : Spec.EndMargin;

        public float RemainingDistance => Math.Max(0f, (StopDistance - _distance) * _sign);

        public Vector3 PointAt(float distance)
        {
            return Start + (Direction * Clamp(distance, 0f, Length));
        }

        public float ClosestDistance(Vector3 point)
        {
            RequireFinite(point, nameof(point));
            return Clamp(Vector3.Dot(point - Start, Direction), 0f, Length);
        }

        public float DistanceTo(Vector3 point)
        {
            return Vector3.Distance(point, PointAt(ClosestDistance(point)));
        }

        // 坂なら向きによらず低い端へ。上り坂へ向かって掴まっても止まって戻るだけで、掴まる意味が無い。
        public int ChooseTravelSign(Vector3 facing)
        {
            RequireFinite(facing, nameof(facing));
            if (Math.Abs(Direction.Y) >= LevelSlope)
            {
                return Direction.Y < 0f ? 1 : -1;
            }

            return Vector3.Dot(new Vector3(facing.X, 0f, facing.Z), Direction) < 0f ? -1 : 1;
        }

        public bool TryGrab(Vector3 hand, Vector3 velocity, Vector3 facing)
        {
            RequireFinite(velocity, nameof(velocity));
            var sign = ChooseTravelSign(facing);
            var distance = Clamp(ClosestDistance(hand), Spec.EndMargin, Length - Spec.EndMargin);
            var stop = sign > 0 ? Length - Spec.EndMargin : Spec.EndMargin;
            if ((stop - distance) * sign <= MinRideDistance)
            {
                return false;
            }

            _sign = sign;
            _distance = distance;
            _grabHeight = PointAt(distance).Y;
            _entrySpeed = Clamp(Math.Max(Vector3.Dot(velocity, TravelDirection), Spec.MinSpeed), Spec.MinSpeed, Spec.MaxSpeed);
            _speed = _entrySpeed;
            _isRiding = true;
            _hasArrived = false;
            return true;
        }

        public Vector3 Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!_isRiding)
            {
                return Vector3.Zero;
            }

            var before = _distance;
            _speed = SpeedAt(PointAt(before).Y);
            var stop = StopDistance;
            var after = before + (_sign * _speed * deltaSeconds);
            if ((after - stop) * _sign >= 0f)
            {
                after = stop;
                _isRiding = false;
                _hasArrived = true;
            }

            _distance = after;
            return PointAt(after) - PointAt(before);
        }

        // 加速度を毎フレーム積むと刻みで着く速さがずれるので、掴まった所から下がった高さで決める (力学的エネルギーの保存)。
        public float SpeedAt(float height)
        {
            var squared = (_entrySpeed * _entrySpeed) + (2f * Spec.Gravity * (_grabHeight - height));
            var speed = squared > 0f ? (float)Math.Sqrt(squared) : 0f;
            return Clamp(speed, Spec.MinSpeed, Spec.MaxSpeed);
        }

        public bool Release()
        {
            if (!_isRiding)
            {
                return false;
            }

            _isRiding = false;
            return true;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
