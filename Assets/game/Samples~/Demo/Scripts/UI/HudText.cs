using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
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
                    return CheckpointSaved;
                case HudNoticeKind.CheckpointRestart:
                    return CheckpointRestart(notice.Seconds);
                case HudNoticeKind.NextMatch:
                    return NextMatch(notice.Seconds);
                case HudNoticeKind.IdleWarning:
                    return IdleWarning(notice.Seconds);
                case HudNoticeKind.Downed:
                    return Downed(notice.Seconds, notice.RevivePercent);
                case HudNoticeKind.Respawn:
                    return Respawn(notice.Seconds);
                case HudNoticeKind.NoRespawnUntilNextMatch:
                    return NoRespawnUntilNextMatch;
                case HudNoticeKind.SpawnProtected:
                    return SpawnProtected(notice.Seconds);
                case HudNoticeKind.StatusEffect:
                    return StatusEffect(notice.KindId, notice.Seconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "notice kind is not a defined HudNoticeKind");
            }
        }

        public static LocalizedText Score(Team? team, int? score)
        {
            return team.HasValue && score.HasValue ? TeamScore(team.Value, score.Value) : default;
        }

        public static LocalizedText Result(bool hasResult, Team? winner, MatchOutcome? soloResult)
        {
            if (hasResult)
            {
                return Result(winner);
            }

            return soloResult.HasValue ? SoloResult(soloResult.Value) : default;
        }

        public static LocalizedText Restart(MatchOutcome? soloResult, string restartKey)
        {
            return soloResult.HasValue && !string.IsNullOrEmpty(restartKey) ? Restart(restartKey) : default;
        }

        public static LocalizedText WeaponName(WeaponSpec spec)
        {
            return spec != null ? DisplayNames.Of(spec) : default;
        }

        public static LocalizedText AmmoType(AmmoTypeSpec ammoType)
        {
            return ammoType != null ? DisplayNames.Of(ammoType) : default;
        }

        public const string StatusEffectKeyPrefix = "hud.status.";

        public static LocalizedText NextMatch(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.nextMatch", seconds);
        }

        public static LocalizedText TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return new LocalizedText("hud.team.alpha");
                case Team.Bravo:
                    return new LocalizedText("hud.team.bravo");
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        public static LocalizedText Score(LocalizedText name, int score)
        {
            return new LocalizedText("hud.score", name, score);
        }

        public static LocalizedText TeamScore(Team team, int score)
        {
            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), score, "score must not be negative");
            }

            return Score(TeamName(team), score);
        }

        public static LocalizedText Draw => new LocalizedText("hud.draw");

        public static LocalizedText Result(Team? winner)
        {
            return winner.HasValue ? Wins(TeamName(winner.Value)) : Draw;
        }

        public static LocalizedText SoloResult(MatchOutcome result)
        {
            switch (result)
            {
                case MatchOutcome.Won:
                    return new LocalizedText("hud.solo.won");
                case MatchOutcome.Lost:
                    return new LocalizedText("hud.solo.lost");
                case MatchOutcome.Draw:
                    return Draw;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "result is not a defined MatchOutcome");
            }
        }

        public static LocalizedText Restart(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            return new LocalizedText("hud.solo.restart", key);
        }

        public static LocalizedText Wins(LocalizedText sideName)
        {
            return new LocalizedText("hud.wins", sideName);
        }

        public static LocalizedText Respawn(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.respawn", seconds);
        }

        public static LocalizedText NoRespawnUntilNextMatch => new LocalizedText("hud.noRespawnUntilNextMatch");

        public static LocalizedText RespawnNotice(RespawnMode mode, int seconds)
        {
            switch (mode)
            {
                case RespawnMode.Individual:
                case RespawnMode.Wave:
                    return Respawn(seconds);
                case RespawnMode.None:
                    return NoRespawnUntilNextMatch;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined RespawnMode");
            }
        }

        public static LocalizedText Downed(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.downed", seconds);
        }

        public static LocalizedText Downed(int seconds, int revivePercent)
        {
            CheckPercent(revivePercent);
            var text = Downed(seconds);
            return revivePercent == 0 ? text : new LocalizedText("hud.downedWithRevive", text, revivePercent);
        }

        public static LocalizedText Reviving(int percent)
        {
            CheckPercent(percent);
            return new LocalizedText("hud.reviving", percent);
        }

        public static LocalizedText SpawnProtected(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.spawnProtected", seconds);
        }

        public static LocalizedText IdleWarning(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.idleWarning", seconds);
        }

        public static LocalizedText StatusEffect(string kindId, int seconds)
        {
            if (string.IsNullOrEmpty(kindId))
            {
                throw new ArgumentException("kind id must not be empty", nameof(kindId));
            }

            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText(StatusEffectKeyPrefix + kindId, seconds);
        }

        private static void CheckPercent(int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "percent must be between 0 and 100");
            }
        }

        public static LocalizedText CheckpointSaved => new LocalizedText("campaign.checkpoint.saved");

        public static LocalizedText CheckpointRestart(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("campaign.checkpoint.restart", seconds);
        }

        public static LocalizedText Grenades(int remaining)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            return new LocalizedText("hud.grenades", remaining);
        }

        public static LocalizedText Grenades(LocalizedText typeName, int remaining)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            return new LocalizedText("hud.grenadesOfType", typeName, remaining);
        }

        // slotNumber を 1 から数えるのは、入力の Ability1 / Ability2 に合わせるため。
        public static LocalizedText Ability(int slotNumber, AbilitySlot slot)
        {
            if (slotNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), slotNumber, "slotNumber must be at least 1");
            }

            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            var charges = slot.Charges;
            var max = slot.Spec.MaxCharges;
            if (slot.IsRunning)
            {
                return new LocalizedText("hud.ability.active", slotNumber, charges, max);
            }

            var wait = slot.SecondsUntilReady;
            if (wait > 0f && !float.IsInfinity(wait))
            {
                return new LocalizedText("hud.ability.wait", slotNumber, charges, max, AbilityWaitSeconds(wait));
            }

            return new LocalizedText("hud.ability", slotNumber, charges, max);
        }

        public static string AbilityWaitSeconds(float seconds)
        {
            return (HudFormat.AbilityWaitTenths(seconds) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
