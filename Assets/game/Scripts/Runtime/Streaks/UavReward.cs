using System;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ストリークの報酬の UAV。使った者から見た生きている敵 (<see cref="IsEnemyOf"/>) すべてに、使った者の <see cref="PingController"/> で
    /// 秒数で消える追う印を付ける (<see cref="PingController.TryMarkEnemy"/>)。印は見えていなくても敵を追い、数の上限に数えない。
    /// 消すのは <see cref="PingController"/> の寿命の仕組みに任せる。値は呼び出し側が渡す。
    /// </summary>
    public static class UavReward
    {
        /// <summary>
        /// user から見た敵すべてに、durationSeconds 秒で消える印を pings で付け、付けた数を返す。<c>[Streak] uav</c> をログに出す。
        /// pings が付けられない (ルールで Off・ネット対戦・使った者が倒れている) なら、そこで止めて 0 を返す。
        /// </summary>
        /// <param name="user">使った者。</param>
        /// <param name="pings">印を付ける部品。ふつうは user のもの。</param>
        /// <param name="durationSeconds">印が消えるまでの秒数。0 より大きい有限の値。</param>
        /// <exception cref="ArgumentNullException">user か pings が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">durationSeconds が 0 以下か有限でない。</exception>
        public static int Use(GameObject user, PingController pings, float durationSeconds)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (pings == null)
            {
                throw new ArgumentNullException(nameof(pings));
            }

            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "durationSeconds must be positive and finite");
            }

            var marked = 0;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                if (!IsEnemyOf(user, candidate))
                {
                    continue;
                }

                if (pings.TryMarkEnemy(candidate, durationSeconds, out _))
                {
                    marked++;
                }
                else if (pings.LastRefusal != Shooter.Core.PingRefusal.NoTarget)
                {
                    break;
                }
            }

            Debug.Log(StreakRewardLog.Uav(user.name, marked, durationSeconds), user);
            return marked;
        }

        /// <summary>
        /// candidate が user から見た生きている敵か。user 自身 (とその子) と倒れた者は敵でない。
        /// 両方に <see cref="SideMember"/> があれば同じ陣営でないときだけ敵で、どちらかに無ければ敵とみなす。
        /// user の弾を通す置いた物 (持ち主か同じ陣営の盾など) は敵でない。
        /// </summary>
        public static bool IsEnemyOf(GameObject user, Damageable candidate)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (candidate == null || candidate.IsDead || candidate.transform.IsChildOf(user.transform))
            {
                return false;
            }

            // 置いた盾は SideMember を持たないので、PingController と同じく弾を通す相手のものを味方として外す。
            var deployable = candidate.GetComponent<Deployable>();
            if (deployable != null && deployable.LetsThrough(user))
            {
                return false;
            }

            var mine = SideMember.Of(user);
            var theirs = SideMember.Of(candidate.gameObject);
            return mine == null || theirs == null || !mine.IsAllyOf(theirs);
        }
    }
}
