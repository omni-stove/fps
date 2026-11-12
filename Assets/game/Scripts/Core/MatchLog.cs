using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチの <c>[Match]</c> 付きログの文言。サーバーとクライアントで同じ書式にし、
    /// 2 つのログを突き合わせてスコアが合うか確かめられるようにする。
    /// ソロの試合 (<see cref="Match"/>) の状態ログ (<see cref="SoloState"/>) も同じタグで出す。
    /// </summary>
    public static class MatchLog
    {
        public const string Tag = "[Match]";

        /// <summary>引き分けで終わったときに winner= に書く値。</summary>
        public const string Draw = "draw";

        /// <example><c>[Match] score A=3 B=1</c></example>
        public static string Score(int alphaScore, int bravoScore)
        {
            return Tag + " score A=" + alphaScore.ToString(CultureInfo.InvariantCulture)
                + " B=" + bravoScore.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] ended winner=Alpha</c>、引き分けなら <c>[Match] ended winner=draw</c></example>
        public static string Ended(Team? winner)
        {
            return Tag + " ended winner=" + (winner.HasValue ? TeamName(winner.Value) : Draw);
        }

        /// <example><c>[Match] started players=2</c></example>
        public static string Started(int playerCount)
        {
            return Tag + " started players=" + playerCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] joined player=1 team=Bravo</c></example>
        public static string Joined(int playerId, Team team)
        {
            return Tag + " joined player=" + playerId.ToString(CultureInfo.InvariantCulture) + " team=" + TeamName(team);
        }

        /// <example><c>[Match] left player=1</c></example>
        public static string Left(int playerId)
        {
            return Tag + " left player=" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] kill killer=0 victim=1</c></example>
        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + killerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] spawn player=1 at=(0.00, 0.00, 20.00)</c></example>
        public static string Spawn(int playerId, SpawnPoint point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            var p = point.Position;
            return Tag + " spawn player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " at=(" + p.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + p.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + p.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>
        /// ソロの試合 (<see cref="Match"/>) の状態。終了後は state に勝敗 (Won / Lost) を、それまでは進行状態を書く。
        /// remaining は残り時間 (秒、小数 1 桁)。
        /// </summary>
        /// <example><c>[Match] state=Won kills=10 remaining=12.3</c></example>
        public static string SoloState(Match match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            return Tag + " state=" + SoloStateName(match)
                + " kills=" + match.Kills.ToString(CultureInfo.InvariantCulture)
                + " remaining=" + match.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] restart</c></example>
        public static string Restart()
        {
            return Tag + " restart";
        }

        /// <summary>試合が終わった後、次の試合が始まるまでの秒数 (小数 1 桁)。</summary>
        /// <example><c>[Match] next match in=10.0s</c></example>
        public static string NextMatch(float delaySeconds)
        {
            if (!(delaySeconds >= 0f) || float.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "delaySeconds must not be negative and must be finite");
            }

            return Tag + " next match in=" + delaySeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }

        private static string SoloStateName(Match match)
        {
            switch (match.State)
            {
                case GameModeState.Waiting:
                    return "Waiting";
                case GameModeState.Playing:
                    return "Playing";
                case GameModeState.Ended:
                    return match.Result == MatchResult.Won ? "Won" : "Lost";
                default:
                    throw new ArgumentOutOfRangeException(nameof(match), match.State, "match has an unknown state");
            }
        }

        private static string TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return "Alpha";
                case Team.Bravo:
                    return "Bravo";
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }
    }
}
