using System;

namespace Shooter.Core
{
    public enum HudNoticeKind
    {
        None,
        CheckpointSaved,
        CheckpointRestart,
        NextMatch,
        IdleWarning,
        Downed,
        Respawn,
        NoRespawnUntilNextMatch,
        SpawnProtected,
        StatusEffect,
    }

    // 文や文言のキーで持たないのは、Core から文言を出さず、何をどう言うかを UI に任せるため。
    public readonly struct HudNotice : IEquatable<HudNotice>
    {
        public HudNoticeKind Kind { get; }

        public int Seconds { get; }

        public int RevivePercent { get; }

        public string KindId => _kindId ?? string.Empty;

        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _kindId;

        private HudNotice(HudNoticeKind kind, int seconds, int revivePercent, string kindId = null)
        {
            Kind = kind;
            Seconds = seconds;
            RevivePercent = revivePercent;
            _kindId = kindId;
        }

        public static HudNotice None => default;

        public bool IsNone => Kind == HudNoticeKind.None;

        public static HudNotice CheckpointSaved => new HudNotice(HudNoticeKind.CheckpointSaved, 0, 0);

        public static HudNotice NoRespawnUntilNextMatch => new HudNotice(HudNoticeKind.NoRespawnUntilNextMatch, 0, 0);

        public static HudNotice CheckpointRestart(int seconds)
        {
            return WithSeconds(HudNoticeKind.CheckpointRestart, seconds);
        }

        public static HudNotice NextMatch(int seconds)
        {
            return WithSeconds(HudNoticeKind.NextMatch, seconds);
        }

        public static HudNotice IdleWarning(int seconds)
        {
            return WithSeconds(HudNoticeKind.IdleWarning, seconds);
        }

        public static HudNotice Respawn(int seconds)
        {
            return WithSeconds(HudNoticeKind.Respawn, seconds);
        }

        public static HudNotice SpawnProtected(int seconds)
        {
            return WithSeconds(HudNoticeKind.SpawnProtected, seconds);
        }

        public static HudNotice StatusEffect(string kindId, int seconds)
        {
            if (string.IsNullOrEmpty(kindId))
            {
                throw new ArgumentException("kind id must not be empty", nameof(kindId));
            }

            CheckSeconds(seconds);
            return new HudNotice(HudNoticeKind.StatusEffect, seconds, 0, kindId);
        }

        public static HudNotice Downed(int seconds, int revivePercent)
        {
            CheckSeconds(seconds);
            if (revivePercent < 0 || revivePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(revivePercent), revivePercent, "revivePercent must be between 0 and 100");
            }

            return new HudNotice(HudNoticeKind.Downed, seconds, revivePercent);
        }

        public static HudNotice ForRespawn(RespawnMode mode, int seconds)
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

        public bool Equals(HudNotice other)
        {
            return Kind == other.Kind && Seconds == other.Seconds && RevivePercent == other.RevivePercent
                && string.Equals(KindId, other.KindId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is HudNotice other && Equals(other);
        }

        public static bool operator ==(HudNotice left, HudNotice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HudNotice left, HudNotice right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Seconds, RevivePercent, KindId);
        }

        public override string ToString()
        {
            return Kind + (KindId.Length > 0 ? " " + KindId : string.Empty) + (Seconds != 0 ? " " + Seconds + "s" : string.Empty) + (RevivePercent != 0 ? " " + RevivePercent + "%" : string.Empty);
        }

        private static HudNotice WithSeconds(HudNoticeKind kind, int seconds)
        {
            CheckSeconds(seconds);
            return new HudNotice(kind, seconds, 0);
        }

        private static void CheckSeconds(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }
        }
    }
}
