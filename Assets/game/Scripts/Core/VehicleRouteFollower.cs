using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public enum VehicleRoutePhase
    {
        Driving = 0,

        Reversing = 1,

        Finished = 2,
    }

    public readonly struct VehicleRouteCommand
    {
        public VehicleRouteCommand(float steer, float throttle, bool brake, int targetIndex, float targetSpeed, VehicleRoutePhase phase)
        {
            Steer = steer;
            Throttle = throttle;
            Brake = brake;
            TargetIndex = targetIndex;
            TargetSpeed = targetSpeed;
            Phase = phase;
        }

        public float Steer { get; }

        public float Throttle { get; }

        public bool Brake { get; }

        public int TargetIndex { get; }

        public float TargetSpeed { get; }

        public VehicleRoutePhase Phase { get; }
    }

    // 高さを見ず水平面だけで追うのは、坂や段差で点の高さと車の高さがずれても、着いたかどうかと向きが変わらないようにするため。
    public sealed class VehicleRouteFollower
    {
        private const float _epsilon = 1e-6f;
        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

        private int _index;
        private float _reverseSteer;

        public VehicleRouteFollower(VehicleRouteSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public VehicleRouteSpec Spec { get; }

        public int TargetIndex => _index;

        public VehicleRoutePhase Phase { get; private set; }

        public bool IsFinished => Phase == VehicleRoutePhase.Finished;

        public float StalledSeconds { get; private set; }

        public float ReverseSecondsLeft { get; private set; }

        public void Restart(int index = 0)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            _index = index;
            Phase = VehicleRoutePhase.Driving;
            StalledSeconds = 0f;
            ReverseSecondsLeft = 0f;
            _reverseSteer = 0f;
        }

        public VehicleRouteCommand Tick(IReadOnlyList<Vector3> points, bool loop, Vector3 position, Vector3 forward, float forwardSpeed, float deltaSeconds)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!IsFinite(forward))
            {
                throw new ArgumentOutOfRangeException(nameof(forward), forward, "forward must be finite");
            }

            if (float.IsNaN(forwardSpeed) || float.IsInfinity(forwardSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(forwardSpeed), forwardSpeed, "forwardSpeed must be finite");
            }

            for (var i = 0; i < points.Count; i++)
            {
                if (!IsFinite(points[i]))
                {
                    throw new ArgumentException($"points[{i}] must be finite: {points[i]}", nameof(points));
                }
            }

            var count = points.Count;
            if (count == 0)
            {
                return Stop(VehicleRoutePhase.Finished);
            }

            // 点が 1 つの輪は同じ点を回り続けるだけなので、行き先として止まる。
            var looping = loop && count >= 2;
            if (_index >= count)
            {
                _index = count - 1;
            }

            if (IsFinished)
            {
                return Stop(VehicleRoutePhase.Finished);
            }

            var pos = Flat(position);
            Advance(points, looping, pos, forwardSpeed);
            if (IsFinished)
            {
                StalledSeconds = 0f;
                ReverseSecondsLeft = 0f;
                return Stop(VehicleRoutePhase.Finished);
            }

            if (Phase == VehicleRoutePhase.Reversing)
            {
                ReverseSecondsLeft = Math.Max(0f, ReverseSecondsLeft - deltaSeconds);
                if (ReverseSecondsLeft > 0f)
                {
                    return Reverse();
                }

                Phase = VehicleRoutePhase.Driving;
                StalledSeconds = 0f;
            }

            var target = LookAheadPoint(points, looping, pos, Spec.LookAheadAt(forwardSpeed));
            var angle = HeadingError(Flat(forward), target - pos);
            var steer = Clamp(angle / Spec.FullSteerAngle, -1f, 1f);
            // 向きのずれで 0 まで落とすと、止まったままアクセルが 0 になり詰まりも測れず動けなくなるので、詰まりの速さより上に保つ。
            var turnSpeed = Math.Max(Spec.CornerSpeedFor(angle), Math.Min(Spec.CruiseSpeed, Spec.StuckSpeed * 2f + _epsilon));
            var targetSpeed = Math.Min(turnSpeed, UpcomingSpeedLimit(points, looping, pos));

            var throttle = 0f;
            var brake = false;
            var shortfall = targetSpeed - forwardSpeed;
            if (shortfall > 0f)
            {
                throttle = Math.Min(1f, shortfall / Spec.SpeedBand);
            }
            else if (shortfall < -Spec.SpeedBand)
            {
                brake = true;
            }

            if (throttle > 0f && forwardSpeed < Spec.StuckSpeed)
            {
                StalledSeconds += deltaSeconds;
            }
            else
            {
                StalledSeconds = 0f;
            }

            if (StalledSeconds >= Spec.StuckSeconds)
            {
                // 下がりながら向かう先と逆へ切ると鼻先が向かう先へ回るので、次に前へ出たときに同じ壁へ当たりにくい。
                StalledSeconds = 0f;
                Phase = VehicleRoutePhase.Reversing;
                ReverseSecondsLeft = Spec.ReverseSeconds;
                _reverseSteer = angle >= 0f ? -1f : 1f;
                return Reverse();
            }

            return new VehicleRouteCommand(steer, throttle, brake, _index, targetSpeed, VehicleRoutePhase.Driving);
        }

        private void Advance(IReadOnlyList<Vector3> points, bool looping, Vector2 pos, float forwardSpeed)
        {
            var lookAhead = Spec.LookAheadAt(forwardSpeed);
            var count = points.Count;
            var radiusSquared = Spec.ArriveRadius * Spec.ArriveRadius;
            for (var guard = 0; guard < count; guard++)
            {
                var reached = Vector2.DistanceSquared(pos, Flat(points[_index])) <= radiusSquared || Passed(points, looping, pos, lookAhead);
                if (!reached)
                {
                    return;
                }

                if (!looping && _index == count - 1)
                {
                    Phase = VehicleRoutePhase.Finished;
                    return;
                }

                _index = (_index + 1) % count;
            }
        }

        // 先読みで角の内側を切ると、着いたとみなす輪にも入らず入る区間の先へも抜けないまま次の区間へ移る。
        // 戻ってその点を回り直さないよう、角を二等分する線を越えたら通ったことにする。
        private bool Passed(IReadOnlyList<Vector3> points, bool looping, Vector2 pos, float lookAhead)
        {
            var count = points.Count;
            // 最初の点はどの向きから入るか決まっていないので、輪に入ったときだけ着いたことにする。
            if (!TryPrevious(count, looping, _index, out var previous))
            {
                return false;
            }

            var point = Flat(points[_index]);
            var incoming = Direction(Flat(points[previous]), point);
            var outgoing = looping || _index < count - 1
                ? Direction(point, Flat(points[(_index + 1) % count]))
                : Vector2.Zero;
            var across = incoming + outgoing;
            if (across.LengthSquared() <= _epsilon)
            {
                // 折り返す角では二等分の向きが消えるので、入ってきた向きで測る。
                across = incoming;
            }

            if (!(across.LengthSquared() > _epsilon) || Vector2.Dot(pos - point, across) < 0f)
            {
                return false;
            }

            // 二等分線は鋭い角ほど入る区間と平行に近づき、角から遠い所でも越えてしまう。先読みで内側を切れる範囲に限る。
            return Vector2.DistanceSquared(pos, point) <= lookAhead * lookAhead;
        }

        private static Vector2 Direction(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var length = delta.Length();
            return length > _epsilon ? delta / length : Vector2.Zero;
        }

        // 車から道の上でいちばん近い所を起点に、道に沿って先読みの距離だけ進んだ点を狙う。点そのものを狙うと、点の手前で急に向きを変えるため。
        private Vector2 LookAheadPoint(IReadOnlyList<Vector3> points, bool looping, Vector2 pos, float lookAhead)
        {
            var count = points.Count;
            var next = Flat(points[_index]);
            var current = TryPrevious(count, looping, _index, out var previous)
                ? ClosestOnSegment(Flat(points[previous]), next, pos)
                : pos;
            var remaining = lookAhead;
            var index = _index;
            for (var guard = 0; guard <= count; guard++)
            {
                next = Flat(points[index]);
                var distance = Vector2.Distance(current, next);
                if (distance >= remaining)
                {
                    return distance > _epsilon ? current + ((next - current) * (remaining / distance)) : next;
                }

                remaining -= distance;
                current = next;
                if (!looping && index == count - 1)
                {
                    return current;
                }

                index = (index + 1) % count;
            }

            return current;
        }

        private float UpcomingSpeedLimit(IReadOnlyList<Vector3> points, bool looping, Vector2 pos)
        {
            var count = points.Count;
            var limit = Spec.CruiseSpeed;
            // これより先の角は、巡航の速さから落としても間に合うので見なくてよい。
            var horizon = (Spec.CruiseSpeed * Spec.CruiseSpeed) / (2f * Spec.Deceleration);
            var distance = Vector2.Distance(pos, Flat(points[_index]));
            var index = _index;
            for (var guard = 0; guard < count; guard++)
            {
                var last = !looping && index == count - 1;
                var speedAtPoint = last ? 0f : Spec.CornerSpeedFor(TurnAt(points, looping, index, pos));
                limit = Math.Min(limit, Spec.SpeedToReach(speedAtPoint, distance));
                if (last || distance > horizon)
                {
                    break;
                }

                var following = (index + 1) % count;
                distance += Vector2.Distance(Flat(points[index]), Flat(points[following]));
                index = following;
            }

            return limit;
        }

        // 前の点が無い最初の点は、車のいる所から入るものとして曲がる角度を測る。
        private static float TurnAt(IReadOnlyList<Vector3> points, bool looping, int index, Vector2 pos)
        {
            var count = points.Count;
            if (!looping && index == count - 1)
            {
                return 0f;
            }

            var point = Flat(points[index]);
            var incoming = point - (TryPrevious(count, looping, index, out var previous) ? Flat(points[previous]) : pos);
            var outgoing = Flat(points[(index + 1) % count]) - point;
            return Math.Abs(HeadingError(incoming, outgoing));
        }

        private VehicleRouteCommand Reverse()
        {
            return new VehicleRouteCommand(_reverseSteer, -Spec.ReverseThrottle, false, _index, 0f, VehicleRoutePhase.Reversing);
        }

        private VehicleRouteCommand Stop(VehicleRoutePhase phase)
        {
            return new VehicleRouteCommand(0f, 0f, true, _index, 0f, phase);
        }

        private static bool TryPrevious(int count, bool looping, int index, out int previous)
        {
            previous = index > 0 ? index - 1 : count - 1;
            return index > 0 || looping;
        }

        // 右へ回すと正。Unity の左手系 (x が右、z が前) に合わせる。
        private static float HeadingError(Vector2 from, Vector2 to)
        {
            if (from.LengthSquared() <= _epsilon || to.LengthSquared() <= _epsilon)
            {
                return 0f;
            }

            var cross = (from.Y * to.X) - (from.X * to.Y);
            var dot = Vector2.Dot(from, to);
            return (float)Math.Atan2(cross, dot) * _radiansToDegrees;
        }

        private static Vector2 ClosestOnSegment(Vector2 from, Vector2 to, Vector2 point)
        {
            var segment = to - from;
            var lengthSquared = segment.LengthSquared();
            if (lengthSquared <= _epsilon)
            {
                return to;
            }

            var t = Clamp(Vector2.Dot(point - from, segment) / lengthSquared, 0f, 1f);
            return from + (segment * t);
        }

        private static Vector2 Flat(Vector3 value)
        {
            return new Vector2(value.X, value.Z);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
