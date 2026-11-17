using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガーディアンの HUD の行 (表のキー <c>mode.guardian.*</c> の <see cref="LocalizedText"/>)。
    /// </summary>
    public sealed class GuardianHudFormatter : IModeHudFormatter
    {
        public static LocalizedText Alive => new LocalizedText("mode.guardian.alive");
        public static LocalizedText Fallen => new LocalizedText("mode.guardian.fallen");

        /// <summary>自分がリーダーのときに出す行。</summary>
        public static LocalizedText YouAreLeader => new LocalizedText("mode.guardian.youAreLeader");

        /// <summary>自分の陣営のリーダーが倒れていて、自分も倒れているときに出す行。</summary>
        public static LocalizedText NoRespawnLeaderDown => new LocalizedText("mode.guardian.noRespawnLeaderDown");

        /// <example><c>Leader(Side.Alpha, false)</c> は <c>ALPHA リーダー 生存</c></example>
        public static LocalizedText Leader(Side side, bool fallen)
        {
            return new LocalizedText("mode.guardian.leader", ModeHudText.SideName(side), fallen ? Fallen : Alive);
        }

        /// <summary>
        /// リーダーのいる陣営ごとの生死の行。ownSide があればその行を先頭に、残りは陣営の ID の昇順に並べる。
        /// </summary>
        public static IReadOnlyList<LocalizedText> Leaders(GuardianRespawn leaders, Side? ownSide)
        {
            if (leaders == null)
            {
                throw new ArgumentNullException(nameof(leaders));
            }

            var lines = new List<LocalizedText>();
            foreach (var side in leaders.LeaderSides)
            {
                var line = Leader(side, leaders.HasLeaderFallen(side));
                if (side == ownSide)
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

        public LocalizedText Time(ModeStatus status) => ModeStatusFormat.Remaining(status);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ModeStatusFormat.Scores(status);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            ModeStatusFormat.AppendRespawn(status, lines);
            if (!(status.Detail is GuardianStatus detail))
            {
                return;
            }

            var leaders = detail.Leaders;
            if (detail.LocalPlayerId.HasValue && status.OwnSide.HasValue)
            {
                var side = status.OwnSide.Value;
                if (leaders.GetLeader(side) == detail.LocalPlayerId && !leaders.HasLeaderFallen(side))
                {
                    lines.Add(YouAreLeader);
                }
                else if (detail.IsLocalPlayerEliminated && leaders.HasLeaderFallen(side))
                {
                    lines.Add(NoRespawnLeaderDown);
                }
            }

            lines.AddRange(Leaders(leaders, status.OwnSide));
        }

        public LocalizedText Result(ModeStatus status) => ModeStatusFormat.Result(status);

        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefault()
        {
            ModeHudFormatters.Register<GuardianStatus>(new GuardianHudFormatter());
        }
    }
}
