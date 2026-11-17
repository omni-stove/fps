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
                case HudNoticeKind.StatusEffect:
                    return HudFormat.StatusEffect(notice.KindId, notice.Seconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "notice kind is not a defined HudNoticeKind");
            }
        }

        public static LocalizedText Score(Team? team, int? score)
        {
            return team.HasValue && score.HasValue ? HudFormat.TeamScore(team.Value, score.Value) : default;
        }

        /// <summary>チームの結果があればそれを、無ければソロの試合の勝敗を出す。どちらも無ければ空の文。</summary>
        public static LocalizedText Result(bool hasResult, Team? winner, MatchResult? soloResult)
        {
            if (hasResult)
            {
                return HudFormat.Result(winner);
            }

            return soloResult.HasValue ? HudFormat.SoloResult(soloResult.Value) : default;
        }

        /// <summary>ソロの試合が終わっていて、やり直すキーがあるときだけ案内を出す。</summary>
        public static LocalizedText Restart(MatchResult? soloResult, string restartKey)
        {
            return soloResult.HasValue && !string.IsNullOrEmpty(restartKey) ? HudFormat.Restart(restartKey) : default;
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
