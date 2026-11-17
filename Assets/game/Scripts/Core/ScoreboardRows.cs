using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // キル数が同じならアシスト数の多い順、それも同じならデス数の少ない順、それも同じなら ScoreboardEntry.PlayerId の小さい順にして、
    // 一覧が届くたびに同点の行が入れ替わって見えないようにする。
    public static class ScoreboardRows
    {
        // 誰もいないチームも空の欄で返すのは、開くたびに欄の位置が変わらないようにするため。
        public static IReadOnlyList<ScoreboardSection> Build(IEnumerable<ScoreboardEntry> entries, Team firstTeam)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var second = HudFormat.Opponent(firstTeam);
            var first = new List<ScoreboardEntry>();
            var rest = new List<ScoreboardEntry>();
            var seen = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry.Team != Team.Alpha && entry.Team != Team.Bravo)
                {
                    throw new ArgumentOutOfRangeException(nameof(entries), entry.Team, "entry " + entry.PlayerId + " has an invalid team");
                }

                if (!seen.Add(entry.PlayerId))
                {
                    throw new ArgumentException("player " + entry.PlayerId + " appears more than once", nameof(entries));
                }

                (entry.Team == firstTeam ? first : rest).Add(entry);
            }

            first.Sort(Compare);
            rest.Sort(Compare);
            return new[]
            {
                new ScoreboardSection(firstTeam, first),
                new ScoreboardSection(second, rest),
            };
        }

        public static int Compare(ScoreboardEntry a, ScoreboardEntry b)
        {
            var byKills = b.Kills.CompareTo(a.Kills);
            if (byKills != 0)
            {
                return byKills;
            }

            var byAssists = b.Assists.CompareTo(a.Assists);
            if (byAssists != 0)
            {
                return byAssists;
            }

            var byDeaths = a.Deaths.CompareTo(b.Deaths);
            if (byDeaths != 0)
            {
                return byDeaths;
            }

            return a.PlayerId.CompareTo(b.PlayerId);
        }
    }
}
