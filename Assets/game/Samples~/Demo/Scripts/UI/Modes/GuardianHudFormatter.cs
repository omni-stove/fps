using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class GuardianHudFormatter : IModeHudFormatter
    {
        public static LocalizedText Alive => new LocalizedText("mode.guardian.alive");
        public static LocalizedText Fallen => new LocalizedText("mode.guardian.fallen");

        public static LocalizedText YouAreLeader => new LocalizedText("mode.guardian.youAreLeader");

        public static LocalizedText NoRespawnLeaderDown => new LocalizedText("mode.guardian.noRespawnLeaderDown");

        public static LocalizedText Leader(Side side, bool fallen)
        {
            return new LocalizedText("mode.guardian.leader", ModeHudText.SideName(side), fallen ? Fallen : Alive);
        }

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
