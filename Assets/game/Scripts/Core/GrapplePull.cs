using System;
using System.Numerics;

namespace Fps.Core
{
    // 動かす物理 (CharacterController) は持ち主にあるので、ここでは毎 Tick 実際の位置を受け取って速さと残りの距離だけを決める。
    public sealed class GrapplePull
    {
        // 壁や天井に引っかかったまま MaxPullSeconds まで待たせないよう、近づかなくなったらこの秒数で打ち切る。
        public const float StuckSeconds = 0.25f;

        // CharacterController が面に沿って滑るだけの細かな動きは近づいたとみなさない。
        public const float ProgressEpsilon = 0.05f;

        private readonly GrappleSpec _spec;
        private float _closest;
        private float _stuckSeconds;

        public GrapplePull(GrappleSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public GrappleSpec Spec => _spec;

        public bool IsPulling { get; private set; }

        public GrappleEnd Ended { get; private set; }

        public float Speed { get; private set; }

        public Vector3 Velocity { get; private set; }

        public float ElapsedSeconds { get; private set; }

        // 引き寄せる向きに乗っている勢いはそのまま使う。向かう先の逆へ走っていても、0 から速め直すだけで逆向きには引かない。
        public void Start(Vector3 position, Vector3 velocity, Vector3 target)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(velocity, nameof(velocity));
            RequireFinite(target, nameof(target));

            var offset = target - position;
            var distance = offset.Length();
            var along = distance > 1e-6f ? Vector3.Dot(velocity, offset / distance) : 0f;
            Speed = Math.Min(_spec.PullSpeed, Math.Max(0f, along));
            Velocity = distance > 1e-6f ? offset / distance * Speed : Vector3.Zero;
            ElapsedSeconds = 0f;
            _closest = distance;
            _stuckSeconds = 0f;
            Ended = GrappleEnd.None;
            IsPulling = true;
        }

        // 着いた Tick は止まる位置までちょうど動かし、速度を 0 にする (縁で止まる)。
        public Vector3 Step(Vector3 position, Vector3 target, float deltaSeconds)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(target, nameof(target));
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsPulling)
            {
                return Vector3.Zero;
            }

            var offset = target - position;
            var distance = offset.Length();
            if (distance <= _spec.ArriveDistance)
            {
                Finish(GrappleEnd.Arrived);
                return offset;
            }

            ElapsedSeconds += deltaSeconds;
            if (distance < _closest - ProgressEpsilon)
            {
                _closest = distance;
                _stuckSeconds = 0f;
            }
            else
            {
                _stuckSeconds += deltaSeconds;
            }

            if (_stuckSeconds >= StuckSeconds)
            {
                Finish(GrappleEnd.Blocked);
                return Vector3.Zero;
            }

            if (ElapsedSeconds >= _spec.MaxPullSeconds)
            {
                Finish(GrappleEnd.TimedOut);
                return Vector3.Zero;
            }

            var direction = offset / distance;
            Speed = Math.Min(_spec.PullSpeed, Speed + (_spec.PullAcceleration * deltaSeconds));
            Velocity = direction * Speed;
            var travel = Speed * deltaSeconds;
            if (travel >= distance - _spec.ArriveDistance)
            {
                Finish(GrappleEnd.Arrived);
                return offset;
            }

            return direction * travel;
        }

        // 離した Tick の速度を返す。Jump で離したときはこれを勢いとして渡す。
        public Vector3 Release(GrappleEnd reason)
        {
            if (reason == GrappleEnd.None || reason == GrappleEnd.Arrived)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "a release needs a reason other than None or Arrived");
            }

            if (!IsPulling)
            {
                return Vector3.Zero;
            }

            var velocity = Velocity;
            Finish(reason);
            return velocity;
        }

        private void Finish(GrappleEnd reason)
        {
            IsPulling = false;
            Ended = reason;
            Speed = 0f;
            Velocity = Vector3.Zero;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X)
                || float.IsNaN(value.Y) || float.IsInfinity(value.Y)
                || float.IsNaN(value.Z) || float.IsInfinity(value.Z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }
    }
}
