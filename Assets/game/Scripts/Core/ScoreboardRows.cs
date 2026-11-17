using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// スコアボードの並べ方。参加者をチームごとに分け、キルの多い順に並べる。行に出す文はスコアボードの UI が作る。
    /// キル数が同じならアシスト数の多い順、それも同じならデス数の少ない順、それも同じなら <see cref="ScoreboardEntry.PlayerId"/> の小さい順にして、
    /// 一覧が届くたびに同点の行が入れ替わって見えないようにする。
    /// </summary>
    public static class ScoreboardRows
    {
        /// <summary>
        /// entries をチームごとに分けて並べる。firstTeam の欄を先に、相手チームの欄を後に、必ず 2 つ返す
        /// (誰もいないチームも空の欄で返し、開くたびに欄の位置が変わらないようにする)。
        /// </summary>
        /// <param name="entries">参加者。順番は問わない。</param>
        /// <param name="firstTeam">先に出すチーム。オンラインでは自分のチーム。</param>
        /// <exception cref="ArgumentNullException">entries が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">firstTeam か、どれかの行のチームが Team の値でない。</exception>
        /// <exception cref="ArgumentException">同じ <see cref="ScoreboardEntry.PlayerId"/> の行が 2 つ以上ある。</exception>
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

        /// <summary>
        /// 並べる順。a を先に出すなら負。キルの多い順、アシストの多い順、デス数の少ない順、<see cref="ScoreboardEntry.PlayerId"/> の小さい順。
        /// </summary>
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
