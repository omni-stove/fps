using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class UavReward
    {
        // 印を消すのは PingController の寿命の仕組みに任せる。
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
            // 印を断られても回り続けるのは、印の規則が切ってあってもミニマップには載せるため。
            var marking = true;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                if (!IsEnemyOf(user, candidate))
                {
                    continue;
                }

                MinimapReveals.Reveal(candidate, durationSeconds);
                if (!marking)
                {
                    continue;
                }

                if (pings.TryMarkEnemy(candidate, durationSeconds, out _))
                {
                    marked++;
                }
                else if (pings.LastRefusal != Shooter.Core.PingRefusal.NoTarget)
                {
                    marking = false;
                }
            }

            Debug.Log(StreakRewardLog.Uav(user.name, marked, durationSeconds), user);
            return marked;
        }

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
