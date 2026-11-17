using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ScoreboardSection
    {
        public Team Team { get; }

        public IReadOnlyList<ScoreboardEntry> Rows { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int Assists { get; }

        internal ScoreboardSection(Team team, IReadOnlyList<ScoreboardEntry> rows)
        {
            Team = team;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));

            long kills = 0;
            long deaths = 0;
            long assists = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                kills += rows[i].Kills;
                deaths += rows[i].Deaths;
                assists += rows[i].Assists;
            }

            // 各行は int に収まっても合計はあふれうるので、表示が負にならないよう上限で止める
            Kills = (int)Math.Min(kills, int.MaxValue);
            Deaths = (int)Math.Min(deaths, int.MaxValue);
            Assists = (int)Math.Min(assists, int.MaxValue);
        }
    }
}
