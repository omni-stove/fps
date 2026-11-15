using System;

namespace Fps.Core
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
    }

    // 文や LocalizedText で持たないのは、Core から文言を出さず、何をどう言うかを UI に任せるため。
    public readonly struct HudNotice : IEquatable<HudNotice>
    {
        public HudNoticeKind Kind { get; }

        public int Seconds { get; }

        public int RevivePercent { get; }

        private HudNotice(HudNoticeKind kind, int seconds, int revivePercent)
        {
            Kind = kind;
            Seconds = seconds;
            RevivePercent = revivePercent;
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
            return Kind == other.Kind && Seconds == other.Seconds && RevivePercent == other.RevivePercent;
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
            return HashCode.Combine(Kind, Seconds, RevivePercent);
        }

        public override string ToString()
        {
            return Kind + (Seconds != 0 ? " " + Seconds + "s" : string.Empty) + (RevivePercent != 0 ? " " + RevivePercent + "%" : string.Empty);
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
