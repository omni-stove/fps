using System.Globalization;

namespace Shooter.Core
{
    public static class ReplayLog
    {
        public const string Tag = "[Replay]";

        public static string RecordingStarted(ReplayHeader header, int pending)
        {
            return Tag + " recording " + header + " (" + Count(pending) + " events before the match was accepted)";
        }

        public static string RecordingAbandoned(string reason)
        {
            return Tag + " not recording this match: " + reason;
        }

        public static string RecordingFailed(string message)
        {
            return Tag + " stopped recording; the match goes on without it: " + message;
        }

        public static string RecordingTo(string path)
        {
            return Tag + " recording this match to " + path;
        }

        public static string CannotRecord(string path, string message)
        {
            return Tag + " not recording this match: cannot open " + path + " (" + message + ")";
        }

        public static string DiscardedEmpty(string path)
        {
            return Tag + " removed " + path + " because nothing was recorded";
        }

        public static string DiscardFailed(string path, string message)
        {
            return Tag + " could not remove the empty recording " + path + " (" + message + ")";
        }

        public static string RecordingFinished(int records, int durationTicks, int tickRate)
        {
            return Tag + " recorded " + Count(records) + " events over " + Seconds(durationTicks, tickRate) + " s";
        }

        public static string PlaybackStarted(ReplayHeader header)
        {
            return Tag + " playing " + header;
        }

        public static string PlaybackEnded(int records, bool truncated)
        {
            return Tag + " playback reached the end after " + Count(records) + " events"
                + (truncated ? " (the file was cut off; it was likely recorded when the game stopped)" : string.Empty);
        }

        public static string PlaybackFailed(string message)
        {
            return Tag + " cannot read the rest of the replay: " + message;
        }

        public static string PlaybackStopped(string reason)
        {
            return Tag + " playback stopped" + (string.IsNullOrEmpty(reason) ? string.Empty : " (" + reason + ")");
        }

        public static string ListSkipped(string path, string message)
        {
            return Tag + " skipped " + path + " in the replay list: " + message;
        }

        public static string Listed(string directory, int count)
        {
            return Tag + " " + Count(count) + " replays in " + directory;
        }

        public static string ListFailed(string directory, string message)
        {
            return Tag + " cannot list the replays in " + directory + ": " + message;
        }

        public static string PlayRequested(string id, string path)
        {
            return Tag + " play " + id + " from " + path;
        }

        public static string PlayFailed(string id, string message)
        {
            return Tag + " cannot play " + id + ": " + message;
        }

        public static string NotFound(string id)
        {
            return Tag + " no replay with id '" + id + "'";
        }

        public static string Deleted(string id)
        {
            return Tag + " deleted " + id;
        }

        public static string DeleteFailed(string id, string message)
        {
            return Tag + " cannot delete " + id + ": " + message;
        }

        public static string Pruned(string id, int kept)
        {
            return Tag + " deleted the old replay " + id + " to keep the newest " + Count(kept);
        }

        public static string LibraryPublished(bool present)
        {
            return Tag + " the replay list " + (present ? "was published; showing it" : "was withdrawn; showing that replays are unavailable");
        }

        public static string ViewChanged(int? targetId)
        {
            return Tag + " view " + (targetId.HasValue ? "follows player " + Count(targetId.Value) : "is the free camera");
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Seconds(int ticks, int tickRate)
        {
            return (ticks / (double)(tickRate < 1 ? 1 : tickRate)).ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
