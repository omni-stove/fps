using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public static class ExtractionLog
    {
        public const string Tag = "[Extraction]";

        public static string Started(ExtractionSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var text = new StringBuilder(Tag).Append(" started points=");
            AppendIds(text, session.OpenPointIds);
            text.Append(" raid=").Append(Seconds(session.Spec.RaidSeconds))
                .Append(" open-after=").Append(Seconds(session.Spec.OpenAfterSeconds))
                .Append(" hold=").Append(Seconds(session.Spec.ExtractHoldSeconds))
                .Append(" brought=");
            AppendItems(text, session.BroughtIn);
            return text.ToString();
        }

        public static string Ended(ExtractionSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!session.IsOver)
            {
                throw new ArgumentException("the raid has not ended yet", nameof(session));
            }

            var text = new StringBuilder(Tag).Append(" ended outcome=").Append(session.Outcome)
                .Append(" at=").Append(Seconds(session.ElapsedSeconds))
                .Append(" stored=");
            AppendItems(text, session.Stored);
            text.Append(" lost=");
            AppendItems(text, session.Lost);
            return text.ToString();
        }

        private static void AppendItems(StringBuilder text, IReadOnlyList<ItemCount> items)
        {
            if (items.Count == 0)
            {
                text.Append('-');
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(items[i].ItemId).Append('x').Append(items[i].Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AppendIds(StringBuilder text, IReadOnlyList<string> ids)
        {
            text.Append(ids.Count == 0 ? "-" : string.Join(",", ids));
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }
    }
}
