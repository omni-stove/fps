using System;
using System.Numerics;

namespace Shooter.Core
{
    // TeamDeathmatchSession は距離を見ないので、サーバーはここで距離・味方 (AreAllies)・状態をそろえて確かめてから TryRevive に渡す。
    public static class ReviveReach
    {
        public const float DefaultRange = 1.5f;

        public static ReviveRejection Check(
            TeamDeathmatchSession session, int reviverId, int targetId, Vector3 reviverPosition, Vector3 targetPosition, float range)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!(range >= 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must not be negative and must be finite");
            }

            if (session.Spec.Rules.Actions.Downed != Downed.Revive)
            {
                return ReviveRejection.DownedOff;
            }

            if (!session.HasPlayer(reviverId) || !session.HasPlayer(targetId))
            {
                return ReviveRejection.NotJoined;
            }

            if (session.State != GameModeState.Playing)
            {
                return ReviveRejection.NotPlaying;
            }

            if (reviverId == targetId)
            {
                return ReviveRejection.Self;
            }

            if (!session.AreAllies(reviverId, targetId))
            {
                return ReviveRejection.Enemy;
            }

            if (!session.IsAlive(reviverId) || session.IsDowned(reviverId))
            {
                return ReviveRejection.ReviverDown;
            }

            if (!session.IsDowned(targetId))
            {
                return ReviveRejection.TargetNotDowned;
            }

            if (!IsFinite(reviverPosition) || !IsFinite(targetPosition))
            {
                return ReviveRejection.InvalidPosition;
            }

            return IsWithin(reviverPosition, targetPosition, range) ? ReviveRejection.None : ReviveRejection.TooFar;
        }

        public static bool IsWithin(Vector3 a, Vector3 b, float range)
        {
            if (!IsFinite(a) || !IsFinite(b) || !(range >= 0f))
            {
                return false;
            }

            return Vector3.DistanceSquared(a, b) <= range * range;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
