using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class KillConfirmedLog
    {
        public const string LogPrefix = ModeLog.Tag + " killconfirmed";

        public static string DropLog(KillConfirmedTag tag)
        {
            return LogPrefix + " drop" + Describe(tag);
        }

        public static string CollectLog(KillConfirmedTag tag, int collectorId, Side collectorSide, KillConfirmedResult result)
        {
            string action;
            switch (result)
            {
                case KillConfirmedResult.Confirmed:
                    action = " confirm";
                    break;
                case KillConfirmedResult.Denied:
                    action = " deny";
                    break;
                default:
                    action = " ignore";
                    break;
            }

            var text = LogPrefix + action + Describe(tag) + " by=" + Number(collectorId) + " by-side=" + Number(collectorSide.Id);
            return result == KillConfirmedResult.Confirmed || result == KillConfirmedResult.Denied ? text : text + " reason=" + result;
        }

        public static string DiscardLog(KillConfirmedTag tag, string collectorName)
        {
            return LogPrefix + " discard" + Describe(tag) + " by=" + (string.IsNullOrEmpty(collectorName) ? "-" : collectorName);
        }

        public static string ExpireLog(KillConfirmedTag tag)
        {
            return LogPrefix + " expire" + Describe(tag);
        }

        private static string Describe(KillConfirmedTag tag)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return " tag=" + Number(tag.Id) + " owner=" + Number(tag.OwnerId) + " side=" + Number(tag.OwnerSide.Id);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
