using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// ネット対戦の蘇生の要求を、<see cref="TeamDeathmatchSession.TryRevive"/> に渡してよいか確かめる。
    /// Session は距離を見ないので、サーバーはここで距離・チーム・状態をそろえて確かめてから渡す。
    /// </summary>
    public static class ReviveReach
    {
        /// <summary>起こせる距離 (m) の既定値。起こす人と起こされる人の本体の中心どうしで測る。</summary>
        public const float DefaultRange = 1.5f;

        /// <summary>
        /// reviverId が targetId を起こしてよいか。受け付けるなら <see cref="ReviveRejection.None"/>、だめなら最初に引っかかった理由を返す。
        /// </summary>
        /// <param name="session">試合。</param>
        /// <param name="reviverId">起こす人。</param>
        /// <param name="targetId">起こされる人。</param>
        /// <param name="reviverPosition">起こす人の本体の中心。</param>
        /// <param name="targetPosition">起こされる人の本体の中心。</param>
        /// <param name="range">起こせる距離 (m)。0 以上の有限値。</param>
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

            if (session.GetTeam(reviverId) != session.GetTeam(targetId))
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

        /// <summary>a と b の距離が range 以下なら true。どちらかが有限値でなければ false。</summary>
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
