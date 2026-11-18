using System.Globalization;

namespace Shooter.NetV2
{
    public static class BlockLog
    {
        public const string Tag = "[Block]";

        public static string Loaded(int blocked, int muted)
        {
            return Tag + " loaded blocked=" + Number(blocked) + " muted=" + Number(muted);
        }

        public static string Changed(BlockKind kind, int participantId, bool on, bool remembered)
        {
            return Tag + " " + Name(kind) + "=" + (on ? "on" : "off") + " participant=" + Number(participantId)
                + (remembered ? " saved" : " this match only (no user id for the participant)");
        }

        public static string Remembered(BlockKind kind, int participantId)
        {
            return Tag + " " + Name(kind) + " participant=" + Number(participantId) + " now has a user id; saved";
        }

        public static string RefusedSelf(BlockKind kind, int participantId)
        {
            return Tag + " " + Name(kind) + " refused participant=" + Number(participantId) + " (the local player)";
        }

        public static string RefusedInvalid(BlockKind kind, int participantId)
        {
            return Tag + " " + Name(kind) + " refused participant=" + Number(participantId) + " (not a participant id)";
        }

        public static string SaveFailed(string reason)
        {
            return Tag + " could not save the block list: " + reason;
        }

        private static string Name(BlockKind kind)
        {
            return kind == BlockKind.Block ? "block" : "mute";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
