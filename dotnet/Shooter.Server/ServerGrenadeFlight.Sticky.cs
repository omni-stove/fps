using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerGrenadeFlight
    {
        // くっついた物がまだあるかを、面の向こうへこの距離まで線を引いて確かめる
        private const float _surfaceProbeSlack = 0.02f;

        private ServerBodies _characters;
        private Func<int, bool> _hittable;
        private int _ignoredCharacter;
        private bool _onSurface;
        private int _surfaceColliderId;
        private Vector3 _surfaceNormal;
        private long _carrierTeleportTick;
        private float _carrierYaw;
        private Vector3 _carrierOffset;
        private Quaternion _carrierRotation;

        public int? StuckToPlayerId { get; private set; }

        public void CollideWithCharacters(ServerBodies characters, int ignoredPlayerId, Func<int, bool> hittable)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _hittable = hittable ?? throw new ArgumentNullException(nameof(hittable));
            _ignoredCharacter = ignoredPlayerId;
        }

        partial void TryStick(in WorldHit hit, ref bool stuck)
        {
            _onSurface = true;
            _surfaceColliderId = hit.ColliderId;
            _surfaceNormal = hit.Normal;
            stuck = true;
        }

        private bool TryStickToCharacter(Vector3 direction, float reach)
        {
            if (Stickiness != GrenadeStickiness.SurfacesAndCharacters || _characters == null)
            {
                return false;
            }

            ServerBody nearest = null;
            var nearestDistance = reach;
            foreach (var body in _characters.All)
            {
                if (!body.HasSpawned || body.PlayerId == _ignoredCharacter || !_hittable(body.PlayerId))
                {
                    continue;
                }

                body.Capsule(out var bottom, out var top);
                if (SweepCapsule(Position, direction, nearestDistance, bottom, top, body.Radius + BodyRadius, out var distance))
                {
                    nearest = body;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                return false;
            }

            Position += direction * Math.Max(0f, nearestDistance - _skin);
            StuckToPlayerId = nearest.PlayerId;
            _carrierTeleportTick = nearest.TeleportTick;
            _carrierYaw = nearest.Yaw;
            _carrierOffset = Position - nearest.Position;
            _carrierRotation = Rotation;
            return true;
        }

        private void FollowWhatItIsStuckTo()
        {
            if (StuckToPlayerId.HasValue)
            {
                var id = StuckToPlayerId.Value;
                if (!_characters.TryGet(id, out var body) || body.TeleportTick != _carrierTeleportTick || !_hittable(id))
                {
                    Release();
                    return;
                }

                var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, body.Yaw - _carrierYaw);
                Position = body.Position + Vector3.Transform(_carrierOffset, turn);
                Rotation = Quaternion.Normalize(turn * _carrierRotation);
                return;
            }

            // 壊れた建物の部品は当たりから外れるので、くっついた面に向かう線が当たらなくなったら支えを失っている
            if (_onSurface
                && !_world.RaycastCollider(_surfaceColliderId, Position, -_surfaceNormal, BodyRadius + _surfaceProbeSlack, out _))
            {
                Release();
            }
        }

        private void Release()
        {
            IsStuck = false;
            StuckToPlayerId = null;
            _onSurface = false;
            Velocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
        }

        internal static bool SweepCapsule(Vector3 origin, Vector3 direction, float maxDistance, Vector3 a, Vector3 b, float radius, out float distance)
        {
            distance = 0f;
            if (DistanceSquaredToSegment(origin, a, b) <= radius * radius)
            {
                return true;
            }

            var best = float.PositiveInfinity;
            var axis = b - a;
            var fromA = origin - a;
            var axisSqr = Vector3.Dot(axis, axis);
            if (axisSqr > 1e-12f)
            {
                var axisDir = Vector3.Dot(axis, direction);
                var axisFrom = Vector3.Dot(axis, fromA);
                var quadA = axisSqr - (axisDir * axisDir);
                var quadB = (axisSqr * Vector3.Dot(direction, fromA)) - (axisFrom * axisDir);
                var quadC = (axisSqr * Vector3.Dot(fromA, fromA)) - (axisFrom * axisFrom) - (radius * radius * axisSqr);
                var discriminant = (quadB * quadB) - (quadA * quadC);
                if (quadA > 1e-12f && discriminant >= 0f)
                {
                    var t = (-quadB - MathF.Sqrt(discriminant)) / quadA;
                    var along = axisFrom + (t * axisDir);
                    if (t >= 0f && along > 0f && along < axisSqr)
                    {
                        best = t;
                    }
                }
            }

            best = Math.Min(best, SweepSphere(origin, direction, a, radius));
            best = Math.Min(best, SweepSphere(origin, direction, b, radius));
            if (best > maxDistance)
            {
                return false;
            }

            distance = best;
            return true;
        }

        private static float SweepSphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
        {
            var offset = origin - center;
            var half = Vector3.Dot(offset, direction);
            var discriminant = (half * half) - (Vector3.Dot(offset, offset) - (radius * radius));
            if (discriminant < 0f)
            {
                return float.PositiveInfinity;
            }

            var t = -half - MathF.Sqrt(discriminant);
            return t >= 0f ? t : float.PositiveInfinity;
        }

        private static float DistanceSquaredToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var segment = b - a;
            var lengthSqr = segment.LengthSquared();
            var t = lengthSqr > 0f ? Math.Clamp(Vector3.Dot(point - a, segment) / lengthSqr, 0f, 1f) : 0f;
            return Vector3.DistanceSquared(point, a + (segment * t));
        }
    }
}
