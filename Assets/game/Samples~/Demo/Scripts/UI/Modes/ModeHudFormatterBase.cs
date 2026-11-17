using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// モードの書式役 (<see cref="IModeHudFormatter"/>) の土台。残り時間・陣営ごとの得点・リスポーンまでの秒数・結果を
    /// <see cref="ModeStatus"/> の値から <see cref="ModeHudText"/>・<see cref="HudText"/> の文にし、派生はモード固有の行 (<see cref="AppendModeLines"/>) を足す。
    /// </summary>
    public abstract class ModeHudFormatterBase : IModeHudFormatter
    {
        /// <summary>残り時間。時間切れの無いモード (<see cref="ModeStatus.RemainingSeconds"/> が null) は空の文。</summary>
        /// <example>残り 65 秒なら <c>01:05</c></example>
        /// <exception cref="ArgumentNullException">status が null。</exception>
        public virtual LocalizedText Time(ModeStatus status)
        {
            var remaining = Require(status).RemainingSeconds;
            return remaining.HasValue ? LocalizedText.Literal(HudNumberFormat.RemainingTime(remaining.Value)) : default;
        }

        /// <summary>
        /// 陣営ごとの得点の行。自分の陣営 (<see cref="ModeStatus.OwnSide"/>) があればその行を先頭に、残りは <see cref="ModeStatus.SideScores"/> の順に並べ、
        /// 単独でいちばん得点の多い陣営に印を付ける。
        /// </summary>
        /// <exception cref="ArgumentNullException">status が null。</exception>
        public virtual IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            var sideScores = Require(status).SideScores;
            var leader = LeadingSide(sideScores);
            var lines = new List<ModeHudScoreLine>(sideScores.Count);
            foreach (var sideScore in sideScores)
            {
                var side = sideScore.Side;
                var line = new ModeHudScoreLine(side, ModeHudText.ScoreText(side, sideScore.Score), side == status.OwnSide, side == leader);
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

        /// <summary>リスポーンを待っていればその行を、続けてモード固有の行 (<see cref="AppendModeLines"/>) を lines に足す。</summary>
        /// <exception cref="ArgumentNullException">status か lines が null。</exception>
        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            Require(status);
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (status.RespawnSeconds.HasValue)
            {
                lines.Add(HudText.Respawn(HudFormat.DisplayedSeconds(status.RespawnSeconds.Value)));
            }

            AppendModeLines(status, lines);
        }

        /// <summary>試合が終わっていれば勝った陣営か引き分けの文、終わっていなければ空の文。</summary>
        /// <exception cref="ArgumentNullException">status が null。</exception>
        public virtual LocalizedText Result(ModeStatus status)
        {
            if (!Require(status).IsEnded)
            {
                return default;
            }

            return status.Winner.HasValue ? HudText.Wins(ModeHudText.SideName(status.Winner.Value)) : HudText.Draw;
        }

        /// <summary>リスポーンの行の後に、このモード固有の行を lines に足す。既定では何も足さない。</summary>
        protected virtual void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
        }

        private static ModeStatus Require(ModeStatus status)
        {
            return status ?? throw new ArgumentNullException(nameof(status));
        }

        private static Side? LeadingSide(IReadOnlyList<SideScore> sideScores)
        {
            Side? leader = null;
            var best = double.NegativeInfinity;
            var tied = false;
            foreach (var sideScore in sideScores)
            {
                if (sideScore.Score > best)
                {
                    best = sideScore.Score;
                    leader = sideScore.Side;
                    tied = false;
                }
                else if (sideScore.Score == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }
    }
}
