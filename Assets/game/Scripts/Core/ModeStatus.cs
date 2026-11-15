using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// オフラインのモードの今の状態。モードの Runner が状態の変わるたびに作り直して UI に渡し、UI がこれを読んで何をどう出すかを決める。
    /// モード固有の状態は <see cref="Detail"/> に、そのモードの Core の状態 (例: <see cref="SearchAndDestroy"/>・<see cref="Hardpoint"/>) をそのまま入れる。
    /// </summary>
    /// <remarks>
    /// 文を作る役を UI に移し終えるまでの間、Runner が今作っている文 (<see cref="Time"/>・<see cref="Scores"/>・<see cref="Lines"/>・<see cref="Result"/>) も一緒に運ぶ。
    /// <see cref="Detail"/> の型の書式役が UI に無いモードは、この文をそのまま出す。
    /// </remarks>
    public sealed class ModeStatus
    {
        /// <param name="remainingSeconds">残り時間 (秒)。時間切れの無いモードは null。負と NaN は受け付けない。</param>
        /// <param name="sideScores">陣営ごとの得点。null は空。</param>
        /// <param name="ownSide">この画面のプレイヤーの陣営。いなければ null。</param>
        /// <param name="respawnSeconds">この画面のプレイヤーが戻るまでの秒数。待っていなければ null。負と NaN は受け付けない。</param>
        /// <param name="isEnded">試合が終わったか。</param>
        /// <param name="winner">勝った陣営。終わる前と引き分けは null。</param>
        /// <param name="detail">モード固有の状態。無ければ null。</param>
        /// <param name="time">今の文の残り時間。</param>
        /// <param name="scores">今の文の得点の行 (上から)。null は空。</param>
        /// <param name="lines">今の文の、得点の下に足す行。null は空。</param>
        /// <param name="result">今の文の結果。空の文なら結果を出さない。</param>
        /// <exception cref="ArgumentOutOfRangeException">remainingSeconds か respawnSeconds が負か NaN。</exception>
        public ModeStatus(
            float? remainingSeconds,
            IReadOnlyList<SideScore> sideScores,
            Side? ownSide,
            float? respawnSeconds,
            bool isEnded,
            Side? winner,
            object detail,
            LocalizedText time,
            IReadOnlyList<ModeHudScoreLine> scores,
            IReadOnlyList<LocalizedText> lines,
            LocalizedText result)
        {
            RemainingSeconds = RequireNonNegative(remainingSeconds, nameof(remainingSeconds));
            RespawnSeconds = RequireNonNegative(respawnSeconds, nameof(respawnSeconds));
            SideScores = sideScores ?? Array.Empty<SideScore>();
            OwnSide = ownSide;
            IsEnded = isEnded;
            Winner = winner;
            Detail = detail;
            Time = time;
            Scores = scores ?? Array.Empty<ModeHudScoreLine>();
            Lines = lines ?? Array.Empty<LocalizedText>();
            Result = result;
        }

        /// <summary>残り時間 (秒)。時間切れの無いモードは null。</summary>
        public float? RemainingSeconds { get; }

        /// <summary>陣営ごとの得点。並びはモードの陣営の順 (<see cref="ModeSession.Sides"/>)。</summary>
        public IReadOnlyList<SideScore> SideScores { get; }

        /// <summary>この画面のプレイヤーの陣営。いなければ null。</summary>
        public Side? OwnSide { get; }

        /// <summary>この画面のプレイヤーが戻るまでの秒数。待っていなければ null。</summary>
        public float? RespawnSeconds { get; }

        /// <summary>試合が終わったか。ラウンド制のモードのラウンドの終わりでは false のまま。</summary>
        public bool IsEnded { get; }

        /// <summary>勝った陣営。終わる前と引き分けは null。</summary>
        public Side? Winner { get; }

        /// <summary>モード固有の状態 (そのモードの Core の状態)。無ければ null。UI は型ごとの書式役でこれを文にする。</summary>
        public object Detail { get; }

        /// <summary>Runner が今作っている残り時間の文。</summary>
        public LocalizedText Time { get; }

        /// <summary>Runner が今作っている得点の行 (上から)。</summary>
        public IReadOnlyList<ModeHudScoreLine> Scores { get; }

        /// <summary>Runner が今作っている、得点の下に足す行。</summary>
        public IReadOnlyList<LocalizedText> Lines { get; }

        /// <summary>Runner が今作っている結果の文。空の文なら結果を出さない。</summary>
        public LocalizedText Result { get; }

        public override string ToString()
        {
            return "remaining=" + (RemainingSeconds.HasValue ? RemainingSeconds.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-")
                + " sides=" + SideScores.Count
                + " own=" + (OwnSide.HasValue ? OwnSide.Value.ToString() : "-")
                + " ended=" + IsEnded
                + " winner=" + (Winner.HasValue ? Winner.Value.ToString() : "-")
                + " detail=" + (Detail != null ? Detail.GetType().Name : "-");
        }

        private static float? RequireNonNegative(float? seconds, string paramName)
        {
            if (seconds.HasValue && !(seconds.Value >= 0f))
            {
                throw new ArgumentOutOfRangeException(paramName, seconds.Value, paramName + " must be zero or positive");
            }

            return seconds;
        }
    }
}
