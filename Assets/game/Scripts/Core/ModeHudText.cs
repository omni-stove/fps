using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// オフラインのモードの HUD (陣営ごとの得点・残り時間・結果) に出す文字列。
    /// Alpha と Bravo にあたる陣営は既存の HUD と同じチーム名 (<see cref="HudFormat.TeamName"/>) で、それ以外は <c>SIDE n</c> で書く。
    /// </summary>
    public static class ModeHudText
    {
        /// <summary>Alpha と Bravo 以外の陣営名の頭。</summary>
        public const string SidePrefix = "SIDE ";

        /// <example><c>SideName(Side.Alpha)</c> は <c>ALPHA</c>、<c>SideName(new Side(2))</c> は <c>SIDE 2</c></example>
        public static string SideName(Side side)
        {
            return side.TryGetTeam(out var team)
                ? HudFormat.TeamName(team)
                : SidePrefix + side.Id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// HUD に出す得点。拠点のように時間で入る点は小数になるので、届いていない点を出さないよう切り捨てる。
        /// 負と NaN は 0、int に収まらない値は <see cref="int.MaxValue"/> にする。
        /// </summary>
        public static int DisplayScore(double score)
        {
            if (!(score > 0d))
            {
                return 0;
            }

            var floored = Math.Floor(score);
            return floored >= int.MaxValue ? int.MaxValue : (int)floored;
        }

        /// <example><c>ScoreText(new Side(2), 3.7d)</c> は <c>SIDE 2 3</c></example>
        public static string ScoreText(Side side, double score)
        {
            return SideName(side) + " " + DisplayScore(score).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>残り時間。<see cref="HudFormat.Time"/> と同じ書式。</summary>
        public static string Time(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return HudFormat.Time(session.RemainingSeconds);
        }

        /// <summary>
        /// session の陣営ごとの得点の行。ownSide があれば (session の陣営にあれば) その行を先頭に、残りは陣営の ID の昇順に並べる。
        /// </summary>
        /// <param name="session">得点を読むモード。</param>
        /// <param name="ownSide">自分の陣営。分からなければ null。</param>
        public static IReadOnlyList<ModeHudScoreLine> Scores(ModeSession session, Side? ownSide)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sides = session.Sides;
            var leader = session.LeadingSide;
            var lines = new List<ModeHudScoreLine>(sides.Count);
            foreach (var side in sides)
            {
                var line = new ModeHudScoreLine(side, ScoreText(side, session.GetScore(side)), side == ownSide, side == leader);
                if (line.IsOwn)
                {
                    lines.Insert(0, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>試合が終わったときの結果。終わっていなければ <see cref="HudFormat.NoResult"/>。</summary>
        /// <example><c>Result(session)</c> は <c>SIDE 2 の勝ち</c>、引き分けなら <c>引き分け</c></example>
        public static string Result(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (session.State != GameModeState.Ended)
            {
                return HudFormat.NoResult;
            }

            return session.Winner.HasValue ? SideName(session.Winner.Value) + " の勝ち" : HudFormat.Draw;
        }
    }
}
