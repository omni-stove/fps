using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// エクスキューション (<see cref="ExecutionSession"/>) の HUD に出す文字列。得点の欄には両チームが取ったラウンドの数を、
    /// その下にラウンドの番号・待ちの残り・両チームの残り人数・自分のダウンやインタラクトの進みを出す。
    /// </summary>
    public static class ExecutionHudText
    {
        /// <example><c>Round(2, 3)</c> は <c>ラウンド 2 (3 本先取)</c></example>
        /// <exception cref="ArgumentOutOfRangeException">roundNumber か winsToWin が 1 未満。</exception>
        public static string Round(int roundNumber, int winsToWin)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "roundNumber must be at least 1");
            }

            if (winsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(winsToWin), winsToWin, "winsToWin must be at least 1");
            }

            return "ラウンド " + Number(roundNumber) + " (" + Number(winsToWin) + " 本先取)";
        }

        /// <summary>ラウンドが始まるまでの案内。秒数は切り上げた整数で受ける。</summary>
        /// <example><c>Preparation(3)</c> は <c>開始まで 3 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static string Preparation(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "開始まで " + Number(seconds) + " 秒";
        }

        /// <summary>前のラウンドの結果。winner が null なら引き分け。</summary>
        /// <example><c>RoundResult(Side.Alpha)</c> は <c>前のラウンド: ALPHA の勝ち</c></example>
        public static string RoundResult(Side? winner)
        {
            return "前のラウンド: " + (winner.HasValue ? ModeHudText.SideName(winner.Value) + " の勝ち" : HudFormat.Draw);
        }

        /// <summary>チームの残り人数。ダウン中がいればその数を添える。</summary>
        /// <example><c>Remaining(Side.Bravo, 3, 1)</c> は <c>BRAVO 残り 3 (ダウン 1)</c>、<c>Remaining(Side.Bravo, 3, 0)</c> は <c>BRAVO 残り 3</c></example>
        /// <exception cref="ArgumentOutOfRangeException">remaining か downed が負か、downed が remaining より多い。</exception>
        public static string Remaining(Side side, int remaining, int downed)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            if (downed < 0 || downed > remaining)
            {
                throw new ArgumentOutOfRangeException(nameof(downed), downed, "downed must be between 0 and remaining");
            }

            var text = ModeHudText.SideName(side) + " 残り " + Number(remaining);
            return downed == 0 ? text : text + " (ダウン " + Number(downed) + ")";
        }

        /// <summary>敵にとどめを刺している人の案内。</summary>
        /// <example><c>Executing(40)</c> は <c>とどめ 40%</c></example>
        /// <exception cref="ArgumentOutOfRangeException">percent が 0〜100 の外。</exception>
        public static string Executing(int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "percent must be between 0 and 100");
            }

            return "とどめ " + Number(percent) + "%";
        }

        /// <summary>
        /// 得点の欄に出す、両チームが取ったラウンドの数。ownSide があればその行を先頭にし、単独で多いチームを先頭に立っているとする。
        /// </summary>
        public static IReadOnlyList<ModeHudScoreLine> RoundWins(ExecutionSession session, Side? ownSide)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var alpha = session.Round.GetRoundWins(Side.Alpha);
            var bravo = session.Round.GetRoundWins(Side.Bravo);
            Side? leader = alpha == bravo ? (Side?)null : alpha > bravo ? Side.Alpha : Side.Bravo;
            var lines = new List<ModeHudScoreLine>(2);
            foreach (var side in new[] { Side.Alpha, Side.Bravo })
            {
                var wins = side == Side.Alpha ? alpha : bravo;
                var line = new ModeHudScoreLine(side, ModeHudText.ScoreText(side, wins), side == ownSide, side == leader);
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

        /// <summary>
        /// 得点の下に出す行。ラウンドの番号、待ちの間は前のラウンドの結果と開始までの秒数、両チームの残り人数 (自分のチームを先に)、
        /// localPlayerId がダウン中ならその案内 (<see cref="HudFormat.Downed(int, int)"/>)、interactTargetId を起こしているかとどめを刺しているならその進み。
        /// </summary>
        /// <param name="session">読むモード。</param>
        /// <param name="localPlayerId">この画面で操作している人。いなければ null。</param>
        /// <param name="interactTargetId">localPlayerId がいまインタラクトを押している相手。押していなければ null。</param>
        public static IReadOnlyList<string> Lines(ExecutionSession session, int? localPlayerId, int? interactTargetId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var round = session.Round;
            var lines = new List<string>();
            if (round.RoundNumber >= 1)
            {
                lines.Add(Round(round.RoundNumber, round.WinsToWin));
            }

            if (round.Phase == RoundPhase.Preparing)
            {
                if (session.HasFinishedRound)
                {
                    lines.Add(RoundResult(session.LastRoundWinner));
                }

                lines.Add(Preparation(HudFormat.DisplayedSeconds(round.PreparationRemaining)));
            }

            var mode = session.Mode;
            var local = localPlayerId.HasValue && mode.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            var ownSide = local.HasValue ? mode.GetSide(local.Value) : (Side?)null;
            var first = ownSide ?? Side.Alpha;
            var second = first == Side.Alpha ? Side.Bravo : Side.Alpha;
            lines.Add(Remaining(first, session.CountRemaining(first), session.CountDowned(first)));
            lines.Add(Remaining(second, session.CountRemaining(second), session.CountDowned(second)));

            if (local.HasValue && session.IsDowned(local.Value))
            {
                var bleedout = session.GetBleedoutRemaining(local.Value) ?? 0f;
                lines.Add(HudFormat.Downed(HudFormat.DisplayedSeconds(bleedout), HudFormat.RevivePercent(session.GetReviveProgress(local.Value))));
            }
            else if (local.HasValue && interactTargetId.HasValue && session.IsDowned(interactTargetId.Value) && mode.HasPlayer(interactTargetId.Value))
            {
                var target = interactTargetId.Value;
                lines.Add(mode.GetSide(target) == ownSide
                    ? HudFormat.Reviving(HudFormat.RevivePercent(session.GetReviveProgress(target)))
                    : Executing(HudFormat.RevivePercent(session.GetExecuteProgress(target))));
            }

            return lines;
        }

        /// <summary>試合が終わったときの結果。ラウンドの終わりでは出さず、先取数に届いたときだけ出す。</summary>
        /// <example>終わっていれば <c>ALPHA の勝ち</c>、終わっていなければ <see cref="HudFormat.NoResult"/></example>
        public static string Result(ExecutionSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var round = session.Round;
            if (round.Phase != RoundPhase.Ended || !round.Winner.HasValue)
            {
                return HudFormat.NoResult;
            }

            return ModeHudText.SideName(round.Winner.Value) + " の勝ち";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
