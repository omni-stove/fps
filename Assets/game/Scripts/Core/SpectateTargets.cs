using System.Collections.Generic;

namespace Shooter.Core
{
    // どの端末でも同じ順に回るよう、Id の昇順に並べる。
    public static class SpectateTargets
    {
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
