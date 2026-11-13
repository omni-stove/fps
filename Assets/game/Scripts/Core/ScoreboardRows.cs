using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// スコアボードの並べ方と、行に出す文字列。参加者をチームごとに分け、キルの多い順に並べる。
    /// キル数が同じならデス数の少ない順、それも同じなら <see cref="ScoreboardEntry.PlayerId"/> の小さい順にして、
    /// 一覧が届くたびに同点の行が入れ替わって見えないようにする。
    /// </summary>
    public static class ScoreboardRows
    {
        /// <summary>オフラインの自分の名前。</summary>
        public const string LocalPlayerName = "あなた";

        /// <summary><see cref="ScoreboardConnection.Connected"/> の表示。</summary>
        public const string ConnectedText = "接続中";

        /// <summary><see cref="ScoreboardConnection.Disconnected"/> の表示。</summary>
        public const string DisconnectedText = "切断";

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
        /// 並べる順。a を先に出すなら負。キルの多い順、デス数の少ない順、<see cref="ScoreboardEntry.PlayerId"/> の小さい順。
        /// </summary>
        public static int Compare(ScoreboardEntry a, ScoreboardEntry b)
        {
            var byKills = b.Kills.CompareTo(a.Kills);
            if (byKills != 0)
            {
                return byKills;
            }

            var byDeaths = a.Deaths.CompareTo(b.Deaths);
            if (byDeaths != 0)
            {
                return byDeaths;
            }

            return a.PlayerId.CompareTo(b.PlayerId);
        }

        /// <summary>オンラインの参加者の名前。</summary>
        /// <example><c>PlayerName(3)</c> は <c>Player 3</c></example>
        public static string PlayerName(int playerId)
        {
            return "Player " + playerId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>オフラインの標的の名前。number は 1 から数える。</summary>
        /// <example><c>TargetName(2)</c> は <c>標的 2</c></example>
        /// <exception cref="ArgumentOutOfRangeException">number が 1 未満。</exception>
        public static string TargetName(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return "標的 " + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>キル数・デス数の欄の文字列。</summary>
        /// <exception cref="ArgumentOutOfRangeException">count が負。</exception>
        public static string Count(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            return count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>接続状態の欄の文字列。<see cref="ScoreboardConnection.None"/> は空文字。</summary>
        /// <exception cref="ArgumentOutOfRangeException">connection が ScoreboardConnection の値でない。</exception>
        public static string ConnectionText(ScoreboardConnection connection)
        {
            switch (connection)
            {
                case ScoreboardConnection.None:
                    return string.Empty;
                case ScoreboardConnection.Connected:
                    return ConnectedText;
                case ScoreboardConnection.Disconnected:
                    return DisconnectedText;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connection), connection, "connection is not a valid ScoreboardConnection");
            }
        }
    }
}
