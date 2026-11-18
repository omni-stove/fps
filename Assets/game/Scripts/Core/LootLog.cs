using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public static class LootLog
    {
        public const string Tag = "[Loot]";

        public static string Opened(string containerName, string tableId, IReadOnlyList<ItemCount> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var text = new StringBuilder(Tag)
                .Append(" opened container=").Append(Word(containerName))
                .Append(" table=").Append(Word(tableId))
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

                text.Append(items[i].ItemId).Append('x').Append(items[i].Count.ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        public static string UnknownTable(string containerName, string tableId)
        {
            return Tag + " container=" + Word(containerName) + " names the table " + Word(tableId)
                + ", which is not in the loot tables; it opens empty";
        }

        // 空白を含む名前は詰めて出す。ログを空白で区切って読めるようにするため。
        private static string Word(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().Replace(' ', '_');
        }
    }
}
