using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// オフラインのモードの HUD (陣営ごとの得点・残り時間・結果) に出す文。表のキー <c>hud.*</c> と引数の <see cref="LocalizedText"/> で返す。
    /// Alpha と Bravo にあたる陣営は既存の HUD と同じチーム名 (<see cref="HudText.TeamName"/>) で、それ以外は <c>SIDE n</c> で書く。
    /// 陣営名 (<see cref="SideName"/>) は、ほかのモードの文が引数に入れて使う。
    /// </summary>
    public static class ModeHudText
    {
        /// <example><c>SideName(Side.Alpha)</c> は <c>ALPHA</c>、<c>SideName(new Side(2))</c> は <c>SIDE 2</c></example>
        public static LocalizedText SideName(Side side)
        {
            return side.TryGetTeam(out var team)
                ? HudText.TeamName(team)
                : new LocalizedText("hud.side", side.Id);
        }

        /// <summary>時間で入る点は小数になるので、届いていない点を出さないよう <see cref="HudFormat.DisplayedScore"/> で切り捨てて出す。</summary>
        /// <example><c>ScoreText(new Side(2), 3.7d)</c> は <c>SIDE 2 3</c></example>
        public static LocalizedText ScoreText(Side side, double score)
        {
            return HudText.Score(SideName(side), HudFormat.DisplayedScore(score));
        }

        /// <summary>陣営の残り人数。</summary>
        /// <example><c>Remaining(Side.Bravo, 3)</c> は <c>BRAVO 残り 3</c></example>
        public static LocalizedText Remaining(Side side, int remaining)
        {
            return new LocalizedText("hud.mode.remaining", SideName(side), remaining);
        }

        /// <summary>人数の行 text に、ダウン中の人数 downed を添える。</summary>
        /// <example><c>WithDowned(Remaining(Side.Bravo, 3), 1)</c> は <c>BRAVO 残り 3 (ダウン 1)</c></example>
        public static LocalizedText WithDowned(LocalizedText text, int downed)
        {
            return new LocalizedText("hud.mode.withDowned", text, downed);
        }

        /// <summary>残り時間。<see cref="HudNumberFormat.RemainingTime"/> と同じ書式で、言語に依らないので表を引かずにそのまま運ぶ。</summary>
        public static LocalizedText Time(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return LocalizedText.Literal(HudNumberFormat.RemainingTime(session.RemainingSeconds));
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

        /// <summary>試合が終わったときの結果。終わっていなければ <c>default</c> (空の文)。</summary>
        /// <example><c>Result(session)</c> は <c>SIDE 2 の勝ち</c>、引き分けなら <c>引き分け</c></example>
        public static LocalizedText Result(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (session.State != GameModeState.Ended)
            {
                return default;
            }

            return session.Winner.HasValue ? HudText.Wins(SideName(session.Winner.Value)) : HudText.Draw;
        }
    }
}
