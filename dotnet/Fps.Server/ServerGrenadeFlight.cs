using System;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// 投げたグレネードの球の飛び方 (Unity の NetGrenade の Rigidbody の代わり)。重力で落ち、<c>.fpsmap</c> の世界に当たると跳ね返り、
    /// 摩擦で滑りが転がりに変わる。人とほかのグレネードには当たらない (Unity でも全員の CharacterController との衝突を切っていた)。
    /// </summary>
    /// <remarks>
    /// 値は Unity のグレネード (ThrownGrenade の BodyRadius・Bounciness・摩擦、NetGrenade.prefab の Rigidbody) と、
    /// プロジェクトの物理の設定 (重力・跳ね返りのしきい値) にそろえる。
    /// </remarks>
    internal sealed class ServerGrenadeFlight
    {
        public const float BodyRadius = 0.08f;
        public const float Bounciness = 0.35f;
        public const float Friction = 0.6f;
        public const float AngularDamping = 0.05f;
        public const float BounceThreshold = 2f;
        public const float Gravity = -9.81f;

        private const int _maxSweeps = 4;
        private const float _skin = 0.001f;

        // 中身の詰まった球の慣性 (2/5 m r^2) で、滑りを 0 にする摩擦の力積は滑りの速さ × 質量 × 2/7
        private const float _slipToImpulse = 2f / 7f;

        private readonly IWorldCollision _world;

        public ServerGrenadeFlight(IWorldCollision world, Vector3 position, Vector3 velocity)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (!IsFinite(position) || !IsFinite(velocity))
            {
                throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "position and velocity must be finite");
            }

            Position = position;
            Velocity = velocity;
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        public Vector3 AngularVelocity { get; private set; }

        public Quaternion Rotation { get; private set; } = Quaternion.Identity;

        public int Bounces { get; private set; }

        public void Step(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be positive and finite");
            }

            Velocity += new Vector3(0f, Gravity * deltaSeconds, 0f);
            var remaining = deltaSeconds;
            for (var i = 0; i < _maxSweeps && remaining > 0f; i++)
            {
                var speed = Velocity.Length();
                var distance = speed * remaining;
                if (distance <= 1e-6f)
                {
                    break;
                }

                var direction = Velocity / speed;
                if (!_world.CapsuleCast(Position, Position, BodyRadius, direction, distance, ServerHitboxes.RaycastLayers, out var hit))
                {
                    Position += direction * distance;
                    remaining = 0f;
                    break;
                }

                var travel = Math.Max(0f, hit.Distance - _skin);
                Position += direction * travel;
                remaining -= remaining * (travel / distance);
                Bounce(hit.Normal);
            }

            AngularVelocity /= 1f + (deltaSeconds * AngularDamping);
            var spin = AngularVelocity.Length();
            if (spin > 1e-6f)
            {
                Rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(AngularVelocity / spin, spin * deltaSeconds) * Rotation);
            }
        }

        private void Bounce(Vector3 normal)
        {
            var into = Vector3.Dot(Velocity, normal);
            if (into >= 0f)
            {
                return;
            }

            var tangent = Velocity - (normal * into);
            var outward = -into > BounceThreshold ? -into * Bounciness : 0f;
            if (outward > 0f)
            {
                Bounces++;
            }

            var normalImpulse = outward - into;

            // 接した点の滑り (並進 + 回転) を、摩擦の力積の上限まで打ち消す
            var slip = tangent + Vector3.Cross(AngularVelocity, -normal * BodyRadius);
            var slipSpeed = slip.Length();
            if (slipSpeed > 1e-6f)
            {
                var impulse = Math.Min(slipSpeed * _slipToImpulse, Friction * normalImpulse);
                var change = -slip / slipSpeed * impulse;
                tangent += change;
                AngularVelocity += Vector3.Cross(-normal * BodyRadius, change) * (2.5f / (BodyRadius * BodyRadius));
            }

            Velocity = tangent + (normal * outward);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
