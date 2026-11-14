using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 分隊への指示を誰に出すかを決める。状態は持たず、同じ入力なら同じ相手を返す。
    /// <list type="bullet">
    /// <item>生きていて、指示した人と同じ陣営の bot だけに出す。</item>
    /// <item>分隊のあるモード (陣営 = 分隊) では、離れていても分隊の全員に出す。</item>
    /// <item>分隊の無いモードでは、指示した人から <see cref="SquadOrderSpec.NearbyRadius"/> 以内 (水平、境界を含む) の bot だけに出す。</item>
    /// </list>
    /// </summary>
    public static class SquadOrderRecipients
    {
        /// <param name="side">指示した人の陣営。</param>
        /// <param name="position">指示した人の足元。分隊の無いモードで距離を測る。</param>
        /// <param name="hasSquads">分隊のあるモードなら true。</param>
        /// <param name="nearbyRadius">分隊の無いモードで指示が届く距離 (m)。0 より大きい有限値。</param>
        /// <param name="candidates">指示を受けられるかもしれない bot (指示した人は入れない)。</param>
        /// <param name="recipients">指示を出す bot の candidates の番号を、小さい順に足す。前の中身は消す。</param>
        /// <returns>足した数。</returns>
        public static int Select(
            Side side,
            Vector3 position,
            bool hasSquads,
            float nearbyRadius,
            IReadOnlyList<SquadOrderCandidate> candidates,
            List<int> recipients)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (recipients == null)
            {
                throw new ArgumentNullException(nameof(recipients));
            }

            if (!(nearbyRadius > 0f) || float.IsInfinity(nearbyRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(nearbyRadius), nearbyRadius, "nearbyRadius must be positive and finite");
            }

            recipients.Clear();
            var here = new Vector2(position.X, position.Z);
            var limit = nearbyRadius * nearbyRadius;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsAlive || candidate.Side != side)
                {
                    continue;
                }

                if (!hasSquads)
                {
                    var distance = Vector2.DistanceSquared(here, new Vector2(candidate.Position.X, candidate.Position.Z));
                    if (!(distance <= limit))
                    {
                        continue;
                    }
                }

                recipients.Add(i);
            }

            return recipients.Count;
        }
    }
}
