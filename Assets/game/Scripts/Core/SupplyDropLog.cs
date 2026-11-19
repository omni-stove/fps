using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    // key=value を空白で区切って読めるよう、値に空白を入れない。
    public static class SupplyDropLog
    {
        public const string Tag = "[SupplyDrop]";

        public static string Stage(int phaseIndex, int count)
        {
            return Tag + " stage phase=" + Number(phaseIndex + 1) + " crates=" + Number(count);
        }

        public static string Dropped(int crate, Vector3 from, float groundHeight)
        {
            return Tag + " dropped crate=" + Number(crate) + " at=" + Point(from) + " ground=" + Length(groundHeight);
        }

        public static string Landed(int crate, Vector3 position)
        {
            return Tag + " landed crate=" + Number(crate) + " at=" + Point(position);
        }

        public static string Opened(int crate, int playerId, IReadOnlyList<ItemCount> items, int extras)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var text = new StringBuilder(Tag)
                .Append(" opened crate=").Append(Number(crate))
                .Append(" by=").Append(Number(playerId))
                .Append(" items=");
            if (items.Count == 0)
            {
                text.Append('-');
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(items[i].ItemId).Append('x').Append(Number(items[i].Count));
            }

            return text.Append(" extras=").Append(Number(extras)).ToString();
        }

        public static string NoGround(int crate, Vector3 from)
        {
            return Tag + " crate=" + Number(crate) + " found no ground below " + Point(from) + "; it lands at the zone height";
        }

        public static string UnknownTable(string tableId)
        {
            return Tag + " tableId=" + (string.IsNullOrWhiteSpace(tableId) ? "-" : tableId.Trim().Replace(' ', '_'))
                + " is not in the loot tables; crates give only their fixed contents";
        }

        private static string Point(Vector3 value)
        {
            return "(" + Length(value.X) + "," + Length(value.Y) + "," + Length(value.Z) + ")";
        }

        private static string Length(float value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
