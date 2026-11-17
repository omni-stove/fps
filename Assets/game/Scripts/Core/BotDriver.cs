using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 向き (yaw / pitch) は持たず毎回受け取るのは、リスポーンや瞬間移動の後もそのまま使えるようにするため。
    public sealed class BotDriver
    {
        private const float _minLengthSquared = 1e-12f;
        private const float _degreesPerRadian = (float)(180.0 / Math.PI);

        private readonly BotInputSpec _spec;
        private readonly Random _random;
        private readonly LookAngles _angles = new LookAngles();

        private Vector2 _aimError;
        private float _aimErrorAge;
        private bool _hasAimError;

        // 乱数を外から渡すのは、テストで固定できるようにするため。
        public BotDriver(BotInputSpec spec, Random random)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public BotInputSpec Spec => _spec;

        public void ResetAim()
        {
            _hasAimError = false;
            _aimErrorAge = 0f;
            _aimError = Vector2.Zero;
        }

        // 撃つかをこの tick に回した後の正面で決めるのは、振り向く途中で撃たせないため。
        public BotInputPlan Plan(
            float deltaSeconds, EnemyDecision decision, Vector3 position, Vector3 eye, float yaw, float pitch, IReadOnlyList<Vector3> path)
        {
            Validate(deltaSeconds, decision, position, eye, yaw, pitch, path);

            _angles.Set(yaw, pitch);
            var currentYaw = _angles.Yaw;
            var currentPitch = _angles.Pitch;

            var moving = TrySteer(decision.MoveTo, position, path, out var heading, out var remaining);

            Vector3? aimPoint = decision.LookAt.HasValue ? AimPoint(deltaSeconds, eye, decision.LookAt.Value) : (Vector3?)null;
            var aimDirection = aimPoint.HasValue ? aimPoint.Value - eye : Vector3.Zero;
            var canAim = aimDirection.LengthSquared() >= _minLengthSquared;

            float desiredYaw;
            float desiredPitch;
            if (canAim)
            {
                DeathCamFraming.LookAt(eye, aimPoint.Value, currentYaw, LookAngles.DefaultMaxPitch, out desiredYaw, out desiredPitch);
            }
            else if (moving && !aimPoint.HasValue)
            {
                desiredYaw = (float)Math.Atan2(heading.X, heading.Y) * _degreesPerRadian;
                desiredPitch = 0f;
            }
            else
            {
                desiredYaw = currentYaw;
                desiredPitch = currentPitch;
            }

            Turn(currentYaw, currentPitch, desiredYaw, desiredPitch, _spec.MaxTurnDegreesPerSecond * deltaSeconds);
            var newYaw = _angles.Yaw;
            var newPitch = _angles.Pitch;
            var forward = DeathCamFraming.Forward(newYaw, newPitch);

            var move = moving ? ToLocal(heading, newYaw) : Vector2.Zero;
            var fire = decision.Fire && canAim && AngleDegrees(forward, aimDirection) <= _spec.FireToleranceDegrees;
            var sprint = moving
                && !decision.Fire
                && remaining >= _spec.SprintDistance
                && move.Y >= (float)Math.Cos(_spec.SprintMaxAngleDegrees / _degreesPerRadian);
            var crouch = _spec.CrouchWhenHolding && !decision.MoveTo.HasValue && decision.LookAt.HasValue;

            return new BotInputPlan(move, newYaw, newPitch, sprint, crouch, fire, eye, forward);
        }

        // Hold と Follow で向きと射撃を判断のままにするのは、撃たれたり敵が見えたりすれば反撃させつつ、
        // 移動だけは指示に従わせて追ったり巡回したりで持ち場から離れさせないため。
        public static EnemyDecision FollowOrder(
            EnemyDecision decision,
            EnemyState state,
            BotErrand order,
            Vector3 position,
            SquadOrderSpec spec,
            BotInputSpec input,
            out bool overridesMove)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            overridesMove = false;
            if (!order.IsOrder || state == EnemyState.Dead)
            {
                return decision;
            }

            if (!IsFinite(order.Destination) || !IsFinite(position))
            {
                throw new ArgumentException(
                    $"the order destination {order.Destination} and the position {position} must be finite", nameof(order));
            }

            var engaged = state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.TakeCover;
            var distance = Vector2.Distance(Flat(position), Flat(order.Destination));
            var arrived = distance <= input.ArrivalDistance;
            switch (order.Kind)
            {
                case BotErrandKind.Hold:
                    overridesMove = true;
                    return !arrived
                        ? new EnemyDecision(order.Destination, decision.LookAt, decision.Fire, false)
                        : new EnemyDecision(null, decision.LookAt, decision.Fire, decision.Crouch);
                case BotErrandKind.Attack:
                    if (engaged)
                    {
                        return decision;
                    }

                    overridesMove = true;
                    return new EnemyDecision(!arrived ? order.Destination : (Vector3?)null, order.Destination, false);
                default:
                    if (distance > spec.LeashDistance || (!engaged && distance > spec.FollowDistance))
                    {
                        overridesMove = true;
                        return new EnemyDecision(order.Destination, decision.LookAt, decision.Fire, false);
                    }

                    if (engaged)
                    {
                        return decision;
                    }

                    overridesMove = true;
                    return new EnemyDecision(null, decision.LookAt, decision.Fire, decision.Crouch);
            }
        }

        // 人と同じ移動に渡すので、PlayerMotor が入力を yaw で回すのと逆の回転にする。
        public static Vector2 ToLocal(Vector2 worldFlat, float yaw)
        {
            var radians = yaw / _degreesPerRadian;
            var sin = (float)Math.Sin(radians);
            var cos = (float)Math.Cos(radians);

            return new Vector2(
                (worldFlat.X * cos) - (worldFlat.Y * sin),
                (worldFlat.X * sin) + (worldFlat.Y * cos));
        }

        public static float AngleDegrees(Vector3 a, Vector3 b)
        {
            var lengths = (float)Math.Sqrt(a.LengthSquared() * b.LengthSquared());
            if (!(lengths >= _minLengthSquared) || float.IsInfinity(lengths))
            {
                return 180f;
            }

            var cos = Math.Max(-1f, Math.Min(1f, Vector3.Dot(a, b) / lengths));
            return (float)Math.Acos(cos) * _degreesPerRadian;
        }

        // yaw と pitch の差を 1 本のベクトルとして縮め、斜めに振るときも上限を超えないようにする。
        private void Turn(float yaw, float pitch, float desiredYaw, float desiredPitch, float maxStep)
        {
            var yawDelta = LookAngles.ToSigned(desiredYaw - yaw);
            var pitchDelta = desiredPitch - pitch;
            var length = (float)Math.Sqrt((yawDelta * yawDelta) + (pitchDelta * pitchDelta));
            if (length > maxStep)
            {
                var scale = maxStep / length;
                yawDelta *= scale;
                pitchDelta *= scale;
            }

            _angles.Set(yaw + yawDelta, pitch + pitchDelta);
        }

        // 誤差を毎 tick 引かずに AimErrorRefreshSeconds ごとに引き直すのは、毎 tick 引くと平均して真ん中に当たり人より強くなるため。
        private Vector3 AimPoint(float deltaSeconds, Vector3 eye, Vector3 target)
        {
            _aimErrorAge += deltaSeconds;
            if (!_hasAimError || _aimErrorAge >= _spec.AimErrorRefreshSeconds)
            {
                _aimError = _spec.AimErrorPerMeter > 0f ? SampleUnitDisc() : Vector2.Zero;
                _aimErrorAge = 0f;
                _hasAimError = true;
            }

            var toTarget = target - eye;
            var distance = toTarget.Length();
            if (distance * distance < _minLengthSquared || _aimError == Vector2.Zero)
            {
                return target;
            }

            Perpendiculars(toTarget / distance, out var right, out var up);
            return target + (((right * _aimError.X) + (up * _aimError.Y)) * (_spec.AimErrorPerMeter * distance));
        }

        // 円の中で一様にするには、半径を一様ではなく面積に比例させて選ぶ (sqrt)。
        private Vector2 SampleUnitDisc()
        {
            var radius = Math.Sqrt(_random.NextDouble());
            var angle = _random.NextDouble() * 2.0 * Math.PI;
            return new Vector2((float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)));
        }

        // forward (長さ 1) に直交する右と上。真上・真下を狙うときは右を +X にする。
        private static void Perpendiculars(Vector3 forward, out Vector3 right, out Vector3 up)
        {
            var flatRight = Vector3.Cross(Vector3.UnitY, forward);
            right = flatRight.LengthSquared() >= 1e-6f ? Vector3.Normalize(flatRight) : Vector3.UnitX;
            up = Vector3.Normalize(Vector3.Cross(forward, right));
        }

        private bool TrySteer(Vector3? moveTo, Vector3 position, IReadOnlyList<Vector3> path, out Vector2 heading, out float remaining)
        {
            heading = Vector2.Zero;
            remaining = 0f;
            if (!moveTo.HasValue)
            {
                return false;
            }

            var count = path != null && path.Count > 0 ? path.Count : 1;
            var here = Flat(position);
            for (var i = 0; i < count; i++)
            {
                var corner = Flat(Corner(moveTo.Value, path, i));
                var toCorner = corner - here;
                var distance = toCorner.Length();
                if (distance <= _spec.ArrivalDistance)
                {
                    continue;
                }

                heading = toCorner / distance;
                remaining = distance;
                for (var k = i + 1; k < count; k++)
                {
                    remaining += Vector2.Distance(Flat(Corner(moveTo.Value, path, k - 1)), Flat(Corner(moveTo.Value, path, k)));
                }

                return true;
            }

            return false;
        }

        private static Vector3 Corner(Vector3 moveTo, IReadOnlyList<Vector3> path, int index)
        {
            return path != null && path.Count > 0 ? path[index] : moveTo;
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }

        private static void Validate(
            float deltaSeconds, EnemyDecision decision, Vector3 position, Vector3 eye, float yaw, float pitch, IReadOnlyList<Vector3> path)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!IsFinite(eye))
            {
                throw new ArgumentOutOfRangeException(nameof(eye), eye, "eye must be finite");
            }

            if (!IsFinite(yaw))
            {
                throw new ArgumentOutOfRangeException(nameof(yaw), yaw, "yaw must be finite");
            }

            if (!IsFinite(pitch))
            {
                throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "pitch must be finite");
            }

            if ((decision.MoveTo.HasValue && !IsFinite(decision.MoveTo.Value))
                || (decision.LookAt.HasValue && !IsFinite(decision.LookAt.Value)))
            {
                throw new ArgumentException("decision must have finite MoveTo and LookAt", nameof(decision));
            }

            if (path == null)
            {
                return;
            }

            for (var i = 0; i < path.Count; i++)
            {
                if (!IsFinite(path[i]))
                {
                    throw new ArgumentException("path corner " + i + " must be finite", nameof(path));
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }
    }
}
