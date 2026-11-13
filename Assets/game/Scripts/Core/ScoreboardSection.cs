using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// スコアボードの 1 チーム分。<see cref="ScoreboardRows.Build"/> が作る。
    /// </summary>
    public sealed class ScoreboardSection
    {
        public Team Team { get; }

        /// <summary>このチームの参加者。キルの多い順 (<see cref="ScoreboardRows.Compare"/>)。誰もいなければ空。</summary>
        public IReadOnlyList<ScoreboardEntry> Rows { get; }

        /// <summary>このチームの参加者のキル数の合計。</summary>
        public int Kills { get; }

        /// <summary>このチームの参加者のデス数の合計。</summary>
        public int Deaths { get; }

        internal ScoreboardSection(Team team, IReadOnlyList<ScoreboardEntry> rows)
        {
            Team = team;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));

            long kills = 0;
            long deaths = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                kills += rows[i].Kills;
                deaths += rows[i].Deaths;
            }

            // 各行は int に収まっても合計はあふれうるので、表示が負にならないよう上限で止める
            Kills = (int)Math.Min(kills, int.MaxValue);
            Deaths = (int)Math.Min(deaths, int.MaxValue);
        }
    }
}
