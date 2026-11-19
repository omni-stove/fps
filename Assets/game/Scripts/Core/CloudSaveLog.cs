using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、テストがログの行を同じ関数で作って突き合わせるため。アカウントの Id は行に入れない。
    public static class CloudSaveLog
    {
        public const string LogTag = "[CloudSave]";

        public static string Off(string reason)
        {
            return LogTag + " local only: " + Require(reason, nameof(reason));
        }

        public static string SignedIn(int slots)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} signed in; syncing {1} slot(s)", LogTag, slots);
        }

        public static string SignedOut()
        {
            return LogTag + " signed out; saving locally until the next sign-in";
        }

        public static string Uploaded(string slot, CloudSaveStamp stamp)
        {
            return LogTag + " uploaded slot=" + Require(slot, nameof(slot)) + " " + stamp;
        }

        public static string Downloaded(string slot, CloudSaveStamp stamp)
        {
            return LogTag + " took the newer cloud copy of slot=" + Require(slot, nameof(slot)) + " " + stamp;
        }

        public static string UpToDate(string slot)
        {
            return LogTag + " slot=" + Require(slot, nameof(slot)) + " is up to date";
        }

        public static string KeptReplaced(string slot, string backupSlot)
        {
            return LogTag + " kept the replaced local copy of slot=" + Require(slot, nameof(slot))
                + " in slot=" + Require(backupSlot, nameof(backupSlot));
        }

        public static string Failed(string slot, string error, double retrySeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} could not sync slot={1}: {2}; retrying in {3:0.#}s",
                LogTag,
                Require(slot, nameof(slot)),
                Require(error, nameof(error)),
                retrySeconds);
        }

        public static string ListFailed(string error, double retrySeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} could not list the cloud saves: {1}; retrying in {2:0.#}s",
                LogTag,
                Require(error, nameof(error)),
                retrySeconds);
        }

        public static string CloudRejected(string slot, string problem)
        {
            return LogTag + " left the cloud copy of slot=" + Require(slot, nameof(slot)) + " untouched: " + Require(problem, nameof(problem));
        }

        public static string NameTooLong(string slot, int maxLength)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} slot={1} is kept locally only: its cloud file name is longer than {2} characters",
                LogTag,
                Require(slot, nameof(slot)),
                maxLength);
        }

        public static string StampsRejected(string problem)
        {
            return LogTag + " the save times could not be read; every local slot counts as older than the cloud: " + Require(problem, nameof(problem));
        }

        public static string StampsNotSaved(string problem)
        {
            return LogTag + " could not save the save times: " + Require(problem, nameof(problem));
        }

        public static string RegisteredLate(int startupSlots)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} save slots were registered after the sync started; {1} startup slot(s) are checked from the next start",
                LogTag,
                startupSlots);
        }

        private static string Require(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            return value;
        }
    }
}
