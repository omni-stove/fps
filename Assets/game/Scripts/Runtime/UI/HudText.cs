using System;
using Fps.Core;

namespace Fps.Runtime
{
    public static class HudText
    {
        public static LocalizedText Notice(HudNotice notice)
        {
            switch (notice.Kind)
            {
                case HudNoticeKind.None:
                    return default;
                case HudNoticeKind.CheckpointSaved:
                    return HudFormat.CheckpointSaved;
                case HudNoticeKind.CheckpointRestart:
                    return HudFormat.CheckpointRestart(notice.Seconds);
                case HudNoticeKind.NextMatch:
                    return HudFormat.NextMatch(notice.Seconds);
                case HudNoticeKind.IdleWarning:
                    return HudFormat.IdleWarning(notice.Seconds);
                case HudNoticeKind.Downed:
                    return HudFormat.Downed(notice.Seconds, notice.RevivePercent);
                case HudNoticeKind.Respawn:
                    return HudFormat.Respawn(notice.Seconds);
                case HudNoticeKind.NoRespawnUntilNextMatch:
                    return HudFormat.NoRespawnUntilNextMatch;
                case HudNoticeKind.SpawnProtected:
                    return HudFormat.SpawnProtected(notice.Seconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "notice kind is not a defined HudNoticeKind");
            }
        }

        public static LocalizedText Score(Team? team, int? score)
        {
            return team.HasValue && score.HasValue ? HudFormat.TeamScore(team.Value, score.Value) : default;
        }

        public static LocalizedText Result(bool hasResult, Team? winner)
        {
            return hasResult ? HudFormat.Result(winner) : default;
        }

        public static LocalizedText WeaponName(WeaponSpec spec)
        {
            return spec != null ? spec.Name : default;
        }

        public static LocalizedText AmmoType(AmmoTypeSpec ammoType)
        {
            return ammoType != null ? ammoType.Name : default;
        }
    }
}
