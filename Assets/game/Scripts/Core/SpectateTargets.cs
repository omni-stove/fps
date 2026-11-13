using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 倒されてからリスポーンまでの観戦 (<see cref="DeathCamera.Ally"/>) で、どの味方を映すかの選び方。
    /// 映せるのは、見ている本人と同じチームで生きている (ダウン中も含む) 本人以外のプレイヤー。
    /// 並びは Id の昇順で、どの端末でも同じ順に回る。Next / Previous は並びの端で反対側へ回り込む。
    /// 映していた味方が倒れたり抜けたりしたら、その Id の次 (大きい方) の味方へ移る。
    /// </summary>
    public static class SpectateTargets
    {
        /// <summary>
        /// 映す味方の Id。映せる味方がいなければ null。
        /// </summary>
        /// <param name="candidates">試合にいるプレイヤー。本人や敵・倒れているプレイヤーを含んでよい。null は空とみなす。</param>
        /// <param name="viewer">見ている本人の Id。</param>
        /// <param name="team">見ている本人のチーム。</param>
        /// <param name="current">今映している味方の Id。まだ選んでいなければ null。</param>
        /// <param name="step">
        /// 0 なら今の味方を保つ (映せなくなっていれば次の味方)、正なら Next を押した回数、負なら Previous を押した回数。
        /// </param>
        public static ulong? Pick(IReadOnlyList<SpectateCandidate> candidates, ulong viewer, Team team, ulong? current, int step)
        {
            var allies = Allies(candidates, viewer, team);
            var count = allies.Count;
            if (count == 0)
            {
                return null;
            }

            var index = current.HasValue ? allies.BinarySearch(current.Value) : -1;
            int start;
            int moves;
            if (current.HasValue && index >= 0)
            {
                start = index;
                moves = step;
            }
            else
            {
                // 映していた味方がもういない。BinarySearch の補数は、その Id より大きい最初の味方の位置
                var after = current.HasValue ? ~index : 0;
                if (step >= 0)
                {
                    start = after;
                    moves = step > 0 ? step - 1 : 0;
                }
                else
                {
                    start = after - 1;
                    moves = step + 1;
                }
            }

            return allies[Wrap((long)start + moves, count)];
        }

        /// <summary>
        /// 実際に使うやられ演出。<see cref="DeathCamera.Ally"/> なのに映せる味方がいない (オフラインや、味方が全員倒れている) ときは
        /// <see cref="DeathCamera.Free"/> に落とす。それ以外は rule のまま。
        /// </summary>
        /// <param name="rule">試合のルールのやられ演出。</param>
        /// <param name="hasAlly">映せる味方がいるか (<see cref="Pick"/> が null でないか)。</param>
        public static DeathCamera ResolveMode(DeathCamera rule, bool hasAlly)
        {
            return rule == DeathCamera.Ally && !hasAlly ? DeathCamera.Free : rule;
        }

        private static List<ulong> Allies(IReadOnlyList<SpectateCandidate> candidates, ulong viewer, Team team)
        {
            var allies = new List<ulong>();
            if (candidates == null)
            {
                return allies;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Id != viewer && candidate.Team == team && candidate.IsAlive && !allies.Contains(candidate.Id))
                {
                    allies.Add(candidate.Id);
                }
            }

            allies.Sort();
            return allies;
        }

        private static int Wrap(long index, int count)
        {
            var wrapped = index % count;
            return (int)(wrapped < 0 ? wrapped + count : wrapped);
        }
    }
}
