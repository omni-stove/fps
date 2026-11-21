using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 判断 (EnemyBrain) が見失った相手を追う・探す・物陰から狙う間に、その居場所へ手榴弾を投げる。
    // 見えている相手には撃つ方が速く確かなので投げない
    internal sealed partial class ServerBotAgent
    {
        // 低い弧で届く角度を下から探す。上の端を 90° より手前にするのは、真上へ近い弧は水平に進まず距離で解けないため
        private const float _minAimDegrees = -45f;
        private const float _maxAimDegrees = 75f;
        private const float _aimStepDegrees = 1f;
        private const float _minAimDistance = 1e-3f;

        private float _nextGrenadeAt;

        public bool CanThrowGrenade => UsesTactics && Clock >= _nextGrenadeAt;

        public void NoteGrenadeThrown()
        {
            _nextGrenadeAt = Clock + _spec.Tactics.GrenadeIntervalSeconds;
        }

        internal static bool WantsGrenade(EnemyState state, bool seesTarget, Vector3 self, Vector3? lookAt, BotTacticsSpec tactics)
        {
            ArgumentNullException.ThrowIfNull(tactics);
            if (!tactics.ThrowGrenades || seesTarget || !lookAt.HasValue)
            {
                return false;
            }

            if (state != EnemyState.Chase && state != EnemyState.Search && state != EnemyState.TakeCover)
            {
                return false;
            }

            var distance = Vector2.Distance(new Vector2(self.X, self.Z), new Vector2(lookAt.Value.X, lookAt.Value.Z));
            return distance >= tactics.GrenadeMinDistance && distance <= tactics.GrenadeMaxDistance;
        }

        // サーバーの ThrowGrenade と同じ投げ方 (目から ReleaseDistance 先で放し、向き × ThrowSpeed に ThrowLiftSpeed を上へ足す) で、
        // 跳ねる前の弧が狙う点の真上か真下を通る向きを返す。届かなければ false
        internal static bool TryAimGrenade(Vector3 eye, Vector3 target, GrenadeSpec spec, float gravity, out Vector3 direction)
        {
            ArgumentNullException.ThrowIfNull(spec);
            direction = default;
            var flat = new Vector2(target.X - eye.X, target.Z - eye.Z);
            var distance = flat.Length();
            if (!(distance > _minAimDistance) || !float.IsFinite(distance) || !float.IsFinite(target.Y) || !(gravity < 0f))
            {
                return false;
            }

            var heading = flat / distance;
            var previousDegrees = 0f;
            var previousMiss = 0f;
            var hasPrevious = false;
            for (var degrees = _minAimDegrees; degrees <= _maxAimDegrees; degrees += _aimStepDegrees)
            {
                var miss = HeightMiss(eye, target.Y, distance, degrees, spec, gravity);
                if (hasPrevious && previousMiss < 0f && miss >= 0f)
                {
                    var t = previousMiss / (previousMiss - miss);
                    direction = Direction(heading, previousDegrees + ((degrees - previousDegrees) * t));
                    return true;
                }

                previousDegrees = degrees;
                previousMiss = miss;
                hasPrevious = true;
            }

            return false;
        }

        // 狙う点の水平距離に着いた時の高さから、狙う点の高さを引いた値
        private static float HeightMiss(Vector3 eye, float targetY, float distance, float degrees, GrenadeSpec spec, float gravity)
        {
            var radians = degrees / ServerBotMath.DegreesPerRadian;
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            var releaseY = eye.Y + (sin * spec.ReleaseDistance);
            var remaining = distance - (cos * spec.ReleaseDistance);
            if (remaining <= 0f)
            {
                return releaseY - targetY;
            }

            var seconds = remaining / (spec.ThrowSpeed * cos);
            var rise = (spec.ThrowSpeed * sin) + spec.ThrowLiftSpeed;
            return releaseY + (rise * seconds) + (0.5f * gravity * seconds * seconds) - targetY;
        }

        private static Vector3 Direction(Vector2 heading, float degrees)
        {
            var radians = degrees / ServerBotMath.DegreesPerRadian;
            var cos = MathF.Cos(radians);
            return new Vector3(heading.X * cos, MathF.Sin(radians), heading.Y * cos);
        }
    }
}
