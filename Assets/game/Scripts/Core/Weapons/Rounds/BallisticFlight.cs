using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class BallisticFlight
    {
        public const int MaxSweepsPerStep = 4;

        // 跳ね返った直後の始点が面の上に乗ったままだと、次の Raycast がまた同じ面に当たるので、少しだけ離す。
        private const float _bounceSkin = 0.001f;

        private const float _minChord = 1e-6f;

        private readonly ProjectileSpec _projectile;
        private readonly HomingSpec _homing;
        private readonly IWorldCollision _world;
        private readonly int _layerMask;
        private readonly Vector3 _acceleration;
        // キャラの当たり判定を世界と同じ物理で持つか (Unity) 別に持つか (サーバー) は場所で違うので、レイヤーで決めずに見分け方を受け取る。
        private readonly Func<WorldHit, bool> _isTarget;

        public BallisticFlight(
            ProjectileSpec projectile,
            HomingSpec homing,
            IWorldCollision world,
            int layerMask,
            Vector3 origin,
            Vector3 direction,
            Vector3 gravity,
            Func<WorldHit, bool> isTarget = null,
            float speedMultiplier = 1f)
        {
            _projectile = projectile ?? throw new ArgumentNullException(nameof(projectile));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            RequireFinite(origin, nameof(origin));
            RequireFinite(direction, nameof(direction));
            RequireFinite(gravity, nameof(gravity));
            var length = direction.Length();
            if (length < _minChord)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            _homing = homing;
            _layerMask = layerMask;
            _acceleration = gravity * projectile.GravityMultiplier;
            _isTarget = isTarget;
            Position = origin;
            Velocity = direction / length * (projectile.Speed * SpecGuard.PositiveFinite(speedMultiplier, nameof(speedMultiplier)));
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        public float Age { get; private set; }

        public float Distance { get; private set; }

        public int Bounces { get; private set; }

        public BallisticState State { get; private set; } = BallisticState.Flying;

        public bool IsDone => State != BallisticState.Flying;

        public BallisticStep Step(float deltaSeconds, Vector3? homingTarget = null)
        {
            SpecGuard.PositiveFinite(deltaSeconds, nameof(deltaSeconds));
            if (IsDone)
            {
                throw new InvalidOperationException("the flight has already ended as " + State);
            }

            var from = Position;
            var left = _projectile.LifetimeSeconds - Age;
            var expires = deltaSeconds >= left;
            var remaining = expires ? left : deltaSeconds;
            if (_homing != null)
            {
                Velocity = ProjectileSteering.Steer(_homing, Position, Velocity, homingTarget, deltaSeconds);
            }

            var hasHit = false;
            var lastHit = default(WorldHit);
            for (var sweep = 0; sweep < MaxSweepsPerStep && remaining > 0f; sweep++)
            {
                // 速度を足し込むだけ (オイラー法) だと刻みの大きさで落ち方が変わるので、放物線の式で次の位置を出し、その弦で当たりを調べる。
                var next = Position + (Velocity * remaining) + (_acceleration * (0.5f * remaining * remaining));
                var chord = next - Position;
                var length = chord.Length();
                if (length < _minChord || !_world.Raycast(Position, chord / length, length, _layerMask, out var hit))
                {
                    Advance(next, remaining, length);
                    remaining = 0f;
                    break;
                }

                hasHit = true;
                lastHit = hit;
                var elapsed = remaining * (hit.Distance / length);
                Advance(hit.Point, elapsed, hit.Distance);
                remaining -= elapsed;
                if (_isTarget != null && _isTarget(hit))
                {
                    return End(BallisticState.HitTarget, from, hit);
                }

                switch (_projectile.OnWorldHit)
                {
                    case WorldHitResponse.Stop:
                        return End(BallisticState.Stopped, from, hit);
                    case WorldHitResponse.Stick:
                        return End(BallisticState.Stuck, from, hit);
                    default:
                        Bounce(hit);
                        break;
                }
            }

            // 隅で跳ね返り続けて当たりを調べ切れなかった残りの時間は、その場に留まったことにする。
            Age += remaining;
            if (expires)
            {
                Age = _projectile.LifetimeSeconds;
                State = BallisticState.Expired;
            }

            return new BallisticStep(State, from, Position, hasHit, lastHit);
        }

        private void Advance(Vector3 to, float seconds, float distance)
        {
            Position = to;
            Velocity += _acceleration * seconds;
            Age += seconds;
            Distance += distance;
        }

        private void Bounce(WorldHit hit)
        {
            Bounces++;
            var normal = hit.Normal;
            var into = Vector3.Dot(Velocity, normal);
            if (into < 0f)
            {
                Velocity -= normal * (2f * into);
            }

            Position = hit.Point + (normal * _bounceSkin);
        }

        private BallisticStep End(BallisticState state, Vector3 from, WorldHit hit)
        {
            State = state;
            return new BallisticStep(state, from, Position, true, hit);
        }

        private static void RequireFinite(Vector3 value, string paramName)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be finite");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
